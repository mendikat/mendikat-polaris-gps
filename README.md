# Mendikat TrailLab

Aplicación de escritorio Windows para abrir, visualizar, inspeccionar, editar y guardar tracks **GPX**
y archivos de waypoints **WPT** sobre cartografía online (IGN España por defecto).

> **Estado: Incremento 7 (ajuste de altitudes con el MDT del IGN) terminado, pendiente de aprobación.**
> Se abren GPX y WPT, se dibujan sobre 8 cartografías (IGN por defecto), se pueden seleccionar, borrar, insertar,
> mover y editar puntos con deshacer/rehacer, y se guardan en el mismo formato de forma segura.
> También muestra el perfil de elevación sincronizado con el mapa permite simplificar tracks, regenerar sus tiempos y ajustar sus altitudes con el MDT del IGN. Queda el incremento 8 (robustez y cierre). Ver [specs/ROADMAP.md](specs/ROADMAP.md).

## Uso rápido

| Acción | Cómo |
|--------|------|
| Ver los puntos de un track, ruta o waypoints | Seleccionarlo en **Capas** (panel izquierdo); la casilla lo muestra u oculta en el mapa |
| Seleccionar un punto | Clic en la lista o en el mapa (cerca del punto); Ctrl/Mayús + clic en la lista para varios |
| Eliminar | **Supr** o clic derecho › Eliminar. Sin puntos seleccionados, elimina el track o ruta (con confirmación) |
| Insertar un punto intermedio | **Insert**: punto medio entre el seleccionado y el siguiente |
| Mover un punto | Seleccionarlo y arrastrarlo en el mapa, o cambiar latitud/longitud en el detalle y pulsar **Aplicar** (Intro) |
| Editar nombre, descripción, elevación, hora | Panel «Punto seleccionado» › **Aplicar**; **Descartar** (Esc) deshace lo escrito |
| Deshacer / rehacer | **Ctrl+Z** / **Ctrl+Y** (menú Edición) |
| Perfil de elevación | Seleccionar un track o ruta en **Capas** › botón **Perfil** (o Ver › Perfil de elevación). Muestra distancia, desnivel positivo y negativo, elevación mínima y máxima, y tiempo total, en movimiento y detenido. Puntero sobre el perfil: marca el punto en el mapa; clic: lo selecciona; ← →: punto anterior / siguiente |
| Simplificar un track o ruta | Seleccionarlo en **Capas** › *Herramientas › Simplificar track…*; tolerancia en metros con vista previa del número de puntos |
| Regenerar tiempos | *Herramientas › Regenerar tiempos…*: fecha, hora y zona horaria de inicio, y velocidad (km/h, m/s, min/km) o intervalo fijo; vista previa en hora local y UTC |
| Ajustar altitudes con el MDT | *Herramientas › Ajustar altitudes con el MDT del IGN…* › **Consultar** (5 m o 25 m; requiere Internet) › revisar cuántos puntos cambian y cuántos no tienen dato › **Aplicar**. Sólo España; el origen de cada elevación se ve en el detalle del punto |

## Documentación (Spec-Driven Development)

| Documento | Contenido |
|-----------|-----------|
| [specs/PROJECT.md](specs/PROJECT.md) | Visión, objetivos, alcance, glosario y decisiones |
| [specs/REQUIREMENTS.md](specs/REQUIREMENTS.md) | Requisitos con identificador y criterios de aceptación |
| [specs/ACCEPTANCE.md](specs/ACCEPTANCE.md) | Escenarios Given/When/Then |
| [specs/ROADMAP.md](specs/ROADMAP.md) | Incrementos, dependencias y definición de terminado |
| [specs/TRACEABILITY.md](specs/TRACEABILITY.md) | Requisito → código → prueba |
| [specs/DECISIONS/](specs/DECISIONS/) | ADR: plataforma, UI, coordenadas, cartografía, GPX, WPT, DEM, algoritmos, estructura |

## Requisitos

- Windows 10 versión 2004 o posterior / Windows 11, x64 (exigido por SkiaSharp, usado por el mapa; ADR-0009).
- Desarrollo: SDK de .NET 10 (`global.json` fija 10.0.402).
- Ejecución: runtime de escritorio .NET 10 (o publicación autocontenida, a decidir en el incremento 8).
- Internet sólo para la cartografía: abrir y ver archivos funciona sin conexión.

## Compilar, probar y ejecutar

Desde la carpeta del repositorio:

```powershell
dotnet build                                   # compila la solución (advertencias = errores)
dotnet test                                    # ejecuta las pruebas (Microsoft.Testing.Platform)
dotnet run --project src\Mendikat.TrailLab.App  # compila y arranca la aplicación
```

`dotnet run` sin `--project` no funciona en la raíz, porque allí está la solución y no el proyecto de la aplicación.
Tras compilar, también se puede abrir directamente
`src\Mendikat.TrailLab.App\bin\Debug\net10.0-windows10.0.19041.0\Mendikat.TrailLab.exe`.

Archivos de usuario:
- Configuración: `%APPDATA%\Mendikat\TrailLab\settings.json` (cartografía elegida).
- Registro técnico: `%LOCALAPPDATA%\Mendikat\TrailLab\logs\traillab-AAAAMMDD.log` (se conservan los 10 últimos;
  la carpeta del perfil de usuario se sustituye por `%USERPROFILE%`).

