using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// MELEE1 — First Fractal reimplementado.
	/// Las espadas nacen alrededor del cursor y convergen al centro.
	/// 
	/// Codificación de datos:
	///   velocity.X / velocity.Y  → posición del cursor (leída en init, luego ignorada)
	///   ai[0]                    → ángulo de spawn (radianes)
	///   ai[1]                    → radio de spawn (píxeles)
	///   localAI[0]               → timer de vida (sube cada AI tick)
	///   localAI[1]               → flag de inicialización (0=no, 1=sí)
	/// </summary>
	public abstract class AmeFractalBlade : ModProjectile
	{
		// ════════════════════════════════════════════════════════════════════
		// CONSTANTES — tunea aquí sin tocar la lógica
		// ════════════════════════════════════════════════════════════════════

		/// <summary>Duración total en AI-ticks (con extraUpdates=1 son 30 frames reales).</summary>
		private const float LIFETIME = 62f;

		/// <summary>
		/// Fracción de LIFETIME donde la espada llega al cursor.
		/// 0.58 = la espada llega al 58% de su vida, luego sale disparada el 42% restante.
		/// </summary>
		private const float CONVERGE_AT = 0.58f;

		/// <summary>Multiplicador de la curvatura espiral en vuelo.</summary>
		private const float CURVE_STRENGTH = 0.06f;

		/// <summary>
		/// Cuántas partículas spawnear en el burst de convergencia.
		/// Se reparte en dos oleadas para el efecto acumulativo.
		/// </summary>
		private const int BURST_DUST_COUNT = 22;

		// ════════════════════════════════════════════════════════════════════
		// VARIABLES ESTÁTICAS — cursor compartido entre todos los shots del swing
		// ════════════════════════════════════════════════════════════════════

		public static float SharedCursorX;
		public static float SharedCursorY;

		// ════════════════════════════════════════════════════════════════════
		// ESTADO INTERNO
		// ════════════════════════════════════════════════════════════════════

		private Vector2 _targetCenter;    // Posición del cursor al spawn
		private Vector2 _spawnPosition;   // Posición de origen de esta espada
		private bool    _hasExploded;     // Burst ya ejecutado?
		private bool    _isRedVariant;    // Color del trail: rojo o negro
		private float   _fixedScale;      // Escala aleatoria fijada al spawn

		// Trail de nebulosa (mismo sistema que AmeBladeBase, adaptado)
		private const int ENERGY_TRAIL_LENGTH = 26;
		private Vector2[] _energyTrailPos = new Vector2[ENERGY_TRAIL_LENGTH];
		private int       _energyTrailCount;
		private float     _noiseOffset;

		// Soft-glow texture compartida
		private static Texture2D _softGlow;
		private static bool      _glowCreated;

		// VertexStrip para el ribbon de punta
		private VertexStrip _tipStrip = new VertexStrip();

		private const int TIP_TRAIL_LENGTH = 24;
		private Vector2[] _tipOldPos = new Vector2[TIP_TRAIL_LENGTH];
		private float[]   _tipOldRot = new float[TIP_TRAIL_LENGTH];
		private int       _tipTrailIndex;

		// Propiedades de acceso rápido a localAI
		private float LocalTimer
		{
			get => Projectile.localAI[0];
			set => Projectile.localAI[0] = value;
		}
		private bool Initialized
		{
			get => Projectile.localAI[1] == 1f;
			set => Projectile.localAI[1] = value ? 1f : 0f;
		}

		// ════════════════════════════════════════════════════════════════════
		// SETUP
		// ════════════════════════════════════════════════════════════════════

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 28;
			ProjectileID.Sets.TrailingMode[Projectile.type]     = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width  = 60;
			Projectile.height = 60;
			Projectile.friendly          = true;
			Projectile.DamageType        = DamageClass.Melee;
			Projectile.penetrate         = -1;
			Projectile.tileCollide       = false;
			Projectile.ignoreWater       = true;
			Projectile.extraUpdates      = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown  = 8;
			Projectile.timeLeft          = 300;
			Projectile.noEnchantmentVisuals = true;
		}

		// Desactivar movimiento automático — nosotros controlamos Center manualmente
		public override bool ShouldUpdatePosition() => false;

		// ════════════════════════════════════════════════════════════════════
		// AI PRINCIPAL
		// ════════════════════════════════════════════════════════════════════

		public override void AI()
		{
			// ── INICIALIZACIÓN (una sola vez) ──────────────────────────────
			if (!Initialized)
			{
				Initialized = true;

				// velocity codifica la posición del cursor enviada desde AmeWeapon
				_targetCenter  = new Vector2(Projectile.velocity.X, Projectile.velocity.Y);

				// Posición orbital de spawn
				float spawnAngle  = Projectile.ai[0];
				float spawnRadius = Projectile.ai[1] > 0f ? Projectile.ai[1] : 170f;
				_spawnPosition = _targetCenter + new Vector2(
					MathF.Cos(spawnAngle),
					MathF.Sin(spawnAngle)
				) * spawnRadius;

				Projectile.Center = _spawnPosition;

				// Escala y color fijados al nacer
				_fixedScale    = 0.72f + Main.rand.NextFloat(0f, 0.46f);
				_isRedVariant  = Main.rand.NextBool();
				Projectile.scale = _fixedScale;

				_hasExploded   = false;

				Projectile.netUpdate = true;
			}

			// ── TIMER ──────────────────────────────────────────────────────
			LocalTimer += 1f;

			if (LocalTimer >= LIFETIME)
			{
				Projectile.Kill();
				return;
			}

			float progress = LocalTimer / LIFETIME; // 0 → 1

			// ── MOVIMIENTO ────────────────────────────────────────────────
			Vector2 newCenter;

			if (progress <= CONVERGE_AT)
			{
				// FASE 1: volar desde spawn → cursor con curvatura espiral
				float p1     = progress / CONVERGE_AT;          // 0 → 1
				float eased  = EaseInOutCubic(p1);

				Vector2 linear = Vector2.Lerp(_spawnPosition, _targetCenter, eased);

				// Vector perpendicular al eje spawn→cursor
				Vector2 axis = (_targetCenter - _spawnPosition).SafeNormalize(Vector2.Zero);
				Vector2 perp = new Vector2(-axis.Y, axis.X);

				// Curvatura senoidal — máxima a mitad del viaje, cero al llegar
				float curveMag  = MathF.Sin(p1 * MathHelper.Pi);
				float curveLen  = (_targetCenter - _spawnPosition).Length() * CURVE_STRENGTH;
				float curveSide = (MathF.Sin(Projectile.ai[0] * 2.3f) >= 0f) ? 1f : -1f;

				newCenter = linear + perp * curveMag * curveLen * curveSide;
			}
			else
			{
				// FASE 2: continuar hacia adelante pasando el cursor (misma dirección de viaje)
				float p2    = (progress - CONVERGE_AT) / (1f - CONVERGE_AT); // 0 → 1
				float eased = EaseOutCubic(p2);

				// Misma dirección que fase 1: spawn → cursor → y más allá
				Vector2 exitDir  = (_targetCenter - _spawnPosition).SafeNormalize(Vector2.Zero);
				float   exitDist = 130f * eased;
				newCenter = _targetCenter + exitDir * exitDist;

				// Burst de partículas al llegar por primera vez
				if (!_hasExploded)
				{
					_hasExploded = true;
					SpawnConvergenceBurst();
				}
			}

			Projectile.Center = newCenter;

			// ── ROTACIÓN — punta siempre apuntando en dirección de viaje (spawn→cursor→adelante) ──
			// RecordTipPosition usa (rotation - 45°) para la punta → el offset correcto es +PiOver4
			Vector2 travelDir = _targetCenter - _spawnPosition; // dirección de viaje fija (no cambia entre fases)
			if (travelDir.LengthSquared() > 4f)
				Projectile.rotation = travelDir.ToRotation() + MathHelper.PiOver4;

			// ── SPRITE DIRECTION — basado en dirección de viaje real ──────
			if (travelDir.LengthSquared() > 4f)
			{
				Projectile.spriteDirection = travelDir.X > 0f ? 1 : -1;
				Projectile.direction       = Projectile.spriteDirection;
			}

			// ── OPACIDAD ──────────────────────────────────────────────────
			float fadeIn  = Utils.GetLerpValue(0f, 7f,         LocalTimer, clamped: true);
			float fadeOut = Utils.GetLerpValue(LIFETIME, LIFETIME - 9f, LocalTimer, clamped: true);
			Projectile.Opacity = fadeIn * fadeOut;

			// ── TRAILS ────────────────────────────────────────────────────
			RecordEnergyTrailPosition();
			RecordTipPosition();
			_noiseOffset += 0.11f;

			// ── ILUMINACIÓN ───────────────────────────────────────────────
			if (_isRedVariant)
				Lighting.AddLight(Projectile.Center, 0.85f, 0.12f, 0.05f);
			else
				Lighting.AddLight(Projectile.Center, 0.22f, 0.04f, 0.02f);
		}

		// ════════════════════════════════════════════════════════════════════
		// BURST DE CONVERGENCIA — Idea 3
		// Se llama exactamente una vez, cuando la espada llega al cursor.
		// ════════════════════════════════════════════════════════════════════

		private void SpawnConvergenceBurst()
		{
			// Oleada 1: partículas que explotan hacia afuera
			for (int i = 0; i < BURST_DUST_COUNT; i++)
			{
				float angle = (MathHelper.TwoPi / BURST_DUST_COUNT) * i;
				float speed = Main.rand.NextFloat(4f, 9f);
				Dust d = Dust.NewDustDirect(
					_targetCenter - new Vector2(4f), 8, 8,
					DustID.Shadowflame, 0f, 0f, 0, default, 1.6f
				);
				d.velocity  = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
				d.noGravity = true;
				d.fadeIn    = 0.6f;
			}

			// Oleada 2: partículas más pequeñas y rápidas en ángulos intermedios
			for (int i = 0; i < 12; i++)
			{
				float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
				float speed = Main.rand.NextFloat(7f, 14f);
				Dust d = Dust.NewDustDirect(
					_targetCenter - new Vector2(4f), 8, 8,
					DustID.Shadowflame, 0f, 0f, 60, default, 1.1f
				);
				d.velocity  = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
				d.noGravity = true;
			}

			// Flash de luz — intensidad proporcional a la escala de esta espada
			float intensity = _fixedScale * 1.6f;
			Lighting.AddLight(_targetCenter, intensity * 1.0f, intensity * 0.2f, intensity * 0.08f);

			// Pequeño sonido de impacto (usa sonido de espada vanilla suave)
			SoundEngine.PlaySound(
				new SoundStyle("Terraria/Sounds/Item_1") { Volume = 0.3f, PitchVariance = 0.4f },
				_targetCenter
			);
		}

		// ════════════════════════════════════════════════════════════════════
		// COLISIÓN
		// ════════════════════════════════════════════════════════════════════

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Rectangle expanded = projHitbox;
			expanded.Inflate(22, 22);
			if (expanded.Intersects(targetHitbox))
				return true;

			float cp = 0f;
			if (Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(), targetHitbox.Size(),
				Projectile.Center,
				Projectile.Center + Projectile.rotation.ToRotationVector2() * 80f,
				32f, ref cp))
				return true;

			return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 72f;
		}

		public override Color? GetAlpha(Color lightColor)
		{
			return new Color(255, 255, 255, (int)(255f * Projectile.Opacity));
		}

		// ════════════════════════════════════════════════════════════════════
		// TRAIL — NEBULA CLOUD (mismo sistema que AmeBladeBase, colores propios)
		// ════════════════════════════════════════════════════════════════════

		private void RecordEnergyTrailPosition()
		{
			for (int i = ENERGY_TRAIL_LENGTH - 1; i > 0; i--)
				_energyTrailPos[i] = _energyTrailPos[i - 1];
			_energyTrailPos[0] = Projectile.Center;
			if (_energyTrailCount < ENERGY_TRAIL_LENGTH)
				_energyTrailCount++;
		}

		private void RecordTipPosition()
		{
			for (int i = TIP_TRAIL_LENGTH - 1; i > 0; i--)
			{
				_tipOldPos[i] = _tipOldPos[i - 1];
				_tipOldRot[i] = _tipOldRot[i - 1];
			}
			_tipOldPos[0] = Projectile.Center + (Projectile.rotation - MathHelper.ToRadians(45f)).ToRotationVector2() * 36f;
			_tipOldRot[0] = Projectile.rotation;
			if (_tipTrailIndex < TIP_TRAIL_LENGTH)
				_tipTrailIndex++;
		}

		private static void EnsureGlowTexture()
		{
			if (_glowCreated && _softGlow != null && !_softGlow.IsDisposed) return;

			const int SIZE = 64;
			_softGlow = new Texture2D(Main.graphics.GraphicsDevice, SIZE, SIZE);
			Color[] data = new Color[SIZE * SIZE];
			float center = SIZE / 2f;
			for (int y = 0; y < SIZE; y++)
			for (int x = 0; x < SIZE; x++)
			{
				float dx   = x - center;
				float dy   = y - center;
				float dist = MathF.Sqrt(dx * dx + dy * dy) / center;
				float a    = MathHelper.Clamp(1f - dist, 0f, 1f);
				a = a * a * a;
				byte b = (byte)(a * 255f);
				data[y * SIZE + x] = new Color(b, b, b, b);
			}
			_softGlow.SetData(data);
			_glowCreated = true;
		}

		// ════════════════════════════════════════════════════════════════════
		// DRAW
		// ════════════════════════════════════════════════════════════════════

		public override bool PreDraw(ref Color lightColor)
		{
			EnsureGlowTexture();
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

			DrawNebulaCloud();
			DrawTipVertexStrip();

			// Espada principal
			Vector2 drawOrigin   = texture.Size() * 0.5f;
			Vector2 drawPosition = Projectile.Center - Main.screenPosition;
			Color   drawColor    = new Color(255, 255, 255, (int)(255f * Projectile.Opacity));

			Main.EntitySpriteDraw(
				texture, drawPosition, null, drawColor,
				Projectile.rotation, drawOrigin,
				Projectile.scale, SpriteEffects.None, 0
			);

			return false;
		}

		private void DrawNebulaCloud()
		{
			if (_energyTrailCount < 3 || _softGlow == null) return;

			int     count      = _energyTrailCount;
			Vector2 glowOrigin = new Vector2(_softGlow.Width * 0.5f, _softGlow.Height * 0.5f);

			// ── HALO EXTERIOR ──────────────────────────────────────────────
			for (int i = 0; i < count; i++)
			{
				Vector2 pos = _energyTrailPos[i];
				if (pos == Vector2.Zero) continue;

				float progress = (float)i / count;
				float life     = 1f - progress;
				float fade     = life * life;

				Vector2 dir = Vector2.UnitY;
				if (i < count - 1 && _energyTrailPos[i + 1] != Vector2.Zero)
					dir = (_energyTrailPos[i + 1] - pos).SafeNormalize(Vector2.UnitY);
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				float noise  = MathF.Sin(progress * 4.5f + _noiseOffset * 0.5f) * 16f;
				float noise2 = MathF.Sin(progress * 9.0f + _noiseOffset * 0.9f) * 7f;
				Vector2 offset = normal * (noise + noise2) * fade;

				float scale = MathHelper.Lerp(1.3f, 2.2f, MathF.Sin(progress * MathHelper.Pi))
				            * (1f + MathF.Sin(progress * 5f + _noiseOffset * 0.7f) * 0.18f)
				            * fade * Projectile.scale;
				if (progress < 0.08f) scale *= progress / 0.08f;

				Color color = _isRedVariant
					? Color.Lerp(new Color(110, 14, 5), new Color(38, 3, 0), progress)
					: Color.Lerp(new Color(32, 5, 4), new Color(8, 1, 1), progress);
				color *= fade * 0.38f * Projectile.Opacity;

				Main.EntitySpriteDraw(_softGlow, pos + offset - Main.screenPosition,
					null, color, progress * 2f, glowOrigin, scale, SpriteEffects.None, 0);
				Main.EntitySpriteDraw(_softGlow, pos - offset * 0.65f - Main.screenPosition,
					null, color * 0.72f, -progress * 1.5f, glowOrigin, scale * 0.82f, SpriteEffects.None, 0);
			}

			// ── CUERPO ─────────────────────────────────────────────────────
			for (int i = 0; i < count; i++)
			{
				Vector2 pos = _energyTrailPos[i];
				if (pos == Vector2.Zero) continue;

				float progress = (float)i / count;
				float life     = 1f - progress;
				float fade     = (float)Math.Pow(life, 2.4);

				Vector2 dir = Vector2.UnitY;
				if (i < count - 1 && _energyTrailPos[i + 1] != Vector2.Zero)
					dir = (_energyTrailPos[i + 1] - pos).SafeNormalize(Vector2.UnitY);
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				float noise  = MathF.Sin(progress * 6f + _noiseOffset * 0.8f + 1.4f) * 9f;
				float noise2 = MathF.Cos(progress * 11f + _noiseOffset * 1.2f) * 3.5f;
				Vector2 offset = normal * (noise + noise2) * fade;

				float scale = MathHelper.Lerp(0.75f, 1.4f, MathF.Sin(progress * MathHelper.Pi))
				            * (1f + MathF.Sin(progress * 7f + _noiseOffset) * 0.14f)
				            * fade * Projectile.scale;
				if (progress < 0.08f) scale *= progress / 0.08f;

				Color color = _isRedVariant
					? Color.Lerp(new Color(210, 42, 14), new Color(85, 8, 0), progress)
					: Color.Lerp(new Color(48, 8, 6), new Color(14, 2, 1), progress);
				color *= fade * 0.48f * Projectile.Opacity;

				float rot = progress * 3f + _noiseOffset * 0.3f;
				Main.EntitySpriteDraw(_softGlow, pos + offset - Main.screenPosition,
					null, color, rot, glowOrigin, scale, SpriteEffects.None, 0);

				Vector2 micro = normal * MathF.Sin(progress * 13f + _noiseOffset * 1.5f) * 5f * fade;
				Main.EntitySpriteDraw(_softGlow, pos + micro - Main.screenPosition,
					null, color * 0.78f, -rot * 0.7f, glowOrigin, scale * 0.72f, SpriteEffects.None, 0);
			}

			// ── NÚCLEO BRILLANTE ───────────────────────────────────────────
			for (int i = 0; i < count; i++)
			{
				Vector2 pos = _energyTrailPos[i];
				if (pos == Vector2.Zero) continue;

				float progress = (float)i / count;
				float life     = 1f - progress;
				float fade     = (float)Math.Pow(life, 3);

				float scale = MathHelper.Lerp(0.38f, 0.8f, MathF.Sin(progress * MathHelper.Pi))
				            * (1f + MathF.Sin(_noiseOffset * 2f + progress * 8f) * 0.09f)
				            * fade * Projectile.scale;
				if (progress < 0.08f) scale *= progress / 0.08f;

				Color color = _isRedVariant
					? Color.Lerp(new Color(255, 125, 55), new Color(215, 38, 8), progress)
					: Color.Lerp(new Color(68, 12, 8), new Color(20, 3, 2), progress);
				color *= fade * 0.68f * Projectile.Opacity;

				Main.EntitySpriteDraw(_softGlow, pos - Main.screenPosition,
					null, color, _noiseOffset + progress, glowOrigin, scale, SpriteEffects.None, 0);
			}

			// ── DESTELLOS ─────────────────────────────────────────────────
			for (int i = 0; i < count; i += 2)
			{
				Vector2 pos = _energyTrailPos[i];
				if (pos == Vector2.Zero) continue;

				float progress = (float)i / count;
				float life     = 1f - progress;
				float fade     = life * life * life;

				float sparkle = MathF.Sin(progress * 12f + _noiseOffset * 3f);
				if (sparkle < 0.3f) continue;

				Vector2 dir = Vector2.UnitY;
				if (i < count - 1 && _energyTrailPos[i + 1] != Vector2.Zero)
					dir = (_energyTrailPos[i + 1] - pos).SafeNormalize(Vector2.UnitY);
				Vector2 normal  = new Vector2(-dir.Y, dir.X);
				Vector2 sparklePos = pos + normal * MathF.Sin(progress * 8f + _noiseOffset * 1.4f) * 14f * fade;

				float sparkScale = 0.18f * fade * sparkle * Projectile.scale;
				Color sparkColor = (_isRedVariant ? new Color(255, 205, 155) : new Color(175, 75, 48))
				                 * (fade * 0.82f * Projectile.Opacity);

				Main.EntitySpriteDraw(_softGlow, sparklePos - Main.screenPosition,
					null, sparkColor, 0f, glowOrigin, sparkScale, SpriteEffects.None, 0);
			}
		}

		private void DrawTipVertexStrip()
		{
			int validCount = Math.Min(_tipTrailIndex, TIP_TRAIL_LENGTH);
			if (validCount < 3) return;

			try { Main.spriteBatch.End(); } catch { }

			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate, BlendState.AlphaBlend,
				SamplerState.PointWrap, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			Vector2[] pos = new Vector2[validCount];
			float[]   rot = new float[validCount];
			Array.Copy(_tipOldPos, pos, validCount);
			Array.Copy(_tipOldRot, rot, validCount);

			MiscShaderData tipShader = GameShaders.Misc["FinalFractal"];
			tipShader.UseImage0(TextureAssets.Extra[ExtrasID.FinalFractal]);

			_tipStrip.PrepareStrip(pos, rot, TipColorFunc, TipWidthFunc, -Main.screenPosition);
			tipShader.Apply();
			_tipStrip.DrawTrail();

			try { Main.spriteBatch.End(); } catch { }

			Main.spriteBatch.Begin(
				SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);
		}

		private Color TipColorFunc(float progress)
		{
			float fade = (1f - progress) * (1f - progress);
			Color base_ = _isRedVariant
				? Color.Lerp(new Color(255, 28, 8), new Color(195, 10, 0), progress)
				: Color.Lerp(new Color(135, 14, 8), new Color(58, 5, 2), progress);
			base_.A = 0;
			return base_ * (fade * Projectile.Opacity);
		}

		private float TipWidthFunc(float progress)
		{
			float taper = 1f - progress;
			float width = MathHelper.Lerp(26f, 7f, progress) * taper;
			if (progress < 0.15f)
				width *= MathHelper.Lerp(0.65f, 1f, progress / 0.15f);
			return width * Projectile.scale * Projectile.Opacity;
		}

		// ════════════════════════════════════════════════════════════════════
		// EASING
		// ════════════════════════════════════════════════════════════════════

		private static float EaseInOutCubic(float t)
			=> t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

		private static float EaseOutCubic(float t)
			=> 1f - MathF.Pow(1f - t, 3f);
	}
}
