using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// Modo Melee 2: 15 espadas simultáneas con arcos variados - Sistema anterior
	/// </summary>
	public class AmeMeleeMultiBlade : ModProjectile
	{
		private Vector2 startPos;
		private Vector2 targetPos;

		// ai[0] = progreso (0 a 1)
		// ai[1] = dirección curva * variación (0.5 a 2.0)

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 60;
			Projectile.height = 60;

			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;

			Projectile.penetrate = -1;
			Projectile.timeLeft = 300;

			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;

			Projectile.extraUpdates = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
		}

		public override void OnSpawn(IEntitySource source)
		{
			Player player = Main.player[Projectile.owner];
			startPos = player.Center;
			targetPos = Main.MouseWorld;
			Projectile.ai[0] = 0f;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];

			float speed = 0.016f;
			Projectile.ai[0] += speed;
			float progress = Projectile.ai[0];

			if (progress >= 1f)
			{
				Projectile.Kill();
				return;
			}

			// Actualizar posiciones iniciales
			if (Projectile.ai[0] == speed || startPos == Vector2.Zero)
			{
				startPos = player.Center;
				targetPos = Main.MouseWorld;
			}

			// Sistema de ida y regreso
			Vector2 from, to;
			float currentProgress;

			if (progress <= 0.5f)
			{
				// Ida hacia el cursor
				from = startPos;
				to = targetPos;
				currentProgress = progress * 2f;
			}
			else
			{
				// Regreso al jugador
				from = targetPos;
				to = player.Center;
				currentProgress = (progress - 0.5f) * 2f;
			}

			// Interpolación lineal base
			Vector2 linear = Vector2.Lerp(from, to, currentProgress);

			// Calcular curvatura (función seno)
			float curve = (float)Math.Sin(currentProgress * MathHelper.Pi);
			
			// Dirección perpendicular
			Vector2 direction = (to - from).SafeNormalize(Vector2.UnitX);
			Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

			// Fuerza de curvatura variada
			float curveStrength = 120f * Projectile.ai[1];

			// Aplicar posición final con curva
			Projectile.Center = linear + perpendicular * curve * curveStrength;

			// Rotación continua
			Projectile.rotation += 0.4f;

			if (Projectile.velocity != Vector2.Zero)
			{
				Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
			}

			// Efectos visuales
			if (Main.rand.NextBool(3))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
				dust.noGravity = true;
				dust.velocity *= 0.3f;
			}
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (!projHitbox.Intersects(targetHitbox))
			{
				return false;
			}

			float collisionPoint = 0f;
			Vector2 lineStart = Projectile.Center;
			Vector2 lineDirection = Projectile.rotation.ToRotationVector2();
			Vector2 lineEnd = lineStart + lineDirection * 100f;

			if (Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(),
				targetHitbox.Size(),
				lineStart,
				lineEnd,
				25f,
				ref collisionPoint))
			{
				return true;
			}

			return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 60f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 8; i++)
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Shadowflame, 0f, 0f, 100, default, 1.5f);
				dust.noGravity = true;
				dust.velocity *= 2f;
			}

			target.velocity += Projectile.velocity.SafeNormalize(Vector2.UnitY) * 3f;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
			Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, Projectile.height * 0.5f);

			for (int k = 0; k < Projectile.oldPos.Length; k++)
			{
				if (Projectile.oldPos[k] == Vector2.Zero)
					continue;

				Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
				Color color = Projectile.GetAlpha(lightColor) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
				Main.EntitySpriteDraw(texture, drawPos, null, color * 0.5f, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
			}

			return true;
		}
	}
}
