# 🗡️ GUÍA COMPLETA — MELEE1: FIRST FRACTAL REIMPLEMENTADO
## AmeMod — Sistema de Espadas desde el Cursor

---

## ¿Qué era la First Fractal de Terraria?

La **First Fractal** era un arma prototipo de Terraria que nunca llegó al juego final (se filtró en código fuente). Su mecánica era sencilla pero visualmente brutal:

- Al atacar, **NO disparaba proyectiles desde el jugador**
- En cambio, invocaba espadas **directamente en el área del cursor**
- Las espadas aparecían desde **posiciones circulares alrededor del cursor**, convergiendo al centro o divergiendo desde él
- El efecto era que en el punto donde apuntabas, surgía una **explosión de espadas** como si el punto fuera el origen del ataque, no el jugador

Esto es fundamentalmente diferente a la Zenith (que orbita desde el jugador hacia el cursor) o la Final Fractal (que orbita alrededor del jugador).

---

## 🎯 COMPORTAMIENTO A IMPLEMENTAR

### Concepto exacto:
1. El jugador hace click izquierdo
2. Se lanzan **N espadas** (entre 3 y 6 por swing, usando las 19 espadas)
3. Cada espada **aparece a cierta distancia alrededor del cursor** en una posición angular distribuida
4. Las espadas **convergen hacia el centro del cursor** desde esas posiciones orbitales
5. Al llegar al centro, **explotan/desaparecen** o rebotan y se dispersan
6. Cada espada usa una `AmeBladeBase` diferente (las 19 ya existentes), elegida al azar

### Diferencia clave vs Melee2:
- **Melee2**: Las espadas orbitan **alrededor del jugador**, moviéndose hacia el cursor (AI de Zenith)
- **Melee1 (nuevo)**: Las espadas nacen **alrededor del cursor**, convergen **al cursor**

---

## 📐 SISTEMA DE AI — Cómo implementarlo

### Paso 1: Nuevo archivo `AmeFractalBlade.cs`

Crea `Projectiles/Modes/AmeFractalBlade.cs`:

