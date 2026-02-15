using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// Modo Melee 1 - Sistema Zenith con progreso normalizado
	/// ai[0] = variación aleatoria del arco (-100 a 100)
	/// localAI[0] = progreso (0 a 1)
	/// localAI[1] = fase (0 = ida, 1 = regreso)
	/// </summary>
	public class AmeZenithBlade : ModProjectile
	{
		private Vector2 startPosition;
		private Vector2 targetPosition;
		private Vector2 direction;
		private float distance;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 40;
			Projectile.height = 40;
			Projectile.friendly = true;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 300;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
		}

		public override void OnSpawn(IEntitySource source)
		{
			Player player = Main.player[Projectile.owner];

			startPosition = player.MountedCenter;

			// 🔥 Guardamos el destino UNA sola vez
			targetPosition = Main.MouseWorld;

			direction = targetPosition - startPosition;
			distance = direction.Length();

			if (distance == 0)
				distance = 1f;

			direction.Normalize();

			Projectile.localAI[0] = 0f; // progreso
			Projectile.localAI[1] = 0f; // fase (0 = ida, 1 = regreso)
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];

			if (Projectile.localAI[1] == 0f)
			{
				// =========================
				// 🟢 FASE 0 – IDA EXACTA
				// =========================

				Projectile.localAI[0] += 0.08f; // velocidad de progreso

				float progress = Projectile.localAI[0];

				if (progress >= 1f)
				{
					progress = 1f;
					Projectile.localAI[1] = 1f; // cambiar a fase regreso
				}

				// 🎯 Interpolación exacta - NO se pasa del cursor
				Projectile.Center = startPosition + direction * distance * progress;
			}
			else
			{
				// =========================
				// 🔵 FASE 1 – REGRESO SUAVE
				// =========================

				// 🔥 Posición detrás del jugador - desaparece en la espalda
				Vector2 backPosition = player.MountedCenter - direction * 30f;

				Vector2 toPlayer = backPosition - Projectile.Center;

				float returnSpeed = 35f;

				Vector2 desiredVelocity = toPlayer.SafeNormalize(Vector2.Zero) * returnSpeed;

				// Suavizado estilo Zenith
				Projectile.velocity = Vector2.Lerp(
					Projectile.velocity,
					desiredVelocity,
					0.2f
				);

				Projectile.Center += Projectile.velocity;

				if (toPlayer.Length() < 25f)
				{
					Projectile.Kill();
				}
			}

			// Rotación alineada a movimiento
			if (Projectile.velocity.Length() > 0.1f)
				Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

			Lighting.AddLight(Projectile.Center, 0.6f, 0.2f, 0.8f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
			Vector2 drawOrigin = texture.Size() * 0.5f;

			for (int i = 0; i < Projectile.oldPos.Length; i++)
			{
				if (Projectile.oldPos[i] == Vector2.Zero)
					continue;

				float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
				Color color = Color.White * alpha * 0.6f;

				Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + drawOrigin;

				Main.EntitySpriteDraw(
					texture,
					drawPos,
					null,
					color,
					Projectile.rotation,
					drawOrigin,
					Projectile.scale,
					SpriteEffects.None,
					0
				);
			}

			return true;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			// Sistema de colisión mejorado para mejor detección
			if (projHitbox.Intersects(targetHitbox))
				return true;

			// Colisión por línea (útil para espadas rápidas)
			Vector2 start = Projectile.Center;
			Vector2 end = Projectile.Center + Projectile.velocity;
			float collisionPoint = 0f;
			
			return Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(),
				targetHitbox.Size(),
				start,
				end,
				Projectile.width * 0.5f,
				ref collisionPoint
			);
		}
	}
}
