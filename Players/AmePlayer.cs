using Terraria;
using Terraria.ModLoader;

namespace Ame.Players
{
	/// <summary>
	/// ModPlayer que maneja estados y efectos específicos del jugador en AmeMod
	/// </summary>
	public class AmePlayer : ModPlayer
	{
		// Variables para tracking de estados
		public bool hasAmeWeapon = false;
		
		public override void ResetEffects()
		{
			hasAmeWeapon = false;
		}

		public override void PostUpdateEquips()
		{
			// Verificar si el jugador tiene el arma Ame equipada
			if (Player.HeldItem.ModItem is Items.AmeWeapon)
			{
				hasAmeWeapon = true;
			}
		}

		public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
		{
			// Bonus adicional para proyectiles de Ame
			if (proj.ModProjectile != null)
			{
				string typeName = proj.ModProjectile.GetType().Namespace ?? "";
				if (typeName.Contains("Ame.Projectiles.Modes"))
				{
					// 5% de bonus de daño para todos los proyectiles de Ame
					modifiers.FinalDamage *= 1.05f;
				}
			}
		}

		public override void PostUpdate()
		{
			// Efectos visuales cuando tiene el arma equipada
			if (hasAmeWeapon && Main.rand.NextBool(10))
			{
				Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, 
					Terraria.ID.DustID.RainbowMk2, 0f, 0f, 100, default, 0.8f);
				dust.noGravity = true;
				dust.velocity *= 0.3f;
			}
		}
	}
}
