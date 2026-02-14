using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// Proyectil estilo Zenith REAL - Comportamiento basado en el código fuente de Terraria
	/// ai[0] = variación aleatoria del arco (-100 a 100)
	/// </summary>
	public class AmeZenithBlade : ModProjectile
	{
		private Vector2 startPos;
		private Vector2 targetPos;
		private float progress = 0f;

		public override void SetStaticDefaults()
		{
			// DisplayName se maneja via localización
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 60;
			Projectile.height = 60;

			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;

		Projectile.penetrate = -1;
		Projectile.timeLeft = 300;  // 5 segundos para ida y vuelta

	Projectile.tileCollide = false;
		Projectile.ignoreWater = true;

		Projectile.extraUpdates = 1; // Más fluido
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = 15;
	}

	public override void OnSpawn(IEntitySource source)
	{
		Player player = Main.player[Projectile.owner];
		startPos = player.Center;
		targetPos = Main.MouseWorld;
		progress = 0f;
	}

	public override void AI()
	{
		Player player = Main.player[Projectile.owner];		// Velocidad de progreso
		float speed = 0.016f;
		progress += speed;

		if (progress >= 1f)
		{
			Projectile.Kill();
			return;
		}

		// Actualizar posiciones iniciales si es necesario
		if (progress == speed || startPos == Vector2.Zero)
		{
			startPos = player.Center;
			targetPos = Main.MouseWorld;
		}

		// Sistema de ida y regreso
		Vector2 from, to;
		float currentProgress;

		if (progress <= 0.5f)
		{
			// FASE 1: Ida hacia el cursor
			from = startPos;
			to = targetPos;
			currentProgress = progress * 2f;
		}
		else
		{
			// FASE 2: Regreso al jugador
			from = targetPos;
			to = player.Center;
			currentProgress = (progress - 0.5f) * 2f;
		}

		// Interpolación lineal base
		Vector2 linear = Vector2.Lerp(from, to, currentProgress);

		// Calcular curvatura (función seno para arco suave)
		float curve = (float)Math.Sin(currentProgress * MathHelper.Pi);

	// Dirección perpendicular para aplicar la curva
	Vector2 direction = (to - from).SafeNormalize(Vector2.UnitX);
	Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

	// Fuerza de curvatura REDUCIDA para arcos más controlados
	float arcVariation = Projectile.ai[0] / 100f;  // -1.0 a 1.0 (rango reducido)
	float curveStrength = 80f * arcVariation;  // 80f = arcos moderados, no se salen de pantalla		// Guardar posición anterior para calcular dirección real
		Vector2 oldPos = Projectile.Center;

		// Aplicar posición final con curva
		Projectile.Center = linear + perpendicular * curve * curveStrength;

		// Rotación DINÁMICA que sigue la trayectoria curva real
		Vector2 realMovement = Projectile.Center - oldPos;
		if (realMovement.LengthSquared() > 1f)
		{
			// La espada apunta hacia donde realmente se mueve (incluye la curva)
			Projectile.rotation = realMovement.ToRotation() + MathHelper.PiOver4;
		}

		// Efectos visuales
		if (Main.rand.NextBool(2))
		{
			Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
				DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
			dust.noGravity = true;
			dust.velocity *= 0.3f;
		}

		// Fade out al final
		if (Projectile.timeLeft < 20)
		{
			Projectile.alpha += 12;
		}
	}	/// <summary>
	/// Colisión mejorada - Sistema de línea como Zenith real
	/// </summary>
	public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
	{
		// Colisión básica por hitbox expandido
		Rectangle expandedHitbox = projHitbox;
		expandedHitbox.Inflate(30, 30);
		if (expandedHitbox.Intersects(targetHitbox))
			return true;

		// Colisión de línea (simulando el filo de la espada)
		float collisionPoint = 0f;
		Vector2 start = Projectile.Center;
		Vector2 end = Projectile.Center + Projectile.rotation.ToRotationVector2() * 100f;

		if (Collision.CheckAABBvLineCollision(
			targetHitbox.TopLeft(),
			targetHitbox.Size(),
			start,
			end,
			40f,  // Radio de colisión muy generoso
			ref collisionPoint))
		{
			return true;
		}

		// Fallback: distancia simple (más generoso)
		float distance = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
		return distance < 100f;
	}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Efecto de impacto
			for (int i = 0; i < 10; i++)
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Shadowflame, 0f, 0f, 100, default, 1.8f);
				dust.noGravity = true;
				dust.velocity *= 3f;
			}

			// Aplicar knockback adicional
			if (Projectile.velocity != Vector2.Zero)
			{
				target.velocity += Projectile.velocity.SafeNormalize(Vector2.UnitY) * 4f;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// Trail effect (estela)
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;

			Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, Projectile.height * 0.5f);

			// Dibujar trail
			for (int k = 0; k < Projectile.oldPos.Length; k++)
			{
				if (Projectile.oldPos[k] == Vector2.Zero)
					continue;

				Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
				Color color = Projectile.GetAlpha(lightColor) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
				Main.EntitySpriteDraw(texture, drawPos, null, color * 0.5f, Projectile.oldRot[k], drawOrigin, Projectile.scale, SpriteEffects.None, 0);
			}

			return true;
		}
	}
}