```csharp
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
    /// MELEE1 — First Fractal reimplementado
    /// Las espadas aparecen alrededor del cursor y convergen al centro.
    /// 
    /// ai[0] = ángulo de origen (en radianes, dónde nació la espada alrededor del cursor)
    /// ai[1] = radio inicial de spawn (distancia al cursor al nacer)
    /// localAI[0] = timer de vida (0 → 60)
    /// localAI[1] = flag de inicialización (0 = no init, 1 = init)
    /// </summary>
    public abstract class AmeFractalBlade : ModProjectile
    {
        // ════════════════════════════════════════════════════════
        // VARIABLES DE ESTADO
        // ════════════════════════════════════════════════════════

        // Posición del cursor guardada al spawn (NO seguir el cursor en vuelo)
        private Vector2 _targetCenter;
        // Posición de spawn (alrededor del cursor)
        private Vector2 _spawnPosition;
        // Flag de init
        private bool _initialized = false;

        // Timer local
        private float LocalTimer
        {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        // ════════════════════════════════════════════════════════
        // CONSTANTES DE COMPORTAMIENTO — TUNEABLES
        // ════════════════════════════════════════════════════════

        // Duración total en ticks (con extraUpdates=1, son 30 frames reales)
        private const float LIFETIME = 60f;
        // Fracción de la vida donde la espada llega al centro (0.6 = 60% del tiempo)
        private const float CONVERGE_POINT = 0.55f;
        // Radio inicial alrededor del cursor
        // (se puede sobreescribir con ai[1] para variedad)
        private const float BASE_SPAWN_RADIUS = 180f;

        // ════════════════════════════════════════════════════════
        // SETUP
        // ════════════════════════════════════════════════════════

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 28;
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
            Projectile.extraUpdates = 1;           // 2 AI ticks por game tick
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.timeLeft = 300;
            Projectile.noEnchantmentVisuals = true;
        }

        // Desactivar movimiento automático de position += velocity
        public override bool ShouldUpdatePosition() => false;

        // ════════════════════════════════════════════════════════
        // AI PRINCIPAL
        // ════════════════════════════════════════════════════════

        public override void AI()
        {
            // ── INICIALIZACIÓN (solo una vez) ──
            if (!_initialized)
            {
                _initialized = true;

                // Guardar cursor en el momento del spawn
                // La velocity codifica la posición del cursor enviada desde AmeWeapon:
                // velocity.X = cursor.X, velocity.Y = cursor.Y
                _targetCenter = new Vector2(Projectile.velocity.X, Projectile.velocity.Y);

                // Radio de spawn (desde ai[1], con fallback a constante)
                float spawnRadius = Projectile.ai[1] > 0f ? Projectile.ai[1] : BASE_SPAWN_RADIUS;

                // Ángulo de spawn (ai[0])
                float spawnAngle = Projectile.ai[0];

                // Posición de spawn = cursor + offset circular
                _spawnPosition = _targetCenter + new Vector2(
                    MathF.Cos(spawnAngle),
                    MathF.Sin(spawnAngle)
                ) * spawnRadius;

                // Colocar la espada en su posición de spawn
                Projectile.Center = _spawnPosition;

                // Escala aleatoria para variedad visual
                Projectile.scale = 0.75f + Main.rand.NextFloat(0f, 0.5f);

                Projectile.netUpdate = true;
            }

            // ── PROGRESO DE VIDA ──
            LocalTimer += 1f;

            if (LocalTimer >= LIFETIME)
            {
                Projectile.Kill();
                return;
            }

            float progress = LocalTimer / LIFETIME; // 0.0 → 1.0

            // ── MOVIMIENTO: convergencia al cursor ──
            // Fase 1 (0 → CONVERGE_POINT): la espada vuela desde spawn hacia el cursor
            // Fase 2 (CONVERGE_POINT → 1.0): la espada se dispersa/desvanece desde el cursor

            Vector2 currentPosition;

            if (progress <= CONVERGE_POINT)
            {
                // Normalizar progreso de fase 1 (0 → 1)
                float phase1 = progress / CONVERGE_POINT;

                // Easing: aceleración al inicio, frenazo al llegar (ease in-out)
                float eased = EaseInOutCubic(phase1);

                // Interpolación lineal spawn → cursor
                Vector2 linear = Vector2.Lerp(_spawnPosition, _targetCenter, eased);

                // ── CURVATURA ESPIRAL ──
                // La espada no va en línea recta, sino en una curva espiral
                // Esto hace que parezca que "gira" mientras converge

                // Vector desde spawn al cursor
                Vector2 toTarget = _targetCenter - _spawnPosition;
                float totalDistance = toTarget.Length();

                // Perpendicular al vector spawn→cursor
                Vector2 perp = new Vector2(-toTarget.Y, toTarget.X).SafeNormalize(Vector2.Zero);

                // Curvatura: función seno que llega a 0 cuando alcanza el cursor
                // Máxima en la mitad del viaje, 0 al inicio y al final (en el cursor)
                float curveMagnitude = MathF.Sin(phase1 * MathHelper.Pi);

                // Factor de curvatura — ajusta qué tan cerrada es la espiral
                // Puedes exponerlo como constante para tunear
                float curveStrength = totalDistance * 0.35f;

                // Dirección de la curva: algunas espadas curvan a la izquierda, otras a la derecha
                // Se decide con el signo del ángulo de spawn
                float curveSide = (MathF.Sin(Projectile.ai[0] * 2f) > 0f) ? 1f : -1f;

                currentPosition = linear + perp * curveMagnitude * curveStrength * curveSide;
            }
            else
            {
                // Fase 2: la espada SALE DISPARADA desde el cursor en la dirección opuesta a su origen
                float phase2 = (progress - CONVERGE_POINT) / (1f - CONVERGE_POINT); // 0 → 1
                float eased2 = EaseOutQuad(phase2);

                // Dirección de salida = dirección desde cursor hacia spawn (opuesta a la entrada)
                Vector2 exitDirection = (_spawnPosition - _targetCenter).SafeNormalize(Vector2.Zero);

                // La espada sale más lejos de lo que entró (efecto de "rebote")
                float exitDistance = 80f * eased2;
                currentPosition = _targetCenter + exitDirection * exitDistance;
            }

            Projectile.Center = currentPosition;

            // ── ROTACIÓN ──
            // La espada rota continuamente, más rápido cerca del cursor
            float rotationSpeed = MathHelper.Pi * 0.08f;
            if (progress > 0.3f && progress < CONVERGE_POINT)
                rotationSpeed *= 1.5f; // Acelera al llegar

            // Sentido de rotación según el lado de la curva
            float rotDir = (MathF.Sin(Projectile.ai[0] * 2f) > 0f) ? 1f : -1f;
            Projectile.rotation += rotationSpeed * rotDir;

            // ── SPRITE DIRECTION ──
            // Mirar hacia el cursor mientras converge
            Vector2 toCursor = _targetCenter - Projectile.Center;
            if (toCursor.LengthSquared() > 1f)
            {
                Projectile.spriteDirection = toCursor.X > 0f ? 1 : -1;
                Projectile.direction = Projectile.spriteDirection;
            }

            // ── OPACIDAD ──
            // Fade in rápido al spawn, fade out suave al desaparecer
            float fadeIn  = Utils.GetLerpValue(0f, 8f, LocalTimer, clamped: true);
            float fadeOut = Utils.GetLerpValue(LIFETIME, LIFETIME - 10f, LocalTimer, clamped: true);
            Projectile.Opacity = fadeIn * fadeOut;

            // ── ILUMINACIÓN ──
            // (cada subclase puede sobreescribir para colores distintos)
            EmitLight();
        }

        // ════════════════════════════════════════════════════════
        // ILUMINACIÓN — sobreescribible por subclases
        // ════════════════════════════════════════════════════════

        protected virtual void EmitLight()
        {
            Lighting.AddLight(Projectile.Center, 0.8f, 0.15f, 0.05f);
        }

        // ════════════════════════════════════════════════════════
        // COLISIÓN
        // ════════════════════════════════════════════════════════

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Rectangle expanded = projHitbox;
            expanded.Inflate(25, 25);
            if (expanded.Intersects(targetHitbox))
                return true;

            float cp = 0f;
            if (Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center,
                Projectile.Center + Projectile.rotation.ToRotationVector2() * 80f,
                35f, ref cp))
                return true;

            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 80f;
        }

        // ════════════════════════════════════════════════════════
        // EASING FUNCTIONS
        // ════════════════════════════════════════════════════════

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        // ════════════════════════════════════════════════════════
        // COLOR
        // ════════════════════════════════════════════════════════

        public override Color? GetAlpha(Color lightColor)
        {
            return new Color(255, 255, 255, (int)(255f * Projectile.Opacity));
        }
    }
}
```

