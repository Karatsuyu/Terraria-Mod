using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.WorldBuilding;
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

	public override void SetStaticDefaults()
		{
			// DisplayName y Tooltip se manejan via archivos de localización
		}

	public override void SetDefaults()
	{
		Item.width = 40;
		Item.height = 40;
		
		// Daño base del arma
		Item.damage = 200;
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
			// Multiplicadores de daño según modo (se aplican al daño base de 200)
			switch (CurrentMode)
			{
				case WeaponMode.Melee1:
				case WeaponMode.Melee2:
					Item.DamageType = DamageClass.Melee;
					damage *= 1.0f;  // 100% del daño (200 daño)
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

	// 🔥 VANILLA PORT - LimitPointToPlayerReachableArea (Simplified)
	private void LimitPointToPlayerReachableArea(Player player, ref Vector2 pointPosition)
	{
		// Clamp to world bounds with 200 tile margin
		float margin = 200f * 16f; // 200 tiles in pixels
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
				// Sistema Zenith con interpolación exacta
				int shotNumber = (player.itemAnimationMax - player.itemAnimation) / player.itemTime;
				
				// 🔥 CURSOR GUARDADO - Guardar SOLO en el primer disparo
				Vector2 targetPos;
				if (shotNumber == 0)
				{
					// Primer shot: guardar cursor en ai[2] (parte entera) para compartir entre proyectiles
					targetPos = Main.MouseWorld;
					Projectiles.Modes.AmeZenithBlade.SharedCursorX = targetPos.X;
					Projectiles.Modes.AmeZenithBlade.SharedCursorY = targetPos.Y;
				}
				else
				{
					// Shots 2-6: usar cursor guardado
					targetPos = new Vector2(Projectiles.Modes.AmeZenithBlade.SharedCursorX, 
											Projectiles.Modes.AmeZenithBlade.SharedCursorY);
				}
				
				// Variación del arco aleatorio (-100 a 100)
				float arcVariation = Main.rand.Next(-100, 101);
				
				// Velocity codifica dirección de órbita (varía por shot)
				Vector2 velocityDirection = (targetPos - player.MountedCenter).SafeNormalize(Vector2.Zero);
				
				// DISPAROS 2-6: Variar la dirección de órbita pero MISMO destino
				if (shotNumber >= 1)
				{
					NPC target = FindNearestEnemy(targetPos, 400f);
					
					if (target != null)
					{
						velocityDirection = (target.Center - player.MountedCenter).SafeNormalize(Vector2.Zero);
					}
					else
					{
						// Dispersión en la dirección de órbita
						float angleVariation = Main.rand.NextFloat(-0.5f, 0.5f);
						velocityDirection = velocityDirection.RotatedBy(angleVariation);
					}
				}
				
				// Velocidad codifica dirección de órbita
				Vector2 baseVelocity = velocityDirection * 10f;
				
				// Crear proyectil
				Projectile.NewProjectile(
					source,
					player.MountedCenter,
					baseVelocity,
					ModContent.ProjectileType<Projectiles.Modes.AmeZenithBlade>(),
					damage,
					knockback,
					player.whoAmI,
					arcVariation,  // ai[0] - variación del arco
					0f             // ai[1] - no usado
				);
				return false;

			case WeaponMode.Melee2:
				// 🔥 ZENITH VANILLA PORT 1:1
				int num164 = (player.itemAnimationMax - player.itemAnimation) / player.itemTime;

				// 🔥 Perfil de textura/color (CRITICAL)
				int profile = FinalFractalHelper.GetRandomProfileIndex();

				if (num164 == 0)
					profile = 4956; // primera espada siempre Zenith base

				// 🔥 Limitar cursor al área alcanzable (CRITICAL)
				Vector2 mousePos = Main.MouseWorld;
				LimitPointToPlayerReachableArea(player, ref mousePos);

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

			// 🔥 ARRAY DE LAS 18 ESPADAS - Selección RANDOM como Zenith vanilla
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
				ModContent.ProjectileType<Projectiles.Modes.AmeBlade18>()
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
	}
}
