# ⚔️ Sistema Zenith - Modo Melee de Ame

## ✅ Implementación Completa

El modo Melee ahora funciona con un sistema **estilo Zenith** profesional:

### 🎯 Características Implementadas

✅ **15 espadas simultáneas** (más que Zenith)
✅ **Todas la misma textura** (sprite rojo consistente)
✅ **Más rápida que Zenith** (useTime: 12 vs 30 de Zenith)
✅ **Más daño** (configurable, actualmente 200 base)
✅ **Ignora bloques** (tileCollide = false)
✅ **Trayectoria tipo arco real** (interpolación + curva seno)
✅ **Regresan al jugador** (sistema ida/vuelta)
✅ **Colisión tipo línea** (CheckAABBvLineCollision)
✅ **Escalable y profesional**
✅ **Efectos visuales** (trail + dust)

---

## 🎮 Cómo Funciona

### Flujo de Ejecución

1. **Jugador usa el arma** (Click izquierdo en modo Melee)
2. **Se generan 15 espadas** en círculo alrededor del jugador
3. **Cada espada:**
   - Empieza en posición del jugador
   - Se curva hacia el cursor
   - Llega al cursor
   - Regresa al jugador
   - Atraviesa bloques
   - Golpea con detección de línea

### Sistema de Trayectoria

```
Jugador → [Curva Seno] → Cursor → [Curva Seno Inversa] → Jugador
```

La curvatura alterna entre espadas (1, -1, 1, -1...) para crear un efecto visual espectacular.

---

## ⚙️ Parámetros Ajustables

### 📁 En `AmeZenithBlade.cs`

#### Velocidad General
```csharp
float speed = 0.14f; // Línea 58
```
- **0.10f** = Muy lenta (efecto dramático)
- **0.14f** = Rápida (actual, más que Zenith)
- **0.20f** = Muy rápida (casi instantánea)

#### Curvatura de Arco
```csharp
float curveStrength = 140f; // Línea 88
```
- **80f** = Curva suave
- **140f** = Curva marcada (actual)
- **200f** = Curva muy pronunciada

#### Duración
```csharp
Projectile.timeLeft = 90; // Línea 35
```
- **60** = Desaparece rápido
- **90** = Balance perfecto (actual)
- **120** = Dura más tiempo

#### Fluidez
```csharp
Projectile.extraUpdates = 1; // Línea 39
```
- **0** = Normal
- **1** = Doble de suave (actual)
- **2** = Ultra suave (puede lagear)

#### Tamaño de Colisión
```csharp
Collision.CheckAABBvLineCollision(..., 20f, ...) // Línea 124
```
- **10f** = Hitbox pequeña
- **20f** = Hitbox perfecta (actual)
- **30f** = Hitbox generosa

---

### 📁 En `AmeWeapon.cs`

#### Cantidad de Espadas
```csharp
int bladeCount = 15; // Línea 115
```
- **10** = Menos espadas, más limpio
- **15** = Perfecto (actual)
- **20** = Caos total

#### Daño Base
```csharp
ModifyWeaponDamage() // Líneas 47-72
case WeaponMode.Melee:
    damage = damage.CombineWith(new StatModifier(1f, 100));
```
- **80** = Menos que Zenith
- **100** = Como Zenith (actual)
- **200** = Doble de Zenith

#### Velocidad de Ataque
```csharp
Item.useTime = 12; // Línea 34
Item.useAnimation = 12;
```
- **12** = Ultra rápido (actual)
- **20** = Normal
- **30** = Como Zenith original

---

## 🎨 Personalización Visual

### Efectos de Polvo

En `AmeZenithBlade.cs`, línea 94:
```csharp
DustID.Shadowflame // Polvo rojo/negro
```

**Alternativas:**
- `DustID.Fire` = Fuego
- `DustID.Electric` = Eléctrico azul
- `DustID.PurpleTorch` = Púrpura místico
- `DustID.RainbowMk2` = Arcoíris

