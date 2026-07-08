using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using System;

namespace Ame.Projectiles.Modes
{
	public class AmeRangedShot : ModProjectile
	{
		private const int   GEN1_COUNT    = 8;
		private const int   GEN2_COUNT    = 3;
		private const float GEN1_SPEED    = 13f;
		private const float GEN2_SPEED    = 9f;
		private const float MAIN_LIFETIME = 180f;

		private float _visualTimer;
		private bool  _exploded;
		private int   Generation => (int)Projectile.ai[0];

		// VertexStrip para el trail con shader
		private VertexStrip _strip = new VertexStrip();

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 22;
			ProjectileID.Sets.TrailingMode[Projectile.type]     = 2; // smooth
		}

		public override void SetDefaults()
		{
			Projectile.width    = 18;
			Projectile.height   = 18;
			Projectile.friendly = true;
			Projectile.DamageType           = DamageClass.Ranged;
			Projectile.penetrate            = 1;
			Projectile.tileCollide          = true;
			Projectile.ignoreWater          = true;
			Projectile.timeLeft             = (int)MAIN_LIFETIME;
			Projectile.extraUpdates         = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown  = 8;
			Projectile.alpha = 255;
		}

		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

		public override void AI()
		{
			_visualTimer += 1f;
			Projectile.rotation = Projectile.velocity.ToRotation();
			Projectile.Opacity  = MathHelper.Clamp(Projectile.Opacity + 0.25f, 0f, 1f);

			// Luz roja
			float ls = Generation == 0 ? 0.9f : (Generation == 1 ? 0.55f : 0.3f);
			Lighting.AddLight(Projectile.Center, ls, 0f, 0f);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (_exploded) return;
			_exploded = true;
			Explode(Projectile.Center);
		}

		public override void OnKill(int timeLeft)
		{
			if (_exploded) return;
			_exploded = true;
			Explode(Projectile.Center);
		}

		private void Explode(Vector2 pos)
		{
			// Sonido
			SoundEngine.PlaySound(
				new SoundStyle("Terraria/Sounds/Item_14")
				{ Volume = 0.5f - Generation * 0.12f, PitchVariance = 0.3f }, pos);

			// Efecto visual de shockwave con shader
			float impactRadius = Generation == 0 ? 120f : (Generation == 1 ? 75f : 45f);
			Projectile.NewProjectile(
				Projectile.GetSource_FromThis(), pos, Vector2.Zero,
				ModContent.ProjectileType<AmeImpactEffect>(),
				0, 0f, Projectile.owner,
				impactRadius, Generation
			);

			// Dust adicional (complementa el shader)
			int dustCount = Generation == 0 ? 12 : (Generation == 1 ? 7 : 4);
			for (int i = 0; i < dustCount; i++)
			{
				float a = Main.rand.NextFloat(0f, MathHelper.TwoPi);
				float s = Main.rand.NextFloat(3f, 8f - Generation * 2f);
				Dust d  = Dust.NewDustDirect(pos, 4, 4, DustID.Shadowflame);
				d.velocity  = new Vector2(MathF.Cos(a), MathF.Sin(a)) * s;
				d.noGravity = true;
				d.scale     = 1.2f - Generation * 0.2f;
			}

			float li = 2.5f - Generation * 0.7f;
			Lighting.AddLight(pos, li, li * 0.04f, 0f);

			if (Generation == 0)
			{
				SpawnFragments(pos, GEN1_COUNT, GEN1_SPEED, 1);
				SpawnRiftPortal(pos);
			}
			else if (Generation == 1)
			{
				SpawnFragments(pos, GEN2_COUNT, GEN2_SPEED, 2);
			}
		}

		private void SpawnFragments(Vector2 pos, int count, float speed, int nextGen)
		{
			float baseAngle      = Main.rand.NextFloat(0f, MathHelper.TwoPi);
			float originalDamage = Projectile.ai[1] > 0f ? Projectile.ai[1] : Projectile.damage;
			int   fragDamage     = nextGen == 1 ? (int)(originalDamage * 0.65f) : (int)(originalDamage * 0.35f);

			for (int i = 0; i < count; i++)
			{
				float angle = baseAngle + (MathHelper.TwoPi / count) * i + Main.rand.NextFloat(-0.12f, 0.12f);
				Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed
				            * (0.85f + Main.rand.NextFloat(0f, 0.3f));

				Projectile.NewProjectile(
					Projectile.GetSource_FromThis(), pos, vel,
					ModContent.ProjectileType<AmeRangedShot>(),
					fragDamage, Projectile.knockBack * 0.5f, Projectile.owner,
					nextGen, originalDamage
				);
			}
		}

		private void SpawnRiftPortal(Vector2 pos)
		{
			float riftAngle  = Projectile.velocity.ToRotation() + MathHelper.PiOver2 + Main.rand.NextFloat(-0.4f, 0.4f);
			float origDamage = Projectile.ai[1] > 0f ? Projectile.ai[1] : Projectile.damage;
			int   boltDamage = (int)(origDamage * 0.45f);

			Projectile.NewProjectile(
				Projectile.GetSource_FromThis(), pos, Vector2.Zero,
				ModContent.ProjectileType<AmeRiftPortal>(),
				boltDamage, 0f, Projectile.owner,
				boltDamage, riftAngle
			);
		}

