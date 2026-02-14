using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Ame.Projectiles.Modes
{
	public class AmeMeleeSlash : ModProjectile
	{
		public override void SetStaticDefaults()
		{
			// DisplayName se maneja via archivos de localización
		}

		public override void SetDefaults()
		{
			Projectile.width = 80;
			Projectile.height = 80;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.timeLeft = 20;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ownerHitCheck = true;
			Projectile.alpha = 0;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			
			// Seguir al jugador
			Projectile.Center = player.Center;
			
			// Rotar el proyectil
			Projectile.rotation += 0.4f;
			
			// Ajustar dirección del sprite
			Projectile.direction = player.direction;
			Projectile.spriteDirection = player.direction;
			
			// Efectos visuales
			if (Main.rand.NextBool(3))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.Shadowflame, 0f, 0f, 100, default, 1.5f);
				dust.noGravity = true;
				dust.velocity *= 0.5f;
			}
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			// Bonus de daño crítico
			modifiers.CritDamage *= 1.2f;
		}
	}
}
