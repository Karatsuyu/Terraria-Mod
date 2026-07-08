using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// RANGED MODE — Efecto visual de shockwave en cada explosión.
	/// No hace daño, solo es visual.
	/// ai[0] = radio máximo del shockwave
	/// ai[1] = generación del proyectil que lo originó
	/// </summary>
	public class AmeImpactEffect : ModProjectile
	{
		private const float LIFETIME = 22f;

		private float _visualTimer;
		private static Texture2D _glow;
		private static bool      _glowCreated;

		public override void SetDefaults()
		{
			Projectile.width    = 2;
			Projectile.height   = 2;
			Projectile.friendly = false;
			Projectile.hostile  = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = (int)LIFETIME;
			Projectile.alpha    = 255;
		}

		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;
		public override bool ShouldUpdatePosition() => false;

		public override void AI()
		{
			_visualTimer += 1f;
			float progress = _visualTimer / LIFETIME;
			Projectile.Opacity = 1f - progress;  // fade out
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// Sin shader: fallback con glow aditivo
			EnsureGlow();
			if (_glow == null) return false;

			float vis = Projectile.Opacity;
			if (vis < 0.01f) return false;

			float progress    = _visualTimer / LIFETIME;
			float maxRadius   = Projectile.ai[0];          // radio máximo pasado en ai[0]
			int   generation  = (int)Projectile.ai[1];

			// Radio crece conforme avanza el tiempo
			float radius = maxRadius * MathF.Pow(progress, 0.6f);

			Vector2 center     = Projectile.Center - Main.screenPosition;
			Vector2 glowOrigin = new Vector2(_glow.Width * 0.5f, _glow.Height * 0.5f);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate, BlendState.Additive,
				SamplerState.LinearClamp, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			// Anillo exterior que se expande
			float ringFade  = (1f - progress) * vis;
			float ringScale = (radius * 2f) / _glow.Width;

			Color ringCol = generation == 0
				? new Color(200, 15, 0) * (ringFade * 0.85f)
				: new Color(160, 8, 0)  * (ringFade * 0.7f);
			ringCol.A = 0;
			Main.spriteBatch.Draw(_glow, center, null, ringCol, 0f, glowOrigin, ringScale, SpriteEffects.None, 0);

			// Flash central al inicio
			if (progress < 0.3f)
			{
				float flashFade  = (1f - progress / 0.3f) * vis;
				float flashScale = (radius * 0.8f) / _glow.Width;
				Color flashCol   = new Color(255, 200, 150) * (flashFade * 0.9f);
				flashCol.A = 0;
				Main.spriteBatch.Draw(_glow, center, null, flashCol, 0f, glowOrigin, flashScale, SpriteEffects.None, 0);
			}

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			return false;
		}

		private static void EnsureGlow()
		{
			if (_glowCreated && _glow != null && !_glow.IsDisposed) return;
			const int SIZE = 64;
			_glow = new Texture2D(Main.graphics.GraphicsDevice, SIZE, SIZE);
			Color[] data = new Color[SIZE * SIZE];
			float   c    = SIZE / 2f;
			for (int y = 0; y < SIZE; y++)
			for (int x = 0; x < SIZE; x++)
			{
				float d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
				float a = MathHelper.Clamp(1f - d, 0f, 1f);
				a = a * a;
				byte b = (byte)(a * 255f);
				data[y * SIZE + x] = new Color(b, b, b, b);
			}
			_glow.SetData(data);
			_glowCreated = true;
		}
	}
}
