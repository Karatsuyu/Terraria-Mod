using Terraria;
using Terraria.ModLoader;

namespace Ame.Systems
{
	/// <summary>
	/// Sistema reservado para futuros shaders del mod Ame.
	/// El modo Magic ahora usa BasicEffect + capas VertexStrip (sin shader custom).
	/// </summary>
	public class AmeShaderSystem : ModSystem
	{
		public override void Load()
		{
			if (Main.dedServ) return;

			Terraria.Graphics.Shaders.GameShaders.Misc["Ame:MagicBeam"] = new Terraria.Graphics.Shaders.MiscShaderData(
				ModContent.Request<Microsoft.Xna.Framework.Graphics.Effect>("Ame/Effects/AmeMagicBeam", ReLogic.Content.AssetRequestMode.ImmediateLoad),
				"Technique1"
			);
		}

		public override void Unload()
		{
			if (Terraria.Graphics.Shaders.GameShaders.Misc.ContainsKey("Ame:MagicBeam"))
				Terraria.Graphics.Shaders.GameShaders.Misc.Remove("Ame:MagicBeam");
		}
	}
}

