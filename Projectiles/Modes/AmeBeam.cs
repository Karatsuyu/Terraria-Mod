using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Ame.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Ame.Players;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// MAGIC MODE — Rayo Abismal estilo Voidragon.
	/// Usa 4 sistemas simultáneos para replicar el efecto completo:
	/// 1. Shader HLSL (AmeMagicBeam.fx compilado a .xnb)
	/// 2. HarshNoise.png como textura de ruido (turbulencia)
	/// 3. Custom PrimitiveRenderer con float3 tex coords
	/// 4. Partículas HeavySmoke (estela de fuego abisal)
	/// </summary>
	[ExtendsFromMod("Ame")]
	public class AmeBeam : ModProjectile
	{
		private const float MAX_LENGTH      = 2400f;
		private const float MAX_WIDTH       = 400f;
		private const int   CONTROL_POINTS  = 32;
		private const int   EXPIRE_TICKS    = 5;

		private float     _beamLength;
		private float     _beamAngle;
		private float     _widthScale = 0f;
		private float     _laserGrowth = 0.05f; // Equivale a LaserLength en Calamity
		private Vector2[] _controlPoints;

		// --- MÁQUINA DE ESTADOS (Carga y Disparo) ---
		private int   _state = 0; // 0 = Cargando, 1 = Disparando
		private int   _chargeTimer = 150;
		private float _chargeVisTimer = 0f;
		private float _chargePulseTimer = 0f;
		private float _maxChargeSpeed = 2f;
		private bool  _playChargeSound = true;
		private ReLogic.Utilities.SlotId _soundSlot;

		/// <summary>
		/// Estructura de vértice que coincide EXACTAMENTE con VertexShaderInput del shader:
		/// float4 Position, float4 Color, float3 TextureCoordinates.
		/// El z contiene el halfWidth para corrección de perspectiva.
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

		public override void OnKill(int timeLeft)
		{
			if (Terraria.Audio.SoundEngine.TryGetActiveSound(_soundSlot, out var activeSound))
				activeSound?.Stop();
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead) 
			{ 
				Projectile.Kill(); 
				return; 
			}

			Vector2 origin = owner.MountedCenter + new Vector2(owner.direction * 16f, -4f);
			Projectile.Center = origin;

			_beamAngle = (Main.MouseWorld - origin).ToRotation();
			Projectile.velocity = _beamAngle.ToRotationVector2();

			if (_state == 0) // CARGANDO
			{
				Projectile.timeLeft = EXPIRE_TICKS; // Reset timeLeft to not expire

				if (_playChargeSound)
				{
					Terraria.Audio.SoundStyle charge = new("Ame/Sounds/VoidragonCharge") { Volume = 0.7f, IsLooped = true, Pitch = -0.5f };
					_soundSlot = Terraria.Audio.SoundEngine.PlaySound(charge, Projectile.Center);
					_playChargeSound = false;
				}

				float chargeCompletion = Utils.GetLerpValue(150, 0, _chargeTimer, true);
				_chargeVisTimer = MathHelper.Lerp(0, _maxChargeSpeed, chargeCompletion);
				_chargePulseTimer += 0.6f + MathHelper.Lerp(0, _maxChargeSpeed, MathF.Pow(chargeCompletion, 2.5f));

				// Tiembla la pantalla incrementando progresivamente
				owner.GetModPlayer<AmePlayer>().ScreenShake = MathHelper.Lerp(0f, 4f, chargeCompletion);

				if (Terraria.Audio.SoundEngine.TryGetActiveSound(_soundSlot, out var sSound) && sSound.IsPlaying)
				{
					sSound.Position = Projectile.Center;
					sSound.Pitch = -0.85f + 1.5f * chargeCompletion;
				}

				_chargeTimer--;

				if (_chargeTimer <= 0)
				{
					_state = 1; // Cambiar a DISPARANDO
					Projectile.timeLeft = 500; // Duración del láser

					if (Terraria.Audio.SoundEngine.TryGetActiveSound(_soundSlot, out var cSound))
						cSound?.Stop();

					Terraria.Audio.SoundStyle start = new("Ame/Sounds/VoidragonStrongStart") { Volume = 1f, MaxInstances = 3 };
					Terraria.Audio.SoundStyle start2 = new("Ame/Sounds/MagnaCannonShot") { Volume = 1f, MaxInstances = 3 };
					for (int i = 0; i < 3; i++)
						Terraria.Audio.SoundEngine.PlaySound((i > 0 ? start2 : start) with { Pitch = 0f - 0.5f * i }, Projectile.Center);

					Terraria.Audio.SoundStyle fire = new("Ame/Sounds/VoidragonLaser") { Volume = 1f, IsLooped = true, Pitch = 0f };
					_soundSlot = Terraria.Audio.SoundEngine.PlaySound(fire, Projectile.Center);
				}
				return;
			}

			if (_state == 1) // DISPARANDO
			{
				_widthScale = MathHelper.Clamp(_widthScale + 0.10f, 0f, 1f);
				Projectile.scale = MathHelper.Lerp(0f, 1f, 1f - MathF.Pow(1f - _widthScale, 2f));

				_laserGrowth = MathHelper.Lerp(_laserGrowth, 1f, 0.032f);

				_beamLength = ComputeBeamLength(origin, _beamAngle);

				BuildControlPoints(origin);
				SpawnAbyssalParticles(origin);

				Projectile.Opacity = MathHelper.Clamp(Projectile.Opacity + 0.25f, 0f, 1f);

				// Tiembla la pantalla constantemente por la fuerza del láser
				owner.GetModPlayer<AmePlayer>().ScreenShake = 10f;

				// Mantener el sonido en loop con volumen y pitch constantes
				// (Ya no decae a 0 porque el arma puede disparar de forma infinita)
				if (Terraria.Audio.SoundEngine.TryGetActiveSound(_soundSlot, out var sSound) && sSound.IsPlaying)
				{
					sSound.Position = Projectile.Center;
					sSound.Pitch = 0f;
					sSound.Volume = 1f;
				}
			}
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float cp = 0f;
			Vector2 origin = Projectile.Center;
			return Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(), targetHitbox.Size(),
				origin, origin + Projectile.velocity * _beamLength,
				MAX_WIDTH * 0.35f * Projectile.scale,
				ref cp
			);
		}

		// ══════════════════════════════════════════════
		// RENDERING — Shader + Primitivas + Partículas
		// ══════════════════════════════════════════════
		public override bool PreDraw(ref Color lightColor)
		{
			if (_state == 0)
			{
				Main.spriteBatch.End();
				Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
				DrawChargeEffects();
				Main.spriteBatch.End();
				Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
				return false;
			}

			if (_controlPoints == null || _beamLength < 4f || Projectile.Opacity < 0.01f)
				return false;

			// ── 1. Cargar HarshNoise (textura de turbulencia de Calamity) ──
			var noiseAsset = ModContent.Request<Texture2D>(
				"Ame/ExtraTextures/HarshNoise",
				ReLogic.Content.AssetRequestMode.ImmediateLoad
			);

			// ── 2. Obtener shader ──
			MiscShaderData shaderData = GameShaders.Misc["Ame:MagicBeam"];
			if (shaderData == null || shaderData.Shader == null)
				return false;

			Effect fx = shaderData.Shader;

			// ── 3. Terminar SpriteBatch para dibujar primitivas ──
			Main.spriteBatch.End();

			GraphicsDevice device = Main.instance.GraphicsDevice;

			// ── 4. Dibujar partículas de humo DETRÁS del beam (capa de fondo) ──
			AmeParticleSystem.DrawAllParticles(Main.spriteBatch);

			// ── 5. Configurar parámetros del shader ──
			float beamWidthInterpolant = 1f - Projectile.Opacity;
			fx.Parameters["time"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["glowPower"]?.SetValue(0.8f);
			fx.Parameters["overallColorStrength"]?.SetValue(beamWidthInterpolant);
			fx.Parameters["edgeFadeoutThreshold"]?.SetValue(new Vector2(0.46f, 0.46f));
			fx.Parameters["noiseScale"]?.SetValue(new Vector2(4f, 0.5f));
			fx.Parameters["innerColor"]?.SetValue(Color.Red.ToVector3());
			fx.Parameters["outerColor"]?.SetValue(Color.Black.ToVector3());
			fx.Parameters["overallColor"]?.SetValue(new Color(255, 100, 100).ToVector3());
			fx.Parameters["tipColor"]?.SetValue(Color.White.ToVector3());

			// ── 6. Matriz WVP ──
			Matrix wvp =
				Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0f) *
				Main.GameViewMatrix.TransformationMatrix *
				Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
			fx.Parameters["uWorldViewProjection"]?.SetValue(wvp);

			// ── 7. Enlazar HarshNoise al slot 1 ──
			device.Textures[1] = noiseAsset.Value;
			device.SamplerStates[1] = SamplerState.LinearWrap;

			// ── 8. Estado de blending ──
			device.BlendState = BlendState.Additive;
			device.RasterizerState = RasterizerState.CullNone;

			// ── 9. Construir vértices ──
			int vertCount = CONTROL_POINTS * 2;
			BeamVertex[] verts = new BeamVertex[vertCount];

			Vector2 perpDir = new Vector2(
				-MathF.Sin(_beamAngle),
				 MathF.Cos(_beamAngle)
			);

			for (int i = 0; i < CONTROL_POINTS; i++)
			{
				float completionRatio = (float)i / (CONTROL_POINTS - 1);
				float width = PrimitiveWidthFunction(completionRatio);
				float halfWidth = width * 0.5f;
				Color color = PrimitiveColorFunction(completionRatio);

				Vector2 center = _controlPoints[i];

				float effectiveHalfWidth = MathF.Max(halfWidth, 0.001f);
				float texU = completionRatio;

				Vector2 top = center + perpDir * halfWidth;
				verts[i * 2] = new BeamVertex
				{
					Position = new Vector4(top.X, top.Y, 0f, 1f),
					Color = color,
					TextureCoordinate = new Vector3(texU, 0.5f - effectiveHalfWidth * 0.5f, effectiveHalfWidth)
				};

				Vector2 bot = center - perpDir * halfWidth;
				verts[i * 2 + 1] = new BeamVertex
				{
					Position = new Vector4(bot.X, bot.Y, 0f, 1f),
					Color = color,
					TextureCoordinate = new Vector3(texU, 0.5f + effectiveHalfWidth * 0.5f, effectiveHalfWidth)
				};
			}

			// ── 10. Dibujar primitivas a la pantalla ──
			fx.CurrentTechnique.Passes[0].Apply();
			device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);

			// ── 11. Restaurar estado de SpriteBatch ──
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

		private void DrawChargeEffects()
		{
			Player owner = Main.player[Projectile.owner];
			Vector2 gunTipPosition = Projectile.Center;
			float drawRotation = Projectile.rotation + (Projectile.spriteDirection == -1 ? MathHelper.Pi : 0f);
			SpriteEffects flipSprite = (Projectile.spriteDirection * owner.gravDir == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			Texture2D bloom = ModContent.Request<Texture2D>("Ame/ExtraTextures/BloomCircle").Value;
			Texture2D roar = ModContent.Request<Texture2D>("Ame/ExtraTextures/RoarPulse").Value;
			Texture2D orb = ModContent.Request<Texture2D>("Ame/ExtraTextures/GlowOrbParticle").Value;

			float chargeVisual = MathF.Pow(Utils.GetLerpValue(0, _maxChargeSpeed / 1.5f, _chargeVisTimer, true), 1.5f);
			float finalChargeVisual = MathF.Pow(Utils.GetLerpValue(_maxChargeSpeed / 1.5f, _maxChargeSpeed, _chargeVisTimer, true), 3f);

			int max = 30;
			float ringScaling = MathF.Pow(1 - Utils.GetLerpValue(0, max, _chargePulseTimer % max, true), 1.15f);
			float ringOpacity = 1 - MathF.Abs(Utils.GetLerpValue(0.5f, 1, ringScaling, false));

			// Black aura
			for (int i = 0; i < 15; i++)
			{
				Color lerpColor = Color.Lerp(Color.Black, Color.DarkRed, finalChargeVisual);
				Color color = lerpColor * (0.5f + ringOpacity * 0.8f); // Más visible
				Vector2 scale = new Vector2(0.85f + i * 0.065f, 0.85f - i * 0.065f) * (1f + finalChargeVisual * 1.8f) * owner.gravDir * chargeVisual * (4.25f - ringScaling * 2.5f);
				scale *= 1.35f; // Hacemos toda el aura un 35% más grande
				Main.EntitySpriteDraw(orb, gunTipPosition - Main.screenPosition, null, color, drawRotation + Main.rand.NextFloat(-4f, 4f), orb.Size() / 2, scale, flipSprite, 0);
			}

			// Glow aura
			for (int i = 0; i < 3; i++)
			{
				Color glowColor = Color.Lerp(Color.Red, Color.White, i == 0 ? 0.8f : 0f) * 1.25f; // Colores más brillantes
				
				float scale = (1f + finalChargeVisual * 1.8f) * owner.gravDir * chargeVisual * 0.5f * (i == 0 ? 0.75f : 1f);
				scale *= 1.75f; // Mucho más grande
				Main.EntitySpriteDraw(bloom, gunTipPosition - Main.screenPosition, null, glowColor, drawRotation + Main.rand.NextFloat(-4f, 4f), bloom.Size() / 2, scale, flipSprite, 0);
			}

			// Pulse Rings
			Color ringColor = Color.White * ringOpacity; // Usamos blanco para que sea más intenso
			float ringScale = 1.5f * owner.gravDir * chargeVisual * ringScaling; // 3 veces más grande (0.5f a 1.5f)
			Main.EntitySpriteDraw(roar, gunTipPosition - Main.screenPosition, null, ringColor, drawRotation + Main.rand.NextFloat(-4f, 4f), roar.Size() / 2, ringScale, flipSprite, 0);
		}

		// ══════════════════════════════════════════════════
		// WIDTH & COLOR — Idéntico a AbyssalFire.cs
		// ══════════════════════════════════════════════════
		private float PrimitiveWidthFunction(float completionRatio)
		{
			float maxBodyWidth = Projectile.scale * MAX_WIDTH;
			// Forma idéntica a Calamity: fadeIn desde base + fadeOut hasta LaserLength
			float fadeIn  = Utils.GetLerpValue(0f, 0.05f, completionRatio, true);
			float fadeOut = Utils.GetLerpValue(_laserGrowth, _laserGrowth - 0.1f, completionRatio, true);
			return maxBodyWidth * fadeIn * fadeOut;
		}

		private Color PrimitiveColorFunction(float completionRatio)
		{
			return Color.White * Projectile.Opacity;
		}

		// ══════════════════════════════════════════════════════════
		// PARTÍCULAS — Replica EXACTA del patrón de AbyssalFire.AI()
		// ══════════════════════════════════════════════════════════
		private void SpawnAbyssalParticles(Vector2 origin)
		{
			if (_beamLength < 50f) return;
			Vector2 dir = Projectile.velocity;

			// Iluminación a lo largo del beam
			DelegateMethods.v3_1 = Color.Red.ToVector3() * Projectile.scale * 0.4f;
			Utils.PlotTileLine(origin, origin + dir * _beamLength, MAX_WIDTH * Projectile.scale, DelegateMethods.CastLight);

			if (Projectile.scale < 0.25f) return;

			// ── Partículas principales: emular los 4 tipos de HeavySmokeParticle ──
			float tipRatio = 0.042f;

			for (int i = 0; i < 10; i++)
			{
				// Posición en la punta y en el cuerpo
				float tipT = Main.rand.NextFloat(0f, tipRatio);
				float bodyT = Main.rand.NextFloat(tipRatio, 1f);
				Vector2 tipPos = origin + dir * (_beamLength * tipT);
				Vector2 bodyPos = origin + dir * (_beamLength * bodyT);

				// Velocidad del fuego: dirección hacia el mouse con dispersión
				Vector2 fireDir = dir.RotatedByRandom(MathHelper.ToRadians(20f));
				Vector2 fireVelocity = fireDir * Main.rand.NextFloat(25f, 30f);

				int fireLifetime = Main.rand.Next(45, 60);
				// Aumentar la escala general de las partículas para que llenen el nuevo tamaño del rayo
				float fireScale = Main.rand.NextFloat(4.5f, 6.0f) * Projectile.scale;

				// 1. Fondo: Humo oscuro DarkRed/Negro (NO glowing, semitransparente)
				Color bgColor = Color.Lerp(Color.DarkRed, Color.Black, 0.25f);
				AmeParticleSystem.SpawnSmoke(bodyPos, fireVelocity, bgColor, fireLifetime,
					fireScale, 1f, Main.rand.NextFloat(0.02f, 0.1f) * (Main.rand.NextBool() ? 1f : -1f), false);

				// 2. Frente: Fuego Red/Blanco brillante (GLOWING, aditivo)
				Color fgColor = Main.rand.NextBool(4) ? Color.White : Color.Red;
				AmeParticleSystem.SpawnSmoke(bodyPos, fireVelocity, fgColor, fireLifetime,
					fireScale * 0.6f, 0.8f, Main.rand.NextFloat(0.02f, 0.1f) * (Main.rand.NextBool() ? 1f : -1f), true);

				// 3. Punta: Fuego blanco intenso (GLOWING)
				AmeParticleSystem.SpawnSmoke(tipPos, fireVelocity, Color.White, fireLifetime,
					fireScale * 0.52f, 0.3f, Main.rand.NextFloat(0.02f, 0.1f) * (Main.rand.NextBool() ? 1f : -1f), true);
			}

			// 4. Base del arma: Llamas blancas pequeñas (GLOWING)
			for (int i = 0; i < 3; i++)
			{
				Vector2 fireVelocity = dir.RotatedByRandom(MathHelper.ToRadians(20f)) * Main.rand.NextFloat(15f, 20f);
				AmeParticleSystem.SpawnSmoke(origin - dir * 8f, fireVelocity, Color.White,
					Main.rand.Next(15, 20), 1.6f, 0.7f, Main.rand.NextFloat(0.02f, 0.1f) * (Main.rand.NextBool() ? 1f : -1f), true);
			}

			// Dust extra para variedad (Polvo de antorcha roja)
			if (Main.rand.NextBool(2))
			{
				float t = Main.rand.NextFloat();
				Vector2 pos = origin + dir * (_beamLength * t);
				// El DustID.RedTorch es 60
				Dust d = Dust.NewDustDirect(pos - new Vector2(10), 20, 20, DustID.RedTorch);
				d.velocity  = dir * Main.rand.NextFloat(2f, 8f);
				d.noGravity = true;
				d.scale     = Main.rand.NextFloat(1.5f, 2.5f);
			}
		}

		// ══════════════════════════════
		// UTILIDADES
		// ══════════════════════════════
		private float ComputeBeamLength(Vector2 origin, float angle)
		{
			// El láser original pasa derecho a través de los bloques.
			return MAX_LENGTH;
		}

		private void BuildControlPoints(Vector2 origin)
		{
			if (_controlPoints == null || _controlPoints.Length != CONTROL_POINTS)
				_controlPoints = new Vector2[CONTROL_POINTS];

			Vector2 dir = Projectile.velocity;
			for (int i = 0; i < CONTROL_POINTS; i++)
				_controlPoints[i] = origin + dir * (_beamLength * i / (CONTROL_POINTS - 1f));
		}
	}
}
