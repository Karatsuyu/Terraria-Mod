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
		// VertexStrip para humo/nube detrás de la espada (ancho y difuso)
		private VertexStrip _smokeStrip = new VertexStrip();

		// Posiciones de la punta guardadas manualmente (Projectile.oldPos guarda el centro)
		private const int TIP_TRAIL_LENGTH = 24;
		private Vector2[] _tipOldPos = new Vector2[TIP_TRAIL_LENGTH];
		private float[] _tipOldRot = new float[TIP_TRAIL_LENGTH];
		private int _tipTrailIndex = 0;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
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
			// Cargar textura del proyectil
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

			// ══════════════════════════════════════════════════════
			// PASO 1: Dibujar VertexStrip trails (requiere SpriteBatch switch)
			// ══════════════════════════════════════════════════════
			DrawVertexTrails();

			// ══════════════════════════════════════════════════════
			// PASO 2: Dibujar la espada principal (ya en SpriteBatch normal)
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

		/// <summary>
		/// Dibuja ambos trails con VertexStrip:
		/// 1) Ribbon fino en la punta (como vanilla Zenith)
		/// 2) Nube de humo ancha detrás de la espada (estilo Galaxia)
		/// </summary>
		private void DrawVertexTrails()
		{
			// Necesitamos al menos unas posiciones para dibujar
			int validTipCount = Math.Min(_tipTrailIndex, TIP_TRAIL_LENGTH);
			int validCenterCount = 0;
			for (int i = 0; i < Projectile.oldPos.Length; i++)
			{
				if (Projectile.oldPos[i] != Vector2.Zero)
					validCenterCount++;
				else
					break;
			}

			if (validTipCount < 3 && validCenterCount < 3)
				return;

			// ── Parar SpriteBatch actual y reiniciar con Immediate para shaders ──
			try
			{
				Main.spriteBatch.End();
			}
			catch { }

			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate,
				BlendState.AlphaBlend,
				SamplerState.PointWrap,
				DepthStencilState.None,
				Main.Rasterizer,
				null,
				Main.GameViewMatrix.TransformationMatrix
			);

			// ══════════════════════════════════════════════════════
			// TRAIL 1: NUBE DE HUMO (detrás de la espada, ancha, difusa)
			// Usa Projectile.oldPos (posiciones del centro de la espada)
			// ══════════════════════════════════════════════════════
			if (validCenterCount >= 3)
			{
				MiscShaderData smokeShader = GameShaders.Misc["FinalFractal"];
				smokeShader.UseImage0(TextureAssets.Extra[ExtrasID.FinalFractal]);

				_smokeStrip.PrepareStrip(
					Projectile.oldPos,
					Projectile.oldRot,
					SmokeColorFunction,
					SmokeWidthFunction,
					-Main.screenPosition + Projectile.Size / 2f
				);

				smokeShader.Apply();
				_smokeStrip.DrawTrail();
			}

			// ══════════════════════════════════════════════════════
			// TRAIL 2: RIBBON EN LA PUNTA (fino, brillante, continuo)
			// Usa _tipOldPos (posiciones de la punta de la espada)
			// ══════════════════════════════════════════════════════
			if (validTipCount >= 3)
			{
				// Construir arrays limpios (solo posiciones válidas)
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
			}

			// ── Restaurar SpriteBatch normal ──
			try
			{
				Main.spriteBatch.End();
			}
			catch { }

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
			// Fade fuerte de cabeza a cola
			float fade = (1f - progress);
			fade = fade * fade; // Cuadrático para que se desvanezca más rápido

			Color baseColor;
			if (_isRedVariant)
			{
				// Rojo brillante → rojo oscuro
				baseColor = Color.Lerp(
					new Color(255, 60, 30),  // Rojo brillante/naranja en la cabeza
					new Color(180, 10, 0),   // Rojo oscuro en la cola
					progress
				);
			}
			else
			{
				// Negro/púrpura oscuro → negro puro
				baseColor = Color.Lerp(
					new Color(120, 20, 160), // Púrpura oscuro en la cabeza
					new Color(30, 0, 40),    // Casi negro en la cola
					progress
				);
			}

			baseColor.A = 0; // Alpha 0 = apariencia aditiva/luminosa
			baseColor *= fade * Projectile.Opacity;
			return baseColor;
		}

		private float TipWidthFunction(float progress)
		{
			// Forma de cinta: más ancho en el inicio, se estrecha hacia la cola
			float taper = (1f - progress);
			float width = MathHelper.Lerp(18f, 3f, progress) * taper;

			// Un poquito más ancho cerca del inicio para dar sensación de "cinta"
			if (progress < 0.15f)
				width *= MathHelper.Lerp(0.6f, 1f, progress / 0.15f);

			return width * Projectile.scale * Projectile.Opacity;
		}

		// ════════════════════════════════════════════════════════════
		// COLOR/WIDTH CALLBACKS para la NUBE DE HUMO
		// ════════════════════════════════════════════════════════════

		private Color SmokeColorFunction(float progress)
		{
			float fade = (1f - progress);
			fade = (float)Math.Pow(fade, 1.5); // Más gradual que el ribbon

			Color baseColor;
			if (_isRedVariant)
			{
				// Humo rojo oscuro denso
				baseColor = Color.Lerp(
					new Color(160, 25, 10),  // Rojo oscuro caliente
					new Color(60, 5, 5),     // Rojo muy oscuro / marrón
					progress
				);
			}
			else
			{
				// Humo negro/púrpura denso
				baseColor = Color.Lerp(
					new Color(80, 10, 100),  // Púrpura oscuro
					new Color(15, 0, 20),    // Casi negro
					progress
				);
			}

			baseColor.A = (byte)(80 * fade); // Semi-transparente para efecto de humo
			baseColor *= fade * 0.55f * Projectile.Opacity; // Más sutil que el ribbon
			return baseColor;
		}

		private float SmokeWidthFunction(float progress)
		{
			// Nube ancha: empieza mediana, se ensancha en el medio, se desvanece
			// Forma tipo "nube" / "humo difuso"
			float cloudShape = (float)Math.Sin(progress * Math.PI); // Máximo en el medio
			float taper = (1f - progress * 0.7f); // No se reduce tanto como el ribbon

			float width = MathHelper.Lerp(25f, 40f, cloudShape) * taper;

			// Fade in al principio
			if (progress < 0.1f)
				width *= progress / 0.1f;

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
