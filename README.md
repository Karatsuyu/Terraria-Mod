# AmeMod - Terraria tModLoader

## Descripción
AmeMod es un mod profesional para Terraria que implementa un sistema de armas modulares que trascienden las clases tradicionales del juego.

## Características Principales

### 🗡️ Arma Ame - Sistema Modular
Un arma única con 5 modos diferentes que se pueden cambiar en tiempo real:

1. **Modo Melee** - Corte cuerpo a cuerpo con alto daño
2. **Modo Magic** - Proyectiles mágicos con homing
3. **Modo Ranged** - Disparos de largo alcance con efecto de veneno
4. **Modo Summon** - Invoca un minion que ataca enemigos
5. **Modo Clone** - Crea un clon que ataca automáticamente

### 🎮 Controles
- **Click Izquierdo**: Atacar con el modo actual
- **Click Derecho**: Cambiar entre modos

## Estructura del Proyecto

```
Ame/
├── Ame.cs                      # Clase principal del mod
├── build.txt                   # Configuración del mod
│
├── Systems/
│   └── ModeSystem.cs          # Sistema global de modos
│
├── Items/
│   └── AmeWeapon.cs           # Arma principal con sistema de modos
│
├── Projectiles/
│   └── Modes/
│       ├── AmeMeleeSlash.cs   # Proyectil melee
│       ├── AmeMagicBlast.cs   # Proyectil mágico
│       ├── AmeRangedShot.cs   # Proyectil ranged
│       ├── AmeSummonMinion.cs # Minion invocable
│       └── AmeClone.cs        # Clon del jugador
│
├── Players/
│   └── AmePlayer.cs           # Modificaciones del jugador
│
├── Buffs/
│   └── AmeMinionBuff.cs       # Buff del minion
│
├── Particles/
│   └── (sistema futuro)
│
└── Assets/
    ├── Items/                 # Sprites de items
    ├── Projectiles/          # Sprites de proyectiles
    └── Sounds/               # Efectos de sonido
```

## Sistema de Modos

Cada modo tiene características únicas:

| Modo | Clase | Daño Base | Consumo | Características |
|------|-------|-----------|---------|-----------------|
| Melee | Melee | 100 | Ninguno | Corte rotatorio, penetración infinita |
| Magic | Magic | 80 | 10 Mana | Homing hacia enemigos, rebota en tiles |
| Ranged | Ranged | 70 | Ninguno | Gravedad, aplica veneno |
| Summon | Summon | 60 | 1 Slot | Ataca automáticamente, orbita al jugador |
| Clone | Generic | 90 | Ninguno | Clon independiente que ataca enemigos |

## Requisitos
- Terraria 1.4.4+
- tModLoader v2023.8+
- .NET 8.0

## Instalación para Desarrollo

1. Clona el repositorio en tu carpeta de ModSources:
```
C:\Users\[Usuario]\Documents\My Games\Terraria\tModLoader\ModSources\Ame
```

2. Abre tModLoader y ve a Workshop > Develop Mods

3. Encuentra "AmeMod" en la lista y presiona "Build + Reload"

## Próximas Características

- [ ] Sistema de partículas personalizado
- [ ] Sprites personalizados para cada modo
- [ ] Efectos de sonido únicos
- [ ] Más modos de ataque
- [ ] Sistema de upgrades
- [ ] Recetas de crafteo

## Contribuir
Las contribuciones son bienvenidas. Por favor, abre un issue antes de hacer cambios mayores.

## Licencia
Este mod es de código abierto para fines educativos.

## Autor
Ame - 2026