---

### Paso 2: Subclases de AmeFractalBlade

Crea `Projectiles/Modes/AmeFractalBlades.cs`:

```csharp
namespace Ame.Projectiles.Modes
{
    // Las 19 espadas del modo Melee1 (First Fractal)
    // Heredan de AmeFractalBlade en vez de AmeBladeBase
    // (AmeBladeBase sigue siendo usada solo por Melee2)

    public class AmeFractalBlade01 : AmeFractalBlade { }
    public class AmeFractalBlade02 : AmeFractalBlade { }
    public class AmeFractalBlade03 : AmeFractalBlade { }
    public class AmeFractalBlade04 : AmeFractalBlade { }
    public class AmeFractalBlade05 : AmeFractalBlade { }
    public class AmeFractalBlade06 : AmeFractalBlade { }
    public class AmeFractalBlade07 : AmeFractalBlade { }
    public class AmeFractalBlade08 : AmeFractalBlade { }
    public class AmeFractalBlade09 : AmeFractalBlade { }
    public class AmeFractalBlade10 : AmeFractalBlade { }
    public class AmeFractalBlade11 : AmeFractalBlade { }
    public class AmeFractalBlade12 : AmeFractalBlade { }
    public class AmeFractalBlade13 : AmeFractalBlade { }
    public class AmeFractalBlade14 : AmeFractalBlade { }
    public class AmeFractalBlade15 : AmeFractalBlade { }
    public class AmeFractalBlade16 : AmeFractalBlade { }
    public class AmeFractalBlade17 : AmeFractalBlade { }
    public class AmeFractalBlade18 : AmeFractalBlade { }
    public class AmeFractalBlade19 : AmeFractalBlade { }
}
```

**IMPORTANTE sobre los sprites:**
Estas clases van a buscar automáticamente `AmeFractalBlade01.png`, etc.
Puedes simplemente **copiar los mismos PNGs de las AmeBlade y renombrarlos**, o apuntar a la misma textura sobreescribiendo `Texture` en cada subclase:

```csharp
public class AmeFractalBlade01 : AmeFractalBlade
{
    public override string Texture => "Ame/Projectiles/Modes/AmeBlade01";
}
// ... (repetir para las 19)
```

Esto reutiliza los sprites existentes sin duplicar archivos.

---

### Paso 3: Modificar `AmeWeapon.cs` — case Melee1

Reemplaza el `case WeaponMode.Melee1:` en `Shoot()` con esto:

