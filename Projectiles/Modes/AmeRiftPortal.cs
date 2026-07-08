using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// RANGED MODE — Grieta dimensional que queda en el punto de impacto.
	/// Dura 120 ticks (2 segundos). Detecta enemigos en rango y dispara
	/// proyectiles AmeRiftBolt hacia ellos periódicamente.
	///
	/// ai[0] = daño base heredado del proyectil que la creó
	/// ai[1] = ángulo de la grieta (orientación visual)
	/// localAI[0] = timer de vida (0 → LIFETIME)
	/// localAI[1] = cooldown entre disparos
	/// </summary>
	public class AmeRiftPortal : ModProjectile
	{
		// ═══════════════════════════════════════════════════════
		// CONSTANTES
		// ═══════════════════════════════════════════════════════

		private const float LIFETIME        = 120f;  // 2 segundos
		private const float SHOOT_COOLDOWN  = 18f;   // ticks entre disparos
		private const float DETECT_RANGE    = 320f;  // radio de detección
		private const float BOLT_SPEED      = 14f;

		// Tamaño visual de la grieta
		private const float RIFT_LENGTH     = 56f;   // longitud del corte
		private const float RIFT_WIDTH_MAX  = 18f;   // apertura máxima

		// ═══════════════════════════════════════════════════════
		// ESTADO
		// ═══════════════════════════════════════════════════════

		private float LifeTimer
		{
			get => Projectile.localAI[0];
			set => Projectile.localAI[0] = value;
		}
		private float ShootCooldown
		{
			get => Projectile.localAI[1];
			set => Projectile.localAI[1] = value;
		}

		private float _visualTimer;
		private float _riftAngle;        // ángulo del corte dimensional
		private float _openProgress;     // 0→1 mientras se abre
		private float _closeProgress;    // 0→1 mientras se cierra

		// Textura de glow generada en runtime
		private static Texture2D _glow;
		private static bool      _glowCreated;

		// ═══════════════════════════════════════════════════════
		// SETUP
		// ═══════════════════════════════════════════════════════

		public override void SetDefaults()
		{
			Projectile.width    = 32;
			Projectile.height   = 32;
			Projectile.friendly = false; // la grieta no hace daño directamente
			Projectile.hostile  = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = (int)LIFETIME + 10;
			Projectile.alpha    = 255;
		}

		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;
		public override bool ShouldUpdatePosition() => false;

		// ═══════════════════════════════════════════════════════
		// AI
		// ═══════════════════════════════════════════════════════

		public override void AI()
		{
			LifeTimer    += 1f;
			ShootCooldown = MathHelper.Max(0f, ShootCooldown - 1f);
			_visualTimer += 1f;

			float progress = LifeTimer / LIFETIME;

			// Ángulo visual — aleatorio fijado al spawn vía ai[1]
			_riftAngle = Projectile.ai[1];

			// Apertura: primeros 15 ticks
			_openProgress = MathHelper.Clamp(LifeTimer / 15f, 0f, 1f);
			// Cierre: últimos 20 ticks
			_closeProgress = MathHelper.Clamp((LifeTimer - (LIFETIME - 20f)) / 20f, 0f, 1f);

			float visibilidad = _openProgress * (1f - _closeProgress);
			Projectile.Opacity = visibilidad;

			if (LifeTimer >= LIFETIME)
			{
				Projectile.Kill();
				return;
			}

			// Partículas de energía oscura emanando de la grieta
			if (visibilidad > 0.1f && Main.rand.NextBool(3))
			{
				float randAngle  = _riftAngle + Main.rand.NextFloat(-0.3f, 0.3f);
				float randDist   = Main.rand.NextFloat(0f, RIFT_LENGTH * 0.45f);
				float side       = Main.rand.NextBool() ? 1f : -1f;
				Vector2 perpDir  = new Vector2(-MathF.Sin(_riftAngle), MathF.Cos(_riftAngle));
				Vector2 longDir  = new Vector2(MathF.Cos(_riftAngle), MathF.Sin(_riftAngle));
				Vector2 dustPos  = Projectile.Center
					+ longDir * (randDist * side)
					+ perpDir * Main.rand.NextFloat(2f, 10f) * (Main.rand.NextBool() ? 1f : -1f);

				Dust d = Dust.NewDustDirect(dustPos, 2, 2, DustID.Shadowflame);
				d.velocity  = perpDir * Main.rand.NextFloat(0.5f, 2.5f) * (Main.rand.NextBool() ? 1f : -1f);
				d.noGravity = true;
				d.scale     = 0.8f + Main.rand.NextFloat(0f, 0.5f);
				d.fadeIn    = 0.4f;
			}

			// Luz oscura-roja pulsante
			float lightPulse = visibilidad * (0.6f + MathF.Sin(_visualTimer * 0.22f) * 0.2f);
			Lighting.AddLight(Projectile.Center, lightPulse * 0.7f, 0f, 0f);

			// Buscar enemigos y disparar
			if (ShootCooldown <= 0f && visibilidad > 0.3f)
			{
				NPC target = FindNearestEnemy(Projectile.Center, DETECT_RANGE);
				if (target != null)
				{
					ShootBoltAt(target);
					ShootCooldown = SHOOT_COOLDOWN;
				}
			}
		}

		// ═══════════════════════════════════════════════════════
		// DISPARO DE BOLT
		// ═══════════════════════════════════════════════════════

		private void ShootBoltAt(NPC target)
		{
			Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
			// Ligera dispersión para que no sea perfecto
			toTarget = toTarget.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f));

			int damage = (int)Projectile.ai[0];

			SoundEngine.PlaySound(
				new SoundStyle("Terraria/Sounds/Item_8") { Volume = 0.35f, PitchVariance = 0.3f },
				Projectile.Center
			);

			Projectile.NewProjectile(
				Projectile.GetSource_FromThis(),
				Projectile.Center,
				toTarget * BOLT_SPEED,
				ModContent.ProjectileType<AmeRiftBolt>(),
				damage,
				2f,
				Projectile.owner
			);
		}

		// ═══════════════════════════════════════════════════════
		// DRAW — grieta dimensional
		// ═══════════════════════════════════════════════════════

		// Quad para el shader de la grieta
		private static readonly VertexPositionColorTexture[] _riftQuad = new VertexPositionColorTexture[4];
		private static readonly short[]                      _riftIdx  = { 0, 1, 2, 1, 3, 2 };

		public override bool PreDraw(ref Color lightColor)
		{
			// ── SHADER PATH ─────────────────────────────────────
			if (GameShaders.Misc.ContainsKey("AmeRift"))
			{
				DrawWithShader();
				return false;
			}

			// ── FALLBACK sin shader ──────────────────────────────
			EnsureGlow();
			if (_glow == null) return false;

			float vis = Projectile.Opacity;
			if (vis < 0.01f) return false;

			Vector2 center     = Projectile.Center - Main.screenPosition;
			Vector2 glowOrigin = new Vector2(_glow.Width * 0.5f, _glow.Height * 0.5f);

			// Vectores de la grieta
			Vector2 longDir = new Vector2(MathF.Cos(_riftAngle), MathF.Sin(_riftAngle));
			Vector2 perpDir = new Vector2(-MathF.Sin(_riftAngle), MathF.Cos(_riftAngle));

			// Anchura actual (se abre y se cierra)
			float currentWidth = RIFT_WIDTH_MAX * _openProgress * (1f - _closeProgress * 0.85f);
			float pulse        = 1f + MathF.Sin(_visualTimer * 0.28f) * 0.07f;

			// ── ADITIVO ─────────────────────────────────────────
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate, BlendState.Additive,
				SamplerState.LinearClamp, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			// ── AURA EXTERIOR ROJA-OSCURA ────────────────────────
			// Fondo difuso a lo largo del corte
			int auraSteps = 20;
			for (int i = 0; i <= auraSteps; i++)
			{
				float t      = (float)i / auraSteps - 0.5f; // -0.5 → 0.5
				float taper  = 1f - MathF.Abs(t) * 2f;      // 1 en centro, 0 en puntas
				Vector2 pos  = center + longDir * (t * RIFT_LENGTH * 2f);
				float   aw   = (currentWidth * 3.5f + 12f) / _glow.Width * taper * vis * pulse;
				Color   ac   = new Color(80, 0, 0) * (vis * 0.55f * taper);
				Main.spriteBatch.Draw(_glow, pos, null, ac, _riftAngle, glowOrigin, new Vector2(aw * 0.5f, aw), SpriteEffects.None, 0);
			}

			// ── BORDE BRILLANTE DEL CORTE (dos líneas paralelas) ─
			for (int side = -1; side <= 1; side += 2)
			{
				int edgeSteps = 24;
				for (int i = 0; i <= edgeSteps; i++)
				{
					float t      = (float)i / edgeSteps - 0.5f;
					float taper  = 1f - MathF.Pow(MathF.Abs(t) * 2f, 1.6f);
					if (taper < 0f) continue;

					Vector2 edgePos = center
						+ longDir * (t * RIFT_LENGTH * 1.9f)
						+ perpDir * (currentWidth * 0.42f * side);

					float noise  = 1f + MathF.Sin(t * 8f + _visualTimer * 0.35f + side) * 0.12f;
					float ew     = (3.5f * noise) / _glow.Width * taper * vis;
					Color ec     = new Color(200, 5, 0) * (vis * 0.9f * taper);
					ec.A = 0;
					Main.spriteBatch.Draw(_glow, edgePos, null, ec, 0f, glowOrigin, ew, SpriteEffects.None, 0);

					// Destello en el borde
					if (taper > 0.5f && Main.rand.NextBool(6))
					{
						Color sc = new Color(255, 80, 20) * (vis * taper * 0.6f);
						sc.A = 0;
						Main.spriteBatch.Draw(_glow, edgePos, null, sc, 0f, glowOrigin, ew * 1.8f, SpriteEffects.None, 0);
					}
				}
			}

			// ── INTERIOR NEGRO / VACÍO ────────────────────────────
			// Dibujamos el interior oscuro encima de todo para simular el "agujero"
			// Esto requiere volver a AlphaBlend momentáneamente
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Deferred, BlendState.AlphaBlend,
				SamplerState.LinearClamp, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			int innerSteps = 18;
			for (int i = 0; i <= innerSteps; i++)
			{
				float t     = (float)i / innerSteps - 0.5f;
				float taper = 1f - MathF.Pow(MathF.Abs(t) * 2f, 1.4f);
				if (taper < 0f) continue;

				Vector2 iPos = center + longDir * (t * RIFT_LENGTH * 1.7f);
				float   iw   = (currentWidth * 0.55f) / _glow.Width * taper * vis;
				// Negro con alpha sólido para tapar el fondo
				Color   ic   = new Color(0, 0, 0, (int)(200 * vis * taper));
				Main.spriteBatch.Draw(_glow, iPos, null, ic, _riftAngle, glowOrigin, new Vector2(iw * 0.4f, iw), SpriteEffects.None, 0);
			}

			// Volver a aditivo para los detalles finales
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Immediate, BlendState.Additive,
				SamplerState.LinearClamp, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			// ── DESTELLOS EN LOS EXTREMOS DE LA GRIETA ───────────
			for (int tip = -1; tip <= 1; tip += 2)
			{
				Vector2 tipPos = center + longDir * (tip * RIFT_LENGTH * 0.95f);
				float   tipP   = 1f + MathF.Sin(_visualTimer * 0.45f + tip) * 0.18f;
				float   tScale = (currentWidth * 1.6f) / _glow.Width * tipP * vis;
				Color   tCol   = new Color(220, 10, 0) * (vis * 0.75f);
				tCol.A = 0;
				Main.spriteBatch.Draw(_glow, tipPos, null, tCol, 0f, glowOrigin, tScale, SpriteEffects.None, 0);
			}

			// ── PULSOS DE ENERGÍA DESDE EL CENTRO ────────────────
			// Círculos concéntricos que se expanden desde el centro (energía del portal)
			for (int ring = 0; ring < 3; ring++)
			{
				float ringPhase = (_visualTimer * 0.04f + ring * 0.33f) % 1f;
				float ringScale = (currentWidth * (1.5f + ringPhase * 3f)) / _glow.Width * vis;
				float ringAlpha = (1f - ringPhase) * vis * 0.3f;
				Color ringCol   = new Color(160, 5, 0) * ringAlpha;
				ringCol.A = 0;
				Main.spriteBatch.Draw(_glow, center, null, ringCol, 0f, glowOrigin, ringScale, SpriteEffects.None, 0);
			}

			// ── RESTAURAR ─────────────────────────────────────────
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(
				SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null,
				Main.GameViewMatrix.TransformationMatrix
			);

			return false;
		}

		// ═══════════════════════════════════════════════════════
		// HELPERS
		// ═══════════════════════════════════════════════════════

		private NPC FindNearestEnemy(Vector2 from, float maxDist)
		{
			NPC   best     = null;
			float bestDist = maxDist;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || !npc.CanBeChasedBy() || npc.friendly) continue;
				float d = Vector2.Distance(from, npc.Center);
				if (d < bestDist) { bestDist = d; best = npc; }
			}
			return best;
		}

		private void DrawWithShader()
		{
			float vis      = Projectile.Opacity;
			if (vis < 0.01f) return;

			// Tamaño del quad: lo suficientemente grande para cubrir la grieta + aura
			float quadSize = (AmeRiftPortal_RIFT_LENGTH + 80f);
			Vector2 center = Projectile.Center - Main.screenPosition;

			_riftQuad[0] = new VertexPositionColorTexture(
				new Vector3(center.X - quadSize, center.Y - quadSize * 0.6f, 0), Color.White, new Vector2(0,0));
			_riftQuad[1] = new VertexPositionColorTexture(
				new Vector3(center.X + quadSize, center.Y - quadSize * 0.6f, 0), Color.White, new Vector2(1,0));
			_riftQuad[2] = new VertexPositionColorTexture(
				new Vector3(center.X - quadSize, center.Y + quadSize * 0.6f, 0), Color.White, new Vector2(0,1));
			_riftQuad[3] = new VertexPositionColorTexture(
				new Vector3(center.X + quadSize, center.Y + quadSize * 0.6f, 0), Color.White, new Vector2(1,1));

			var effect = GameShaders.Misc["AmeRift"].Shader;
			if (effect == null) return;

			// MVP ortográfica de pantalla
			effect.Parameters["WorldViewProjection"]?.SetValue(
				Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1));

			effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			effect.Parameters["uOpenProgress"]?.SetValue(_openProgress);
			effect.Parameters["uCloseProgress"]?.SetValue(_closeProgress);
			effect.Parameters["uOpacity"]?.SetValue(vis);
			effect.Parameters["uRiftAngle"]?.SetValue(_riftAngle);
			effect.Parameters["uRiftLength"]?.SetValue(0.7f); // 70% del quad
			effect.Parameters["uRiftWidth"]?.SetValue(0.15f);

			effect.Parameters["uColorEdgeInner"]?.SetValue(new Color(255, 40, 5).ToVector4());
			effect.Parameters["uColorEdgeOuter"]?.SetValue(new Color(90, 3, 0).ToVector4());
			effect.Parameters["uColorEnergy"]?.SetValue(new Color(220, 12, 0).ToVector4());
			effect.Parameters["uColorVoid"]?.SetValue(new Color(2, 0, 0).ToVector4());

			if (Ame.NoiseTexture  != null) effect.Parameters["uNoiseTexture"]?.SetValue(Ame.NoiseTexture);
			if (Ame.NoiseTexture2 != null) effect.Parameters["uNoiseTexture2"]?.SetValue(Ame.NoiseTexture2);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
				SamplerState.LinearWrap, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			effect.CurrentTechnique.Passes[0].Apply();
			Main.graphics.GraphicsDevice.DrawUserIndexedPrimitives(
				PrimitiveType.TriangleList, _riftQuad, 0, 4, _riftIdx, 0, 2);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
		}

		// Constante pública para acceder desde DrawWithShader sin referencia a la const privada
		private const float AmeRiftPortal_RIFT_LENGTH = RIFT_LENGTH;

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
				a = a * a * a;
				byte b = (byte)(a * 255f);
				data[y * SIZE + x] = new Color(b, b, b, b);
			}
			_glow.SetData(data);
			_glowCreated = true;
		}
	}
}
