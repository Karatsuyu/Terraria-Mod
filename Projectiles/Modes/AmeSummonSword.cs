using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using System;
using System.Collections.Generic;

namespace Ame.Projectiles.Modes
{
	/// <summary>
	/// Espada invocada del modo Summon.
	/// Permanece orbitando al jugador en un abanico de 3 arcos semicirculares.
	/// Detecta enemigos, vuela a atacarlos y se retrae con la cadena.
	///
	/// IA MEJORADA:
	///   - Targeting inteligente: distribuye espadas proporcionalmente entre enemigos
	///   - Multi-slash: sigue atacando hasta matar al enemigo
	///   - Refuerzos: si un enemigo aguanta mucho, se unen más espadas
	///   - Velocidad aumentada para combate más agresivo
	///
	/// Codificación de ai[]:
	///   ai[0] = índice de esta espada (0-18)
	///   ai[1] = propietario (player.whoAmI) — redundante pero seguro
	///   localAI[0] = timer de estado
	///   localAI[1] = índice del NPC objetivo (-1 = ninguno)
	/// </summary>
	public abstract class AmeSummonSword : ModProjectile
	{
		// ═══════════════════════════════════════════════════════════
		// CONSTANTES — tuneables
		// ═══════════════════════════════════════════════════════════

		/// <summary>Radios de los 3 arcos (interior, medio, exterior).</summary>
		public static readonly float[] ARC_RADII = { 130f, 220f, 310f };

		/// <summary>Espadas por arco (total 19: 6 + 7 + 6).</summary>
		public static readonly int[] BLADES_PER_ARC = { 6, 7, 6 };

		/// <summary>Ángulos específicos para cada espada dentro de su arco (en grados, 0 = centro/arriba).
		/// Esto garantiza que ninguna espada se cruce radialmente con las de otro arco.</summary>
		public static readonly float[][] ARC_ANGLES = new float[][] {
			new float[] { -75f, -45f, -15f, 15f, 45f, 75f },       // Arco interior (6 espadas)
			new float[] { -115f, -85f, -50f, 0f, 50f, 85f, 115f }, // Arco medio (7 espadas)
			new float[] { -55f, -33f, -11f, 11f, 33f, 55f }        // Arco exterior (6 espadas)
		};

		/// <summary>Rango de detección de enemigos en píxeles.</summary>
		private const float DETECT_RANGE = 1200f;

		/// <summary>Velocidad de vuelo hacia el enemigo (MEJORADA: era 18).</summary>
		private const float CHASE_SPEED = 32f;

		/// <summary>Velocidad de retracción de vuelta al abanico (MEJORADA: era 22).</summary>
		private const float RETRACT_SPEED = 35f;

		/// <summary>Distancia al objetivo para activar el SLASH.</summary>
		private const float SLASH_TRIGGER_DIST = 50f;

		/// <summary>Ticks que dura el tajazo (MEJORADA: era 16).</summary>
		private const float SLASH_DURATION = 12f;

		/// <summary>Cooldown individual tras volver al abanico (MEJORADA: era 90).</summary>
		private const float IDLE_COOLDOWN = 30f;

		/// <summary>Amplitud del bobbing vertical en IDLE (píxeles).</summary>
		private const float BOB_AMPLITUDE = 5f;

		/// <summary>Velocidad de rotación del abanico hacia el enemigo más cercano.</summary>
		private const float FAN_ROTATE_SPEED = 0.025f;

		/// <summary>Versión pública para que AmePlayer pueda leerla.</summary>
		public const float FAN_ROTATE_SPEED_PUBLIC = FAN_ROTATE_SPEED;

		/// <summary>Inercia: qué tan rápido sigue al jugador (0=nada, 1=instantáneo).</summary>
		private const float FOLLOW_LERP = 0.12f;

		/// <summary>Longitud de cadena a partir de la cual empieza a jalar de vuelta.</summary>
		private const float CHAIN_PULL_DIST = 1600f;

		/// <summary>Máximo de slashes consecutivos antes de forzar retracción.</summary>
		private const int MAX_CONSECUTIVE_SLASHES = 8;