## Arquitectura

WPF + MVVM sin framework externo, documentos en pestañas con un mapa compartido (ADR-0002, ADR-0009).

| Proyecto | Contenido |
|----------|-----------|
| `src/Mendikat.TrailLab.Domain` | Modelo: documentos, tracks, segmentos, puntos, waypoints. Sin dependencias de UI |
| `src/Mendikat.TrailLab.Gpx` | Lectores GPX 1.0/1.1 y WPT (OziExplorer, CompeGPS/TwoNav) |
| `src/Mendikat.TrailLab.Mapping` | Catálogo de cartografías, descarga de mosaicos, capas del mapa, formato UTM/DMS |
| `src/Mendikat.TrailLab.App` | WPF: ventana, ViewModels, `MapController`, diálogos, configuración, logging, textos en `Resources/Strings.resx` |
| `tests/Mendikat.TrailLab.Tests` | xUnit v3 con fixtures propios en `Fixtures/`; sin acceso a la red |

## Formatos admitidos

- **GPX 1.1** y **1.0**: lectura y escritura. Se guarda siempre GPX 1.1 válido (un GPX 1.0 se convierte, con aviso)
  conservando metadatos, campos desconocidos y extensiones con sus prefijos. Horas sin zona → UTC con aviso.
- **WPT OziExplorer v1.1** y **CompeGPS/TwoNav**: lectura y escritura (las líneas no editadas se copian tal cual), sólo datum WGS 84; CompeGPS sólo en latitud/longitud.
  Codificación ANSI (Windows-1252) o UTF-8 detectada automáticamente. Detalles y casos especiales en
  [ADR-0006](specs/DECISIONS/ADR-0006-wpt-format.md).

## Cartografía

Predeterminada: Mapa IGN (España). Disponibles: Mapa IGN (España), CyclOSM, OpenTopoMap, OpenStreetMap,
Mapa satélite (ESRI), Mapa Topográfico (ESRI), Géoportail IGN (France) y MtbMap. Ninguna requiere clave API.
La elegida se recuerda entre sesiones. Detalles, atribuciones y condiciones en
[ADR-0004](specs/DECISIONS/ADR-0004-map-providers.md).

## Coordenadas

Barra de estado: UTM WGS84 `30T 575700 4760048` (zona y banda, Este, Norte en metros) y WGS84 en grados,
minutos y segundos `42º 59' 21,60'' N 2º 04' 17,11'' W`. Cálculo con GeographicLib (Karney), con las zonas
especiales de Noruega y Svalbard; fuera de UTM se muestra UPS ([ADR-0003](specs/DECISIONS/ADR-0003-coordinates-geodesy.md)).

## Dependencias y licencias

| Paquete | Licencia |
|---------|----------|
| Microsoft.Extensions.Logging 10.0.13 | MIT |
| GeographicLib.NET 2.3.2 | MIT |
| Mapsui, Mapsui.Tiling, Mapsui.Nts, Mapsui.Wpf 5.1.0 | MIT |
| ↳ BruTile 6.0 · NetTopologySuite 2.6 · SkiaSharp 3.119 · HarfBuzzSharp · Svg.Skia · Topten.RichTextKit | Apache-2.0 · BSD-3 · MIT · MIT · MIT · Apache-2.0 |
| xunit.v3 4.0.2 (sólo pruebas) | Apache-2.0 |

Datos: mosaicos y MDT del IGN España bajo CC BY 4.0 (scne.es); datos OSM bajo ODbL. Servicio de elevaciones: WCS «Modelos Digitales del Terreno de España» (servicios.idee.es/wcs-inspire/mdt).

## Limitaciones conocidas

- Guardar escribe primero un archivo temporal, lo verifica y sólo entonces sustituye el original. Al guardar se avisa
  si algo cambiará en el archivo (GPX 1.0 → 1.1, caracteres que la codificación del WPT no admite, waypoints
  CompeGPS sin altitud). Los números se escriben con su valor exacto, pero su texto puede cambiar (-2.0610 → -2.061).
- El perfil mide la distancia horizontal y, por defecto, no cuenta el hueco entre segmentos (casilla para incluirlo).
- Tras cada edición se redibuja el documento completo en el mapa; con tracks de cientos de miles de puntos puede notarse (se revisará en el Inc 8).
- Los textos de OziExplorer no admiten comas y los nombres de CompeGPS no admiten espacios (limitaciones de esos formatos).
- Los WPT CompeGPS en coordenadas UTM (`U 0`) se rechazan; no se convierten datums distintos de WGS 84.
- OziExplorer: el carácter 209 se muestra como «Ñ» (no se convierte en coma; ADR-0006).
- MDT del IGN: sólo España. El servicio no marca «sin dato» (devuelve 0 en el mar y fuera de España), así que las celdas a 0,000 m se tratan como sin dato y esos puntos no cambian. No se aplica ninguna corrección geoidal (ADR-0007).
- Abrir un archivo muestra progreso indeterminado; no se puede cancelar todavía (Inc 8).
- El comportamiento sin conexión (MAP-07) está probado con red simulada; falta la comprobación manual sin red.
- Sin pruebas automatizadas de interfaz; la ventana se comprueba manualmente.
- Condiciones de uso de Esri y OSM a revisar antes de distribuir públicamente.
- MtbMap y CyclOSM son servidores comunitarios sin garantía de disponibilidad.
