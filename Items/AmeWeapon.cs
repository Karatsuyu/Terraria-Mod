using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.WorldBuilding;
using ReLogic.Content;
using System;

namespace Ame.Items
{
	public partial class AmeWeapon : ModItem
	{
		public enum WeaponMode
		{
			Melee1,    // Zenith estilo real (3 espadas por swing)
			Melee2,    // 15 espadas simultáneas
			Magic,
			Ranged,
			Summon,
			Clone
		}

	public WeaponMode CurrentMode = WeaponMode.Melee1;
	private bool justChangedMode = false;

	// 🔥 Icono animado para Melee2
	private static Asset<Texture2D> melee2Texture;
	// 🔥 Icono para Melee1
	private static Asset<Texture2D> melee1Texture;
	private int melee2FrameCounter = 0;
	private int melee2Frame = 0;
	private int melee2FrameCount = 1; // Se calcula automáticamente al cargar la textura
	private int melee2FrameSpeed = 5; // Ticks por frame de animación

	public override void SetStaticDefaults()
		{
			// Cargar textura animada del icono Melee2
			melee2Texture = ModContent.Request<Texture2D>("Ame/Items/AmeWeapon_Melee2");
			// Cargar textura para Melee1
			melee1Texture = ModContent.Request<Texture2D>("Ame/Projectiles/Modes/IconoMelee1");
		}

	// Textura base guardada para poder restaurar
	private static Asset<Texture2D> baseTexture;

	public override void SetDefaults()
	{
		Item.width = 40;
		Item.height = 40;
		
		// Daño base del arma
		Item.damage = 1000;
		Item.DamageType = DamageClass.Melee;
		
		// Sistema Zenith: 6 espadas por swing
		Item.useAnimation = 30;
		Item.useTime = 5;
		
		Item.useStyle = ItemUseStyleID.Swing;
		Item.autoReuse = true;
		Item.knockBack = 6.5f;
		Item.rare = ItemRarityID.Red;
		Item.value = Item.buyPrice(gold: 20);
		Item.UseSound = SoundID.Item1;
		Item.noMelee = true;
		Item.noUseGraphic = true;
		Item.shoot = ModContent.ProjectileType<Projectiles.Modes.AmeZenithBlade>();
		Item.shootSpeed = 16f;
	}		public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
		{
			// Multiplicadores de daño según modo (se aplican al daño base de 1000)
			switch (CurrentMode)
			{
				case WeaponMode.Melee1:
				case WeaponMode.Melee2:
					Item.DamageType = DamageClass.Melee;
					damage *= 1.0f;  // 100% del daño (1000 daño)
					break;
				case WeaponMode.Magic:
					Item.DamageType = DamageClass.Magic;
					damage *= 0.8f;  // 80% del daño (160 daño)
					break;
				case WeaponMode.Ranged:
					Item.DamageType = DamageClass.Ranged;
					damage *= 0.7f;  // 70% del daño (140 daño)
					break;
				case WeaponMode.Summon:
					Item.DamageType = DamageClass.Summon;
					damage *= 0.6f;  // 60% del daño (120 daño)
					break;
				case WeaponMode.Clone:
					Item.DamageType = DamageClass.Generic;
					damage *= 0.9f;  // 90% del daño (180 daño)
					break;
			}
		}

	public override bool AltFunctionUse(Player player)
	{
		return true;
	}

	public override bool CanUseItem(Player player)
	{
		// Si es click derecho, cambiar a modo rápido solo para cambiar
		if (player.altFunctionUse == 2)
		{
			Item.useTime = 15;
			Item.useAnimation = 15;
			Item.UseSound = null; // Sin sonido de ataque
		}
		else
		{
			// Click izquierdo normal
			Item.useTime = 5;
			Item.useAnimation = 30;
			Item.UseSound = SoundID.Item1;
		}
		return true;
	}

