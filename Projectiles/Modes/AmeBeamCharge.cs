using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// MAGIC MODE — Círculo mágico de carga en la espalda del jugador.
	/// No hace daño. Solo visual de carga antes de disparar el beam.
	/// 
	/// Ciclo: aparece cuando empieza a cargarse, crece hasta CHARGE_TIME ticks,
	/// luego se mantiene pulsando hasta que el jugador dispara o suelta el click.
	/// 
	/// ai[0] = progreso de carga (0 → CHARGE_TIME), escrito por AmeWeapon cada frame
	/// </summary>
	public class AmeBeamCharge : ModProjectile
	{
		// ═══════════════════════════════════════════════════════
		// CONSTANTES
		// ═══════════════════════════════════════════════════════

		public const float CHARGE_TIME = 55f; // ticks para carga completa (~0.9s)

		private const int   RUNE_COUNT      = 12;   // runas en el anillo exterior
		private const int   INNER_RUNE_COUNT = 6;   // runas en el anillo interior
		private const float OUTER_RADIUS    = 68f;
		private const float INNER_RADIUS    = 38f;
		private const float CORE_RADIUS     = 18f;

		// ═══════════════════════════════════════════════════════
		// ESTADO
		// ═══════════════════════════════════════════════════════

		private float ChargeProgress => MathHelper.Clamp(Projectile.ai[0] / CHARGE_TIME, 0f, 1f);
		private bool  IsFullyCharged => Projectile.ai[0] >= CHARGE_TIME;

		private float _rotOuter;   // rotación del anillo exterior
		private float _rotInner;   // rotación del anillo interior (opuesta)
		private float _rotCore;    // rotación del núcleo
		private float _pulseTimer;

		private static Texture2D _glow;
		private static bool      _glowCreated;

		// ═══════════════════════════════════════════════════════
		// SETUP
		// ═══════════════════════════════════════════════════════

		public override void SetDefaults()
		{
			Projectile.width    = 10;
			Projectile.height   = 10;
			Projectile.friendly = false;
			Projectile.hostile  = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = int.MaxValue;
			Projectile.hide     = false;
			Projectile.alpha    = 255; // invisible excepto por PreDraw
		}

		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;
		public override bool ShouldUpdatePosition() => false;

		// ═══════════════════════════════════════════════════════
		// AI
		// ═══════════════════════════════════════════════════════

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead) { Projectile.Kill(); return; }

			// Pegarse a la espalda del jugador
			float backOffset = owner.direction * -22f;
			Projectile.Center = owner.MountedCenter + new Vector2(backOffset, -8f);

			// Rotar anillos
			float speed = 0.5f + ChargeProgress * 1.5f; // acelera con la carga
			_rotOuter  += 0.018f * speed;
			_rotInner  -= 0.028f * speed;
			_rotCore   += 0.055f * speed;
			_pulseTimer += 0.08f;

			// Opacidad: fade-in rápido
			Projectile.Opacity = MathHelper.Clamp(Projectile.Opacity + 0.07f, 0f, 1f);

			// Luz dinámica
			float intensity = 0.4f + ChargeProgress * 1.2f;
			Lighting.AddLight(Projectile.Center, intensity * 0.9f, intensity * 0.05f, intensity * 0.02f);

			// Partículas en la carga completa
			if (IsFullyCharged && Main.rand.NextBool(2))
			{
				float angle  = Main.rand.NextFloat(0f, MathHelper.TwoPi);
				float radius = OUTER_RADIUS + Main.rand.NextFloat(0f, 20f);
				Vector2 pos  = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
				Dust d = Dust.NewDustDirect(pos, 2, 2, DustID.Shadowflame);
				d.velocity  = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 4f);
				d.noGravity = true;
				d.scale     = 0.9f + Main.rand.NextFloat(0f, 0.6f);
			}

			// Partículas de acumulación durante la carga
			if (!IsFullyCharged && Main.rand.NextBool(4))
			{
				float angle  = Main.rand.NextFloat(0f, MathHelper.TwoPi);
				float radius = Main.rand.NextFloat(OUTER_RADIUS * 0.5f, OUTER_RADIUS * 1.8f);
				Vector2 pos  = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
				Dust d = Dust.NewDustDirect(pos, 2, 2, DustID.Shadowflame);
				d.velocity  = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * (2f + ChargeProgress * 3f);
				d.noGravity = true;
				d.scale     = 0.6f;
				d.fadeIn    = 0.4f;
			}
		}

		// ═══════════════════════════════════════════════════════
		// DRAW — círculo mágico completo
		// ═══════════════════════════════════════════════════════

		public override bool PreDraw(ref Color lightColor)
		{
			EnsureGlow();
			if (_glow == null) return false;

			float charge  = ChargeProgress;
			float opacity = Projectile.Opacity * charge;
			if (opacity < 0.01f) return false;

			Vector2 center     = Projectile.Center - Main.screenPosition;
			Vector2 glowOrigin = new Vector2(_glow.Width * 0.5f, _glow.Height * 0.5f);

			// ── CAMBIAR A ADITIVO ─────────────────────────────────
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate,
				BlendState.Additive,
				SamplerState.LinearClamp,
				DepthStencilState.None,
				Main.Rasterizer,
				null,
				Main.GameViewMatrix.TransformationMatrix
			);

			// ── GLOW DE FONDO ─────────────────────────────────────
			float bgScale = (OUTER_RADIUS * 2.4f) / _glow.Width * charge;
			float pulse   = 1f + MathF.Sin(_pulseTimer) * 0.06f;
			Color bgColor = new Color(100, 0, 0) * (opacity * 0.55f * pulse);
			Main.spriteBatch.Draw(_glow, center, null, bgColor, 0f, glowOrigin, bgScale * pulse, SpriteEffects.None, 0);

			// ── ANILLO EXTERIOR — runas ───────────────────────────
			DrawRuneRing(center, glowOrigin, OUTER_RADIUS, RUNE_COUNT, _rotOuter, charge, opacity,
				new Color(255, 15, 0), new Color(90, 0, 0));

			// ── ANILLO INTERIOR ───────────────────────────────────
			DrawRuneRing(center, glowOrigin, INNER_RADIUS, INNER_RUNE_COUNT, _rotInner, charge, opacity,
				new Color(200, 8, 0), new Color(50, 0, 0));

			// ── LÍNEAS DEL PENTÁGONO ──────────────────────────────
			DrawPentagonLines(center, glowOrigin, INNER_RADIUS * 0.85f, _rotCore, charge, opacity);

			// ── NÚCLEO ────────────────────────────────────────────
			DrawCore(center, glowOrigin, charge, opacity);

			// ── FLASH EN CARGA COMPLETA ───────────────────────────
			if (IsFullyCharged)
			{
				float fp    = (MathF.Sin(_pulseTimer * 3f) + 1f) * 0.5f;
				Color fCol  = new Color(255, 50, 0) * (opacity * 0.55f * fp);
				float fScl  = bgScale * 0.7f * (1f + fp * 0.18f);
				Main.spriteBatch.Draw(_glow, center, null, fCol, _rotOuter, glowOrigin, fScl, SpriteEffects.None, 0);
			}

			// ── RESTAURAR ─────────────────────────────────────────
			Main.spriteBatch.End();
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

		/// <summary>
		/// Dibuja un anillo de runas (puntos de glow con variación angular).
		/// </summary>
		private void DrawRuneRing(Vector2 center, Vector2 glowOrigin,
			float radius, int count, float rotation,
			float charge, float opacity, Color colorA, Color colorB)
		{
			float sizeScale = radius / _glow.Width * 0.55f;

			for (int i = 0; i < count; i++)
			{
				float angle      = (MathHelper.TwoPi / count) * i + rotation;
				float appear     = MathHelper.Clamp((charge * count) - i, 0f, 1f);
				float runeOpacity = opacity * appear;
				if (runeOpacity < 0.01f) continue;

				Vector2 runePos = center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);

				float lerp  = (MathF.Sin(_pulseTimer + i * 0.8f) + 1f) * 0.5f;
				Color color = Color.Lerp(colorA, colorB, lerp) * runeOpacity;
				Main.spriteBatch.Draw(_glow, runePos, null, color, angle, glowOrigin, sizeScale, SpriteEffects.None, 0);

				if (i % 2 == 0)
				{
					float sparkle   = (MathF.Sin(_pulseTimer * 2.5f + i) + 1f) * 0.5f;
					Color sparkColor = new Color(255, 60, 10) * (runeOpacity * sparkle * 0.7f);
					Main.spriteBatch.Draw(_glow, runePos, null, sparkColor, 0f, glowOrigin, sizeScale * 0.5f, SpriteEffects.None, 0);
				}
			}

			// Arco de conexión entre runas
			int arcSteps = count * 4;
			for (int s = 0; s < arcSteps; s++)
			{
				float   angle    = (MathHelper.TwoPi / arcSteps) * s + rotation;
				float   arcApp   = MathHelper.Clamp(charge * 3f - (float)s / arcSteps, 0f, 1f);
				if (arcApp < 0.01f) continue;

				Vector2 arcPos   = center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
				Color   arcColor = colorB * (opacity * arcApp * 0.45f);
				Main.spriteBatch.Draw(_glow, arcPos, null, arcColor, 0f, glowOrigin, sizeScale * 0.22f, SpriteEffects.None, 0);
			}
		}

		private void DrawPentagonLines(Vector2 center, Vector2 glowOrigin,
			float radius, float rotation, float charge, float opacity)
		{
			const int SIDES    = 5;
			float     dotScale = radius / _glow.Width * 0.18f;
			Color     lineCol  = new Color(180, 10, 0) * (opacity * charge * 0.65f);

			Vector2[] verts = new Vector2[SIDES];
			for (int i = 0; i < SIDES; i++)
			{
				float a = (MathHelper.TwoPi / SIDES) * i + rotation;
				verts[i] = center + new Vector2(MathF.Cos(a) * radius, MathF.Sin(a) * radius);
			}

			int[][] starEdges = { new[]{0,2}, new[]{2,4}, new[]{4,1}, new[]{1,3}, new[]{3,0} };
			foreach (var edge in starEdges)
			{
				Vector2 from = verts[edge[0]];
				Vector2 to   = verts[edge[1]];
				int     dots = 14;
				for (int d = 0; d <= dots; d++)
				{
					float   t   = (float)d / dots;
					float   app = MathHelper.Clamp(charge * 2f - t, 0f, 1f);
					Vector2 pt  = Vector2.Lerp(from, to, t);
					Main.spriteBatch.Draw(_glow, pt, null, lineCol * app, 0f, glowOrigin, dotScale, SpriteEffects.None, 0);
				}
			}
		}

		private void DrawCore(Vector2 center, Vector2 glowOrigin, float charge, float opacity)
		{
			float pulse     = 1f + MathF.Sin(_pulseTimer * 4f) * 0.12f;
			float coreScale = CORE_RADIUS / _glow.Width * charge * pulse;

			Color coreOuter = new Color(220, 10, 0) * (opacity * 0.9f);
			Main.spriteBatch.Draw(_glow, center, null, coreOuter, _rotCore, glowOrigin, coreScale * 1.6f, SpriteEffects.None, 0);

			Color coreMid = new Color(80, 3, 0) * (opacity * charge);
			Main.spriteBatch.Draw(_glow, center, null, coreMid, 0f, glowOrigin, coreScale * 0.9f, SpriteEffects.None, 0);

			if (charge > 0.7f)
			{
				float    w     = (charge - 0.7f) / 0.3f;
				Color    cw    = new Color(255, 190, 160) * (opacity * w * 1f * pulse);
				Main.spriteBatch.Draw(_glow, center, null, cw, 0f, glowOrigin, coreScale * 0.4f, SpriteEffects.None, 0);
			}
		}

		// ═══════════════════════════════════════════════════════
		// GLOW TEXTURE
		// ═══════════════════════════════════════════════════════

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
				float d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
				float a = MathHelper.Clamp(1f - d, 0f, 1f);
				a = a * a * a;
				byte b = (byte)(a * 255f);
				data[y * SIZE + x] = new Color(b, b, b, b);
			}
			_glow.SetData(data);
			_glowCreated = true;
		}
	}
}
