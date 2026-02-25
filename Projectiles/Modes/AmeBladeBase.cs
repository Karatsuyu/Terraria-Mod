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
	/// CLASE BASE para las 19 espadas del Melee2
	/// Orbit = 100% vanilla Zenith (AI_182_FinalFractal)
	/// Visual FX = VertexStrip ribbon trail (punta) + Nebula cloud puffs (cuerpo)
	/// Colores: rojo o negro aleatorio por espada
	/// </summary>
	public abstract class AmeBladeBase : ModProjectile
	{
		// Color aleatorio por espada: rojo o negro (decidido al spawn)
		private bool _colorInitialized;
		private bool _isRedVariant = true;

		// VertexStrip para trail de punta (ribbon fino y brillante)
		private VertexStrip _tipStrip = new VertexStrip();

		// Posiciones de la punta guardadas manualmente (Projectile.oldPos guarda el centro)
		private const int TIP_TRAIL_LENGTH = 32;
		private Vector2[] _tipOldPos = new Vector2[TIP_TRAIL_LENGTH];
		private float[] _tipOldRot = new float[TIP_TRAIL_LENGTH];
		private int _tipTrailIndex = 0;

		// ═══ NEBULA CLOUD: buffer de posiciones del centro para trail de humo ═══
		private const int ENERGY_TRAIL_LENGTH = 30;
		private Vector2[] _energyTrailPos = new Vector2[ENERGY_TRAIL_LENGTH];
		private int _energyTrailCount = 0;
		private float _noiseOffset = 0f; // Anima la distorsión del humo

		// ═══ TEXTURA SOFT GLOW: generada 1 vez, compartida por todas las espadas ═══
		private static Texture2D _softGlow;
		private static bool _glowCreated = false;

		// ═══ SONIDOS CUSTOM: 4 variantes random al lanzar espada ═══
		private static readonly SoundStyle _bladeSwingSound = new SoundStyle("Ame/Assets/Sounds/BladeSwing3")
		{
			Volume = 0.5f,
			PitchVariance = 0.35f,
			MaxInstances = 0,      // Sin límite — suena en cada espada
		};

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 30;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 60;
			Projectile.height = 60;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
			Projectile.timeLeft = 300;
			Projectile.noEnchantmentVisuals = true; // Sin partículas vanilla de enchantments
		}

		// CRITICAL: Desactivar position += velocity automático
		public override bool ShouldUpdatePosition() => false;

		// Dirección de la punta de la espada
		private Vector2 BladeDirection => (Projectile.rotation - MathHelper.ToRadians(45f)).ToRotationVector2();
		private Vector2 BladeTip => Projectile.Center + BladeDirection * (40f * Projectile.scale);

		public override void AI()
		{
			// ====== INICIALIZACIÓN ======
			if (Projectile.localAI[1] == 0f)
			{
				Projectile.localAI[1] = 1f;
				SoundEngine.PlaySound(_bladeSwingSound, Projectile.Center);
			}

			if (!_colorInitialized)
			{
				_colorInitialized = true;
				_isRedVariant = Main.rand.NextBool();
			}

			Player player = Main.player[Projectile.owner];
			Vector2 mountedCenter = player.MountedCenter;

			// ====== TIEMPO (vanilla exacto) ======
			float lerpValue = Utils.GetLerpValue(900f, 0f, Projectile.velocity.Length() * 2f, clamped: true);
			float num = MathHelper.Lerp(0.7f, 2f, lerpValue);
			Projectile.localAI[0] += num;

			if (Projectile.localAI[0] >= 120f)
			{
				Projectile.Kill();
				return;
			}

			// ====== PROGRESO (vanilla exacto) ======
			float lerpValue2 = Utils.GetLerpValue(0f, 1f, Projectile.localAI[0] / 60f, clamped: true);
			float num2 = Projectile.localAI[0] / 60f;
			float num3 = Projectile.ai[0];
			float num4 = Projectile.velocity.ToRotation();
			float num5 = (float)Math.PI;
			float num6 = ((Projectile.velocity.X > 0f) ? 1 : (-1));

			// ====== ÁNGULO DE ÓRBITA (vanilla exacto) ======
			float num7 = num5 + num6 * lerpValue2 * ((float)Math.PI * 2f);

			// ====== RADIO (vanilla exacto) ======
			float num8 = Projectile.velocity.Length() + Utils.GetLerpValue(0.5f, 1f, lerpValue2, clamped: true) * 40f;
			if (num8 < 60f)
				num8 = 60f;

			// ====== CENTRO (vanilla exacto) ======
			Vector2 vector = mountedCenter + Projectile.velocity;

			// ====== SPINNINGPOINT (vanilla exacto) ======
			Vector2 spinningpoint = new Vector2(1f, 0f).RotatedBy(num7) *
				new Vector2(num8, num3 * MathHelper.Lerp(2f, 1f, lerpValue));

			// ====== POSICIÓN (vanilla exacto) ======
			Vector2 vector2 = vector + spinningpoint.RotatedBy(num4);
			Vector2 vector3 = (1f - Utils.GetLerpValue(0f, 0.5f, lerpValue2, clamped: true)) *
				new Vector2((float)((Projectile.velocity.X > 0f) ? 1 : (-1)) * (0f - num8) * 0.1f, (0f - Projectile.ai[0]) * 0.3f);

			// Posición final = 100% VANILLA
			Projectile.Center = vector2 + vector3;

			// ====== ROTACIÓN (vanilla + ajuste -45° para sprites diagonales) ======
			float num10 = num7 + num4;
			Projectile.rotation = num10 + (float)Math.PI / 2f - MathHelper.ToRadians(45f);
			Projectile.spriteDirection = Projectile.direction = ((Projectile.velocity.X > 0f) ? 1 : (-1));

			if (num3 < 0f)
			{
				Projectile.rotation = num5 + num6 * lerpValue2 * ((float)Math.PI * -2f) + num4;
				Projectile.rotation += (float)Math.PI / 2f - MathHelper.ToRadians(45f);
				Projectile.spriteDirection = Projectile.direction = ((!(Projectile.velocity.X > 0f)) ? 1 : (-1));
			}

			// Opacidad: vanilla fade in/out
			Projectile.Opacity = Utils.GetLerpValue(0f, 5f, Projectile.localAI[0], clamped: true) *
				Utils.GetLerpValue(120f, 115f, Projectile.localAI[0], clamped: true);

			// Escala aleatoria (solo primera vez)
			if (Projectile.localAI[0] <= num + 0.1f)
				Projectile.scale = 0.8f + Main.rand.NextFloat(0f, 0.4f);

			// ====================================================================
			// REGISTRAR POSICIÓN DE LA PUNTA para el ribbon trail
			// ====================================================================
			RecordTipPosition();
			RecordEnergyTrailPosition();

			// Iluminación
			if (_isRedVariant)
				Lighting.AddLight(Projectile.Center, 0.9f, 0.2f, 0.1f);
			else
				Lighting.AddLight(Projectile.Center, 0.25f, 0.04f, 0.02f);

			Vector2 tip = BladeTip;
			if (_isRedVariant)
				Lighting.AddLight(tip, 1.0f, 0.3f, 0.1f);
			else
				Lighting.AddLight(tip, 0.3f, 0.05f, 0.02f);
		}

		/// <summary>
		/// Guarda la posición de la punta en un buffer circular.
		/// Se llama cada AI tick (2x por game tick por extraUpdates=1).
		/// </summary>
		private void RecordTipPosition()
		{
			// Desplazar todas las posiciones una posición hacia atrás (más vieja)
			for (int i = TIP_TRAIL_LENGTH - 1; i > 0; i--)
			{
				_tipOldPos[i] = _tipOldPos[i - 1];
				_tipOldRot[i] = _tipOldRot[i - 1];
			}

			// Posición más nueva en el índice 0
			_tipOldPos[0] = BladeTip;
			_tipOldRot[0] = Projectile.rotation;

			if (_tipTrailIndex < TIP_TRAIL_LENGTH)
				_tipTrailIndex++;
		}

		// ════════════════════════════════════════════════════════════
		// RENDERING: Nebula cloud + VertexStrip tip ribbon + sword sprite
		// ════════════════════════════════════════════════════════════

		/// <summary>
		/// Crea la textura de soft glow (círculo radial suave) una sola vez.
		/// Se comparte entre todas las instancias de espada.
		/// </summary>
		private static void EnsureGlowTexture()
		{
			if (_glowCreated && _softGlow != null && !_softGlow.IsDisposed)
				return;

			const int SIZE = 64;
			_softGlow = new Texture2D(Main.graphics.GraphicsDevice, SIZE, SIZE);
			Color[] data = new Color[SIZE * SIZE];
			float center = SIZE / 2f;

			for (int y = 0; y < SIZE; y++)
			{
				for (int x = 0; x < SIZE; x++)
				{
					float dx = x - center;
					float dy = y - center;
					float dist = MathF.Sqrt(dx * dx + dy * dy) / center;
					float alpha = MathHelper.Clamp(1f - dist, 0f, 1f);
					// Cubic falloff para bordes ultra-suaves (parece humo)
					alpha = alpha * alpha * alpha;
					// Premultiplied alpha para AlphaBlend correcto
					byte a = (byte)(alpha * 255f);
					data[y * SIZE + x] = new Color(a, a, a, a);
				}
			}

			_softGlow.SetData(data);
			_glowCreated = true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			EnsureGlowTexture();

			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

			// ══════════════════════════════════════════════════════
			// CAPA 1: NEBULA CLOUD — puffs suaves de humo (EntitySpriteDraw)
			// Se dibuja PRIMERO, debajo de todo, SIN SpriteBatch switch
			// ══════════════════════════════════════════════════════
			DrawNebulaCloud();

			// ══════════════════════════════════════════════════════
			// CAPA 2: RIBBON PUNTA — VertexStrip (requiere SpriteBatch switch)
			// ══════════════════════════════════════════════════════
			DrawTipVertexStrip();

			// ══════════════════════════════════════════════════════
			// CAPA 3: ESPADA PRINCIPAL
			// ══════════════════════════════════════════════════════
			Vector2 drawOrigin = texture.Size() * 0.5f;
			Vector2 drawPosition = Projectile.Center - Main.screenPosition;
			Color drawColor = new Color(255, 255, 255, (int)(255f * Projectile.Opacity));

			Main.EntitySpriteDraw(
				texture,
				drawPosition,
				null,
				drawColor,
				Projectile.rotation,
				drawOrigin,
				Projectile.scale,
				SpriteEffects.None,
				0
			);

			return false;
		}

		// ════════════════════════════════════════════════════════════
		// NEBULA CLOUD: Trail de humo cósmico estilo Galaxia
		// Círculos suaves superpuestos (puffs) — NO rectángulos
		// ════════════════════════════════════════════════════════════

		/// <summary>
		/// Registra la posición actual del centro para la nebula cloud.
		/// </summary>
		private void RecordEnergyTrailPosition()
		{
			for (int i = ENERGY_TRAIL_LENGTH - 1; i > 0; i--)
				_energyTrailPos[i] = _energyTrailPos[i - 1];

			_energyTrailPos[0] = Projectile.Center;

			if (_energyTrailCount < ENERGY_TRAIL_LENGTH)
				_energyTrailCount++;

			_noiseOffset += 0.12f;
		}

		/// <summary>
		/// Dibuja la nebula cloud estilo Galaxia.
		/// 
		/// En vez de rectángulos estirados (MagicPixel), dibuja CÍRCULOS SUAVES
		/// superpuestos (puffs) con textura radial gradient generada en runtime.
		/// Múltiples puffs por posición a diferentes escalas y offsets crean
		/// la apariencia de humo/nebulosa orgánico.
		/// 
		/// 3 capas de puffs:
		/// 1. EXTERIOR: Grandes, muy transparentes, color oscuro → halo difuso
		/// 2. MEDIO: Tamaño medio, semi-transparentes → cuerpo del humo
		/// 3. INTERIOR: Pequeños, más opacos, brillantes → núcleo luminoso
		/// </summary>
		private void DrawNebulaCloud()
		{
			if (_energyTrailCount < 3 || _softGlow == null)
				return;

			int count = _energyTrailCount;
			Vector2 glowOrigin = new Vector2(_softGlow.Width * 0.5f, _softGlow.Height * 0.5f);

			// ═══════════════════════════════════════════════════
			// CAPA 1: HALO EXTERIOR — puffs grandes, muy difusos
			// Crea el borde suave de la nebulosa
			// ═══════════════════════════════════════════════════
			for (int i = 0; i < count; i++)
			{
				Vector2 pos = _energyTrailPos[i];
				if (pos == Vector2.Zero) continue;

				float progress = (float)i / count;
				float life = 1f - progress;
				float fade = life * life; // Quadratic fade

				// Dirección perpendicular para desplazamiento lateral
				Vector2 dir = Vector2.UnitY;
				if (i < count - 1 && _energyTrailPos[i + 1] != Vector2.Zero)
					dir = (_energyTrailPos[i + 1] - pos).SafeNormalize(Vector2.UnitY);
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				// Ruido orgánico para desplazamiento lateral
				float noise1 = MathF.Sin(progress * 4.5f + _noiseOffset * 0.5f) * 18f;
				float noise2 = MathF.Sin(progress * 9.2f + _noiseOffset * 0.9f) * 8f;
				Vector2 offset = normal * (noise1 + noise2) * fade;

				// Tamaño: grande con variación animada
				float sizeBase = MathHelper.Lerp(1.4f, 2.4f, MathF.Sin(progress * MathHelper.Pi));
				float sizeWobble = 1f + MathF.Sin(progress * 5f + _noiseOffset * 0.7f) * 0.2f;
				float scale = sizeBase * sizeWobble * fade * Projectile.scale;

				// Fade in suave al inicio
				if (progress < 0.08f) scale *= progress / 0.08f;

				Color color;
				if (_isRedVariant)
					color = Color.Lerp(new Color(120, 15, 5), new Color(40, 3, 0), progress);
				else
					color = Color.Lerp(new Color(35, 5, 5), new Color(8, 1, 1), progress);

				color *= fade * 0.40f * Projectile.Opacity;

				Vector2 drawPos = pos + offset - Main.screenPosition;
				Main.EntitySpriteDraw(_softGlow, drawPos, null, color, progress * 2f, glowOrigin, scale, SpriteEffects.None, 0);

				// Segundo puff al lado opuesto (bilateral)
				Vector2 drawPos2 = pos - offset * 0.7f - Main.screenPosition;
				color *= 0.75f;
				Main.EntitySpriteDraw(_softGlow, drawPos2, null, color, -progress * 1.5f, glowOrigin, scale * 0.85f, SpriteEffects.None, 0);
			}

			// ═══════════════════════════════════════════════════
			// CAPA 2: CUERPO DEL HUMO — tamaño medio
			// Forma principal de la nebulosa
			// ═══════════════════════════════════════════════════
			for (int i = 0; i < count; i++)
			{
				Vector2 pos = _energyTrailPos[i];
				if (pos == Vector2.Zero) continue;

				float progress = (float)i / count;
				float life = 1f - progress;
				float fade = (float)Math.Pow(life, 2.5);

				Vector2 dir = Vector2.UnitY;
				if (i < count - 1 && _energyTrailPos[i + 1] != Vector2.Zero)
					dir = (_energyTrailPos[i + 1] - pos).SafeNormalize(Vector2.UnitY);
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				// Distorsión más sutil que la capa exterior
				float noise = MathF.Sin(progress * 6f + _noiseOffset * 0.8f + 1.5f) * 10f;
				float noise2 = MathF.Cos(progress * 11f + _noiseOffset * 1.2f) * 4f;
				Vector2 offset = normal * (noise + noise2) * fade;

				float cloudSwell = MathF.Sin(progress * MathHelper.Pi);
				float scale = MathHelper.Lerp(0.8f, 1.5f, cloudSwell) * fade * Projectile.scale;
				float sizeVar = 1f + MathF.Sin(progress * 7f + _noiseOffset) * 0.15f;
				scale *= sizeVar;

				if (progress < 0.08f) scale *= progress / 0.08f;

				Color color;
				if (_isRedVariant)
					color = Color.Lerp(new Color(220, 45, 15), new Color(90, 8, 0), progress);
				else
					color = Color.Lerp(new Color(50, 8, 6), new Color(15, 2, 1), progress);

				color *= fade * 0.50f * Projectile.Opacity;

				// 2 puffs ligeramente desfasados por posición para densidad
				Vector2 drawPos = pos + offset - Main.screenPosition;
				float rot = progress * 3f + _noiseOffset * 0.3f;
				Main.EntitySpriteDraw(_softGlow, drawPos, null, color, rot, glowOrigin, scale, SpriteEffects.None, 0);

				Vector2 microOffset = normal * MathF.Sin(progress * 13f + _noiseOffset * 1.5f) * 5f * fade;
				Vector2 drawPos2 = pos + microOffset - Main.screenPosition;
				color *= 0.8f;
				Main.EntitySpriteDraw(_softGlow, drawPos2, null, color, -rot * 0.7f, glowOrigin, scale * 0.75f, SpriteEffects.None, 0);
			}

			// ═══════════════════════════════════════════════════
			// CAPA 3: NÚCLEO BRILLANTE — pequeño, más opaco
			// Centro luminoso de la nebulosa
			// ═══════════════════════════════════════════════════
			for (int i = 0; i < count; i++)
			{
				Vector2 pos = _energyTrailPos[i];
				if (pos == Vector2.Zero) continue;

				float progress = (float)i / count;
				float life = 1f - progress;
				float fade = (float)Math.Pow(life, 3);

				float scale = MathHelper.Lerp(0.4f, 0.85f, MathF.Sin(progress * MathHelper.Pi));
				scale *= fade * Projectile.scale;
				float pulse = 1f + MathF.Sin(_noiseOffset * 2f + progress * 8f) * 0.1f;
				scale *= pulse;

				if (progress < 0.08f) scale *= progress / 0.08f;

				Color color;
				if (_isRedVariant)
					color = Color.Lerp(new Color(255, 130, 60), new Color(220, 40, 8), progress);
				else
					color = Color.Lerp(new Color(70, 12, 8), new Color(20, 3, 2), progress);

				color *= fade * 0.70f * Projectile.Opacity;

				Vector2 drawPos = pos - Main.screenPosition;
				Main.EntitySpriteDraw(_softGlow, drawPos, null, color, _noiseOffset + progress, glowOrigin, scale, SpriteEffects.None, 0);
			}

			// ═══════════════════════════════════════════════════
			// CAPA 4: DESTELLOS — puntos brillantes dispersos
			// Simula estrellas/partículas dentro de la nebulosa
			// ═══════════════════════════════════════════════════
			for (int i = 0; i < count; i += 2) // cada 2 posiciones
			{
				Vector2 pos = _energyTrailPos[i];
				if (pos == Vector2.Zero) continue;

				float progress = (float)i / count;
				float life = 1f - progress;
				float fade = life * life * life;

				// Solo aparecen intermitentemente (simula centelleo)
				float sparkle = MathF.Sin(progress * 12f + _noiseOffset * 3f);
				if (sparkle < 0.3f) continue;

				Vector2 dir = Vector2.UnitY;
				if (i < count - 1 && _energyTrailPos[i + 1] != Vector2.Zero)
					dir = (_energyTrailPos[i + 1] - pos).SafeNormalize(Vector2.UnitY);
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				float sparkleOffset = MathF.Sin(progress * 8f + _noiseOffset * 1.4f) * 15f * fade;
				Vector2 sparklePos = pos + normal * sparkleOffset - Main.screenPosition;

				float sparkleScale = 0.2f * fade * sparkle * Projectile.scale;

				Color sparkleColor;
				if (_isRedVariant)
					sparkleColor = new Color(255, 210, 160);
				else
					sparkleColor = new Color(180, 80, 50);

				sparkleColor *= fade * 0.85f * Projectile.Opacity;

				Main.EntitySpriteDraw(_softGlow, sparklePos, null, sparkleColor, 0f, glowOrigin, sparkleScale, SpriteEffects.None, 0);
			}
		}

		// ════════════════════════════════════════════════════════════
		// RIBBON DE PUNTA: VertexStrip con shader FinalFractal
		// ════════════════════════════════════════════════════════════

		/// <summary>
		/// Dibuja el ribbon fino en la punta con VertexStrip + FinalFractal shader.
		/// </summary>
		private void DrawTipVertexStrip()
		{
			int validTipCount = Math.Min(_tipTrailIndex, TIP_TRAIL_LENGTH);
			if (validTipCount < 3)
				return;

			// ── SpriteBatch switch a Immediate para shader ──
			try { Main.spriteBatch.End(); } catch { }

			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate,
				BlendState.AlphaBlend,
				SamplerState.PointWrap,
				DepthStencilState.None,
				Main.Rasterizer,
				null,
				Main.GameViewMatrix.TransformationMatrix
			);

			Vector2[] tipPositions = new Vector2[validTipCount];
			float[] tipRotations = new float[validTipCount];
			Array.Copy(_tipOldPos, tipPositions, validTipCount);
			Array.Copy(_tipOldRot, tipRotations, validTipCount);

			MiscShaderData tipShader = GameShaders.Misc["FinalFractal"];
			tipShader.UseImage0(TextureAssets.Extra[ExtrasID.FinalFractal]);

			_tipStrip.PrepareStrip(
				tipPositions,
				tipRotations,
				TipColorFunction,
				TipWidthFunction,
				-Main.screenPosition
			);

			tipShader.Apply();
			_tipStrip.DrawTrail();

			// ── Restaurar SpriteBatch normal ──
			try { Main.spriteBatch.End(); } catch { }

			Main.spriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				Main.DefaultSamplerState,
				DepthStencilState.None,
				Main.Rasterizer,
				null,
				Main.GameViewMatrix.TransformationMatrix
			);
		}

		// ════════════════════════════════════════════════════════════
		// COLOR/WIDTH CALLBACKS para el ribbon de la PUNTA
		// progress: 0.0 = cabeza (más nuevo), 1.0 = cola (más viejo)
		// ════════════════════════════════════════════════════════════

		private Color TipColorFunction(float progress)
		{
			float fade = (1f - progress);
			fade = fade * fade;

			Color baseColor;
			if (_isRedVariant)
			{
				// Rojo brillante → rojo profundo
				baseColor = Color.Lerp(
					new Color(255, 30, 10),
					new Color(200, 10, 0),
					progress
				);
			}
			else
			{
				// Negro con tinte rojo mínimo → negro puro
				baseColor = Color.Lerp(
					new Color(40, 4, 2),
					new Color(10, 1, 0),
					progress
				);
			}

			// A=0 para blending aditivo con el shader FinalFractal
			baseColor.A = 0;
			baseColor *= fade * Projectile.Opacity;
			return baseColor;
		}

		private float TipWidthFunction(float progress)
		{
			float taper = (1f - progress);
			float width = MathHelper.Lerp(28f, 8f, progress) * taper;

			if (progress < 0.15f)
				width *= MathHelper.Lerp(0.7f, 1f, progress / 0.15f);

			return width * Projectile.scale * Projectile.Opacity;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Rectangle expandedHitbox = projHitbox;
			expandedHitbox.Inflate(30, 30);
			if (expandedHitbox.Intersects(targetHitbox))
				return true;

			float collisionPoint = 0f;
			Vector2 start = Projectile.Center;
			Vector2 end = Projectile.Center + Projectile.rotation.ToRotationVector2() * 100f;

			if (Collision.CheckAABBvLineCollision(
				targetHitbox.TopLeft(),
				targetHitbox.Size(),
				start,
				end,
				40f,
				ref collisionPoint))
			{
				return true;
			}

			return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 100f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Sin partículas de impacto — solo la nebulosa y el ribbon de punta
		}

		public override Color? GetAlpha(Color lightColor)
		{
			return new Color(255, 255, 255, (int)(255f * Projectile.Opacity));
		}
	}
}