```csharp
case WeaponMode.Melee1:
{
    // ── FIRST FRACTAL: espadas desde el cursor ──

    // Array de los 19 tipos de espadas fractal
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

    // Número de espadas por swing (basado en el shotNumber del animation)
    // Con useAnimation=30 y useTime=5 → 6 shots por swing
    int shotNumber = (player.itemAnimationMax - player.itemAnimation) / player.itemTime;

    // Cuántas espadas invocamos por disparo (varía con el shotNumber)
    // Primer shot: 3 espadas; shots siguientes: 1 espada cada uno
    int bladesThisShot = (shotNumber == 0) ? 3 : 1;

    // Cursor actual (o del primer shot si quieres bloquearlo)
    Vector2 cursorPos = Main.MouseWorld;

    for (int b = 0; b < bladesThisShot; b++)
    {
        // Distribuir angularmente alrededor del cursor
        // Si es el primer shot con 3 espadas, distribuirlas equidistantemente
        // Los shots siguientes: ángulo aleatorio

        float spawnAngle;
        if (shotNumber == 0 && bladesThisShot > 1)
        {
            // 3 espadas equidistantes (120° entre sí) + rotación aleatoria base
            float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            spawnAngle = baseAngle + (MathHelper.TwoPi / bladesThisShot) * b;
        }
        else
        {
            // Espada individual en ángulo completamente aleatorio
            spawnAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
        }

        // Radio de spawn: varía levemente para que no salgan todas del mismo círculo
        float spawnRadius = 160f + Main.rand.NextFloat(-40f, 60f);

        // Espada aleatoria de las 19
        int bladeType = fractalTypes[Main.rand.Next(fractalTypes.Length)];

        // CODIFICAR el cursor en velocity (ai se usa para angle y radius)
        // velocity.X = cursor.X, velocity.Y = cursor.Y
        // (AmeFractalBlade lo lee en la inicialización)
        Projectile.NewProjectile(
            source,
            player.MountedCenter,   // posición de spawn (ignorada por ShouldUpdatePosition=false)
            new Vector2(cursorPos.X, cursorPos.Y), // velocity codifica el cursor
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
```

---

### Paso 4: Ajustar `SetDefaults` de AmeWeapon para Melee1

En `CanUseItem`, el modo Melee1 debería tener sus propios valores:

```csharp
public override bool CanUseItem(Player player)
{
    if (player.altFunctionUse == 2)
    {
        Item.useTime = 15;
        Item.useAnimation = 15;
        Item.UseSound = null;
    }
    else
    {
        switch (CurrentMode)
        {
            case WeaponMode.Melee1:
                // First Fractal: animación más lenta, 4 shots por swing
                Item.useTime = 8;
                Item.useAnimation = 32;
                Item.UseSound = SoundID.Item71; // sonido más dramático
                break;
            case WeaponMode.Melee2:
                // Zenith: rápido
                Item.useTime = 5;
                Item.useAnimation = 30;
                Item.UseSound = SoundID.Item1;
                break;
            default:
                Item.useTime = 5;
                Item.useAnimation = 30;
                Item.UseSound = SoundID.Item1;
                break;
        }
    }
    return true;
}
```

---

### Paso 5: Sprites (reutilizar los existentes)

La manera más limpia: en `AmeFractalBlades.cs`, sobreescribe `Texture` en cada clase:

```csharp
public class AmeFractalBlade01 : AmeFractalBlade {
    public override string Texture => "Ame/Projectiles/Modes/AmeBlade01";
}
public class AmeFractalBlade02 : AmeFractalBlade {
    public override string Texture => "Ame/Projectiles/Modes/AmeBlade02";
}
// ... etc para las 19
```

Así no duplicas ningún PNG.

---

## 🎨 IDEAS VISUALES AVANZADAS PARA MELEE1

Ahora la parte interesante. La nebulosa del Melee2 es muy buena, pero hay ideas mucho más impactantes para el Melee1. La idea central es que como las espadas **convergen en un punto** (el cursor), puedes crear efectos que aprovechen ese punto de convergencia para hacer algo que nadie haya visto en un mod de Terraria.

---

### 💡 IDEA 1 — RIFT DE IMPACTO (la más impresionante)

**Concepto:** Cuando las espadas llegan al cursor, en vez de simplemente desaparecer, se dibuja un **portal/grieta dimensional** en ese punto que se expande y colapsa.

