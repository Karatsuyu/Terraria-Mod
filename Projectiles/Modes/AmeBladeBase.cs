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

	// 🔥 CRITICAL: Desactivar el movimiento automático de Terraria
	// Sin esto, Terraria aplica position += velocity DESPUÉS de cada AI(),
	// desplazando la espada +velocity cada frame (300+ pixels!).
	// Esto causaba: overshoot, no salir del jugador, no esconderse detrás.
	public override bool ShouldUpdatePosition() => false;

	public override void AI()
	{
		if (Projectile.localAI[1] == 0f)
		{
			Projectile.localAI[1] = 1f;
			SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
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
		
		// Posición final = 100% VANILLA, sin modificaciones
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
		
		// Polvo visual
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
		
		Lighting.AddLight(Projectile.Center, 0.7f, 0.3f, 1f);
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