		/// <summary>Distancia de retroceso entre slashes consecutivos (px).</summary>
		private const float REPOSITION_DIST = 40f;

		/// <summary>Ticks de pausa breve entre slashes para reposicionamiento.</summary>
		private const float REPOSITION_DURATION = 6f;

		/// <summary>Ticks que un enemigo debe sobrevivir antes de pedir refuerzos.</summary>
		private const float REINFORCE_THRESHOLD_TICKS = 180f;

		/// <summary>Máximo de espadas extra que pueden unirse como refuerzo.</summary>
		private const int MAX_REINFORCEMENTS = 4;

		// ═══════════════════════════════════════════════════════════
		// COORDINADOR ESTÁTICO DE TARGETING
		// ═══════════════════════════════════════════════════════════

		// Cuenta cuántas espadas están activamente persiguiendo/atacando a cada NPC
		private static readonly int[] _swordsOnNpc = new int[Main.maxNPCs];
		// Tiempo que cada NPC lleva siendo atacado (para sistema de refuerzos)
		private static readonly float[] _npcAttackDuration = new float[Main.maxNPCs];
		// Flag de "refuerzo solicitado" por NPC
		private static readonly bool[] _reinforceRequested = new bool[Main.maxNPCs];

		/// <summary>Registra que esta espada comienza a atacar un NPC.</summary>
		private static void RegisterAttack(int npcIndex)
		{
			if (npcIndex >= 0 && npcIndex < Main.maxNPCs)
				_swordsOnNpc[npcIndex]++;
		}

		/// <summary>Registra que esta espada deja de atacar un NPC.</summary>
		private static void UnregisterAttack(int npcIndex)
		{
			if (npcIndex >= 0 && npcIndex < Main.maxNPCs)
				_swordsOnNpc[npcIndex] = Math.Max(0, _swordsOnNpc[npcIndex] - 1);
		}

		/// <summary>Cuántas espadas ya están asignadas a este NPC.</summary>
		private static int GetSwordsOnNpc(int npcIndex)
		{
			if (npcIndex >= 0 && npcIndex < Main.maxNPCs)
				return _swordsOnNpc[npcIndex];
			return 0;
		}

		/// <summary>Actualiza el timer de ataque y marca refuerzos si hace falta.</summary>
		private static void UpdateAttackDuration(int npcIndex)
		{
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
			_npcAttackDuration[npcIndex] += 1f;
			if (_npcAttackDuration[npcIndex] >= REINFORCE_THRESHOLD_TICKS)
				_reinforceRequested[npcIndex] = true;
		}

		/// <summary>Resetea el tracking cuando un NPC muere o ya no es válido.</summary>
		private static void ClearNpcTracking(int npcIndex)
		{
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
			_swordsOnNpc[npcIndex] = 0;
			_npcAttackDuration[npcIndex] = 0f;
			_reinforceRequested[npcIndex] = false;
		}

		// ═══════════════════════════════════════════════════════════
		// ESTADO INTERNO
		// ═══════════════════════════════════════════════════════════

		public enum SwordState { Idle, Chase, Slash, Retract, Reposition }

		private SwordState _state = SwordState.Idle;
		private float _stateTimer;
		private float _idleCooldown;

		// Posición ideal en el abanico (se actualiza cada frame)
		private Vector2 _fanPosition;
		// Posición actual suavizada (para inercia de movimiento)
		private Vector2 _smoothPos;
		private bool _smoothPosInited;

		// Objetivo actual
		private int _targetNpcIndex = -1;
		private Vector2 _slashStartPos;
		private Vector2 _slashEndPos;

		// Multi-slash
		private int _slashCount;
		private bool _isRegistered; // Si esta espada está registrada en el coordinador

		// Ángulo global del abanico (compartido con todas las espadas vía static en AmePlayer)
		// Esta espada lo lee de Players.AmePlayer.SummonFanAngle
		private float _bobTimer;

		// Soft-glow para efecto de aura alrededor de la espada
		private static Texture2D _glow;
		private static bool _glowCreated;

