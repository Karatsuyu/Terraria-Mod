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
	/// Visual FX = VertexStrip ribbon trail (punta) + VertexStrip smoke cloud (cuerpo)
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

		// ═══ CORTINA CÓSMICA: buffer de posiciones del centro para primitive trail ═══
		private const int ENERGY_TRAIL_LENGTH = 30;
		private Vector2[] _energyTrailPos = new Vector2[ENERGY_TRAIL_LENGTH];
		private int _energyTrailCount = 0;
		private float _noiseOffset = 0f; // Anima la distorsión del humo

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
				SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
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
				Lighting.AddLight(Projectile.Center, 0.35f, 0.05f, 0.5f);

			Vector2 tip = BladeTip;
			if (_isRedVariant)
				Lighting.AddLight(tip, 1.0f, 0.3f, 0.1f);
			else
				Lighting.AddLight(tip, 0.4f, 0.1f, 0.6f);
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
		// RENDERING: VertexStrip trails + sword sprite
		// ════════════════════════════════════════════════════════════
		public override bool PreDraw(ref Color lightColor)
		{
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

			// ══════════════════════════════════════════════════════
			// CAPA 1: CORTINA CÓSMICA — primitive ribbon trail (EntitySpriteDraw)
			// Se dibuja PRIMERO, debajo de todo, SIN SpriteBatch switch
			// ══════════════════════════════════════════════════════
			DrawEnergyTrail();

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
		// CORTINA CÓSMICA: Primitive ribbon trail (EntitySpriteDraw)
		// Cinta dinámica continua — NO partículas, NO pixeles
		// ════════════════════════════════════════════════════════════

		/// <summary>
		/// Registra la posición actual del centro para la cortina cósmica.
		/// </summary>
		private void RecordEnergyTrailPosition()
		{
			for (int i = ENERGY_TRAIL_LENGTH - 1; i > 0; i--)
				_energyTrailPos[i] = _energyTrailPos[i - 1];

			_energyTrailPos[0] = Projectile.Center;

			if (_energyTrailCount < ENERGY_TRAIL_LENGTH)
				_energyTrailCount++;

			_noiseOffset += 0.12f; // Anima la distorsión continuamente
		}

		/// <summary>
		/// Dibuja la cortina cósmica con distorsión orgánica.
		/// 3 capas superpuestas con ruido lateral diferente = forma de humo/nebulosa viva.
		/// Cada segmento se desplaza perpendicular al movimiento con sin() a diferentes
		/// frecuencias, rompiendo la forma recta y creando bordes irregulares.
		/// </summary>
		private void DrawEnergyTrail()
		{
			if (_energyTrailCount < 3)
				return;

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			int count = _energyTrailCount;

			// ── CAPA 1: Humo exterior difuso — ruido lento y amplio (forma general de nube) ──
			for (int i = 0; i < count - 1; i++)
			{
				Vector2 posA = _energyTrailPos[i];
				Vector2 posB = _energyTrailPos[i + 1];

				if (posA == Vector2.Zero || posB == Vector2.Zero)
					continue;

				float progress = (float)i / (float)count;
				float fade = 1f - progress;
				fade = (float)Math.Pow(fade, 1.2);

				// Normal perpendicular al movimiento
				Vector2 dir = (posB - posA).SafeNormalize(Vector2.UnitY);
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				// Ruido lento: ondulación amplia que mueve toda la nube
				float noise1 = (float)Math.Sin(progress * 4f + _noiseOffset * 0.7f) * 12f;
				// Ruido medio: irregularidad en los bordes
				float noise2 = (float)Math.Sin(progress * 9f + _noiseOffset * 1.3f + 2.1f) * 6f;
				float totalNoise = (noise1 + noise2) * fade;

				Vector2 offsetA = posA + normal * totalNoise;
				Vector2 offsetB = posB + normal * totalNoise;

				Color color;
				if (_isRedVariant)
				{
					color = Color.Lerp(
						new Color(140, 20, 5, 120),
						new Color(40, 3, 0, 0),
						progress
					);
				}
				else
				{
					color = Color.Lerp(
						new Color(70, 8, 95, 120),
						new Color(10, 0, 18, 0),
						progress
					);
				}
				color *= fade * 0.4f * Projectile.Opacity;

				// Ancho variable con distorsión — se ensancha irregular en el medio
				float cloudSwell = (float)Math.Sin(progress * Math.PI);
				float widthNoise = (float)Math.Sin(progress * 7f + _noiseOffset * 0.9f) * 0.3f + 1f;
				float width = MathHelper.Lerp(30f, 55f, cloudSwell) * (1f - progress * 0.4f) * widthNoise;
				width *= Projectile.scale;

				DrawRibbonSegment(pixel, offsetA, offsetB, color, width);
			}

			// ── CAPA 2: Humo medio — ruido diferente, desplazado al lado opuesto ──
			for (int i = 0; i < count - 1; i++)
			{
				Vector2 posA = _energyTrailPos[i];
				Vector2 posB = _energyTrailPos[i + 1];

				if (posA == Vector2.Zero || posB == Vector2.Zero)
					continue;

				float progress = (float)i / (float)count;
				float fade = 1f - progress;
				fade = fade * fade;

				Vector2 dir = (posB - posA).SafeNormalize(Vector2.UnitY);
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				// Ruido OPUESTO a la capa 1 — crea volumen bilateral
				float noise1 = (float)Math.Sin(progress * 5.5f + _noiseOffset * 0.9f + 3.7f) * -10f;
				float noise2 = (float)Math.Cos(progress * 11f + _noiseOffset * 1.5f) * 5f;
				float totalNoise = (noise1 + noise2) * fade;

				Vector2 offsetA = posA + normal * totalNoise;
				Vector2 offsetB = posB + normal * totalNoise;

				Color color;
				if (_isRedVariant)
				{
					color = Color.Lerp(
						new Color(200, 35, 12, 150),
						new Color(70, 5, 0, 0),
						progress
					);
				}
				else
				{
					color = Color.Lerp(
						new Color(100, 12, 135, 150),
						new Color(20, 0, 30, 0),
						progress
					);
				}
				color *= fade * 0.5f * Projectile.Opacity;

				float cloudSwell = (float)Math.Sin(progress * Math.PI + 0.5f);
				float widthNoise = (float)Math.Sin(progress * 8f + _noiseOffset * 1.1f + 1.5f) * 0.25f + 1f;
				float width = MathHelper.Lerp(22f, 42f, cloudSwell) * (1f - progress * 0.5f) * widthNoise;
				width *= Projectile.scale;

				DrawRibbonSegment(pixel, offsetA, offsetB, color, width);
			}

			// ── CAPA 3: Núcleo brillante — ruido rápido y pequeño (detalle de turbulencia) ──
			for (int i = 0; i < count - 1; i++)
			{
				Vector2 posA = _energyTrailPos[i];
				Vector2 posB = _energyTrailPos[i + 1];

				if (posA == Vector2.Zero || posB == Vector2.Zero)
					continue;

				float progress = (float)i / (float)count;
				float fade = 1f - progress;
				fade = fade * fade * fade; // Cúbico: más concentrado cerca de la espada

				Vector2 dir = (posB - posA).SafeNormalize(Vector2.UnitY);
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				// Ruido rápido: turbulencia fina dentro del humo
				float noise = (float)Math.Sin(progress * 14f + _noiseOffset * 2f) * 4f * fade;

				Vector2 offsetA = posA + normal * noise;
				Vector2 offsetB = posB + normal * noise;

				Color color;
				if (_isRedVariant)
				{
					color = Color.Lerp(
						new Color(255, 70, 25, 180),
						new Color(140, 10, 0, 0),
						progress
					);
				}
				else
				{
					color = Color.Lerp(
						new Color(140, 25, 180, 180),
						new Color(35, 0, 50, 0),
						progress
					);
				}
				color *= fade * 0.75f * Projectile.Opacity;

				// Núcleo estrecho pero intenso
				float width = MathHelper.Lerp(12f, 4f, progress) * Projectile.scale;

				DrawRibbonSegment(pixel, offsetA, offsetB, color, width);
			}
		}

		/// <summary>
		/// Dibuja un segmento de cinta entre dos puntos del mundo.
		/// MagicPixel (1x1 blanco) estirado como rectángulo rotado.
		/// </summary>
		private void DrawRibbonSegment(Texture2D pixel, Vector2 worldA, Vector2 worldB, Color color, float width)
		{
			Vector2 screenA = worldA - Main.screenPosition;
			Vector2 screenB = worldB - Main.screenPosition;
			Vector2 diff = screenB - screenA;
			float length = diff.Length();

			if (length < 1f)
				return;

			Main.EntitySpriteDraw(
				pixel,
				screenA,
				new Rectangle(0, 0, 1, 1),
				color,
				diff.ToRotation(),
				new Vector2(0f, 0.5f),
				new Vector2(length, width),
				SpriteEffects.None,
				0
			);
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
				baseColor = Color.Lerp(
					new Color(255, 60, 30),
					new Color(180, 10, 0),
					progress
				);
			}
			else
			{
				baseColor = Color.Lerp(
					new Color(120, 20, 160),
					new Color(30, 0, 40),
					progress
				);
			}

			baseColor.A = 0;
			baseColor *= fade * Projectile.Opacity;
			return baseColor;
		}

		private float TipWidthFunction(float progress)
		{
			float taper = (1f - progress);
			float width = MathHelper.Lerp(18f, 3f, progress) * taper;

			if (progress < 0.15f)
				width *= MathHelper.Lerp(0.6f, 1f, progress / 0.15f);

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
			// Explosión de chispas al impactar
			for (int i = 0; i < 12; i++)
			{
				int dustType = _isRedVariant ? DustID.Torch : DustID.PurpleTorch;
				Dust d = Dust.NewDustDirect(
					target.Center - new Vector2(16f), 32, 32,
					dustType, 0f, 0f, 100, default,
					Main.rand.NextFloat(1.5f, 2.8f)
				);
				d.noGravity = true;
				d.velocity = (d.position - target.Center).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(3f, 8f);
			}

			// Shadowflame en impacto
			for (int i = 0; i < 6; i++)
			{
				Dust sf = Dust.NewDustDirect(
					target.Center - new Vector2(12f), 24, 24,
					DustID.Shadowflame, 0f, 0f, 0, default,
					Main.rand.NextFloat(1.5f, 2.5f)
				);
				sf.noGravity = true;
				sf.velocity = Main.rand.NextVector2Circular(5f, 5f);
			}
		}

		public override Color? GetAlpha(Color lightColor)
		{
			return new Color(255, 255, 255, (int)(255f * Projectile.Opacity));
		}
	}
}
