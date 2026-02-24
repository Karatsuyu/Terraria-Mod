using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// CLASE BASE para las 19 espadas del Melee2
	/// Orbit = 100% vanilla Zenith (AI_182_FinalFractal)
	/// Visual FX = trail rojo/negro en punta + humo estilo Galaxia
	/// SIN SpriteBatch.End/Begin (causa crash silencioso en tModLoader)
	/// </summary>
	public abstract class AmeBladeBase : ModProjectile
	{
		// Color aleatorio por espada: rojo o negro (decidido al spawn)
		private bool _colorInitialized;
		private bool _isRedVariant = true;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 60;
			Projectile.height = 60;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
			Projectile.timeLeft = 300;
		}

		// CRITICAL: Desactivar position += velocity automático
		public override bool ShouldUpdatePosition() => false;

		// Dirección de la punta de la espada (para spawn de dust)
		private Vector2 BladeDirection => (Projectile.rotation - MathHelper.ToRadians(45f)).ToRotationVector2();
		private Vector2 BladeTip => Projectile.Center + BladeDirection * (40f * Projectile.scale);

		public override void AI()
		{
			// ====== INICIALIZACIÓN ======
			if (Projectile.localAI[1] == 0f)
			{
				Projectile.localAI[1] = 1f;
				SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
			}

			if (!_colorInitialized)
			{
				_colorInitialized = true;
				_isRedVariant = Main.rand.NextBool();
			}

			Player player = Main.player[Projectile.owner];
			Vector2 mountedCenter = player.MountedCenter;

			// ====== TIEMPO (vanilla exacto) ======
			float lerpValue = Utils.GetLerpValue(900f, 0f, Projectile.velocity.Length() * 2f, clamped: true);
			float num = MathHelper.Lerp(0.7f, 2f, lerpValue);
			Projectile.localAI[0] += num;

			if (Projectile.localAI[0] >= 120f)
			{
				Projectile.Kill();
				return;
			}

			// ====== PROGRESO (vanilla exacto) ======
			float lerpValue2 = Utils.GetLerpValue(0f, 1f, Projectile.localAI[0] / 60f, clamped: true);
			float num2 = Projectile.localAI[0] / 60f;
			float num3 = Projectile.ai[0];
			float num4 = Projectile.velocity.ToRotation();
			float num5 = (float)Math.PI;
			float num6 = ((Projectile.velocity.X > 0f) ? 1 : (-1));

			// ====== ÁNGULO DE ÓRBITA (vanilla exacto) ======
			float num7 = num5 + num6 * lerpValue2 * ((float)Math.PI * 2f);

			// ====== RADIO (vanilla exacto) ======
			float num8 = Projectile.velocity.Length() + Utils.GetLerpValue(0.5f, 1f, lerpValue2, clamped: true) * 40f;
			if (num8 < 60f)
				num8 = 60f;

			// ====== CENTRO (vanilla exacto) ======
			Vector2 vector = mountedCenter + Projectile.velocity;

			// ====== SPINNINGPOINT (vanilla exacto) ======
			Vector2 spinningpoint = new Vector2(1f, 0f).RotatedBy(num7) *
				new Vector2(num8, num3 * MathHelper.Lerp(2f, 1f, lerpValue));

			// ====== POSICIÓN (vanilla exacto) ======
			Vector2 vector2 = vector + spinningpoint.RotatedBy(num4);
			Vector2 vector3 = (1f - Utils.GetLerpValue(0f, 0.5f, lerpValue2, clamped: true)) *
				new Vector2((float)((Projectile.velocity.X > 0f) ? 1 : (-1)) * (0f - num8) * 0.1f, (0f - Projectile.ai[0]) * 0.3f);

			// Posición final = 100% VANILLA
			Projectile.Center = vector2 + vector3;

			// ====== ROTACIÓN (vanilla + ajuste -45° para sprites diagonales) ======
			float num10 = num7 + num4;
			Projectile.rotation = num10 + (float)Math.PI / 2f - MathHelper.ToRadians(45f);
			Projectile.spriteDirection = Projectile.direction = ((Projectile.velocity.X > 0f) ? 1 : (-1));

			if (num3 < 0f)
			{
				Projectile.rotation = num5 + num6 * lerpValue2 * ((float)Math.PI * -2f) + num4;
				Projectile.rotation += (float)Math.PI / 2f - MathHelper.ToRadians(45f);
				Projectile.spriteDirection = Projectile.direction = ((!(Projectile.velocity.X > 0f)) ? 1 : (-1));
			}

			// Opacidad: vanilla fade in/out
			Projectile.Opacity = Utils.GetLerpValue(0f, 5f, Projectile.localAI[0], clamped: true) *
				Utils.GetLerpValue(120f, 115f, Projectile.localAI[0], clamped: true);

			// Escala aleatoria (solo primera vez)
			if (Projectile.localAI[0] <= num + 0.1f)
				Projectile.scale = 0.8f + Main.rand.NextFloat(0f, 0.4f);

			// ====================================================================
			// EFECTOS VISUALES - Solo Dust, no afectan gameplay
			// ====================================================================
			if (Projectile.Opacity > 0.2f)
				SpawnVisualDust(num2, player);

			// Iluminación
			if (_isRedVariant)
				Lighting.AddLight(Projectile.Center, 0.9f, 0.2f, 0.1f);
			else
				Lighting.AddLight(Projectile.Center, 0.35f, 0.05f, 0.5f);

			// Luz adicional en la punta
			Vector2 tip = BladeTip;
			if (_isRedVariant)
				Lighting.AddLight(tip, 1.0f, 0.3f, 0.1f);
			else
				Lighting.AddLight(tip, 0.4f, 0.1f, 0.6f);
		}

		/// <summary>
		/// Genera TODAS las partículas visuales por frame.
		/// Se llama cada AI tick (2x por game tick por extraUpdates=1).
		/// </summary>
		private void SpawnVisualDust(float progress, Player player)
		{
			Vector2 tip = BladeTip;
			Vector2 bladeDir = BladeDirection;
			// Velocidad "hacia atrás" de la punta (opuesta a donde apunta la espada)
			Vector2 backVel = -bladeDir * 2f;

			// ══════════════════════════════════════════════════════
			// PUNTA: Rastro continuo de fuego/energía (CADA frame)
			// Esto crea el trail persistente rojo o negro en la punta
			// ══════════════════════════════════════════════════════
			{
				int tipDust = _isRedVariant ? DustID.Torch : DustID.PurpleTorch;
				for (int i = 0; i < 2; i++)
				{
					Vector2 offset = Main.rand.NextVector2Circular(4f, 4f);
					Dust d = Dust.NewDustDirect(
						tip + offset, 2, 2, tipDust,
						backVel.X * 0.4f + Main.rand.NextFloat(-0.5f, 0.5f),
						backVel.Y * 0.4f + Main.rand.NextFloat(-0.5f, 0.5f),
						150, default, Main.rand.NextFloat(1.5f, 2.2f)
					);
					d.noGravity = true;
					d.velocity += player.velocity * 0.05f;
				}
			}

			// ══════════════════════════════════════════════════════
			// PUNTA: Chispas brillantes más pequeñas
			// ══════════════════════════════════════════════════════
			if (Main.rand.NextBool(2))
			{
				int sparkDust = _isRedVariant ? DustID.RedTorch : DustID.ShadowbeamStaff;
				Vector2 offset = Main.rand.NextVector2Circular(6f, 6f);
				Dust d = Dust.NewDustDirect(
					tip + offset, 2, 2, sparkDust,
					Main.rand.NextFloat(-1.5f, 1.5f),
					Main.rand.NextFloat(-1.5f, 1.5f),
					0, default, Main.rand.NextFloat(0.8f, 1.4f)
				);
				d.noGravity = true;
			}

			// ══════════════════════════════════════════════════════
			// CUERPO: Humo denso detrás de la espada (estilo Galaxia)
			// Spawn a lo largo del cuerpo de la espada, no solo punta
			// ══════════════════════════════════════════════════════
			if (progress < 1.5f) // Solo durante órbita activa + un poco después
			{
				// Humo principal - DustID.Smoke con tinte de color
				for (int i = 0; i < 2; i++)
				{
					float along = Main.rand.NextFloat(-0.3f, 0.9f); // posición a lo largo de la espada
					Vector2 pos = Projectile.Center + bladeDir * (along * 35f * Projectile.scale);
					Vector2 spread = Main.rand.NextVector2Circular(8f, 8f);

					Dust smoke = Dust.NewDustDirect(
						pos + spread, 4, 4, DustID.Smoke,
						backVel.X * 0.2f + Main.rand.NextFloat(-0.4f, 0.4f),
						backVel.Y * 0.2f + Main.rand.NextFloat(-0.4f, 0.4f),
						160,
						_isRedVariant ? new Color(140, 15, 15) : new Color(45, 5, 55),
						Main.rand.NextFloat(1.3f, 2.2f)
					);
					smoke.noGravity = true;
					smoke.velocity *= 0.5f;
					smoke.velocity += player.velocity * 0.05f;
					smoke.fadeIn = Main.rand.NextFloat(1.0f, 1.6f);
				}

				// Humo brillante adicional (más luminoso) con menor frecuencia
				if (Main.rand.NextBool(3))
				{
					int glowSmoke = _isRedVariant ? DustID.Torch : DustID.PurpleTorch;
					float along = Main.rand.NextFloat(0f, 0.7f);
					Vector2 pos = Projectile.Center + bladeDir * (along * 30f * Projectile.scale);
					Vector2 spread = Main.rand.NextVector2Circular(10f, 10f);

					Dust d = Dust.NewDustDirect(
						pos + spread, 4, 4, glowSmoke,
						Main.rand.NextFloat(-0.6f, 0.6f),
						Main.rand.NextFloat(-0.6f, 0.6f),
						100, default, Main.rand.NextFloat(1.0f, 1.8f)
					);
					d.noGravity = true;
					d.velocity *= 0.3f;
				}
			}

			// ══════════════════════════════════════════════════════
			// DESTELLOS: Gemas/cristales alrededor de la espada
			// Solo durante la órbita activa (progress < 1.0)
			// ══════════════════════════════════════════════════════
			if (progress < 1.0f && Main.rand.NextBool(3))
			{
				float dist = Projectile.scale * 40f;
				Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(dist, dist);
				int gemDust = _isRedVariant ? DustID.GemRuby : DustID.GemAmethyst;

				Dust d = Dust.NewDustDirect(
					pos, 2, 2, gemDust,
					0f, 0f, 0, default, Main.rand.NextFloat(0.5f, 1.1f)
				);
				d.noGravity = true;
				d.velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
				d.fadeIn = 0.6f;
			}

			// ══════════════════════════════════════════════════════
			// SHADOWFLAME: Efecto de llama oscura en el filo
			// ══════════════════════════════════════════════════════
			if (Main.rand.NextBool(2))
			{
				float along = Main.rand.NextFloat(0.3f, 1.0f);
				Vector2 pos = Projectile.Center + bladeDir * (along * 40f * Projectile.scale);

				Dust d = Dust.NewDustDirect(
					pos, 4, 4, DustID.Shadowflame,
					backVel.X * 0.3f,
					backVel.Y * 0.3f,
					100, default, Main.rand.NextFloat(1.0f, 1.6f)
				);
				d.noGravity = true;
				d.velocity *= 0.4f;
			}
		}

		// RENDERING: Sin SpriteBatch switches - todo con el batch normal de tModLoader
		public override bool PreDraw(ref Color lightColor)
		{
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

			Vector2 drawOrigin = texture.Size() * 0.5f;
			Vector2 drawPosition = Projectile.Center - Main.screenPosition;

			Color drawColor = new Color(255, 255, 255, (int)(255f * Projectile.Opacity));

			// ══════════════════════════════════════════════════════
			// TRAIL: Espadas fantasma con tinte de color fuerte
			// ══════════════════════════════════════════════════════
			for (int i = 0; i < Projectile.oldPos.Length; i++)
			{
				if (Projectile.oldPos[i] == Vector2.Zero)
					continue;

				float trailAlpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;

				// Tinte fuerte rojo o púrpura
				Color tint = _isRedVariant
					? Color.Lerp(drawColor, new Color(255, 50, 50, 220), 0.5f)
					: Color.Lerp(drawColor, new Color(130, 30, 180, 220), 0.5f);

				Color trailColor = tint * trailAlpha * 0.5f;

				Vector2 trailDrawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

				Main.EntitySpriteDraw(
					texture,
					trailDrawPos,
					null,
					trailColor,
					Projectile.oldRot[i],
					drawOrigin,
					Projectile.scale * (0.65f + trailAlpha * 0.35f),
					SpriteEffects.None,
					0
				);
			}

			// ══════════════════════════════════════════════════════
			// ESPADA PRINCIPAL
			// ══════════════════════════════════════════════════════
			Main.EntitySpriteDraw(
				texture,
				drawPosition,
				null,
				drawColor,
				Projectile.rotation,
				drawOrigin,
				Projectile.scale,
				SpriteEffects.None,
				0
			);

			return false;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Rectangle expandedHitbox = projHitbox;
			expandedHitbox.Inflate(30, 30);
			if (expandedHitbox.Intersects(targetHitbox))
				return true;

			float collisionPoint = 0f;
			Vector2 start = Projectile.Center;
			Vector2 end = Projectile.Center + Projectile.rotation.ToRotationVector2() * 100f;

			if (Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(),
				targetHitbox.Size(),
				start,
				end,
				40f,
				ref collisionPoint))
			{
				return true;
			}

			return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 100f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Explosión de chispas al impactar
			for (int i = 0; i < 15; i++)
			{
				int dustType = _isRedVariant ? DustID.Torch : DustID.PurpleTorch;
				Dust d = Dust.NewDustDirect(
					target.Center - new Vector2(16f), 32, 32,
					dustType, 0f, 0f, 100, default,
					Main.rand.NextFloat(1.5f, 2.8f)
				);
				d.noGravity = true;
				d.velocity = (d.position - target.Center).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(3f, 8f);
			}

			// Humo de impacto
			for (int i = 0; i < 8; i++)
			{
				Dust smoke = Dust.NewDustDirect(
					target.Center - new Vector2(20f), 40, 40,
					DustID.Smoke, 0f, 0f, 180,
					_isRedVariant ? new Color(150, 20, 20) : new Color(50, 5, 60),
					Main.rand.NextFloat(1.8f, 3.0f)
				);
				smoke.noGravity = true;
				smoke.velocity = Main.rand.NextVector2Circular(4f, 4f);
			}

			// Shadowflame en impacto
			for (int i = 0; i < 6; i++)
			{
				Dust sf = Dust.NewDustDirect(
					target.Center - new Vector2(12f), 24, 24,
					DustID.Shadowflame, 0f, 0f, 0, default,
					Main.rand.NextFloat(1.5f, 2.5f)
				);
				sf.noGravity = true;
				sf.velocity = Main.rand.NextVector2Circular(5f, 5f);
			}

			// Gemas
			for (int i = 0; i < 5; i++)
			{
				int gem = _isRedVariant ? DustID.GemRuby : DustID.GemAmethyst;
				Dust g = Dust.NewDustDirect(
					target.Center - new Vector2(8f), 16, 16,
					gem, 0f, 0f, 0, default,
					Main.rand.NextFloat(0.8f, 1.6f)
				);
				g.noGravity = true;
				g.velocity = Main.rand.NextVector2Circular(6f, 6f);
			}
		}

		public override Color? GetAlpha(Color lightColor)
		{
			return new Color(255, 255, 255, (int)(255f * Projectile.Opacity));
		}
	}
}
