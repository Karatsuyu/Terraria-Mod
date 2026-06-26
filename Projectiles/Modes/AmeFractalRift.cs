using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// MELEE1 — Rift dimensional que aparece en el cursor.
	/// No hace daño. Solo efecto visual de portal/grieta.
	/// 
	/// Ciclo de vida (60 AI-ticks = ~30 frames reales con extraUpdates=1):
	///   Fase 1 (0–18 ticks)  : El rift se expande desde nada → radio máximo
	///   Fase 2 (18–42 ticks) : Pulsa mientras las espadas convergen
	///   Fase 3 (42–60 ticks) : Colapsa y desaparece
	/// 
	/// ai[0] : número de espadas que se spawnearán (para escalar el rift)
	/// </summary>
	public class AmeFractalRift : ModProjectile
	{
		// ════════════════════════════════════════════════════════════════════
		// CONSTANTES
		// ════════════════════════════════════════════════════════════════════

		private const float LIFETIME        = 60f;   // AI-ticks totales
		private const float EXPAND_END      = 18f;   // Ticks que dura la expansión
		private const float COLLAPSE_START  = 42f;   // Ticks a los que empieza el colapso
		private const float MAX_OUTER_RADIUS = 148f; // Radio del anillo exterior al máximo
		private const float RING_COUNT       = 3f;   // Cuántos anillos concéntricos

		// ════════════════════════════════════════════════════════════════════
		// ESTADO
		// ════════════════════════════════════════════════════════════════════

		private float LocalTimer
		{
			get => Projectile.localAI[0];
			set => Projectile.localAI[0] = value;
		}

		// Textura de línea fina para los anillos (compartida)
		// private static Texture2D _ringTex;
		// private static bool      _ringTexCreated;

		// Textura de soft-glow central (compartida con AmeFractalBlade)
		// En vez de duplicarla, la creamos localmente aquí también —
		// tModLoader cachea los recursos por tipo de textura, así que el costo es mínimo.
		private static Texture2D _glowTex;
		private static bool      _glowTexCreated;

		// ════════════════════════════════════════════════════════════════════
		// SETUP
		// ════════════════════════════════════════════════════════════════════

		public override void SetStaticDefaults()
		{
			// No tiene trail visual propio — solo dibuja en PreDraw
		}

		public override void SetDefaults()
		{
			Projectile.width    = 10;
			Projectile.height   = 10;
			Projectile.friendly = false;  // No hace daño
			Projectile.hostile  = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = 1;
			Projectile.timeLeft = 300;
			Projectile.hide     = false;
			Projectile.alpha    = 255;    // Invisible excepto por PreDraw
		}

		// El rift no se mueve — está fijo en el cursor
		public override bool ShouldUpdatePosition() => false;

		// Textura vacía para que tModLoader no falle al buscar el PNG
		// (el rift se dibuja 100% en PreDraw, no necesita sprite propio)
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

		// ════════════════════════════════════════════════════════════════════
		// AI
		// ════════════════════════════════════════════════════════════════════

		public override void AI()
		{
			LocalTimer += 1f;

			if (LocalTimer >= LIFETIME)
			{
				Projectile.Kill();
				return;
			}

			float progress = LocalTimer / LIFETIME;

			// ── Radio actual según la fase ─────────────────────────────────
			float currentRadius = GetCurrentRadius();

			// ── Iluminación dinámica ───────────────────────────────────────
			// Brilla más durante la fase de pulsación (espadas en vuelo)
			float lightIntensity;
			if (LocalTimer < EXPAND_END)
				lightIntensity = LocalTimer / EXPAND_END;
			else if (LocalTimer < COLLAPSE_START)
				lightIntensity = 1f + 0.25f * MathF.Sin(LocalTimer * 0.22f); // pulso
			else
				lightIntensity = 1f - (LocalTimer - COLLAPSE_START) / (LIFETIME - COLLAPSE_START);

			lightIntensity = MathHelper.Clamp(lightIntensity, 0f, 1.3f);
			Lighting.AddLight(Projectile.Center, lightIntensity * 1.1f, lightIntensity * 0.18f, lightIntensity * 0.06f);

			// ── Partículas de borde del rift (solo en expansión y pulso) ──
			if (LocalTimer < COLLAPSE_START && Main.rand.NextBool(3))
			{
				float dustAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
				// Las partículas salen desde el borde del anillo exterior
				float dustRadius = currentRadius * (0.9f + Main.rand.NextFloat(0f, 0.15f));
				Vector2 dustPos  = Projectile.Center + new Vector2(
					MathF.Cos(dustAngle) * dustRadius,
					MathF.Sin(dustAngle) * dustRadius * 0.42f // elipse
				);
				Dust d = Dust.NewDustDirect(dustPos, 1, 1, DustID.Shadowflame);
				d.noGravity = true;
				d.velocity  = (dustPos - Projectile.Center).SafeNormalize(Vector2.Zero) * 0.8f;
				d.scale     = 1.0f + Main.rand.NextFloat(0f, 0.6f);
				d.fadeIn    = 0.5f;
			}

			// ── Flash al final del colapso (cuando el rift desaparece) ────
			if (LocalTimer >= LIFETIME - 2f && LocalTimer < LIFETIME)
			{
				for (int i = 0; i < 16; i++)
				{
					float a = (MathHelper.TwoPi / 16f) * i;
					Dust d = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.Shadowflame);
					d.velocity  = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 3.5f;
					d.noGravity = true;
					d.scale     = 1.3f;
				}
				Lighting.AddLight(Projectile.Center, 2.0f, 0.35f, 0.1f);
			}
		}

		// ════════════════════════════════════════════════════════════════════
		// RADIO — curva de animación del rift
		// ════════════════════════════════════════════════════════════════════

		private float GetCurrentRadius()
		{
			if (LocalTimer <= EXPAND_END)
			{
				// Expansión: ease-out rápido (0 → MAX_OUTER_RADIUS)
				float t = LocalTimer / EXPAND_END;
				return MAX_OUTER_RADIUS * EaseOutBack(t);
			}
			else if (LocalTimer <= COLLAPSE_START)
			{
				// Pulsación suave alrededor del máximo
				float pulse = MathF.Sin((LocalTimer - EXPAND_END) * 0.18f) * 8f;
				return MAX_OUTER_RADIUS + pulse;
			}
			else
			{
				// Colapso: ease-in hacia 0
				float t = (LocalTimer - COLLAPSE_START) / (LIFETIME - COLLAPSE_START);
				return MAX_OUTER_RADIUS * (1f - EaseInCubic(t));
			}
		}

		// ════════════════════════════════════════════════════════════════════
		// DRAW — todo el rift se dibuja aquí
		// ════════════════════════════════════════════════════════════════════

		public override bool PreDraw(ref Color lightColor)
		{
			EnsureTextures();

			float outerRadius   = GetCurrentRadius();
			float opacityFactor = Projectile.Opacity;

			// Opacidad global según fase
			if (LocalTimer < EXPAND_END)
				opacityFactor *= LocalTimer / EXPAND_END;
			else if (LocalTimer > COLLAPSE_START)
				opacityFactor *= 1f - (LocalTimer - COLLAPSE_START) / (LIFETIME - COLLAPSE_START);

			opacityFactor = MathHelper.Clamp(opacityFactor, 0f, 1f);

			if (opacityFactor < 0.01f)
				return false;

			// ── GLOW CENTRAL ───────────────────────────────────────────────
			DrawCentralGlow(outerRadius, opacityFactor);

			// ── ANILLOS ELÍPTICOS ──────────────────────────────────────────
			DrawEllipticRings(outerRadius, opacityFactor);

			// ── RAYOS INTERNOS ─────────────────────────────────────────────
			DrawInternalRays(outerRadius, opacityFactor);

			return false; // No dibujar el sprite base
		}

		/// <summary>
		/// Dibuja el glow suave en el centro del rift (como un ojo brillante).
		/// </summary>
		private void DrawCentralGlow(float outerRadius, float opacity)
		{
			if (_glowTex == null) return;

			Vector2 origin = new Vector2(_glowTex.Width * 0.5f, _glowTex.Height * 0.5f);
			Vector2 center = Projectile.Center - Main.screenPosition;

			// Glow exterior difuso
			float glowScale = outerRadius / (_glowTex.Width * 0.5f) * 0.7f;
			Color glowColor = new Color(180, 30, 8, 0) * (opacity * 0.35f);
			Main.EntitySpriteDraw(_glowTex, center, null, glowColor, 0f, origin, glowScale, SpriteEffects.None, 0);

			// Glow interior más brillante y pequeño
			float innerScale = glowScale * 0.38f;
			Color innerColor = new Color(255, 80, 20, 0) * (opacity * 0.55f);
			Main.EntitySpriteDraw(_glowTex, center, null, innerColor, 0f, origin, innerScale, SpriteEffects.None, 0);

			// Núcleo: punto muy brillante
			float coreScale = glowScale * 0.12f;
			Color coreColor = new Color(255, 200, 150, 0) * (opacity * 0.85f);
			Main.EntitySpriteDraw(_glowTex, center, null, coreColor, LocalTimer * 0.04f, origin, coreScale, SpriteEffects.None, 0);
		}

		/// <summary>
		/// Dibuja 3 anillos elípticos concéntricos y rotatorios con glow suave.
		/// Cada anillo está hecho de múltiples puntos de glow para simular una línea.
		/// </summary>
		private void DrawEllipticRings(float outerRadius, float opacity)
		{
			if (_glowTex == null) return;

			Vector2 glowOrigin = new Vector2(_glowTex.Width * 0.5f, _glowTex.Height * 0.5f);

			// 3 anillos: exterior, medio, interior
			for (int ring = 0; ring < (int)RING_COUNT; ring++)
			{
				float ringFraction = (ring + 1f) / RING_COUNT; // 0.33, 0.66, 1.0
				float ringRadius   = outerRadius * ringFraction;

				// Cada anillo rota en dirección y velocidad distintas
				float ringRotation = LocalTimer * (0.025f + ring * 0.018f) * (ring % 2 == 0 ? 1f : -1f);

				// Grosor visual y opacidad varían por anillo
				// El exterior es más delgado y oscuro; el interior es más grueso y brillante
				float ringOpacity  = opacity * (0.45f + (1f - ringFraction) * 0.4f);
				float pointScale   = MathHelper.Lerp(0.12f, 0.22f, 1f - ringFraction);

				// Colores: exterior oscuro rojo-marrón → interior rojo brillante
				Color ringColorA = Color.Lerp(new Color(80, 10, 3), new Color(230, 35, 8), 1f - ringFraction);
				Color ringColorB = Color.Lerp(new Color(140, 20, 5), new Color(255, 90, 25), 1f - ringFraction);

				// Número de puntos por anillo (más puntos = anillo más "lleno")
				int pointCount = 36 + ring * 8;

				for (int p = 0; p < pointCount; p++)
				{
					float angle = (MathHelper.TwoPi / pointCount) * p + ringRotation;

					// Elipse: radio X = ringRadius, radio Y = ringRadius * 0.42
					// (la apertura vertical hace que parezca inclinado, como un disco)
					Vector2 pointPos = Projectile.Center + new Vector2(
						MathF.Cos(angle) * ringRadius,
						MathF.Sin(angle) * ringRadius * 0.42f
					);

					// Lerp de color basado en la posición angular (crea degradado en el anillo)
					float colorLerp = (MathF.Sin(angle * 2f + LocalTimer * 0.08f) + 1f) * 0.5f;
					Color pointColor = Color.Lerp(ringColorA, ringColorB, colorLerp);
					pointColor.A = 0; // blending aditivo
					pointColor  *= ringOpacity;

					Main.EntitySpriteDraw(
						_glowTex,
						pointPos - Main.screenPosition,
						null,
						pointColor,
						angle, // rotar el glow en la dirección del anillo
						glowOrigin,
						pointScale,
						SpriteEffects.None, 0
					);
				}

				// Puntos de mayor brillo en 4 posiciones del anillo (como "nodos")
				for (int node = 0; node < 4; node++)
				{
					float nodeAngle = (MathHelper.TwoPi / 4f) * node + ringRotation;
					Vector2 nodePos = Projectile.Center + new Vector2(
						MathF.Cos(nodeAngle) * ringRadius,
						MathF.Sin(nodeAngle) * ringRadius * 0.42f
					);
					float nodePulse = (MathF.Sin(LocalTimer * 0.3f + node * MathHelper.PiOver2) + 1f) * 0.5f;
					Color nodeColor = new Color(255, 120, 40, 0) * (ringOpacity * (0.7f + nodePulse * 0.5f));

					Main.EntitySpriteDraw(
						_glowTex,
						nodePos - Main.screenPosition,
						null, nodeColor, nodeAngle, glowOrigin,
						pointScale * 2.2f, SpriteEffects.None, 0
					);
				}
			}
		}

		/// <summary>
		/// Dibuja rayos de energía desde el centro hacia el borde del anillo exterior.
		/// Aparecen y desaparecen pulsando para dar sensación de energía activa.
		/// </summary>
		private void DrawInternalRays(float outerRadius, float opacity)
		{
			if (_glowTex == null) return;

			Vector2 glowOrigin = new Vector2(_glowTex.Width * 0.5f, _glowTex.Height * 0.5f);

			// 8 rayos distribuidos angularmente
			const int RAY_COUNT = 8;
			float baseRotation  = LocalTimer * 0.032f;

			for (int r = 0; r < RAY_COUNT; r++)
			{
				float rayAngle = (MathHelper.TwoPi / RAY_COUNT) * r + baseRotation;

				// Cada rayo pulsa con fase diferente
				float phasedPulse = MathF.Sin(LocalTimer * 0.28f + r * MathHelper.PiOver2);
				float rayOpacity  = opacity * MathHelper.Clamp((phasedPulse + 1f) * 0.5f, 0.1f, 1f);

				if (rayOpacity < 0.08f) continue;

				// El rayo se dibuja como una serie de puntos de glow a lo largo de su longitud
				int   segCount   = 12;
				float rayLength  = outerRadius * 0.88f;

				for (int seg = 0; seg < segCount; seg++)
				{
					float segT    = (float)seg / segCount; // 0 (centro) → 1 (borde)
					float segDist = rayLength * segT;

					// Posición en elipse
					Vector2 segPos = Projectile.Center + new Vector2(
						MathF.Cos(rayAngle) * segDist,
						MathF.Sin(rayAngle) * segDist * 0.42f
					);

					// Fade: brillante al centro, desaparece en el borde
					float segFade  = 1f - segT;
					float segScale = MathHelper.Lerp(0.08f, 0.22f, segFade * segFade);

					Color segColor = Color.Lerp(
						new Color(255, 150, 50, 0),  // centro: naranja brillante
						new Color(180, 25, 5, 0),    // borde: rojo oscuro
						segT
					) * (rayOpacity * segFade * segFade);

					Main.EntitySpriteDraw(
						_glowTex,
						segPos - Main.screenPosition,
						null, segColor, rayAngle,
						glowOrigin, segScale,
						SpriteEffects.None, 0
					);
				}
			}
		}

		// ════════════════════════════════════════════════════════════════════
		// TEXTURA DE SOFT GLOW — generada en runtime, compartida
		// ════════════════════════════════════════════════════════════════════

		private static void EnsureTextures()
		{
			if (!_glowTexCreated || _glowTex == null || _glowTex.IsDisposed)
			{
				const int SIZE = 64;
				_glowTex = new Texture2D(Main.graphics.GraphicsDevice, SIZE, SIZE);
				Color[] data = new Color[SIZE * SIZE];
				float center = SIZE / 2f;
				for (int y = 0; y < SIZE; y++)
				for (int x = 0; x < SIZE; x++)
				{
					float dx   = x - center;
					float dy   = y - center;
					float dist = MathF.Sqrt(dx * dx + dy * dy) / center;
					float a    = MathHelper.Clamp(1f - dist, 0f, 1f);
					a = a * a * a; // cubic falloff
					byte b = (byte)(a * 255f);
					data[y * SIZE + x] = new Color(b, b, b, b);
				}
				_glowTex.SetData(data);
				_glowTexCreated = true;
			}
		}

		// ════════════════════════════════════════════════════════════════════
		// EASING
		// ════════════════════════════════════════════════════════════════════

		/// <summary>Expande pasando ligeramente del máximo y regresando (rebote suave).</summary>
		private static float EaseOutBack(float t)
		{
			const float c1 = 1.70158f;
			const float c3 = c1 + 1f;
			return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
		}

		private static float EaseInCubic(float t) => t * t * t;
	}
}