		// ═══════════════════════════════════════════════════════
		// DRAW — trail con VertexStrip + shader AmeTrail
		// ═══════════════════════════════════════════════════════

		public override bool PreDraw(ref Color lightColor)
		{
			bool hasShader = GameShaders.Misc.ContainsKey("AmeTrail");

			// Necesitamos al menos 2 posiciones válidas para el strip
			int validCount = 0;
			for (int i = 0; i < Projectile.oldPos.Length; i++)
				if (Projectile.oldPos[i] != Vector2.Zero) validCount++;

			if (validCount < 2)
				return FallbackDraw(); // sin shader mientras no hay trail

			if (!hasShader)
				return FallbackDraw();

			// ── SETUP SPRITEBATCH PARA VERTEXSTRIP ─────────────
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate, BlendState.Additive,
				SamplerState.LinearWrap, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			// ── CONFIGURAR SHADER ───────────────────────────────
			var shaderData = GameShaders.Misc["AmeTrail"];
			var effect     = shaderData.Shader;

			// Texturas de ruido
			if (Ame.NoiseTexture != null)
				effect.Parameters["uNoiseTexture"]?.SetValue(Ame.NoiseTexture);

			// Parámetros de tiempo y estado
			effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			effect.Parameters["uOpacity"]?.SetValue(Projectile.Opacity);
			effect.Parameters["uTrailLength"]?.SetValue(
				Vector2.Distance(Projectile.oldPos[0], Projectile.oldPos[validCount - 1])
			);
			effect.Parameters["uGeneration"]?.SetValue((float)Generation);

			// Colores según generación
			Color cCore = Generation == 0 ? new Color(255, 220, 180) : new Color(255, 180, 100);
			Color cMid  = Generation == 0 ? new Color(255, 30, 5)    : new Color(200, 15, 0);
			Color cEdge = Generation == 0 ? new Color(100, 3, 0)     : new Color(70, 2, 0);

			effect.Parameters["uColorCore"]?.SetValue(cCore.ToVector4());
			effect.Parameters["uColorMid"]?.SetValue(cMid.ToVector4());
			effect.Parameters["uColorEdge"]?.SetValue(cEdge.ToVector4());

			// ── PREPARAR Y DIBUJAR VERTEXSTRIP ─────────────────
			// Anchura del trail según generación
			float baseWidth = Generation == 0 ? 28f : (Generation == 1 ? 16f : 9f);

			_strip.PrepareStrip(
				Projectile.oldPos,
				Projectile.oldRot,
				ColorFunc,
				pos => WidthFunc(pos, baseWidth),
				-Main.screenPosition + Projectile.Size * 0.5f,
				includeBacksides: true
			);

			effect.CurrentTechnique.Passes[0].Apply();
			_strip.DrawTrail();

			// ── GLOW DEL PROYECTIL SOBRE EL TRAIL ──────────────
			DrawProjectileGlow();

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			return false;
		}

		private Color ColorFunc(float progress)
		{
			// El color del vértice es White con alpha que decae hacia la punta
			// El shader maneja el color real, aquí solo pasamos el alpha por segmento
			float fade = 1f - progress;
			fade = fade * fade; // cuadrático
			return new Color(255, 255, 255, (int)(fade * 255f * Projectile.Opacity));
		}

		private float WidthFunc(float progress, float baseWidth)
		{
			// Más ancho cerca del origen, más delgado en la punta
			float taper    = 1f - progress;
			float pulse    = 1f + MathF.Sin(_visualTimer * 0.4f + progress * 8f) * 0.06f;
			return baseWidth * taper * pulse * Projectile.Opacity;
		}

		private void DrawProjectileGlow()
		{
			// Glow simple sobre la posición actual (sin textura de glow externa)
			// Usa el mismo spriteBatch aditivo que ya está activo
			// Dibujamos 3 círculos concéntricos usando puntos sprite si hubiera textura,
			// pero como no tenemos PNG, usamos dust visual solo → OK para la demo.
			// En producción: agregar Assets/Projectiles/GlowDot.png (círculo blanco 32x32)
		}

		private bool FallbackDraw()
		{
			// Fallback sin shader: glow básico aditivo igual que antes
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
				SamplerState.LinearClamp, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			if (Ame.NoiseTexture != null)
			{
				var origin  = new Vector2(Ame.NoiseTexture.Width * 0.5f, Ame.NoiseTexture.Height * 0.5f);
				float sz    = Generation == 0 ? 26f : (Generation == 1 ? 16f : 10f);
				var   dp    = Projectile.Center - Main.screenPosition;
				Color col   = new Color(180, 5, 0) * Projectile.Opacity;
				col.A = 0;
				Main.spriteBatch.Draw(Ame.NoiseTexture, dp, null, col, 0f, origin, sz / Ame.NoiseTexture.Width, SpriteEffects.None, 0);
			}

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			return false;
		}
	}
}