	public override bool? UseItem(Player player)
	{
		if (player.altFunctionUse == 2)
		{
			// Solo cambiar una vez por click
			if (!justChangedMode)
			{
				justChangedMode = true;
				CycleMode();
				Main.NewText("Modo actual: " + CurrentMode.ToString(), GetModeColor());
			}
		}
		else
		{
			justChangedMode = false;
		}
		return true;
	}

	private void CycleMode()
		{
			int next = ((int)CurrentMode + 1) % Enum.GetValues(typeof(WeaponMode)).Length;
			CurrentMode = (WeaponMode)next;
		}

		private Color GetModeColor()
		{
			switch (CurrentMode)
			{
				case WeaponMode.Melee1: return new Color(255, 100, 100);  // Rojo (Zenith)
				case WeaponMode.Melee2: return new Color(255, 150, 50);   // Naranja (Multi-espadas)
				case WeaponMode.Magic: return new Color(100, 100, 255);
				case WeaponMode.Ranged: return new Color(100, 255, 100);
				case WeaponMode.Summon: return new Color(255, 255, 100);
				case WeaponMode.Clone: return new Color(200, 100, 255);
				default: return Color.White;
			}
		}

		/// <summary>
		/// Buscar el enemigo más cercano a una posición
		/// </summary>
		private NPC FindNearestEnemy(Vector2 position, float maxDistance)
		{
			NPC closest = null;
			float closestDist = maxDistance;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc.active && npc.CanBeChasedBy() && !npc.friendly)
				{
					float distance = Vector2.Distance(position, npc.Center);
					if (distance < closestDist)
					{
						closestDist = distance;
						closest = npc;
					}
				}
			}

