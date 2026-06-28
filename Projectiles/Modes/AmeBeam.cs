using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace Ame.Projectiles.Modes
{
	[ExtendsFromMod("Ame")]
	public class AmeBeam : ModProjectile
	{
		private const float MAX_LENGTH     = 2400f;
		private const float MAX_WIDTH      = 130f;
		private const int   CONTROL_POINTS = 32;
		private const int   EXPIRE_TICKS   = 5;

		private float     _beamLength;
		private float     _beamAngle;
		private float     _widthScale = 0f;
		private Vector2[] _controlPoints;

		/// <summary>
		/// Estructura de vértice personalizada que coincide EXACTAMENTE con el VertexShaderInput
		/// del shader de Calamity: float4 Position, float4 Color, float3 TextureCoordinates.
		/// Terraria vanilla usa float2 para tex coords, lo cual causa división por cero en el shader.
		/// </summary>
		private struct BeamVertex : IVertexType
		{
			public Vector4 Position;
			public Color   Color;
			public Vector3 TextureCoordinate;

			private static readonly VertexDeclaration _declaration = new VertexDeclaration(
				new VertexElement(0,  VertexElementFormat.Vector4, VertexElementUsage.Position, 0),
				new VertexElement(16, VertexElementFormat.Color,   VertexElementUsage.Color, 0),
				new VertexElement(20, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 0)
			);

			public VertexDeclaration VertexDeclaration => _declaration;
		}

		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = (int)MAX_LENGTH + 100;
		}

		public override void SetDefaults()
		{
			Projectile.width    = 20;
			Projectile.height   = 20;
			Projectile.friendly = true;
			Projectile.hostile  = false;
			Projectile.DamageType           = DamageClass.Magic;
			Projectile.penetrate            = -1;
			Projectile.tileCollide          = false;
			Projectile.ignoreWater          = true;
			Projectile.timeLeft             = EXPIRE_TICKS;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown  = 6;
			Projectile.noEnchantmentVisuals = true;
			Projectile.alpha                = 255;
		}

		public override bool ShouldUpdatePosition() => false;

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead) { Projectile.Kill(); return; }

			_widthScale = MathHelper.Clamp(_widthScale + 0.10f, 0f, 1f);
			Projectile.scale = _widthScale;

			Vector2 origin = owner.MountedCenter + new Vector2(owner.direction * 16f, -4f);
			Projectile.Center = origin;

			_beamAngle = (Main.MouseWorld - origin).ToRotation();
			Projectile.velocity = _beamAngle.ToRotationVector2();

			_beamLength = ComputeBeamLength(origin, _beamAngle);

			BuildControlPoints(origin);
			EmitParticles(origin);

			Projectile.Opacity = MathHelper.Clamp(Projectile.Opacity + 0.25f, 0f, 1f);
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float cp = 0f;
			Vector2 origin = Projectile.Center;
			return Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(), targetHitbox.Size(),
				origin, origin + Projectile.velocity * _beamLength,
				MAX_WIDTH * 0.30f * Projectile.scale,
				ref cp
			);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (_controlPoints == null || _beamLength < 4f || Projectile.Opacity < 0.01f)
				return false;

			// Cargar textura de ruido (se usa como sampler en el slot 1 del shader)
			var noiseAsset = ModContent.Request<Texture2D>(
				"Ame/Projectiles/Modes/AmePlasma",
				ReLogic.Content.AssetRequestMode.ImmediateLoad
			);

			// Obtener shader registrado en AmeShaderSystem
			MiscShaderData shaderData = GameShaders.Misc["Ame:MagicBeam"];
			if (shaderData == null || shaderData.Shader == null)
				return false;

			Effect fx = shaderData.Shader;

			// --- Terminar SpriteBatch para dibujar primitivas ---
			Main.spriteBatch.End();

			GraphicsDevice device = Main.instance.GraphicsDevice;

			// --- Configurar parámetros del shader (idéntico a AbyssalFire.cs) ---
			fx.Parameters["time"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["glowPower"]?.SetValue(0.8f);
			fx.Parameters["overallColorStrength"]?.SetValue(1f - Projectile.Opacity);
			fx.Parameters["edgeFadeoutThreshold"]?.SetValue(new Vector2(0.46f, 0.46f));
			fx.Parameters["noiseScale"]?.SetValue(new Vector2(4f, 0.5f));
			fx.Parameters["innerColor"]?.SetValue(Color.DarkViolet.ToVector3());
			fx.Parameters["outerColor"]?.SetValue(Color.Black.ToVector3());
			fx.Parameters["overallColor"]?.SetValue(Color.White.ToVector3());
			fx.Parameters["tipColor"]?.SetValue(Color.White.ToVector3());

			// Matriz World-View-Projection: mundo → pantalla → clip space
			Matrix wvp =
				Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0f) *
				Main.GameViewMatrix.TransformationMatrix *
				Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
			fx.Parameters["uWorldViewProjection"]?.SetValue(wvp);

			// Enlazar textura de ruido al slot 1 (register(s1) en HLSL)
			device.Textures[1] = noiseAsset.Value;
			device.SamplerStates[1] = SamplerState.LinearWrap;

			// Estado de blending aditivo para el efecto de fuego
			device.BlendState = BlendState.Additive;
			device.RasterizerState = RasterizerState.CullNone;

			// --- Construir strip de vértices con float3 tex coords ---
			int vertCount = CONTROL_POINTS * 2;
			BeamVertex[] verts = new BeamVertex[vertCount];

			Vector2 perpDir = new Vector2(
				-MathF.Sin(_beamAngle),
				 MathF.Cos(_beamAngle)
			);

			for (int i = 0; i < CONTROL_POINTS; i++)
			{
				float t = (float)i / (CONTROL_POINTS - 1);
				float halfWidth = PrimitiveWidthFunction(t) * 0.5f;
				Color color = PrimitiveColorFunction(t);

				Vector2 center = _controlPoints[i]; // posición en mundo (la matriz WVP transforma)

				// Vértice superior
				Vector2 top = center + perpDir * halfWidth;
				verts[i * 2] = new BeamVertex
				{
					Position = new Vector4(top.X, top.Y, 0f, 1f),
					Color = color,
					TextureCoordinate = new Vector3(t, 0f, 1f) // z=1 evita div por cero
				};

				// Vértice inferior
				Vector2 bot = center - perpDir * halfWidth;
				verts[i * 2 + 1] = new BeamVertex
				{
					Position = new Vector4(bot.X, bot.Y, 0f, 1f),
					Color = color,
					TextureCoordinate = new Vector3(t, 1f, 1f) // z=1 evita div por cero
				};
			}

			// --- Dibujar el triangle strip con el shader aplicado ---
			fx.CurrentTechnique.Passes[0].Apply();
			device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);

			// --- Restaurar estados ---
			device.BlendState = BlendState.AlphaBlend;
			Main.spriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				Main.DefaultSamplerState,
				DepthStencilState.None,
				Main.Rasterizer,
				null,
				Main.GameViewMatrix.TransformationMatrix
			);

			return false;
		}

		private float PrimitiveWidthFunction(float completionRatio)
		{
			float maxBodyWidth = Projectile.scale * MAX_WIDTH;
			float fadeIn  = Utils.GetLerpValue(0f, 0.05f, completionRatio, true);
			float fadeOut = Utils.GetLerpValue(1f, 0.9f,  completionRatio, true);
			return maxBodyWidth * fadeIn * fadeOut;
		}

		private Color PrimitiveColorFunction(float completionRatio)
		{
			return Color.White * Projectile.Opacity;
		}

		private float ComputeBeamLength(Vector2 origin, float angle)
		{
			Vector2 dir = angle.ToRotationVector2();
			float length = 0f;
			for (float f = 0f; f < MAX_LENGTH; f += 8f)
			{
				if (Collision.SolidCollision(origin + dir * f - new Vector2(4f), 8, 8))
				{
					length = f;
					break;
				}
				length = f;
			}
			return length;
		}

		private void BuildControlPoints(Vector2 origin)
		{
			if (_controlPoints == null || _controlPoints.Length != CONTROL_POINTS)
				_controlPoints = new Vector2[CONTROL_POINTS];

			Vector2 dir = Projectile.velocity;
			for (int i = 0; i < CONTROL_POINTS; i++)
				_controlPoints[i] = origin + dir * (_beamLength * i / (CONTROL_POINTS - 1f));
		}

		private void EmitParticles(Vector2 origin)
		{
			if (_beamLength < 50f) return;
			Vector2 dir = Projectile.velocity;

			DelegateMethods.v3_1 = Color.DarkViolet.ToVector3() * Projectile.scale * 0.4f;
			Utils.PlotTileLine(origin, origin + dir * _beamLength, MAX_WIDTH * Projectile.scale, DelegateMethods.CastLight);

			if (Projectile.scale < 0.25f) return;

			if (Main.rand.NextBool(3))
			{
				float t   = Main.rand.NextFloat();
				Vector2 pos = origin + dir * (_beamLength * t);
				Dust d = Dust.NewDustDirect(pos - new Vector2(10), 20, 20, DustID.Shadowflame);
				d.velocity  = dir * Main.rand.NextFloat(2f, 8f);
				d.noGravity = true;
				d.scale     = Main.rand.NextFloat(1f, 2f);
			}
		}
	}
}
