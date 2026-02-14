using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Ame.Projectiles.Modes
{
	public class AmeSummonMinion : ModProjectile
	{
		public override void SetStaticDefaults()
		{
			// DisplayName se maneja via archivos de localización
			Main.projFrames[Projectile.type] = 1;
			ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
			ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
			ProjectileID.Sets.CultistIsResistantTo[Projectile.type] = true;
		}

		public override void SetDefaults()
		{
			Projectile.width = 40;
			Projectile.height = 40;
			Projectile.friendly = true;
			Projectile.minion = true;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.minionSlots = 1f;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 18000;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			
			// Verificar que el jugador siga vivo y tenga el minion activo
			if (player.dead || !player.active)
			{
				player.ClearBuff(ModContent.BuffType<Buffs.AmeMinionBuff>());
			}
			if (player.HasBuff(ModContent.BuffType<Buffs.AmeMinionBuff>()))
			{
				Projectile.timeLeft = 2;
			}
			
			// Buscar enemigos
			NPC target = FindTarget();
			
			if (target != null)
			{
				// Moverse hacia el enemigo
				Vector2 direction = target.Center - Projectile.Center;
				direction.Normalize();
				
				float speed = 8f;
				Projectile.velocity = (Projectile.velocity * 20f + direction * speed) / 21f;
				
				// Atacar si está cerca
				if (Vector2.Distance(Projectile.Center, target.Center) < 100f && Projectile.ai[0]++ > 30)
				{
					Projectile.ai[0] = 0;
					
					// Disparar proyectil
					if (Main.myPlayer == Projectile.owner)
					{
						Vector2 shootVel = direction * 10f;
						Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootVel,
							ProjectileID.MiniRetinaLaser, Projectile.damage, Projectile.knockBack, Projectile.owner);
					}
				}
			}
			else
			{
				// Seguir al jugador cuando no hay enemigos
				Vector2 toPlayer = player.Center - Projectile.Center;
				float distance = toPlayer.Length();
				
				if (distance > 200f)
				{
					toPlayer.Normalize();
					Projectile.velocity = (Projectile.velocity * 20f + toPlayer * 10f) / 21f;
				}
				else
				{
					// Orbitar alrededor del jugador
					Projectile.ai[1] += 0.05f;
					Vector2 offset = new Vector2(100f, 0).RotatedBy(Projectile.ai[1]);
					Projectile.velocity = (player.Center + offset - Projectile.Center) * 0.1f;
				}
			}
			
			// Rotación visual
			Projectile.rotation += 0.1f;
			
			// Efectos de polvo
			if (Main.rand.NextBool(5))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.YellowTorch, 0f, 0f, 100, default, 1f);
				dust.noGravity = true;
			}
		}

		private NPC FindTarget()
		{
			float detectionRange = 600f;
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
			
			return closest;
		}

		public override bool? CanCutTiles()
		{
			return false;
		}

		public override bool MinionContactDamage()
		{
			return true;
		}
	}
}
