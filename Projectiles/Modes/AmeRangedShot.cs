using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Ame.Projectiles.Modes
{
	public class AmeRangedShot : ModProjectile
	{
		public override void SetStaticDefaults()
		{
			// DisplayName se maneja via archivos de localización
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Ranged;
			Projectile.timeLeft = 300;
			Projectile.penetrate = 1;
			Projectile.alpha = 0;
			Projectile.light = 0.3f;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			// Rotación basada en velocidad
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			
			// Efecto de estela
			if (Main.rand.NextBool(3))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.GreenTorch, 0f, 0f, 100, default, 1f);
				dust.noGravity = true;
				dust.velocity *= 0.1f;
			}
			
			// Gravedad ligera
			Projectile.velocity.Y += 0.1f;
			
			// Límite de velocidad vertical
			if (Projectile.velocity.Y > 16f)
			{
				Projectile.velocity.Y = 16f;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Efecto de impacto
			for (int i = 0; i < 5; i++)
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.GreenTorch, 0f, 0f, 100, default, 1.2f);
				dust.velocity *= 1.5f;
			}
			
			// Aplicar debuff
			target.AddBuff(BuffID.Poisoned, 180); // 3 segundos de veneno
		}

		public override void OnKill(int timeLeft)
		{
			// Efecto al destruirse
			for (int i = 0; i < 10; i++)
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.GreenTorch, 0f, 0f, 100, default, 1f);
				dust.velocity *= 1.5f;
			}
		}
	}
}