		// Accesores localAI
		private float StateTimer  { get => Projectile.localAI[0]; set => Projectile.localAI[0] = value; }
		private float TargetIndex { get => Projectile.localAI[1]; set => Projectile.localAI[1] = value; }

		// Índice de esta espada en el abanico (0-18)
		public int SwordIndex => (int)Projectile.ai[0];

		// ═══════════════════════════════════════════════════════════
		// SETUP
		// ═══════════════════════════════════════════════════════════

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
			ProjectileID.Sets.TrailingMode[Projectile.type]     = 0;
			// Las espadas invocadas ignoran la gravedad
			ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
		}

		public override void SetDefaults()
		{
			Projectile.width    = 46;
			Projectile.height   = 46;
			Projectile.friendly = true;
			Projectile.hostile  = false;
			Projectile.DamageType       = DamageClass.Summon;
			Projectile.minion           = true;
			Projectile.penetrate        = -1;
			Projectile.tileCollide      = false;
			Projectile.ignoreWater      = true;
			Projectile.timeLeft         = int.MaxValue; // Permanente hasta desactivar
			Projectile.minionSlots      = 0f;           // No consume slots
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown  = 15; // Reducido de 25 para más golpes por segundo
			Projectile.noEnchantmentVisuals = true;
			// Sin movimiento automático
			Projectile.extraUpdates = 0;
		}

		public override bool ShouldUpdatePosition() => false;