			return closest;
		}

	// 🔥 VANILLA PORT - LimitPointToPlayerReachableArea (Menos agresiva para evitar problemas en Inframundo)
	private void LimitPointToPlayerReachableArea(Player player, ref Vector2 pointPosition)
	{
		// Clamp to world bounds with smaller margin to avoid issues in Underworld
		float margin = 50f * 16f; // Solo 50 tiles de margen en vez de 200
		pointPosition.X = MathHelper.Clamp(pointPosition.X, margin, Main.maxTilesX * 16f - margin);
		pointPosition.Y = MathHelper.Clamp(pointPosition.Y, margin, Main.maxTilesY * 16f - margin);
	}		// 🔥 VANILLA PORT - GetZenithTarget
		private bool GetZenithTarget(Player player, Vector2 searchCenter, float maxDistance, out NPC targetNPC)
		{
			targetNPC = null;

			float closestDistance = maxDistance;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];

				if (!npc.CanBeChasedBy(player))
					continue;

				float distance = Vector2.Distance(searchCenter, npc.Center);

				if (distance < closestDistance)
				{
					closestDistance = distance;
					targetNPC = npc;
				}
			}

			return targetNPC != null;
		}


	public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
		Vector2 position, Vector2 velocity, int type, int damage, float knockback)
	{
		// No disparar si es click derecho (cambio de modo)
		if (player.altFunctionUse == 2)
			return false;

		switch (CurrentMode)
		{
			case WeaponMode.Melee1:
			{
				// ════════════════════════════════════════════════════════
				// FIRST FRACTAL — Espadas desde el cursor
				// Las espadas nacen alrededor del cursor y convergen al centro.
				// ════════════════════════════════════════════════════════

				int shotNumber = (player.itemAnimationMax - player.itemAnimation) / player.itemTime;

				// Guardar el cursor en el primer disparo del swing
				Vector2 cursorPos;
				if (shotNumber == 0)
				{
					cursorPos = Main.MouseWorld;
					// Guardar en variables estáticas para los shots siguientes
					Projectiles.Modes.AmeFractalBlade.SharedCursorX = cursorPos.X;
					Projectiles.Modes.AmeFractalBlade.SharedCursorY = cursorPos.Y;

					// Spawnear el rift visual SOLO en el primer shot (una vez por swing)
					Projectile.NewProjectile(
						source,
						cursorPos,                   // el rift nace en el cursor
						Vector2.Zero,                // no se mueve
						ModContent.ProjectileType<Projectiles.Modes.AmeFractalRift>(),
						0,                           // sin daño
						0f,
						player.whoAmI,
						3f,                          // ai[0] = número de espadas (info visual)
						0f
					);
				}
				else
				{
					// Shots siguientes: usar cursor guardado del primer shot
					cursorPos = new Vector2(
						Projectiles.Modes.AmeFractalBlade.SharedCursorX,
						Projectiles.Modes.AmeFractalBlade.SharedCursorY
					);
				}

				// Array de las 19 espadas fractales
				int[] fractalTypes = new int[]
				{
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade01>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade02>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade03>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade04>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade05>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade06>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade07>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade08>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade09>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade10>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade11>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade12>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade13>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade14>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade15>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade16>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade17>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade18>(),
					ModContent.ProjectileType<Projectiles.Modes.AmeFractalBlade19>(),
				};

				// Primer shot: 3 espadas equidistantes (forman triángulo)
				// Shots 1-5: 1 espada en ángulo aleatorio cada uno
				int bladesThisShot = (shotNumber == 0) ? 3 : 1;

				for (int b = 0; b < bladesThisShot; b++)
				{
					float spawnAngle;
					if (shotNumber == 0 && bladesThisShot > 1)
					{
						// Distribuir equiangularmente + rotación base aleatoria
						float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
						spawnAngle = baseAngle + (MathHelper.TwoPi / bladesThisShot) * b;
					}
					else
					{
						spawnAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
					}

					// Radio de spawn: leve variación para que no sean perfectamente circulares
					float spawnRadius = 155f + Main.rand.NextFloat(-30f, 55f);

					// Espada aleatoria de las 19
					int bladeType = fractalTypes[Main.rand.Next(fractalTypes.Length)];

					// velocity codifica la posición del cursor (leída en AmeFractalBlade.AI init)
					Projectile.NewProjectile(
						source,
						player.MountedCenter,                 // posición inicial (ignorada por ShouldUpdatePosition=false)
						new Vector2(cursorPos.X, cursorPos.Y), // velocity = cursor
						bladeType,
						damage,
						knockback,
						player.whoAmI,
						spawnAngle,   // ai[0] = ángulo de spawn
						spawnRadius   // ai[1] = radio de spawn
					);
				}

				return false;
			}

			case WeaponMode.Melee2:
				// 🔥 ZENITH VANILLA PORT 1:1
				int num164 = (player.itemAnimationMax - player.itemAnimation) / player.itemTime;

				// 🔥 Perfil aleatorio para cada espada
				int profile = Main.rand.Next(5000, 5020);

				if (num164 == 0)
					profile = 4956; // primera espada siempre Zenith base

				Vector2 mousePos = Main.MouseWorld;

				Vector2 direction = mousePos - player.MountedCenter;

				// 🔥 Sistema de targeting exacto de vanilla
				if (num164 == 1 || num164 == 2)
				{
					NPC target;
					bool found = GetZenithTarget(player, mousePos, 400f, out target);

					if (found)
						direction = target.Center - player.MountedCenter;

					bool applySpread = num164 == 2;

					if (num164 == 1 && !found)
						applySpread = true;

					if (applySpread)
					direction += Main.rand.NextVector2Circular(150f, 150f);
			}

			// velocity codifica DIRECCIÓN + DISTANCIA al cursor
			Vector2 projectileVelocity = direction / 2f;
			float arc = Main.rand.Next(-100, 101);

			// 🔥 ARRAY DE LAS 19 ESPADAS - Selección RANDOM como Zenith vanilla
			int[] swordTypes = new int[]
			{
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade01>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade02>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade03>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade04>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade05>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade06>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade07>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade08>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade09>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade10>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade11>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade12>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade13>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade14>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade15>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade16>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade17>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade18>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade19>()
			};

			// Seleccionar espada aleatoria
			int randomSwordType = swordTypes[Main.rand.Next(swordTypes.Length)];

			// 🔥 Spawn con PROFILE correcto y tipo de espada RANDOM
			Projectile.NewProjectile(
				source,
				player.MountedCenter,
				projectileVelocity,
				randomSwordType, // ← RANDOM entre las 18 espadas
				damage,
				knockback,
				player.whoAmI,
				arc,      // ai[0] - variación del arco
				profile   // ai[1] - PROFILE (CRITICAL)
			);
			return false;

			case WeaponMode.Magic:
					if (player.statMana >= 10)
					{
						player.statMana -= 10;
						Projectile.NewProjectile(source, position, velocity,
							ModContent.ProjectileType<Projectiles.Modes.AmeMagicBlast>(),
							damage, knockback, player.whoAmI);
					}
					else
					{
						Main.NewText("¡No tienes suficiente maná!", new Color(100, 100, 255));
					}
					break;

				case WeaponMode.Ranged:
					Projectile.NewProjectile(source, position, velocity,
						ModContent.ProjectileType<Projectiles.Modes.AmeRangedShot>(),
						damage, knockback, player.whoAmI);
					break;

				case WeaponMode.Summon:
					Projectile.NewProjectile(source, position, Vector2.Zero,
						ModContent.ProjectileType<Projectiles.Modes.AmeSummonMinion>(),
						damage, knockback, player.whoAmI);
					break;

				case WeaponMode.Clone:
					Projectile.NewProjectile(source, position, Vector2.Zero,
						ModContent.ProjectileType<Projectiles.Modes.AmeClone>(),
						damage, knockback, player.whoAmI);
					break;
			}

			return false;
		}

		public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
		{
			TooltipLine modeLine = new TooltipLine(Mod, "ModeInfo", $"Modo actual: {CurrentMode}")
			{
				OverrideColor = GetModeColor()
			};
			tooltips.Add(modeLine);
		}

		// 🔥 ICONO ANIMADO - Actualizar frame de animación
		public override void Update(ref float gravity, ref float maxFallSpeed)
		{
			// Animar solo cuando está en el mundo (drop)
			if (CurrentMode == WeaponMode.Melee2)
				UpdateMelee2Animation();
		}

		public override void UpdateInventory(Player player)
		{
			// Animar en el inventario
			if (CurrentMode == WeaponMode.Melee2)
				UpdateMelee2Animation();
			
			// Actualizar textura para mods de espalda
			UpdateItemTexture();
		}

		private void UpdateMelee2Animation()
		{
			melee2FrameCounter++;
			if (melee2FrameCounter >= melee2FrameSpeed)
			{
				melee2FrameCounter = 0;
				melee2Frame++;
				if (melee2Frame >= melee2FrameCount)
					melee2Frame = 0;
			}
		}

		// 🔥 DIBUJO EN INVENTARIO - Icono animado para Melee2
		public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
		{
			if (CurrentMode == WeaponMode.Melee1 && melee1Texture != null && melee1Texture.IsLoaded)
			{
				Texture2D tex = melee1Texture.Value;
				Rectangle sourceRect = new Rectangle(0, 0, tex.Width, tex.Height);
				Vector2 drawOrigin = new Vector2(tex.Width / 2f, tex.Height / 2f);
				spriteBatch.Draw(tex, position, sourceRect, drawColor, 0f, drawOrigin, scale, SpriteEffects.None, 0f);
				return false;
			}

			if (CurrentMode != WeaponMode.Melee2 || melee2Texture == null || !melee2Texture.IsLoaded)
				return true; // Dibujar icono normal para otros modos

			Texture2D melee2Tex = melee2Texture.Value;
			
			int frameHeight = melee2Tex.Width;
			melee2FrameCount = melee2Tex.Height / frameHeight;
			if (melee2FrameCount < 1) melee2FrameCount = 1;

			Rectangle melee2SourceRect = new Rectangle(0, melee2Frame * frameHeight, melee2Tex.Width, frameHeight);
			
			float finalScale = scale * 0.85f;
			
			Vector2 melee2DrawOrigin = new Vector2(melee2Tex.Width / 2f, frameHeight / 2f);

			spriteBatch.Draw(
				melee2Tex,
				position,
				melee2SourceRect,
				drawColor,
				0f,
				melee2DrawOrigin,
				finalScale,
				SpriteEffects.None,
				0f
			);

			return false;
		}

		// 🔥 Mantener la textura principal actualizada para mods externos (ej: arma en espalda)
		// Esto reemplaza la textura que Terraria usa internamente para dibujar el item
		public override void PostUpdate()
		{
			UpdateItemTexture();
		}

		// Instancia persistente de la animación (NO recrear cada tick)
		private static Terraria.DataStructures.DrawAnimationVertical melee2DrawAnim;

		private void UpdateItemTexture()
		{
			if (CurrentMode == WeaponMode.Melee2 && melee2Texture != null && melee2Texture.IsLoaded)
			{
				// Reemplazar la textura principal del item con el spritesheet animado
				Terraria.GameContent.TextureAssets.Item[Item.type] = melee2Texture;
				
				// Crear la animación UNA SOLA VEZ (si se recrea cada tick, el frame se resetea a 0)
				if (melee2DrawAnim == null)
				{
					Texture2D tex = melee2Texture.Value;
					int frameHeight = tex.Width; // 120px (cada frame es cuadrado)
					int totalFrames = tex.Height / frameHeight;
					if (totalFrames < 1) totalFrames = 1;
					melee2DrawAnim = new Terraria.DataStructures.DrawAnimationVertical(melee2FrameSpeed, totalFrames);
				}
				
				// Asignar la instancia persistente (Terraria llama GetFrame() que avanza la animación)
				Main.itemAnimations[Item.type] = melee2DrawAnim;
			}
			else if (CurrentMode == WeaponMode.Melee1 && melee1Texture != null && melee1Texture.IsLoaded)
			{
				// Usar el icono de Melee1
				Terraria.GameContent.TextureAssets.Item[Item.type] = melee1Texture;
				Main.itemAnimations[Item.type] = null;
			}
			else
			{
				// Restaurar textura y animación original
				if (baseTexture == null)
					baseTexture = ModContent.Request<Texture2D>(Texture);
				Terraria.GameContent.TextureAssets.Item[Item.type] = baseTexture;
				Main.itemAnimations[Item.type] = null;
			}
		}

		// 🔥 DIBUJO EN MUNDO - Icono animado cuando el arma está tirada
		public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
		{
			if (CurrentMode == WeaponMode.Melee1 && melee1Texture != null && melee1Texture.IsLoaded)
			{
				Texture2D tex = melee1Texture.Value;
				Rectangle sourceRect = new Rectangle(0, 0, tex.Width, tex.Height);
				Vector2 drawOrigin = new Vector2(tex.Width / 2f, tex.Height / 2f);
				Vector2 drawPos = Item.Center - Main.screenPosition;
				spriteBatch.Draw(tex, drawPos, sourceRect, lightColor, rotation, drawOrigin, scale, SpriteEffects.None, 0f);
				return false;
			}

			if (CurrentMode != WeaponMode.Melee2 || melee2Texture == null || !melee2Texture.IsLoaded)
				return true;

			Texture2D melee2Tex = melee2Texture.Value;
			
			int frameHeight = melee2Tex.Width;
			melee2FrameCount = melee2Tex.Height / frameHeight;
			if (melee2FrameCount < 1) melee2FrameCount = 1;

			Rectangle melee2SourceRect = new Rectangle(0, melee2Frame * frameHeight, melee2Tex.Width, frameHeight);
			
			Vector2 melee2DrawOrigin = new Vector2(melee2Tex.Width / 2f, frameHeight / 2f);
			Vector2 melee2DrawPos = Item.Center - Main.screenPosition;

			spriteBatch.Draw(
				melee2Tex,
				melee2DrawPos,
				melee2SourceRect,
				lightColor,
				rotation,
				melee2DrawOrigin,
				scale,
				SpriteEffects.None,
				0f
			);

			return false;
		}
	}
}
