# Comandos Útiles para AmeMod

## 🛠️ Comandos de Desarrollo

### Testing Rápido en Terraria
Cuando estés en el juego con el mod cargado, abre la consola con `F10` o `/` y usa:

```
# Obtener el arma Ame directamente
/give Ame/AmeWeapon

# O con cantidad
/give Ame/AmeWeapon 1

# Si el comando no funciona, intenta:
/item Ame.AmeWeapon
```

### Comandos PowerShell para Build

```powershell
# Ir a la carpeta del mod
cd "C:\Users\Usuario\Documents\My Games\Terraria\tModLoader\ModSources\Ame"

# Ver estructura del proyecto
tree /F

# Buscar errores en archivos
Get-ChildItem -Recurse -Filter *.cs | Select-String "TODO|FIXME|HACK"

# Contar líneas de código
(Get-ChildItem -Recurse -Filter *.cs | Get-Content | Measure-Object -Line).Lines

# Abrir en VS Code
code .
```

### Comandos Útiles de tModLoader

```
# En el juego (presiona F10 o escribe / para abrir consola)

# Dar items
/give [ItemID] [cantidad]

# Modo godmode
/godmode

# Mata todos los enemigos
/butcher

# Invocar boss
/boss [BossID]

# Cambiar hora
/time [noon|midnight|dawn|dusk]

# Modo creativo
/creative
```

## 📝 Comandos Git (Si usas control de versiones)

```bash
# Inicializar repositorio
git init

# Agregar archivos
git add .

# Primer commit
git commit -m "Implementación inicial de AmeMod con sistema modular de armas"

# Crear .gitignore
echo "bin/
obj/
*.user
*.suo
.vs/" > .gitignore

# Conectar con GitHub
git remote add origin https://github.com/tu-usuario/AmeMod.git
git push -u origin main
```

## 🔍 Debugging

### Ver logs de tModLoader
```powershell
# Windows
notepad "$env:USERPROFILE\Documents\My Games\Terraria\tModLoader\Logs\client.log"

# O usar PowerShell
Get-Content "$env:USERPROFILE\Documents\My Games\Terraria\tModLoader\Logs\client.log" -Tail 50
```

### Limpiar build cache
```powershell
# Eliminar bin y obj
Remove-Item -Recurse -Force ".\bin", ".\obj"
```

## 🎨 Comandos para Assets

### Crear sprites placeholder con PowerShell
```powershell
# Crear un sprite simple de prueba (requiere ImageMagick o similar)
# Alternativamente, usa Paint.NET o similar manualmente

# Estructura de carpetas ya creada en Assets/
```

## 📦 Publicación

### Generar .tmod file
El archivo .tmod se genera automáticamente en:
```
C:\Users\Usuario\Documents\My Games\Terraria\tModLoader\Mods\Ame.tmod
```

### Subir a Workshop (Steam)
1. Abre tModLoader
2. Ve a Workshop > Manage Mods
3. Selecciona tu mod
4. Click "Publish"

## ⚡ Atajos del Editor

### Visual Studio Code
- `Ctrl + Shift + B` - Build
- `F5` - Debug
- `Ctrl + P` - Buscar archivo
- `Ctrl + Shift + F` - Buscar en todos los archivos
- `F12` - Ir a definición

### tModLoader
- `F10` - Abrir consola
- `F11` - Fullscreen
- `F12` - Screenshot

## 🧪 Testing Checklist

Cuando pruebes el mod, verifica:

- [ ] El mod carga sin errores
- [ ] El arma se puede craftear/obtener
- [ ] Todos los 5 modos funcionan
- [ ] El cambio de modo muestra el mensaje correcto
- [ ] Los proyectiles aparecen y causan daño
- [ ] El minion persiste y ataca
- [ ] El clon se comporta correctamente
- [ ] Los efectos de polvo aparecen
- [ ] No hay crashes al cambiar modos rápidamente
- [ ] El arma persiste después de reload

## 📊 Estadísticas del Proyecto

```powershell
# Ver estadísticas
Get-ChildItem -Recurse -Filter *.cs | 
    Measure-Object -Property Length -Sum | 
    Select-Object Count, @{Name="TotalKB";Expression={$_.Sum / 1KB}}

# Clases implementadas
Get-ChildItem -Recurse -Filter *.cs | Select-String "public class" | Measure-Object

# Métodos override
Get-ChildItem -Recurse -Filter *.cs | Select-String "public override" | Measure-Object
```

## 🔧 Troubleshooting Rápido

### Mod no aparece en lista
```powershell
# Verificar que existe build.txt
Test-Path ".\build.txt"

# Verificar contenido
Get-Content ".\build.txt"
```

### Errores de compilación
```powershell
# Ver errores recientes
Get-Content "$env:USERPROFILE\Documents\My Games\Terraria\tModLoader\Logs\client.log" | 
    Select-String "Error|Exception" -Context 2,2
```

### Reload rápido
1. Edita el código
2. Guarda (Ctrl+S)
3. En tModLoader: Workshop > Develop Mods > Build + Reload
4. O usa: `/reload` en consola

---

**Tip:** Guarda este archivo como referencia rápida mientras desarrollas! 🚀