**Cómo hacerlo:**
- Crea un proyectil separado llamado `AmeFractalRift` que se spawnea en el cursor al activar el arma (solo 1 por swing, dura lo que dura el swing)
- Este proyectil no hace daño, solo es visual
- Dibuja una serie de **elipses concéntricas rotativas** usando líneas de `Dust` o directamente con `VertexStrip`
- Las elipses colapsan hacia el centro mientras las espadas llegan
- Cuando la última espada aterriza, el rift hace un **flash de luz** con `Lighting.AddLight` en radio grande

**Resultado visual:** Las espadas emergen de un portal rasgado en el espacio, lo que hace que la mecánica "desde el cursor" tenga una explicación visual coherente.

**Código base del Rift:**

```csharp
// En AmeFractalRift.cs
public override void AI()
{
    // Expande rápido, luego colapsa
    float progress = LocalTimer / 40f; // 40 ticks de vida

    if (progress < 0.3f)
        _currentRadius = MathHelper.Lerp(0f, 120f, progress / 0.3f);
    else
        _currentRadius = MathHelper.Lerp(120f, 0f, (progress - 0.3f) / 0.7f);

    // Dibujar anillos con Dust
    int dustCount = 24;
    for (int i = 0; i < dustCount; i++)
    {
        float angle = (MathHelper.TwoPi / dustCount) * i + LocalTimer * 0.05f;
        Vector2 dustPos = Projectile.Center + new Vector2(
            MathF.Cos(angle) * _currentRadius,
            MathF.Sin(angle) * _currentRadius * 0.4f // elipse, no círculo
        );
        Dust d = Dust.NewDustDirect(dustPos, 1, 1, DustID.Shadowflame);
        d.noGravity = true;
        d.velocity = Vector2.Zero;
        d.scale = 1.2f;
    }

    Lighting.AddLight(Projectile.Center, 1.2f * (1f - progress), 0.2f, 0.05f);
}
```

---

### 💡 IDEA 2 — TRAIL DE ENERGÍA CON SHADER PROPIO (técnicamente impresionante)

**Concepto:** En vez del trail de nebulosa que sigue la espada, el trail forma **líneas de energía** que conectan TODAS las espadas activas al cursor, como cables de luz tensos.

**Cómo hacerlo:**
- En cada `AmeFractalBlade.AI()`, usar `VertexStrip` para dibujar una línea desde la posición actual de la espada hasta `_targetCenter` (el cursor)
- La línea tiene opacidad baja (30-50%), color rojo-naranja, y se va desvaneciendo con la distancia
- El efecto es que mientras las 4-6 espadas vuelan hacia el cursor, hay **cables de luz** conectando cada espada al punto de convergencia

**Resultado visual:** Cuando atacas, ves múltiples "hilos de energía" que se acortan y colapsan, como si estuvieras jalando las espadas hacia el cursor con cuerdas de luz.

---

### 💡 IDEA 3 — EXPLOSIÓN DE PARTÍCULAS EN EL PUNTO DE CONVERGENCIA

**Concepto:** Cuando cada espada llega al cursor (al final de la fase 1), se genera un **burst de partículas** en ese punto exacto, que se acumulan con cada espada que llega.

**Cómo hacerlo:**

En `AmeFractalBlade`, detectar cuando la espada llega al centro:

```csharp
// Al final de la fase 1 (primer tick de fase 2)
float progress = LocalTimer / LIFETIME;
if (progress > CONVERGE_POINT && !_hasExploded)
{
    _hasExploded = true;
    // Burst de partículas en _targetCenter
    for (int i = 0; i < 20; i++)
    {
        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
        float speed = Main.rand.NextFloat(3f, 8f);
        Dust d = Dust.NewDustDirect(
            _targetCenter, 1, 1,
            DustID.Shadowflame, 0f, 0f, 0, default, 1.5f
        );
        d.velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
        d.noGravity = true;
    }
    // Flash de luz
    Lighting.AddLight(_targetCenter, 2.0f, 0.4f, 0.1f);
}
```

**Resultado visual:** Cada espada que aterriza genera una pequeña explosión, y cuando llegan todas juntas el centro del cursor **estalla en luz y partículas**.

---

### 💡 IDEA 4 — DEFORMACIÓN DE ESPACIO (la más única/innovadora)

**Concepto:** Al activar el modo, dibujar un **efecto de lente gravitacional** alrededor del cursor que distorsiona los píxeles del fondo. Las espadas parecen ser "succionadas" hacia el punto como si hubiera un agujero negro.

