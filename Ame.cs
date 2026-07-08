using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace Ame
{
	/// <summary>
	/// AmeMod - Un mod que implementa un sistema de armas modulares que trascienden clases
	/// </summary>
	public class Ame : Mod
	{
		/// <summary>Textura de ruido para shaders del modo Ranged (se carga desde ExtraTextures/HarshNoise.png)</summary>
		public static Texture2D NoiseTexture;
		/// <summary>Segunda textura de ruido para shaders del modo Ranged (se carga desde ExtraTextures/GlowOrbParticle.png)</summary>
		public static Texture2D NoiseTexture2;

		public override void Load()
		{
			// Cargar texturas de ruido para shaders del modo Ranged
			if (!Main.dedServ)
			{
				Asset<Texture2D> noise1 = ModContent.Request<Texture2D>("Ame/ExtraTextures/HarshNoise",
					AssetRequestMode.ImmediateLoad);
				Asset<Texture2D> noise2 = ModContent.Request<Texture2D>("Ame/ExtraTextures/GlowOrbParticle",
					AssetRequestMode.ImmediateLoad);

				if (noise1.IsLoaded) NoiseTexture  = noise1.Value;
				if (noise2.IsLoaded) NoiseTexture2 = noise2.Value;
			}
		}

		public override void Unload()
		{
			NoiseTexture  = null;
			NoiseTexture2 = null;
		}
	}
}