		// ═══════════════════════════════════════════════════════════
		// AI PRINCIPAL
		// ═══════════════════════════════════════════════════════════

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];

			// Si el jugador muere o está inactivo → matar proyectil
			if (!owner.active || owner.dead)
			{
				CleanupRegistration();
				Projectile.Kill();
				return;
			}

			// Timers
			_bobTimer     += 0.06f;
			StateTimer    += 1f;
			_stateTimer   += 1f;
			_idleCooldown  = MathHelper.Max(0f, _idleCooldown - 1f);

			// Calcular posición ideal en el abanico este frame
			_fanPosition = ComputeFanPosition(owner);

			// Inicializar posición suavizada al primer frame
			if (!_smoothPosInited)
			{
				_smoothPos       = _fanPosition;
				Projectile.Center = _fanPosition;
				_smoothPosInited  = true;
			}

			// Máquina de estados
			switch (_state)
			{
				case SwordState.Idle:       UpdateIdle(owner);       break;
				case SwordState.Chase:      UpdateChase(owner);      break;
				case SwordState.Slash:      UpdateSlash(owner);      break;
				case SwordState.Reposition: UpdateReposition(owner); break;
				case SwordState.Retract:    UpdateRetract(owner);    break;
			}

			// Opacidad: fade-in rápido al invocar
			Projectile.Opacity = MathHelper.Clamp(Projectile.Opacity + 0.06f, 0f, 1f);
		}

		/// <summary>Limpia el registro de esta espada del coordinador al morir.</summary>
		public override void OnKill(int timeLeft)
		{
			CleanupRegistration();
		}

		private void CleanupRegistration()
		{
			if (_isRegistered && _targetNpcIndex >= 0)
			{
				UnregisterAttack(_targetNpcIndex);
				_isRegistered = false;
			}
		}

		// ═══════════════════════════════════════════════════════════
		// ESTADOS
		// ═══════════════════════════════════════════════════════════

		private void UpdateIdle(Player owner)
		{
			// Suavizar hacia la posición del abanico
			_smoothPos    = Vector2.Lerp(_smoothPos, _fanPosition, FOLLOW_LERP);
			Projectile.Center = _smoothPos;

			// Bobbing vertical suave
			Projectile.Center += new Vector2(0f, MathF.Sin(_bobTimer) * BOB_AMPLITUDE);

			// Rotar suavemente (como flotando)
			float targetRot = GetIdleRotation();
			Projectile.rotation = LerpAngle(Projectile.rotation, targetRot, 0.08f);

			// No buscar enemigos si está en cooldown
			if (_idleCooldown > 0f) return;

			// ── TARGETING INTELIGENTE ──
			NPC chosenTarget = ChooseTargetSmart(owner);
			if (chosenTarget != null)
			{
				_targetNpcIndex = chosenTarget.whoAmI;
				TargetIndex     = _targetNpcIndex;
				_slashCount     = 0;
				RegisterAttack(_targetNpcIndex);
				_isRegistered = true;
				TransitionTo(SwordState.Chase);
			}
		}

		private void UpdateChase(Player owner)
		{
			// Validar objetivo
			NPC target = GetCurrentTarget();
			if (target == null)
			{
				BeginRetract();
				return;
			}

			// Actualizar tracking de duración de ataque
			UpdateAttackDuration(_targetNpcIndex);

			// Jalar de vuelta si la cadena está demasiado tensa
			float distToOwner = Vector2.Distance(Projectile.Center, owner.Center);
			if (distToOwner > CHAIN_PULL_DIST)
			{
				BeginRetract();
				return;
			}

			// Volar hacia el objetivo
			Vector2 toTarget = target.Center - Projectile.Center;
			float   dist     = toTarget.Length();

			if (dist < SLASH_TRIGGER_DIST)
			{
				// Llegó — activar tajazo
				_slashStartPos = Projectile.Center;
				_slashEndPos   = target.Center;
				TransitionTo(SwordState.Slash);
				return;
			}

			Vector2 dir = toTarget.SafeNormalize(Vector2.Zero);
			Projectile.Center += dir * Math.Min(CHASE_SPEED, dist);

			// Rotar para apuntar hacia el objetivo
			float desiredRot = dir.ToRotation() + MathHelper.PiOver4;
			Projectile.rotation = LerpAngle(Projectile.rotation, desiredRot, 0.25f);

			// Iluminación en vuelo (rojo carmesí)
			Lighting.AddLight(Projectile.Center, 0.6f, 0.1f, 0.1f);
		}

		private void UpdateSlash(Player owner)
		{
			float progress = _stateTimer / SLASH_DURATION;

			if (progress >= 1f)
			{
				_slashCount++;

				// ── MULTI-SLASH: ¿Seguir atacando? ──
				NPC target = GetCurrentTarget();
				if (target != null && _slashCount < MAX_CONSECUTIVE_SLASHES)
				{
					// El enemigo sigue vivo → reposicionarse brevemente y volver a atacar
					TransitionTo(SwordState.Reposition);
					return;
				}

				// Enemigo muerto o se alcanzó el máximo de slashes → retractar
				BeginRetract();
				return;
			}

			// Animación de tajazo: avanza rápido y vuelve un poco (rebote)
			float curve;
			if (progress < 0.5f)
				curve = EaseOutQuart(progress / 0.5f);       // Avance rápido
			else
				curve = 1f - 0.3f * EaseOutQuad((progress - 0.5f) / 0.5f); // Rebote

			Projectile.Center = Vector2.Lerp(_slashStartPos, _slashEndPos, curve);

			// Rotación rápida durante el tajazo
			Projectile.rotation += 0.35f;

			// Flash de luz al impactar (mitad del tajazo)
			if (_stateTimer == (int)(SLASH_DURATION * 0.5f))
			{
				Lighting.AddLight(Projectile.Center, 1.5f, 0.2f, 0.2f);
				SoundEngine.PlaySound(
					new SoundStyle("Terraria/Sounds/Item_1") { Volume = 0.4f, PitchVariance = 0.3f },
					Projectile.Center
				);

				// Partículas de impacto (rojo carmesí)
				for (int i = 0; i < 10; i++)
				{
					float a = Main.rand.NextFloat(0f, MathHelper.TwoPi);
					Dust d  = Dust.NewDustDirect(Projectile.Center, 4, 4, DustID.RedTorch);
					d.velocity  = new Vector2(MathF.Cos(a), MathF.Sin(a)) * Main.rand.NextFloat(2f, 6f);
					d.noGravity = true;
					d.scale     = 1.2f;
				}
			}
		}

		/// <summary>Estado intermedio entre slashes: la espada retrocede brevemente antes de volver a atacar.</summary>
		private void UpdateReposition(Player owner)
		{
			NPC target = GetCurrentTarget();
			if (target == null)
			{
				BeginRetract();
				return;
			}

			if (_stateTimer >= REPOSITION_DURATION)
			{
				// Listo para el siguiente slash
				_slashStartPos = Projectile.Center;
				_slashEndPos   = target.Center;
				TransitionTo(SwordState.Slash);
				return;
			}

			// Retroceder brevemente en dirección opuesta al enemigo
			Vector2 awayDir = (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitY);
			float repositionSpeed = REPOSITION_DIST / REPOSITION_DURATION;
			Projectile.Center += awayDir * repositionSpeed;

			// Seguir apuntando al enemigo
			Vector2 toTarget = target.Center - Projectile.Center;
			float desiredRot = toTarget.SafeNormalize(Vector2.Zero).ToRotation() + MathHelper.PiOver4;
			Projectile.rotation = LerpAngle(Projectile.rotation, desiredRot, 0.3f);
		}

		private void UpdateRetract(Player owner)
		{
			Vector2 toFan = _fanPosition - Projectile.Center;
			float   dist  = toFan.Length();

			if (dist < 12f)
			{
				// Llegó a casa
				_smoothPos    = _fanPosition;
				Projectile.Center = _fanPosition;
				_idleCooldown = IDLE_COOLDOWN;
				TransitionTo(SwordState.Idle);
				return;
			}

			// Acelerar la retracción
			float speed = Math.Min(RETRACT_SPEED + dist * 0.05f, dist);
			Projectile.Center += toFan.SafeNormalize(Vector2.Zero) * speed;

			// La posición suavizada sigue al proyectil durante la retracción
			_smoothPos = Projectile.Center;

			// Rotar hacia la posición idle
			float targetRot = GetIdleRotation();
			Projectile.rotation = LerpAngle(Projectile.rotation, targetRot, 0.12f);
		}

		/// <summary>Comienza la retracción, limpiando el registro del coordinador.</summary>
		private void BeginRetract()
		{
			CleanupRegistration();
			_targetNpcIndex = -1;
			TransitionTo(SwordState.Retract);
		}

		// ═══════════════════════════════════════════════════════════
		// TARGETING INTELIGENTE
		// ═══════════════════════════════════════════════════════════

		/// <summary>
		/// Elige un objetivo de forma inteligente:
		/// - Si hay pocos enemigos, solo envía pocas espadas (proporcional)
		/// - Si hay muchos, los distribuye equitativamente
		/// - Si un enemigo pide refuerzos (lleva mucho tiempo vivo), se une
		/// </summary>
		private NPC ChooseTargetSmart(Player owner)
		{
			// 1. Recopilar todos los enemigos en rango
			List<int> enemiesInRange = new List<int>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || !npc.CanBeChasedBy() || npc.friendly) continue;
				
				// Limpiar tracking de NPCs muertos/inválidos durante el scan
				float d = Vector2.Distance(owner.Center, npc.Center);
				if (d < DETECT_RANGE)
					enemiesInRange.Add(i);
			}

			// Limpiar tracking de NPCs que ya no existen
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || !npc.CanBeChasedBy())
					ClearNpcTracking(i);
			}

			if (enemiesInRange.Count == 0) return null;

			// 2. ¿Hay algún NPC pidiendo refuerzos?
			for (int i = 0; i < enemiesInRange.Count; i++)
			{
				int npcIdx = enemiesInRange[i];
				if (_reinforceRequested[npcIdx] && GetSwordsOnNpc(npcIdx) < MAX_REINFORCEMENTS + 1)
				{
					// Este enemigo necesita ayuda — unirse
					return Main.npc[npcIdx];
				}
			}

			// 3. Distribución proporcional: preferir enemigos con MENOS espadas asignadas
			// Primero, buscar enemigos que NO tengan NINGUNA espada asignada
			NPC bestUnassigned = null;
			float bestUnassignedDist = float.MaxValue;

			for (int i = 0; i < enemiesInRange.Count; i++)
			{
				int npcIdx = enemiesInRange[i];
				if (GetSwordsOnNpc(npcIdx) == 0)
				{
					float d = Vector2.Distance(owner.Center, Main.npc[npcIdx].Center);
					if (d < bestUnassignedDist)
					{
						bestUnassignedDist = d;
						bestUnassigned = Main.npc[npcIdx];
					}
				}
			}

			if (bestUnassigned != null)
			{
				// Hay un enemigo sin ninguna espada — asignar esta (1 enemigo = 1 espada)
				return bestUnassigned;
			}

			// 4. Todos los enemigos ya tienen al menos 1 espada.
			//    Solo enviar más si hay muchos enemigos o si los enemigos son tanky.
			//    Buscar el que tiene MENOS espadas proporcionalmente.
			NPC bestTarget = null;
			int lowestSwords = int.MaxValue;
			float closestDist = float.MaxValue;

			for (int i = 0; i < enemiesInRange.Count; i++)
			{
				int npcIdx = enemiesInRange[i];
				int swordsOn = GetSwordsOnNpc(npcIdx);
				NPC npc = Main.npc[npcIdx];

				// Calcular cuántas espadas "merece" este enemigo basado en su vida
				// Enemigos con más vida merecen más espadas
				int maxDesired = Math.Max(1, (int)Math.Ceiling(npc.lifeMax / 500f));
				maxDesired = Math.Min(maxDesired, 6); // Cap en 6 espadas por enemigo

				if (swordsOn >= maxDesired) continue; // Ya tiene suficientes

				float d = Vector2.Distance(owner.Center, npc.Center);

				if (swordsOn < lowestSwords || (swordsOn == lowestSwords && d < closestDist))
				{
					lowestSwords = swordsOn;
					closestDist = d;
					bestTarget = npc;
				}
			}

			return bestTarget;
		}

		// ═══════════════════════════════════════════════════════════
		// HELPERS DE POSICIÓN Y ESTADO
		// ═══════════════════════════════════════════════════════════

		/// <summary>
		/// Calcula la posición de esta espada en el abanico, aplicando
		/// el ángulo global de rotación del abanico.
		/// </summary>
		private Vector2 ComputeFanPosition(Player owner)
		{
			int idx = SwordIndex; // 0-18

			// Determinar arco y posición dentro del arco
			int arc = 0;
			int posInArc = 0;

			if (idx < 6)
			{
				arc      = 0;
				posInArc = idx;
			}
			else if (idx < 13)
			{
				arc      = 1;
				posInArc = idx - 6;
			}
			else
			{
				arc      = 2;
				posInArc = idx - 13;
			}

			float radius        = ARC_RADII[arc];

			// Obtener el ángulo predefinido y exacto para evitar cruces
			float localAngle = MathHelper.ToRadians(ARC_ANGLES[arc][posInArc]);

			// Ángulo global del abanico: lee del AmePlayer (compartido por todas las espadas)
			float fanAngle = Players.AmePlayer.SummonFanAngle;

			float baseAngle = -MathHelper.PiOver2 + fanAngle;
			float finalAngle = baseAngle + localAngle;

			float radiusX = radius;
			// Expandir horizontalmente (elípticamente) solo el arco del medio para que lleguen más lejos a los lados
			if (arc == 1)
			{
				radiusX *= 1.75f; 
			}

			return owner.MountedCenter + new Vector2(MathF.Cos(finalAngle) * radiusX, MathF.Sin(finalAngle) * radius);
		}

		/// <summary>Rotación idle: la espada apunta "hacia afuera" desde el jugador.</summary>
		private float GetIdleRotation()
		{
			Player owner    = Main.player[Projectile.owner];
			Vector2 toSword = Projectile.Center - owner.MountedCenter;
			return toSword.ToRotation() + MathHelper.PiOver4;
		}

		private void TransitionTo(SwordState newState)
		{
			_state      = newState;
			_stateTimer = 0f;
			StateTimer  = 0f;
		}

		private NPC GetCurrentTarget()
		{
			if (_targetNpcIndex < 0 || _targetNpcIndex >= Main.maxNPCs) return null;
			NPC npc = Main.npc[_targetNpcIndex];
			return (npc.active && !npc.friendly && npc.CanBeChasedBy()) ? npc : null;
		}

		// ═══════════════════════════════════════════════════════════
		// COLISIÓN — activa solo en SLASH
		// ═══════════════════════════════════════════════════════════

		public override bool? CanDamage()
		{
			return _state == SwordState.Slash;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (_state != SwordState.Slash) return false;
			Rectangle expanded = projHitbox;
			expanded.Inflate(20, 20);
			return expanded.Intersects(targetHitbox);
		}

		// ═══════════════════════════════════════════════════════════
		// DRAW — espada + cadena
		// ═══════════════════════════════════════════════════════════

		public override bool PreDraw(ref Color lightColor)
		{
			EnsureGlow();
			Player owner = Main.player[Projectile.owner];

			// 1. Dibujar la cadena PRIMERO (debajo de la espada)
			DrawChain(owner);

			// 2. Aura de glow
			DrawGlow();

			// 3. Trail en CHASE/SLASH/REPOSITION
			if (_state == SwordState.Chase || _state == SwordState.Slash || _state == SwordState.Reposition)
				DrawTrail();

			// 4. Espada principal
			DrawSword(lightColor);

			return false;
		}

		private void DrawChain(Player owner)
		{
			// Punto de origen: espalda del jugador (ajustado según direction)
			Vector2 chainStart = owner.MountedCenter + new Vector2(-owner.direction * 8f, 4f);
			Vector2 chainEnd   = Projectile.Center + new Vector2(0f, Projectile.height * 0.3f);

			// Bézier cúbico con curva colgante (catenary aproximada)
			// Los puntos de control crean el efecto de cadena que cuelga
			float dist         = Vector2.Distance(chainStart, chainEnd);
			float sagAmount    = Math.Min(dist * 0.4f, 80f); // cuánto cuelga la cadena

			// Control points: caen hacia abajo desde cada extremo
			Vector2 ctrl1 = chainStart + new Vector2(0f, sagAmount * 0.8f);
			Vector2 ctrl2 = chainEnd   + new Vector2(0f, sagAmount * 0.6f);

			// Número de segmentos según longitud
			int segments = Math.Max(6, (int)(dist / 18f));

			// Color de la cadena según si es de día o de noche
			Color chainColor = Main.dayTime ? Color.Black : new Color(200, 0, 0); // negro de día, rojo de noche
			chainColor *= Projectile.Opacity;

			// Aura muy sutil
			Color chainAuraColor = Main.dayTime ? new Color(0, 0, 0, 50) : new Color(255, 0, 0, 50);
			chainAuraColor *= Projectile.Opacity;

			// Necesitamos la textura de cadena — usamos la de Terraria vanilla (Chain)
			// Terraria tiene cadenas en Textures/Misc/Chain
			Texture2D chainTex = TextureAssets.Chain.Value;
			if (chainTex == null) return;

			Vector2 chainOrigin = chainTex.Size() * 0.5f;

			for (int i = 0; i < segments; i++)
			{
				float t0 = (float)i       / segments;
				float t1 = (float)(i + 1) / segments;

				Vector2 p0 = BezierCubic(chainStart, ctrl1, ctrl2, chainEnd, t0);
				Vector2 p1 = BezierCubic(chainStart, ctrl1, ctrl2, chainEnd, t1);

				// Rotación del eslabón = dirección del segmento
				Vector2 seg    = p1 - p0;
				float   rot    = seg.ToRotation() + MathHelper.PiOver2;
				Vector2 midPt  = (p0 + p1) * 0.5f;

				// Aura sutil (se dibuja desfasada)
				for (int j = 0; j < 4; j++)
				{
					Vector2 offset = new Vector2(2f, 0).RotatedBy(j * MathHelper.PiOver2);
					Main.EntitySpriteDraw(
						chainTex,
						midPt - Main.screenPosition + offset,
						null,
						chainAuraColor,
						rot,
						chainOrigin,
						1.05f, // Ligeramente más grande para que haga de aura
						SpriteEffects.None,
						0
					);
				}

				// Cadena principal
				Main.EntitySpriteDraw(
					chainTex,
					midPt - Main.screenPosition,
					null,
					chainColor,
					rot,
					chainOrigin,
					1f,
					SpriteEffects.None,
					0
				);
			}
		}

		private void DrawGlow()
		{
			if (_glow == null) return;

			Vector2 glowOrigin = new Vector2(_glow.Width * 0.5f, _glow.Height * 0.5f);
			Vector2 drawPos    = Projectile.Center - Main.screenPosition;

			// Intensidad según estado
			float intensity = _state switch
			{
				SwordState.Chase      => 0.7f,
				SwordState.Slash      => 1.2f,
				SwordState.Reposition => 0.6f,
				SwordState.Retract    => 0.5f,
				_                     => 0.35f + MathF.Sin(_bobTimer * 2f) * 0.08f
			};

			Color glowColor = new Color(220, 20, 60, 0) * (intensity * Projectile.Opacity); // Rojo carmesí
			float glowScale = 1.6f * Projectile.scale;

			Main.EntitySpriteDraw(_glow, drawPos, null, glowColor, 0f, glowOrigin, glowScale, SpriteEffects.None, 0);
		}

		private void DrawTrail()
		{
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D tex    = TextureAssets.Projectile[Projectile.type].Value;
			Vector2   origin = tex.Size() * 0.5f;

			for (int i = 0; i < Projectile.oldPos.Length; i++)
			{
				if (Projectile.oldPos[i] == Vector2.Zero) continue;

				float  progress = (float)i / Projectile.oldPos.Length;
				float  alpha    = (1f - progress) * 0.35f * Projectile.Opacity;
				Color  col      = new Color(220, 20, 60, 0) * alpha; // Rojo carmesí
				float  scale    = Projectile.scale * (1f - progress * 0.5f);

				Main.EntitySpriteDraw(
					tex,
					Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
					null, col,
					Projectile.oldRot[i],
					origin, scale,
					SpriteEffects.None, 0
				);
			}
		}

		private void DrawSword(Color lightColor)
		{
			Main.instance.LoadProjectile(Projectile.type);
			Texture2D tex    = TextureAssets.Projectile[Projectile.type].Value;
			Vector2   origin = tex.Size() * 0.5f;
			Vector2   drawPos = Projectile.Center - Main.screenPosition;

			SpriteEffects fx = (Projectile.spriteDirection == -1)
				? SpriteEffects.FlipHorizontally
				: SpriteEffects.None;

			// Ignora iluminación para que brille siempre en la oscuridad
			Color drawColor = Color.White * Projectile.Opacity;

			// Flash blanco durante el tajazo
			if (_state == SwordState.Slash)
			{
				float flashT = _stateTimer / SLASH_DURATION;
				float flash  = MathF.Sin(flashT * MathHelper.Pi);
				drawColor    = Color.Lerp(drawColor, Color.White, flash * 0.7f);
			}

			Main.EntitySpriteDraw(tex, drawPos, null, drawColor, Projectile.rotation, origin, Projectile.scale, fx, 0);
		}

		public override Color? GetAlpha(Color lightColor)
		{
			return lightColor * Projectile.Opacity;
		}

		// ═══════════════════════════════════════════════════════════
		// HELPERS
		// ═══════════════════════════════════════════════════════════

		private static Vector2 BezierCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
		{
			float u  = 1f - t;
			float u2 = u * u;
			float u3 = u2 * u;
			float t2 = t * t;
			float t3 = t2 * t;
			return u3 * p0 + 3f * u2 * t * p1 + 3f * u * t2 * p2 + t3 * p3;
		}

		private static float LerpAngle(float from, float to, float t)
		{
			float delta = MathHelper.WrapAngle(to - from);
			return from + delta * t;
		}

		private static float EaseOutQuart(float t) => 1f - MathF.Pow(1f - t, 4f);
		private static float EaseOutQuad(float t)  => 1f - (1f - t) * (1f - t);

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
				float d  = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
				float a  = MathHelper.Clamp(1f - d, 0f, 1f);
				a        = a * a * a;
				byte b   = (byte)(a * 255f);
				data[y * SIZE + x] = new Color(b, b, b, b);
			}
			_glow.SetData(data);
			_glowCreated = true;
		}
	}
}
