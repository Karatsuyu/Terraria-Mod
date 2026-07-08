using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// RANGED MODE — Proyectil secundario disparado por AmeRiftPortal.
	/// Pequeño, rápido, con homing suave y trail de energía oscura.
	/// </summary>
	public class AmeRiftBolt : ModProjectile
	{
		private const float LIFETIME   = 90f;
		private const float HOME_SPEED = 0.55f; // aceleración de homing
		private const float MAX_SPEED  = 16f;

		private float _visualTimer;

		private static Texture2D _glow;
		private static bool      _glowCreated;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
			ProjectileID.Sets.TrailingMode[Projectile.type]     = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width    = 12;
			Projectile.height   = 12;
			Projectile.friendly = true;
			Projectile.DamageType       = DamageClass.Ranged;
			Projectile.penetrate        = 1;
			Projectile.tileCollide      = true;
			Projectile.ignoreWater      = true;
			Projectile.timeLeft         = (int)LIFETIME;
			Projectile.extraUpdates     = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown  = 10;
			Projectile.alpha = 255;
		}

		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

		public override void AI()
		{
			_visualTimer += 1f;

			// Homing suave hacia el enemigo más cercano
			NPC target = FindNearestEnemy(Projectile.Center, 280f);
			if (target != null)
			{
				Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
				Projectile.velocity += toTarget * HOME_SPEED;

				// Limitar velocidad
				if (Projectile.velocity.Length() > MAX_SPEED)
					Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MAX_SPEED;
			}

			Projectile.rotation = Projectile.velocity.ToRotation();

			// Trail de energía oscura
			if (Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustDirect(
					Projectile.Center - Projectile.velocity * 0.5f, 4, 4,
					DustID.Shadowflame
				);
				d.noGravity = true;
				d.velocity  = -Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(0.8f, 0.8f);
				d.scale     = 0.8f + Main.rand.NextFloat(0f, 0.4f);
				d.fadeIn    = 0.3f;
			}

			// Luz
			Lighting.AddLight(Projectile.Center, 0.4f, 0f, 0f);

			// Fade in rápido
			Projectile.Opacity = MathHelper.Clamp(Projectile.Opacity + 0.2f, 0f, 1f);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Pequeño burst al impactar
			for (int i = 0; i < 6; i++)
			{
				float a = Main.rand.NextFloat(0f, MathHelper.TwoPi);
				Dust d  = Dust.NewDustDirect(Projectile.Center, 4, 4, DustID.Shadowflame);
				d.velocity  = new Vector2(MathF.Cos(a), MathF.Sin(a)) * Main.rand.NextFloat(1.5f, 4f);
				d.noGravity = true;
				d.scale     = 1f;
			}
			Lighting.AddLight(Projectile.Center, 1.2f, 0.05f, 0f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			EnsureGlow();
			if (_glow == null) return false;

			Vector2 glowOrigin = new Vector2(_glow.Width * 0.5f, _glow.Height * 0.5f);
			float   opacity    = Projectile.Opacity;

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate, BlendState.Additive,
				SamplerState.LinearClamp, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			// Trail
			for (int i = 1; i < Projectile.oldPos.Length; i++)
			{
				if (Projectile.oldPos[i] == Vector2.Zero) continue;
				float progress = (float)i / Projectile.oldPos.Length;
				float fade     = (1f - progress) * opacity;
				float scale    = (1f - progress * 0.7f) * 9f / _glow.Width;
				Color col      = new Color(180, 5, 0) * (fade * 0.6f);
				col.A = 0;
				Main.spriteBatch.Draw(_glow,
					Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
					null, col, Projectile.oldRot[i], glowOrigin, scale, SpriteEffects.None, 0);
			}

			// Cuerpo principal
			Vector2 drawPos = Projectile.Center - Main.screenPosition;
			float pulse     = 1f + MathF.Sin(_visualTimer * 0.4f) * 0.1f;

			// Aura exterior
			Color outerCol = new Color(140, 3, 0) * (opacity * 0.7f);
			outerCol.A = 0;
			Main.spriteBatch.Draw(_glow, drawPos, null, outerCol, 0f, glowOrigin, 14f / _glow.Width * pulse, SpriteEffects.None, 0);

			// Núcleo
			Color coreCol = new Color(255, 40, 5) * (opacity * 0.9f);
			coreCol.A = 0;
			Main.spriteBatch.Draw(_glow, drawPos, null, coreCol, 0f, glowOrigin, 6f / _glow.Width * pulse, SpriteEffects.None, 0);

			// Centro blanco
			Color whiteCol = new Color(255, 200, 180) * (opacity * 1f);
			whiteCol.A = 0;
			Main.spriteBatch.Draw(_glow, drawPos, null, whiteCol, 0f, glowOrigin, 2.5f / _glow.Width, SpriteEffects.None, 0);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			return false;
		}

		private NPC FindNearestEnemy(Vector2 from, float maxDist)
		{
			NPC   best     = null;
			float bestDist = maxDist;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || !npc.CanBeChasedBy() || npc.friendly) continue;
				float d = Vector2.Distance(from, npc.Center);
				if (d < bestDist) { bestDist = d; best = npc; }
			}
			return best;
		}

		private static void EnsureGlow()
		{
			if (_glowCreated && _glow != null && !_glow.IsDisposed) return;
			const int SIZE = 64;
			_glow = new Texture2D(Main.graphics.GraphicsDevice, SIZE, SIZE);
			Color[] data = new Color[SIZE * SIZE];
			float   c    = SIZE / 2f;
			for (int y = 0; y < SIZE; y++)
			for (int x = 0; x < SIZE; x++)
			{
				float dist = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
				float a    = MathHelper.Clamp(1f - dist, 0f, 1f);
				a = a * a * a;
				byte b = (byte)(a * 255f);
				data[y * SIZE + x] = new Color(b, b, b, b);
			}
			_glow.SetData(data);
			_glowCreated = true;
		}
	}
}
