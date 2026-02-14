using Terraria;
using Terraria.ModLoader;

namespace Ame.Buffs
{
	public class AmeMinionBuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			// DisplayName y Description se manejan via archivos de localización
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.Modes.AmeSummonMinion>()] > 0)
			{
				player.buffTime[buffIndex] = 18000;
			}
			else
			{
				player.DelBuff(buffIndex);
				buffIndex--;
			}
		}
	}
}
