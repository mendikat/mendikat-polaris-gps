# ADR-0001 — Plataforma: C# / .NET 10 LTS

- Estado: **Aceptada** (D1: el responsable instalará el SDK oficial de .NET 10)
- Fecha: 2026-10-24

## Contexto

El documento base pide C# sobre .NET 10 LTS salvo justificación. Inspección del entorno (2026-10-24):

- Windows 11 Pro 10.0.26300, x64.
- `dotnet` instalado en `C:\Program Files\dotnet` **sólo con runtimes** `Microsoft.NETCore.App 9.0.21` y
  `Microsoft.WindowsDesktop.App 9.0.21`. **No hay ningún SDK** instalado: no se puede compilar.
- git 2.48.1 disponible. El directorio del proyecto no es un repositorio git.
- Acceso a nuget.org y a los servidores de mosaicos verificado.

## Decisión

Usar **.NET 10 LTS** (`net10.0-windows`) como indica el documento base. No hay motivo para bajar a
.NET 9 (STS, fin de soporte anterior a .NET 10).

## Acción pendiente (D1)

Instalar el SDK de .NET 10. Opciones:

1. **Instalador oficial** del SDK .NET 10 x64 desde dotnet.microsoft.com (requiere permisos de administrador). *Recomendada.*
2. `dotnet-install.ps1 -Channel 10.0` en el perfil de usuario (`%LOCALAPPDATA%\Microsoft\dotnet`), sin administrador; obliga a ajustar `PATH`/`DOTNET_ROOT`.

Se fijará la versión con `global.json` (`rollForward: latestFeature`).

## Resolución (2026-10-24)

El responsable proporcionó el SDK **10.0.402** (runtimes .NET 10.0.13, incluido WindowsDesktop) descomprimido en
`C:\Program Files\dotnet\dotnet-sdk-10.0.402-win-x64\`. Después lo copió (con permisos de administrador) dentro de
la ubicación estándar `C:\Program Files\dotnet\`, junto a los runtimes 9.0, como haría el instalador oficial.
Así `dotnet` funciona desde cualquier terminal sin cambiar el `PATH`, y los `.exe` encuentran el runtime 10.
No se antepuso la carpeta del SDK al `PATH` del sistema: las aplicaciones .NET 9 lanzadas con `dotnet`
dejarían de encontrar su runtime. `global.json` fija 10.0.402.

## Consecuencias

- Distribución (decidida por el responsable en el incremento 8, 2026-10-26): **dependiente del runtime**. El usuario
  instala una vez el runtime de escritorio .NET 10 (x64) y recibe una carpeta de unos 48 MB con la aplicación y sus
  bibliotecas (`dotnet publish srcMendikat.TrailLab.App -p:PublishProfile=win-x64` → `artifactspublishwin-x64`).
  Se descartó la publicación autocontenida (~150 MB por copia) porque el runtime se instala una sola vez. Los símbolos
  nativos de SkiaSharp y HarfBuzz (~100 MB) no se publican; los `.pdb` propios sí, para tener números de línea en el registro.
