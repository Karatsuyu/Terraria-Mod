using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Ame.Projectiles.Modes
{
	public class AmeClone : ModProjectile
	{
		private float aiTimer = 0f;
		
		public override void SetStaticDefaults()
		{
			// DisplayName se maneja via archivos de localización
			Main.projFrames[Projectile.type] = 1;
		}

		public override void SetDefaults()
		{
			Projectile.width = 40;
			Projectile.height = 40;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Generic;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 600; // 10 segundos
			Projectile.tileCollide = false;
			Projectile.alpha = 100;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			aiTimer++;
			
			// Posicionar el clon detrás del jugador
			if (aiTimer < 30)
			{
				// Inicialización: aparecer detrás del jugador
				Vector2 targetPos = player.Center - new Vector2(player.direction * 80, 0);
				Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.1f);
			}
			else
			{
				// Buscar y atacar enemigos
				NPC target = FindClosestEnemy();
				
				if (target != null)
				{
					// Atacar al enemigo
					Vector2 direction = target.Center - Projectile.Center;
					float distance = direction.Length();
					
					if (distance > 50f)
					{
						direction.Normalize();
						Projectile.velocity = direction * 10f;
					}
					else
					{
						Projectile.velocity *= 0.95f;
						
						// Disparar proyectiles cada cierto tiempo
						if (aiTimer % 20 == 0 && Main.myPlayer == Projectile.owner)
						{
							Vector2 shootVel = direction;
							shootVel.Normalize();
							shootVel *= 12f;
							
							Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootVel,
								ProjectileID.ShadowBeamFriendly, Projectile.damage / 2, Projectile.knockBack, 
								Projectile.owner);
						}
					}
				}
				else
				{
					// Volver cerca del jugador si no hay enemigos
					Vector2 targetPos = player.Center - new Vector2(player.direction * 80, 0);
					Vector2 toTarget = targetPos - Projectile.Center;
					float dist = toTarget.Length();
					
					if (dist > 150f)
					{
						toTarget.Normalize();
						Projectile.velocity = toTarget * 12f;
					}
					else
					{
						Projectile.velocity *= 0.9f;
					}
				}
			}
			
			// Rotación suave
			Projectile.rotation = Projectile.velocity.X * 0.05f;
			
			// Efectos visuales de clon
			if (Main.rand.NextBool(3))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.PurpleTorch, 0f, 0f, 100, new Color(200, 100, 255), 1.5f);
				dust.noGravity = true;
				dust.velocity *= 0.5f;
			}
			
			// Efecto de transparencia pulsante
			Projectile.alpha = 100 + (int)(50 * System.Math.Sin(aiTimer * 0.1f));
		}

		private NPC FindClosestEnemy()
		{
			float detectionRange = 500f;
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

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Efecto de impacto sombra
			for (int i = 0; i < 8; i++)
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.PurpleTorch, 0f, 0f, 100, new Color(200, 100, 255), 1.5f);
				dust.velocity *= 2f;
			}
		}

		public override void OnKill(int timeLeft)
		{
			// Efecto de desaparición
			for (int i = 0; i < 20; i++)
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 
					DustID.PurpleTorch, 0f, 0f, 100, new Color(200, 100, 255), 2f);
				dust.noGravity = true;
				dust.velocity *= 3f;
			}
		}
	}
}
