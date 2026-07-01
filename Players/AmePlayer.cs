using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using System;
using System.Collections.Generic;

namespace Ame.Players
{
	/// <summary>
	/// ModPlayer que maneja el estado global del modo Summon:
	///  - Spawn/despawn de las 19 espadas al activar/desactivar el modo
	///  - Rotación del abanico hacia el enemigo más cercano
	///  - Persistencia de las espadas entre cambios de modo
	/// </summary>
	public class AmePlayer : ModPlayer
	{
		// ═══════════════════════════════════════════════════════════
		// ESTADO PÚBLICO — leído por AmeSummonSword
		// ═══════════════════════════════════════════════════════════

		/// <summary>
		/// Ángulo global del abanico de espadas (radianes).
		/// 0 = abanico apunta hacia arriba.
		/// Compartido estáticamente porque todas las espadas del mismo
		/// jugador necesitan leerlo cada frame.
		/// NOTA: En multijugador esto debería ser por-jugador;
		/// para single player static es suficiente.
		/// </summary>
		public static float SummonFanAngle = 0f;

		/// <summary>Verdadero si el modo Summon está activo (espadas desplegadas).</summary>
		public bool SummonActive { get; private set; }

		// ═══════════════════════════════════════════════════════════
		// SCREEN SHAKE
		// ═══════════════════════════════════════════════════════════
		public float ScreenShake { get; set; }

		// ═══════════════════════════════════════════════════════════
		// ESTADO INTERNO
		// ═══════════════════════════════════════════════════════════

		public bool hasAmeWeapon = false;

		// IDs de los proyectiles de las 19 espadas (-1 = no existe)
		private int[] _swordIds = new int[19];

		// Ángulo suavizado del abanico
		private float _fanAngleTarget = 0f;

		// Tipos de proyectil de las 19 espadas (cacheados)
		private static int[] _swordTypes;
		private static bool  _typesLoaded;

		// ═══════════════════════════════════════════════════════════
		// SETUP
		// ═══════════════════════════════════════════════════════════

		public override void Initialize()
		{
			for (int i = 0; i < 19; i++)
				_swordIds[i] = -1;
			SummonActive = false;
		}

		// ═══════════════════════════════════════════════════════════
		// RESET POR FRAME
		// ═══════════════════════════════════════════════════════════

		public override void ResetEffects()
		{
			hasAmeWeapon = false;
		}

		public override void ModifyScreenPosition()
		{
			if (ScreenShake > 0f)
			{
				Main.screenPosition += new Vector2(Main.rand.NextFloat(-ScreenShake, ScreenShake), Main.rand.NextFloat(-ScreenShake, ScreenShake));
			}
		}

		// ═══════════════════════════════════════════════════════════
		// UPDATE PRINCIPAL
		// ═══════════════════════════════════════════════════════════

		public override void PostUpdateEquips()
		{
			hasAmeWeapon = Player.HeldItem.ModItem is Items.AmeWeapon;

			// ── Si las espadas están activas, actualizar el abanico ──
			// (Ahora se mantienen vivas y siguen al jugador incluso si cambia de arma)
			if (SummonActive)
			{
				UpdateFanAngle();
				CleanupDeadSwords();
			}
		}

		// ═══════════════════════════════════════════════════════════
		// SPAWN / DESPAWN
		// ═══════════════════════════════════════════════════════════

		private void SpawnAllSwords(Items.AmeWeapon ameWeapon)
		{
			if (SummonActive) return; // Ya están desplegadas

			EnsureTypesLoaded();

			int damage    = Player.GetWeaponDamage(ameWeapon.Item);
			float knockback = ameWeapon.Item.knockBack;

			for (int i = 0; i < 19; i++)
			{
				// Spawnear la espada — comienza invisible (Opacity=0)
				int projId = Projectile.NewProjectile(
					Player.GetSource_Accessory(ameWeapon.Item),
					Player.MountedCenter,
					Vector2.Zero,
					_swordTypes[i],
					damage,
					knockback,
					Player.whoAmI,
					i,     // ai[0] = índice de esta espada
					Player.whoAmI  // ai[1] = propietario
				);

				if (projId >= 0 && projId < Main.maxProjectiles)
				{
					_swordIds[i] = projId;
					Main.projectile[projId].Opacity = 0f; // fade-in gestionado por la espada
				}
			}

			SummonActive = true;
		}

		public void DespawnAllSwords()
		{
			for (int i = 0; i < 19; i++)
			{
				int id = _swordIds[i];
				if (id >= 0 && id < Main.maxProjectiles && Main.projectile[id].active)
					Main.projectile[id].Kill();
				_swordIds[i] = -1;
			}
			SummonActive = false;
		}

		private void CleanupDeadSwords()
		{
			for (int i = 0; i < 19; i++)
			{
				int id = _swordIds[i];
				if (id < 0 || id >= Main.maxProjectiles) continue;
				if (!Main.projectile[id].active)
					_swordIds[i] = -1;
			}
		}

		// ═══════════════════════════════════════════════════════════
		// ROTACIÓN DEL ABANICO
		// ═══════════════════════════════════════════════════════════

		private void UpdateFanAngle()
		{
			// El abanico ahora se mantendrá estático siempre apuntando hacia arriba (ángulo 0)
			_fanAngleTarget = 0f;

			// Suavizar la rotación (no gira instantáneamente)
			float delta = MathHelper.WrapAngle(_fanAngleTarget - SummonFanAngle);
			SummonFanAngle += delta * Projectiles.Modes.AmeSummonSword.FAN_ROTATE_SPEED_PUBLIC;
		}

		// ═══════════════════════════════════════════════════════════
		// HOOKS ADICIONALES
		// ═══════════════════════════════════════════════════════════

		public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (proj.ModProjectile != null)
			{
				string ns = proj.ModProjectile.GetType().Namespace ?? "";
				if (ns.Contains("Ame.Projectiles.Modes"))
					modifiers.FinalDamage *= 1.05f;
			}
		}

		public override void PostUpdate()
		{
			// Decaimiento del screen shake
			if (ScreenShake > 0f)
			{
				ScreenShake -= 0.5f;
				if (ScreenShake < 0f) ScreenShake = 0f;
			}
		}

		public void ToggleSummonSwords(Items.AmeWeapon ameWeapon)
		{
			if (SummonActive)
				DespawnAllSwords();
			else
				SpawnAllSwords(ameWeapon);
		}

		// ═══════════════════════════════════════════════════════════
		// HELPERS
		// ═══════════════════════════════════════════════════════════

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

		private static void EnsureTypesLoaded()
		{
			if (_typesLoaded) return;
			_swordTypes = new int[]
			{
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword01>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword02>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword03>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword04>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword05>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword06>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword07>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword08>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword09>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword10>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword11>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword12>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword13>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword14>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword15>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword16>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword17>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword18>(),
				ModContent.ProjectileType<Projectiles.Modes.AmeSummonSword19>(),
			};
			_typesLoaded = true;
		}
	}
}
