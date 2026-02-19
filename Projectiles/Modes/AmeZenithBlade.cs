using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.Audio;
using System;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// Modo Melee 1 - Sistema Zenith con órbitas circulares AI_182_FinalFractal
	/// ai[0] = variación aleatoria del arco (-100 a 100)
	/// ai[1] = no usado
	/// </summary>
	public class AmeZenithBlade : ModProjectile
	{
		// 🔥 Variables estáticas para compartir cursor entre TODOS los proyectiles del mismo ataque
		public static float SharedCursorX = 0f;
		public static float SharedCursorY = 0f;
		
		private Vector2 savedTargetPosition;
		private Vector2 savedDirection;
		private Vector2 savedInitialVelocity;
		private bool initialized = false;

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
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 60;
			Projectile.height = 60;
			Projectile.friendly = true;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 300;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			// 🔥 Inicialización - UNA SOLA VEZ
			if (!initialized)
			{
				initialized = true;
				Player player = Main.player[Projectile.owner];
				
				// Guardar velocidad inicial para órbitas
				savedInitialVelocity = Projectile.velocity;
				savedDirection = Projectile.velocity.SafeNormalize(Vector2.Zero);
				
				// 🔥 CORRECCIÓN 1: TODAS convergen al cursor guardado (variable estática)
				savedTargetPosition = new Vector2(SharedCursorX, SharedCursorY);
				
				// 🔥 CORRECCIÓN 2: Spawn VISIBLE desde el jugador
				Projectile.Center = player.MountedCenter;
				Projectile.netUpdate = true;
			}

			// Sonido inicial
			if (!PlayedSound)
			{
				PlayedSound = true;
				SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
			}

			Player currentPlayer = Main.player[Projectile.owner];
			Vector2 mountedCenter = currentPlayer.MountedCenter;

			// Incrementar timer
			LocalTimer += 1f;

			// Duración: 120 frames (60 ida, 60 vuelta)
			if (LocalTimer >= 120f)
			{
				// 🔥 CORRECCIÓN 3: TODAS se ocultan en la misma posición de la espalda
				Vector2 toCursor = (savedTargetPosition - mountedCenter).SafeNormalize(Vector2.Zero);
				Projectile.Center = mountedCenter - toCursor * 30f;
				Projectile.Kill();
				return;
			}

		// Progreso normalizado (0 a 2 en 120 frames)
		float normalizedProgress = LocalTimer / 60f;
		
		// Variación del arco
		float arcVariation = Projectile.ai[0];
		
		// velocityRotation para órbitas (usa savedDirection - comportamiento original)
		float velocityRotation = savedDirection.ToRotation();
		float direction = (savedDirection.X > 0f) ? 1f : -1f;

		// 🔥 Distancia al CURSOR GUARDADO (mismo para todas)
		Vector2 toCursorVec = (savedTargetPosition - mountedCenter).SafeNormalize(Vector2.Zero);
		float maxDistance = Vector2.Distance(mountedCenter, savedTargetPosition);
		
		// 🔥 Progreso triangular 0→1→0
		// 0→1 (frames 0-60): va hacia cursor
		// 1→0 (frames 60-120): regresa del cursor
		float travelProgress = normalizedProgress;
		if (travelProgress > 1f) 
			travelProgress = 2f - travelProgress;
		
		// 🔥 Distancia completa hasta el cursor
		float distance = maxDistance * travelProgress;

		// 🔥 basePosition hacia el CURSOR (línea directa)
		Vector2 basePosition = mountedCenter + toCursorVec * distance;

		// 🔥 ARCO/ÓRBITA usando función SENO - crea curva que pica en el medio
		// Sin(travelProgress * π) = 0 en inicio, 1 en medio (cursor), 0 en retorno
		// Esto crea un ARCO, no círculos
		float arcStrength = (float)Math.Sin(travelProgress * MathHelper.Pi);
		
		// Ángulo de rotación simple para variar dirección de órbita entre espadas
		float rotationAngle = MathHelper.PiOver2 + direction * MathHelper.PiOver4;
		
		// 🔥 Offset perpendicular que crea el ARCO
		// arcStrength hace que el arco sea máximo a mitad de camino
		float arcRadius = 150f * arcStrength; // El arco es máximo en el medio
		Vector2 circularOffset = new Vector2(1f, 0f).RotatedBy(rotationAngle) * 
			new Vector2(arcRadius, arcVariation * arcStrength);

		// Posición final con arco (usa savedDirection para rotación)
		Vector2 finalPosition = basePosition + circularOffset.RotatedBy(velocityRotation);

		// Offset de swing reducido
		float swingStrength = (float)Math.Sin(travelProgress * MathHelper.Pi);
		Vector2 swingOffset = swingStrength * 
			new Vector2(direction * -distance * 0.05f, -arcVariation * 0.15f);

		// Aplicar posición
		Projectile.Center = finalPosition + swingOffset;

		// Rotación visual
		float finalRotation = rotationAngle + velocityRotation;
		Projectile.rotation = finalRotation + MathHelper.PiOver2;

		// Dirección del sprite
		Projectile.spriteDirection = Projectile.direction = (savedDirection.X > 0f) ? 1 : -1;

		// Invertir rotación si el arco es negativo
		if (arcVariation < 0f)
		{
			Projectile.rotation = MathHelper.Pi + direction * normalizedProgress * -MathHelper.TwoPi + velocityRotation;
			Projectile.rotation += MathHelper.PiOver2;
			Projectile.spriteDirection = Projectile.direction = (savedDirection.X > 0f) ? -1 : 1;
		}

			// Efectos visuales (polvo)
			if (normalizedProgress < 1.5f && Main.rand.NextBool(2))
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
				dust.velocity = dustDirection * 2f + currentPlayer.velocity;
			}

			// Iluminación
			Lighting.AddLight(Projectile.Center, 0.7f, 0.3f, 1f);

			// Opacidad (fade in/out)
			Projectile.Opacity = Utils.GetLerpValue(0f, 5f, LocalTimer, true) * 
				Utils.GetLerpValue(120f, 110f, LocalTimer, true);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
			Vector2 drawOrigin = texture.Size() * 0.5f;

			for (int i = 0; i < Projectile.oldPos.Length; i++)
			{
				if (Projectile.oldPos[i] == Vector2.Zero)
					continue;

				float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
				Color color = Color.White * alpha * 0.6f;

				Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + drawOrigin;

				Main.EntitySpriteDraw(
					texture,
					drawPos,
					null,
					color,
					Projectile.rotation,
					drawOrigin,
					Projectile.scale,
					SpriteEffects.None,
					0
				);
			}

			return true;
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
