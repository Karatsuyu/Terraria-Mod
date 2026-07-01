using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Ame.Particles
{
	/// <summary>
	/// Sistema de partículas independiente para el mod Ame.
	/// Utiliza el spritesheet "HeavySmoke.png" de Calamity para replicar la textura original.
	/// </summary>
	[Autoload(Side = ModSide.Client)]
	public class AmeParticleSystem : ModSystem
	{
		private static List<AmeSmoke> _particles;
		private static List<AmeSmoke> _toRemove;
		private const int MAX_PARTICLES = 600;

		public override void Load()
		{
			_particles = new List<AmeSmoke>(MAX_PARTICLES);
			_toRemove = new List<AmeSmoke>(64);
		}

		public override void Unload()
		{
			_particles?.Clear();
			_particles = null;
			_toRemove = null;
		}

		public override void OnWorldUnload()
		{
			_particles?.Clear();
		}

		/// <summary>
		/// Genera una partícula de humo pesado idéntica a Calamity HeavySmokeParticle.
		/// </summary>
		public static void SpawnSmoke(Vector2 position, Vector2 velocity, Color color, int lifetime,
			float scale, float opacity, float rotationSpeed = 0f, bool glowing = false)
		{
			if (Main.dedServ || Main.gamePaused || _particles == null) return;
			if (_particles.Count >= MAX_PARTICLES) return;

			_particles.Add(new AmeSmoke
			{
				Position = position,
				Velocity = velocity,
				Color = color,
				Lifetime = lifetime,
				Time = 0,
				Scale = scale,
				Opacity = opacity,
				Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
				RotationSpeed = rotationSpeed,
				Glowing = glowing,
				Variant = Main.rand.Next(7) // 7 variantes en HeavySmoke.png
			});
		}

		public override void PostUpdateDusts()
		{
			if (_particles == null || Main.dedServ) return;

			_toRemove.Clear();

			foreach (var p in _particles)
			{
				p.Position += p.Velocity;
				p.Time++;

				// Crecimiento inicial, luego encogimiento (idéntico a HeavySmokeParticle)
				if (p.Time / (float)p.Lifetime < 0.2f)
					p.Scale += 0.01f;
				else
					p.Scale *= 0.975f;

				p.Opacity *= 0.98f;
				p.Rotation += p.RotationSpeed * (p.Velocity.X > 0 ? 1f : -1f);
				p.Velocity *= 0.85f;

				// Fade out cerca del final de vida
				float lifeProgress = p.Time / (float)p.Lifetime;
				float fadeMultiplier = Utils.GetLerpValue(1f, 0.85f, lifeProgress, true);
				p.DrawColor = p.Color * fadeMultiplier;

				if (p.Time >= p.Lifetime)
					_toRemove.Add(p);
			}

			foreach (var p in _toRemove)
				_particles.Remove(p);
		}

		/// <summary>
		/// Dibuja TODAS las partículas. Debe llamarse desde PreDraw de AmeBeam.
		/// </summary>
		public static void DrawAllParticles(SpriteBatch spriteBatch)
		{
			if (_particles == null || _particles.Count == 0 || Main.dedServ) return;

			Texture2D tex = ModContent.Request<Texture2D>("Ame/Particles/HeavySmoke").Value;
			if (tex == null) return;

			// Capa 1: Partículas no-glowing (humo de fondo) con NonPremultiplied
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp,
				DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			foreach (var p in _particles)
			{
				if (p.Glowing) continue;
				DrawParticle(spriteBatch, tex, p);
			}

			spriteBatch.End();

			// Capa 2: Partículas glowing (fuego brillante) con Additive
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
				DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			foreach (var p in _particles)
			{
				if (!p.Glowing) continue;
				DrawParticle(spriteBatch, tex, p);
			}

			spriteBatch.End();
		}

		private static void DrawParticle(SpriteBatch spriteBatch, Texture2D tex, AmeSmoke p)
		{
			// HeavySmoke.png tiene 7 columnas (variantes) y 6 filas (frames de animación), 80x80 px por frame
			int frameAmount = 6;
			int animationFrame = (int)Math.Floor(p.Time / ((float)(p.Lifetime / (float)frameAmount)));
			if (animationFrame >= frameAmount) animationFrame = frameAmount - 1;

			Rectangle frame = new Rectangle(80 * p.Variant, 80 * animationFrame, 80, 80);

			Color col = p.DrawColor * p.Opacity;
			Vector2 drawPos = p.Position - Main.screenPosition;
			Vector2 origin = new Vector2(40f, 40f); // 80 / 2
			
			spriteBatch.Draw(tex, drawPos, frame, col, p.Rotation, origin, p.Scale, SpriteEffects.None, 0f);
		}
	}

	/// <summary>
	/// Estructura interna para una partícula de humo.
	/// </summary>
	internal class AmeSmoke
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public Color Color;
		public Color DrawColor;
		public int Lifetime;
		public int Time;
		public float Scale;
		public float Opacity;
		public float Rotation;
		public float RotationSpeed;
		public bool Glowing;
		public int Variant;
	}
}