**Cómo hacerlo:**
- Requiere un **pixel shader** (`.fx`) — más avanzado pero muy único
- El shader toma el render target del mundo y desplaza los UVs radialmente hacia el centro del cursor
- Terraria usa `Main.graphics.GraphicsDevice.SetRenderTarget()` para esto

Si quieres hacerlo sin shader (más accesible), puedes simular el efecto con muchos `Dust` pequeños que espiralan hacia el cursor:

```csharp
// En AmeFractalRift o en el AmePlayer
if (hasAmeWeapon && isSwinging && mode == Melee1)
{
    // Crear 5-8 partículas por frame que espiralan hacia el cursor
    for (int i = 0; i < 6; i++)
    {
        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
        float dist = Main.rand.NextFloat(80f, 200f);
        Vector2 startPos = Main.MouseWorld + new Vector2(
            MathF.Cos(angle) * dist,
            MathF.Sin(angle) * dist
        );
        Dust d = Dust.NewDustDirect(startPos, 1, 1, DustID.Shadowflame);
        // Velocidad hacia el cursor
        d.velocity = (Main.MouseWorld - startPos).SafeNormalize(Vector2.Zero) * 6f;
        d.noGravity = true;
        d.fadeIn = 0.5f;
        d.scale = 0.8f;
    }
}
```

---

### 💡 IDEA 5 — CÍRCULO DE RUNAS (estéticamente único)

**Concepto:** Al activar el arma, aparece un **círculo mágico** alrededor del cursor (como los de anime) que rota y desde cuyos puntos emergen las espadas.

**Cómo hacerlo:**
- Crea un proyectil `AmeFractalCircle` que vive mientras dura el swing
- En `PreDraw`, dibuja arcos de circunferencia usando `Main.EntitySpriteDraw` de líneas cortas rotadas
- Las espadas aparecen exactamente en los puntos del círculo (puedes pasar la posición exacta en los ai[])

**Parámetros visuales del círculo:**
- Radio: 180px
- Rotación: 0.04 radianes/tick
- Color: rojo oscuro con brillo exterior tenue
- Subdivisiones: 8 puntos marcados con destellos

**Resultado visual:** Ves un pentagrama/círculo mágico en el cursor que gira, y desde sus vértices salen disparadas las espadas hacia el centro. Muy anime, muy único en Terraria.

---

## 🏆 MI RECOMENDACIÓN — Combinar Idea 1 + 3

Para el mayor impacto visual con la menor complejidad técnica, combina:

**Rift de impacto** (Idea 1) + **Burst en convergencia** (Idea 3)

El flujo visual quedaría:
1. Click → Aparece un rift/grieta en el cursor (elipses que expanden)
2. Las espadas emergen desde el borde del rift en dirección al centro
3. Cada espada vuela con trail de nebulosa (el que ya tienes en AmeBladeBase, adaptado)
4. Al llegar al centro → burst de partículas + flash
5. El rift colapsa mientras la última espada aterriza

Es una narrativa visual completa de 3 actos que tiene coherencia con la mecánica del arma.

---

## ⚠️ NOTAS IMPORTANTES — No tocar Melee2

Para garantizar que Melee2 no se vea afectado:

1. **`AmeFractalBlade` es una clase separada de `AmeBladeBase`** — no hay herencia entre ellas
2. **Las `AmeFractalBladeXX` son clases distintas de las `AmeBladeXX`** — Melee2 sigue usando `AmeBladeXX`
3. **El `case WeaponMode.Melee1` y `case WeaponMode.Melee2` son completamente independientes** en el switch de `Shoot()`
4. Los sprites se reutilizan apuntando con `override string Texture` — no se mueven ni modifican los PNGs originales

---

## 📋 CHECKLIST DE IMPLEMENTACIÓN

```
[ ] 1. Crear AmeFractalBlade.cs (clase base)
[ ] 2. Crear AmeFractalBlades.cs (las 19 subclases con Texture override)
[ ] 3. Modificar AmeWeapon.cs — case Melee1 en Shoot()
[ ] 4. Modificar AmeWeapon.cs — CanUseItem() para tiempos de Melee1
[ ] 5. Probar que Melee2 sigue funcionando intacto
[ ] 6. (Opcional) Crear AmeFractalRift.cs para el efecto de portal
[ ] 7. (Opcional) Añadir burst de partículas en convergencia
[ ] 8. Tunear LIFETIME, BASE_SPAWN_RADIUS, curveStrength a gusto
```

---

*Guía escrita para AmeMod — 2026*
