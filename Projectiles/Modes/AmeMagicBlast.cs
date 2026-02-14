using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Ame.Projectiles.Modes
{
	public class AmeMagicBlast : ModProjectile
	{
		public override void SetStaticDefaults()
		{
			// DisplayName se maneja via archivos de localización
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Magic;
			Projectile.timeLeft = 180;
			Projectile.penetrate = 3;
			Projectile.alpha = 50;
			Projectile.light = 0.5f;
		}

		public override void AI()
		{
			// Rotación basada en velocidad
			Projectile.rotation = Projectile.velocity.ToRotation();
			
			// Efectos de partículas mágicas
			if (Main.rand.NextBool(2))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.MagicMirror, 0f, 0f, 100, new Color(100, 100, 255), 2f);
				dust.noGravity = true;
				dust.velocity *= 0.3f;
			}
			
			// Homing ligero hacia enemigos
			float detectionRange = 300f;
			float speed = 2f;
			
			NPC closest = null;
			float closestDistance = detectionRange;
			
			foreach (NPC npc in Main.npc)
			{
				if (npc.CanBeChasedBy() && npc.active)
				{
					float distance = Vector2.Distance(npc.Center, Projectile.Center);
					if (distance < closestDistance)
					{
						closestDistance = distance;
						closest = npc;
					}
				}
			}
			
			if (closest != null)
			{
				Vector2 direction = closest.Center - Projectile.Center;
				direction.Normalize();
				Projectile.velocity = (Projectile.velocity * 20f + direction * speed) / 21f;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Efecto de explosión mágica al impactar
			for (int i = 0; i < 10; i++)
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.MagicMirror, 0f, 0f, 100, new Color(100, 100, 255), 1.5f);
				dust.noGravity = true;
				dust.velocity *= 2f;
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			// Rebote en tiles
			if (Projectile.velocity.X != oldVelocity.X)
			{
				Projectile.velocity.X = -oldVelocity.X * 0.5f;
			}
			if (Projectile.velocity.Y != oldVelocity.Y)
			{
				Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
			}
			return false;
		}
	}
}
