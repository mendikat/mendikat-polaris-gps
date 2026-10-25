# ADR-0009 — Estructura de la solución, dependencias y guardado seguro

- Estado: **Aceptada** (revisada en el incremento 2)
- Fecha: 2026-10-24

## Estructura

```
Mendikat.TrailLab.slnx         (formato de solución XML, predeterminado en .NET 10)
global.json                    (fija SDK 10.0.402 y el ejecutor Microsoft.Testing.Platform)
Directory.Build.props          (nullable, advertencias como errores, LangVersion)
src/
  Mendikat.TrailLab.Domain/    net10.0                       Modelo (documentos, tracks, puntos, waypoints). Sin dependencias externas por ahora.
  Mendikat.TrailLab.Gpx/       net10.0                       Lectura (Inc 2) y escritura (Inc 4) de GPX y WPT (IWaypointFileFormat). Depende de Domain.
  Mendikat.TrailLab.Mapping/   net10.0                       Catálogo de proveedores, mosaicos, capas Mapsui, formateo UTM/DMS. Depende de Domain, Mapsui, GeographicLib.
  Mendikat.TrailLab.App/       net10.0-windows10.0.19041.0   WPF, ViewModels, MapController, settings, logging. Depende de todos.
tests/
  Mendikat.TrailLab.Tests/     net10.0-windows10.0.19041.0   xUnit v3; fixtures; pruebas de arquitectura.
```

- **Incremento 1** creó sólo `Domain`, `App` y `Tests`; `Gpx` y `Mapping` se crearon en el incremento 2, cuando
  tuvieron contenido (no se crean proyectos vacíos). El almacén provisional del incremento 1 se sustituyó por
  `App/Services/FileDocumentStore`, que usa los lectores reales.
- Textos de UI en `App/Resources/Strings.resx`; la clase tipada `Strings` se genera en `obj/` durante la
  compilación. Un target del `.csproj` la genera antes de la compilación XAML de WPF (que usa un proyecto temporal).
- El nombre de proyecto `Gpx` alberga también WPT (ambos son formatos de archivo); renombrar a `Formats` se valorará si crece. No se crea proyecto DEM hasta el incremento 7 y sólo si se justifica.
- `Mapping` no necesita WPF: el control `MapControl` y `MapController` (sincroniza documentos y mapa) están en `App`.
- Reglas de dependencia: Domain no depende de nada de UI ni XML; App es el único que conoce WPF.

**Plataforma de destino**: `App` y `Tests` usan `net10.0-windows10.0.19041.0` (Windows 10 versión 2004 o
posterior) porque `SkiaSharp.Views.WPF`, dependencia de Mapsui.Wpf, sólo publica esa variante; con
`net10.0-windows` NuGet restauraba la versión de .NET Framework (aviso NU1701).

## Dependencias NuGet

| Paquete | Versión | Licencia | Uso | Proyecto | Desde |
|---------|---------|----------|-----|----------|-------|
| Microsoft.Extensions.Logging | 10.0.13 | MIT | Abstracción de logging estándar; proveedor de archivo propio (`FileLoggerProvider`), sin Serilog | App | Inc 1 |
| xunit.v3 | 4.0.2 | Apache-2.0 | Pruebas (ejecutor Microsoft.Testing.Platform, sin VSTest ni `Microsoft.NET.Test.Sdk`) | Tests | Inc 1 |
| GeographicLib.NET | 2.3.2 | MIT | UTM/UPS de la barra de estado; geodésicas y TM local a partir del Inc 3 (pasará también a Domain) | Mapping | Inc 2 |
| Mapsui, Mapsui.Tiling, Mapsui.Nts | 5.1.0 | MIT | Mapa, capas de mosaicos y líneas (arrastran BruTile 6.0 Apache-2.0, NetTopologySuite 2.6 BSD-3) | Mapping | Inc 2 |
| Mapsui.Wpf | 5.1.0 | MIT | Control WPF del mapa (arrastra SkiaSharp 3.119 MIT, HarfBuzzSharp MIT, Svg.Skia MIT, Topten.RichTextKit Apache-2.0) | App | Inc 2 |

Incluidos en .NET, sin paquete: `System.Text.Json` (settings), `System.Xml.Linq` (GPX),
`CodePagesEncodingProvider` (Windows-1252 de los WPT).

## Guardado seguro (SAVE-03)

1. Serializar a `<nombre>.<guid>.tmp` en la misma carpeta (mismo volumen).
2. Releer el temporal con el lector correspondiente y validar (recuentos de elementos).
3. `File.Replace(tmp, destino, backup)`; si el destino no existe, `File.Move`.
4. Borrar la copia de respaldo al terminar; ante cualquier fallo, borrar el temporal y dejar el original intacto, informando al usuario.

**Implementación (incremento 4)**: `Gpx/SafeFileWriter` (temporal `<nombre>.<id>.tmp` con `WriteThrough` y
`Flush(true)`, `File.Replace` con respaldo temporal o `File.Move`) y `Gpx/TrailFileWriter.Verify`, que relee los
bytes generados y compara la estructura y todas las posiciones con una copia tomada al empezar a guardar.
La serialización se hace en el hilo de la interfaz (el documento no puede cambiar a medias) y la verificación y
la escritura en segundo plano; mientras se guarda, los comandos de edición están deshabilitados.
