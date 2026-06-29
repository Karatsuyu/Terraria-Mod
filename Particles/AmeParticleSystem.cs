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
	/// Emula el comportamiento de HeavySmokeParticle de Calamity
	/// sin depender de su infraestructura.
	/// </summary>
	[Autoload(Side = ModSide.Client)]
	public class AmeParticleSystem : ModSystem
	{
		private static List<AmeSmoke> _particles;
		private static List<AmeSmoke> _toRemove;
		private static Texture2D _smokeTexture;
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
			_smokeTexture = null;
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
				Glowing = glowing
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

				// Crecimiento inicial, luego encogimiento (como HeavySmokeParticle)
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
		/// Se divide en 2 capas: no-glowing (fondo) y glowing (frente).
		/// </summary>
		public static void DrawAllParticles(SpriteBatch spriteBatch)
		{
			if (_particles == null || _particles.Count == 0 || Main.dedServ) return;

			// Cargar textura procedural la primera vez
			if (_smokeTexture == null || _smokeTexture.IsDisposed)
				_smokeTexture = CreateSmokeTexture(Main.instance.GraphicsDevice);

			// Capa 1: Partículas no-glowing (humo de fondo) con NonPremultiplied
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp,
				DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			foreach (var p in _particles)
			{
				if (p.Glowing) continue;
				DrawParticle(spriteBatch, p);
			}

			spriteBatch.End();

			// Capa 2: Partículas glowing (fuego brillante) con Additive
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
				DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			foreach (var p in _particles)
			{
				if (!p.Glowing) continue;
				DrawParticle(spriteBatch, p);
			}

			spriteBatch.End();
		}

		private static void DrawParticle(SpriteBatch spriteBatch, AmeSmoke p)
		{
			Color col = p.DrawColor * p.Opacity;
			Vector2 drawPos = p.Position - Main.screenPosition;
			Vector2 origin = new Vector2(_smokeTexture.Width / 2f, _smokeTexture.Height / 2f);
			spriteBatch.Draw(_smokeTexture, drawPos, null, col, p.Rotation, origin, p.Scale, SpriteEffects.None, 0f);
		}

		/// <summary>
		/// Genera una textura de humo procedural (soft circle con ruido).
		/// Esto reemplaza la necesidad de un spritesheet externo.
		/// </summary>
		private static Texture2D CreateSmokeTexture(GraphicsDevice device)
		{
			int size = 64;
			Color[] data = new Color[size * size];
			float center = size / 2f;
			Random rng = new Random(42); // seed fijo para consistencia

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = (x - center) / center;
					float dy = (y - center) / center;
					float dist = MathF.Sqrt(dx * dx + dy * dy);

					// Gradiente radial suave con algo de ruido
					float alpha = MathHelper.SmoothStep(1f, 0f, dist);
					alpha *= alpha; // hacer más suave el borde

					// Agregar ruido para que no sea un círculo perfecto (simula humo)
					float noise = 0.7f + (float)rng.NextDouble() * 0.3f;
					alpha *= noise;

					byte a = (byte)(MathHelper.Clamp(alpha, 0f, 1f) * 255);
					data[y * size + x] = new Color(255, 255, 255, a);
				}
			}

			Texture2D tex = new Texture2D(device, size, size);
			tex.SetData(data);
			return tex;
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
	}
}
