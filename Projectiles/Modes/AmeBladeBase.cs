using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.Graphics;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// 🔥 CLASE BASE para las 18 espadas del Melee2
	/// Usa el sistema EXACTO de Zenith vanilla (AI_182_FinalFractal)
	/// </summary>
	public abstract class AmeBladeBase : ModProjectile
	{
	public override void SetStaticDefaults()
	{
		ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
		ProjectileID.Sets.TrailingMode[Projectile.type] = 2; // Guarda posición y rotación
	}		public override void SetDefaults()
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
		// Projectile.hide removido para que las espadas sean visibles
	}

	public override void AI()
	{
		// 🔥 CÓDIGO EXACTO DE LA ZENITH VANILLA - AI_182_FinalFractal			// Sonido inicial (solo primera vez)
			if (Projectile.localAI[1] == 0f)
			{
				Projectile.localAI[1] = 1f;
				SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
			}

			Player player = Main.player[Projectile.owner];
			Vector2 mountedCenter = player.MountedCenter;
			
			// lerpValue: factor de velocidad basado en velocity.Length()
			float lerpValue = Utils.GetLerpValue(900f, 0f, Projectile.velocity.Length() * 2f, clamped: true);
			float num = MathHelper.Lerp(0.7f, 2f, lerpValue);
			Projectile.localAI[0] += num;
			
			// Matar después de 120 frames
			if (Projectile.localAI[0] >= 120f)
			{
				Projectile.Kill();
				return;
			}
			
		// lerpValue2: progreso normalizado (0 a 1)
		float lerpValue2 = Utils.GetLerpValue(0f, 1f, Projectile.localAI[0] / 60f, clamped: true);
		float num2 = Projectile.localAI[0] / 60f;
		
		// num3: variación del arco (ai[0])
		float num3 = Projectile.ai[0];
		
		// num4: rotación de la velocidad (dirección hacia cursor)
		float num4 = Projectile.velocity.ToRotation();
		
		// num5: π
		float num5 = (float)Math.PI;
		
		// num6: dirección (1 o -1 según velocity.X)
		float num6 = ((Projectile.velocity.X > 0f) ? 1 : (-1));
		
		// num7: ángulo de rotación circular (π + dirección * progreso * 2π)
		// Esto hace la órbita completa: empieza atrás (π), va hacia adelante, y regresa atrás (3π)
		float num7 = num5 + num6 * lerpValue2 * ((float)Math.PI * 2f);
		
		// 🔥 RADIO DE ÓRBITA FIJO - No depende de distancia al cursor
		// Empieza pequeño (junto al jugador), crece al medio, se reduce al final
		float baseRadius = 80f; // Radio base de la órbita
		float radiusGrowth = Utils.GetLerpValue(0.5f, 1f, lerpValue2, clamped: true) * 40f;
		float num8 = baseRadius + radiusGrowth;
		
		// 🔥 CENTRO DE ÓRBITA = JUGADOR SIEMPRE
		// El centro de la órbita es el jugador, no un punto intermedio
		Vector2 vector = mountedCenter;
		
		// spinningpoint: offset circular rotado
		// num7 va de π a 3π (un ciclo completo) = empieza detrás, va al frente, regresa atrás
		Vector2 spinningpoint = new Vector2(1f, 0f).RotatedBy(num7) * 
			new Vector2(num8, num3 * MathHelper.Lerp(2f, 1f, lerpValue));
		
		// vector2: posición con offset circular rotada en la dirección del cursor
		Vector2 vector2 = vector + spinningpoint.RotatedBy(num4);
		
		// vector3: offset de "swing" adicional (solo al inicio para impulso)
		Vector2 vector3 = (1f - Utils.GetLerpValue(0f, 0.5f, lerpValue2, clamped: true)) * 
			new Vector2((float)((Projectile.velocity.X > 0f) ? 1 : (-1)) * (0f - num8) * 0.1f, (0f - Projectile.ai[0]) * 0.3f);			// num10: rotación final
			float num10 = num7 + num4;
			
			// 🔥 AJUSTE DIAGONAL - Tus sprites están a 45° (diagonal derecha arriba)
			// Restamos 45° para que queden horizontales durante el movimiento
			Projectile.rotation = num10 + (float)Math.PI / 2f - MathHelper.ToRadians(45f);
			
			// Posición final
			Projectile.Center = vector2 + vector3;
			
			// Dirección del sprite
			Projectile.spriteDirection = Projectile.direction = ((Projectile.velocity.X > 0f) ? 1 : (-1));
			
			// Invertir rotación si num3 (arcVariation) es negativo
			if (num3 < 0f)
			{
				Projectile.rotation = num5 + num6 * lerpValue2 * ((float)Math.PI * -2f) + num4;
				Projectile.rotation += (float)Math.PI / 2f - MathHelper.ToRadians(45f);
				Projectile.spriteDirection = Projectile.direction = ((!(Projectile.velocity.X > 0f)) ? 1 : (-1));
			}
			
			// 🎨 Variación aleatoria de escala (IMPORTANTE para variedad visual)
			if (Projectile.localAI[0] == 1f)
			{
				Projectile.scale = 0.8f + Main.rand.NextFloat(0f, 0.4f); // 0.8 a 1.2
			}
			
			// Efectos visuales (polvo) - solo durante primera mitad
			if (num2 < 1f && Main.rand.NextBool(3))
			{
				Vector2 dustDirection = (Projectile.rotation - (float)Math.PI / 2f).ToRotationVector2();
				Dust dust = Dust.NewDustDirect(
					Projectile.Center + dustDirection * 30f,
					Projectile.width / 2,
					Projectile.height / 2,
					DustID.Shadowflame,
					0f, 0f, 100, default, 1.5f
				);
				dust.noGravity = true;
				dust.velocity = dustDirection * 2f + player.velocity;
			}
			
		// Iluminación
		Lighting.AddLight(Projectile.Center, 0.7f, 0.3f, 1f);
		
		// 🔥 Opacidad (fade in/out) - Se esconde más rápido al regresar al jugador
		float fadeIn = Utils.GetLerpValue(0f, 5f, Projectile.localAI[0], clamped: true);
		float fadeOut = Utils.GetLerpValue(120f, 100f, Projectile.localAI[0], clamped: true); // Empieza a desvanecerse en frame 100
		Projectile.Opacity = fadeIn * fadeOut;
	}		// 🔥 CUSTOM RENDERING - Dibuja la textura de la espada con trail
		public override bool PreDraw(ref Color lightColor)
		{
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
			
			Vector2 drawOrigin = texture.Size() * 0.5f;
			Vector2 drawPosition = Projectile.Center - Main.screenPosition;
			
			// Color con brillo y opacidad
			Color drawColor = new Color(255, 255, 255, (int)(255f * Projectile.Opacity));
			
			// 🌈 TRAIL - Dibujar posiciones anteriores con fade
			for (int i = 0; i < Projectile.oldPos.Length; i++)
			{
				if (Projectile.oldPos[i] == Vector2.Zero)
					continue;
				
				float trailAlpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
				Color trailColor = drawColor * trailAlpha * 0.5f;
				
				Vector2 trailDrawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
				
				Main.EntitySpriteDraw(
					texture,
					trailDrawPos,
					null,
					trailColor,
					Projectile.oldRot[i],
					drawOrigin,
					Projectile.scale * (0.7f + trailAlpha * 0.3f),
					SpriteEffects.None,
					0
				);
			}
			
			// ✨ ESPADA PRINCIPAL - Dibujar la espada actual
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
			
			return false; // No usar el draw por defecto
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			// Hitbox expandido
			Rectangle expandedHitbox = projHitbox;
			expandedHitbox.Inflate(30, 30);
			if (expandedHitbox.Intersects(targetHitbox))
				return true;

			// Colisión de línea (filo de la espada)
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

			// Fallback por distancia
			return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 100f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Efecto de impacto
			for (int i = 0; i < 10; i++)
			{
				Dust dust = Dust.NewDustDirect(
					Projectile.position,
					Projectile.width,
					Projectile.height,
					DustID.Shadowflame,
					0f, 0f, 100, default, 1.8f
				);
				dust.noGravity = true;
				dust.velocity *= 3f;
			}
		}

		public override Color? GetAlpha(Color lightColor)
		{
			// Color brillante como la Zenith
			return new Color(255, 255, 255, (int)(255f * Projectile.Opacity));
		}
	}
}
