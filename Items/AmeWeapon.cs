using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
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


	public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
		Vector2 position, Vector2 velocity, int type, int damage, float knockback)
	{
		// No disparar si es click derecho (cambio de modo)
		if (player.altFunctionUse == 2)
			return false;

		switch (CurrentMode)
		{
			case WeaponMode.Melee1:
				// Sistema Zenith mejorado: ahora dispara 6 espadas por swing
				// Calcular qué disparo es (0 = primero, 1 = segundo, etc.)
				int shotNumber = (player.itemAnimationMax - player.itemAnimation) / player.itemTime;					// Obtener posición del cursor
					Vector2 targetPos = Main.MouseWorld;
					Vector2 directionToCursor = targetPos - player.MountedCenter;
					
					// DISPARO 1: Directo al cursor
					if (shotNumber == 0)
					{
						// Primera espada va directo
						velocity = directionToCursor.SafeNormalize(Vector2.UnitX) * Item.shootSpeed;
					}
					// DISPAROS 2 y 3: Buscar enemigos cercanos o dispersión aleatoria
					else
					{
						// Intentar encontrar un enemigo cerca del cursor
						NPC target = FindNearestEnemy(targetPos, 400f);
						
						if (target != null)
						{
							// Apuntar al enemigo encontrado
							directionToCursor = target.Center - player.MountedCenter;
							velocity = directionToCursor.SafeNormalize(Vector2.UnitX) * Item.shootSpeed;
						}
						else
						{
							// Sin enemigo: dispersión aleatoria circular
							Vector2 randomOffset = Main.rand.NextVector2Circular(150f, 150f);
							directionToCursor += randomOffset;
							velocity = directionToCursor.SafeNormalize(Vector2.UnitX) * Item.shootSpeed;
						}
					}
					
					// Variación aleatoria para cada espada (influye en el arco)
					float randomArc = Main.rand.Next(-100, 101);
					
					// Crear UNA espada con ai personalizado
					Projectile.NewProjectile(
						source,
						player.MountedCenter,
						velocity,
						type,
						damage,
						knockback,
						player.whoAmI,
						randomArc,  // ai[0] - variación del arco
						0f          // ai[1] - no usado
					);
					
					return false;  // No disparar el proyectil default

			case WeaponMode.Melee2:
				// Sistema Zenith REAL - AI_182_FinalFractal (código vanilla adaptado)
				int shotNumber2 = (player.itemAnimationMax - player.itemAnimation) / player.itemTime;
				
				Vector2 targetPos2 = Main.MouseWorld;
				Vector2 directionToCursor2 = targetPos2 - player.MountedCenter;
				
				// Velocidad base (la mitad de la dirección al cursor, como Zenith)
				Vector2 baseVelocity = directionToCursor2 / 2f;
				
				// Variación del arco aleatorio (-100 a 100)
				float arcVariation2 = Main.rand.Next(-100, 101);
				
				// DISPARO 1: Directo
				if (shotNumber2 == 0)
				{
					// Primera espada va directa
				}
				// DISPAROS 2-5: Buscar enemigos o dispersión
				else if (shotNumber2 >= 1)
				{
					NPC target2 = FindNearestEnemy(targetPos2, 400f);
					
					if (target2 != null)
					{
						directionToCursor2 = target2.Center - player.MountedCenter;
						baseVelocity = directionToCursor2 / 2f;
					}
					else
					{
						// Dispersión circular aleatoria
						directionToCursor2 += Main.rand.NextVector2Circular(150f, 150f);
						baseVelocity = directionToCursor2 / 2f;
					}
				}
				
				// Crear proyectil con sistema Zenith REAL
				Projectile.NewProjectile(
					source,
					player.MountedCenter,
					baseVelocity,
					ModContent.ProjectileType<Projectiles.Modes.AmeZenithReal>(),
					damage,
					knockback,
					player.whoAmI,
					arcVariation2,  // ai[0] - variación del arco
					0f              // ai[1] - perfil visual
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
