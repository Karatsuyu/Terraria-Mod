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
	/// Zenith REAL - AI_182_FinalFractal adaptado de Terraria vanilla
	/// ai[0] = variación del arco (-100 a 100)
	/// ai[1] = perfil visual (opcional)
	/// </summary>
	public class AmeZenithReal : ModProjectile
	{
		private float LocalTimer
		{
			get => Projectile.localAI[0];
			set => Projectile.localAI[0] = value;
		}

		private bool PlayedSound
		{
			get => Projectile.localAI[1] == 1f;
			set => Projectile.localAI[1] = value ? 1f : 0f;
		}

		public override void SetStaticDefaults()
		{
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
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
			Projectile.timeLeft = 300;
		}

		public override void AI()
		{
			// Sonido inicial (como Zenith)
			if (!PlayedSound)
			{
				PlayedSound = true;
				SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
			}

			Player player = Main.player[Projectile.owner];
			Vector2 mountedCenter = player.MountedCenter;

			// Cálculo de velocidad basado en distancia
			float velocityLength = Projectile.velocity.Length();
			float lerpValue = Utils.GetLerpValue(900f, 0f, velocityLength * 2f, true);
			float speedMultiplier = MathHelper.Lerp(0.7f, 2f, lerpValue);

			// Incrementar timer local
			LocalTimer += speedMultiplier;

			// Duración: 120 frames (2 segundos)
			if (LocalTimer >= 120f)
			{
				Projectile.Kill();
				return;
			}

			// Progreso normalizado (0 a 1)
			float normalizedProgress = LocalTimer / 60f;
			float lerpValue2 = Utils.GetLerpValue(0f, 1f, normalizedProgress, true);

			// Variación del arco desde ai[0]
			float arcVariation = Projectile.ai[0];
			float velocityRotation = Projectile.velocity.ToRotation();

			// Dirección del giro
			float direction = (Projectile.velocity.X > 0f) ? 1f : -1f;

		// Ángulo de rotación (sistema circular de Zenith)
		float rotationAngle = MathHelper.Pi + direction * lerpValue2 * (MathHelper.TwoPi);

		// Distancia desde el punto de origen
		float distance = velocityLength + Utils.GetLerpValue(0.5f, 1f, lerpValue2, true) * 40f;
		float minDistance = 60f;
		if (distance < minDistance)
			distance = minDistance;

		// Posición base - El cursor está en la posición del jugador + la velocidad COMPLETA
		// (no la mitad, porque Projectile.velocity ya viene dividida desde Shoot)
		Vector2 targetPosition = mountedCenter + Projectile.velocity * 2f; // Multiplicar por 2 porque viene dividida

		// Offset circular (el corazón del sistema Zenith)
		Vector2 circularOffset = new Vector2(1f, 0f).RotatedBy(rotationAngle) * 
			new Vector2(distance, arcVariation * MathHelper.Lerp(2f, 1f, lerpValue));

		// Posición final con rotación aplicada
		Vector2 finalPosition = targetPosition + circularOffset.RotatedBy(velocityRotation);

		// Offset adicional para efecto de "swing"
		Vector2 swingOffset = (1f - Utils.GetLerpValue(0f, 0.5f, lerpValue2, true)) * 
			new Vector2(direction * -distance * 0.1f, -arcVariation * 0.3f);

		// Aplicar posición
		Projectile.Center = finalPosition + swingOffset;

			// Rotación visual (importante para el filo)
			float finalRotation = rotationAngle + velocityRotation;
			Projectile.rotation = finalRotation + MathHelper.PiOver2;

			// Dirección del sprite
			Projectile.spriteDirection = Projectile.direction = (Projectile.velocity.X > 0f) ? 1 : -1;

			// Invertir rotación si el arco es negativo
			if (arcVariation < 0f)
			{
				Projectile.rotation = MathHelper.Pi + direction * lerpValue2 * -MathHelper.TwoPi + velocityRotation;
				Projectile.rotation += MathHelper.PiOver2;
				Projectile.spriteDirection = Projectile.direction = (Projectile.velocity.X > 0f) ? -1 : 1;
			}

			// Efectos visuales (polvo)
			if (normalizedProgress < 1f && Main.rand.NextBool(2))
			{
				Vector2 dustDirection = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
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
			Lighting.AddLight(Projectile.Center, 0.5f, 0.3f, 0.8f);

			// Opacidad (fade in/out)
			Projectile.Opacity = Utils.GetLerpValue(0f, 5f, LocalTimer, true) * 
				Utils.GetLerpValue(120f, 115f, LocalTimer, true);
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
			for (int i = 0; i < 12; i++)
			{
				Dust dust = Dust.NewDustDirect(
					Projectile.position,
					Projectile.width,
					Projectile.height,
					DustID.Shadowflame,
					0f, 0f, 100, default, 2f
				);
				dust.noGravity = true;
				dust.velocity *= 3f;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// Trail effect
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
			Vector2 drawOrigin = texture.Size() * 0.5f;

			// Dibujar trail con fade
			for (int k = 0; k < Projectile.oldPos.Length; k++)
			{
				if (Projectile.oldPos[k] == Vector2.Zero)
					continue;

				Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + 
					new Vector2(0f, Projectile.gfxOffY);
				Color color = Projectile.GetAlpha(lightColor) * 
					((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);

				Main.EntitySpriteDraw(
					texture,
					drawPos,
					null,
					color * 0.5f,
					Projectile.oldRot[k],
					drawOrigin,
					Projectile.scale,
					SpriteEffects.None,
					0
				);
			}

			return true;
		}

		public override Color? GetAlpha(Color lightColor)
		{
			// Color brillante como la Zenith
			return new Color(255, 255, 255, (int)(255f * Projectile.Opacity));
		}
	}
}