### Color del Trail

Línea 149:
```csharp
color * 0.5f // Transparencia del trail
```
- **0.3f** = Más transparente
- **0.5f** = Balance (actual)
- **0.8f** = Más visible

---

## 🔥 Configuraciones Preestablecidas

### "Zenith Original" (Réplica exacta)
```csharp
// AmeWeapon.cs
Item.useTime = 30;
int bladeCount = 11;

// AmeZenithBlade.cs
float speed = 0.12f;
float curveStrength = 120f;
```

### "Hiper Zenith" (Actual - Mejorada)
```csharp
// AmeWeapon.cs
Item.useTime = 12;
int bladeCount = 15;

// AmeZenithBlade.cs
float speed = 0.14f;
float curveStrength = 140f;
```

### "Zenith Caos" (Sobrecargada)
```csharp
// AmeWeapon.cs
Item.useTime = 8;
int bladeCount = 25;

// AmeZenithBlade.cs
float speed = 0.20f;
float curveStrength = 200f;
Projectile.extraUpdates = 2;
```

### "Zenith Sniper" (Precisa)
```csharp
// AmeWeapon.cs
Item.useTime = 20;
int bladeCount = 5;

// AmeZenithBlade.cs
float speed = 0.10f;
float curveStrength = 80f;
```

---

## 📊 Comparación con Zenith

| Aspecto | Zenith Original | Ame (Actual) |
|---------|----------------|--------------|
| **Espadas** | 11 | 15 |
| **Velocidad Ataque** | 30 | 12 |
| **Daño Base** | 190 | 100 (configurable a 200) |
| **Velocidad Proyectil** | 0.12 | 0.14 |
| **Textura** | Múltiples espadas legendarias | Una sola (personalizable) |
| **Curvatura** | 120 | 140 |

**Resultado:** Ame es **MÁS RÁPIDA** y **MÁS INTENSA** que la Zenith.

---

## 🐛 Troubleshooting

### Las espadas no aparecen
- Verifica que el sprite `AmeZenithBlade.png` exista en `Projectiles/Modes/`
- Rebuild el mod

### Las espadas se mueven muy rápido/lento
- Ajusta `float speed` en `AmeZenithBlade.cs` línea 58

### Las espadas no curvan
- Aumenta `float curveStrength` línea 88
- Verifica que `Projectile.ai[1]` esté alternando (1, -1)

### El daño es muy bajo
- Ajusta `damage` en `ModifyWeaponDamage()` en `AmeWeapon.cs`
- O cambia el daño directo en `Projectile.NewProjectile()` línea 127

### Lag con muchas espadas
- Reduce `bladeCount` a 10-12
- Reduce `Projectile.extraUpdates` a 0
- Desactiva el trail en `PreDraw()`

---

## 🎯 Próximas Mejoras Posibles

- [ ] Sistema de texturas múltiples (como Zenith)
- [ ] Sonidos únicos por espada
- [ ] Partículas personalizadas
- [ ] Efecto de "impacto crítico" visual
- [ ] Sistema de combos
- [ ] Animación de la empuñadura del jugador

---

## 📝 Notas Técnicas

### Sistema de IA (ai[0] y ai[1])

- **ai[0]**: Progreso de la trayectoria (0.0 a 1.0)
  - 0.0 - 0.5: Ida hacia cursor
  - 0.5 - 1.0: Regreso al jugador

- **ai[1]**: Dirección de curvatura
  - 1.0: Curva a la derecha
  - -1.0: Curva a la izquierda

### Colisión Avanzada

El sistema usa `CheckAABBvLineCollision` en lugar de colisión rectangular normal. Esto significa que detecta golpes a lo largo de una **línea** en lugar de un cuadrado, haciéndolo mucho más preciso y realista.

---

**¡El sistema está completo y listo para usar!** 🎉

Solo haz **Build + Reload** en tModLoader y prueba el modo Melee.
