# Mendikat TrailLab

Aplicación de escritorio Windows para abrir, visualizar, inspeccionar, editar y guardar archivos **GPX** (tracks, rutas
y waypoints) sobre cartografía online (IGN España por defecto).

> **Estado: versión 0.0.2. Los 8 incrementos del plan están terminados; después se han añadido mejoras pedidas por el responsable
> (ver «Después del plan» en [specs/ROADMAP.md](specs/ROADMAP.md)).**
> Se abren archivos GPX, y KML, KMZ y TCX convertidos a GPX (WPT se retiró el 2026-10-26), se dibujan sobre 8 cartografías (IGN por defecto), se pueden seleccionar, borrar, insertar,
> mover y editar puntos con deshacer/rehacer, y se guardan de forma segura. Los waypoints del GPX se ven en el mapa con su nombre.
> También muestra el perfil de elevación sincronizado con el mapa, y permite simplificar tracks, regenerar sus tiempos y ajustar sus altitudes con el MDT del IGN.
> Los archivos grandes se abren con porcentaje de progreso y se pueden cancelar. Ver [specs/ROADMAP.md](specs/ROADMAP.md).

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
| Invertir el sentido | *Herramientas › Invertir track*: invierte puntos y segmentos del track o ruta seleccionado; las horas se reflejan (mismo inicio y fin, mismos intervalos). Se deshace con Ctrl+Z |
| Ajustar altitudes con el MDT | *Herramientas › Ajustar altitudes con el MDT del IGN…* › **Consultar** (5 m o 25 m; requiere Internet) › revisar cuántos puntos cambian y cuántos no tienen dato › **Aplicar**. Sólo España; el origen de cada elevación se ve en el detalle del punto |
| Abrir KML, KMZ o TCX | *Archivo › Abrir archivo…* (Ctrl+O): se abre como un GPX nuevo «nombre.gpx» sin guardar; **Guardar** pide dónde guardarlo |
| Convertir varios archivos a GPX | *Herramientas › Convertir a GPX…*: elegir los KML, KMZ o TCX, la carpeta de destino (por defecto la de cada archivo) y qué hacer si el GPX ya existe › **Convertir**; informe por archivo |
| Cancelar la apertura de un archivo grande | Botón **Cancelar** de la barra de estado, o **Esc** |
| Usar sólo el teclado | **Alt** + letra subrayada abre los menús; **Tab** recorre paneles y campos; flechas en las listas |
| Ayuda | **F1** o *Ayuda › Ayuda de Mendikat TrailLab* (guía HTML en el navegador). En el mismo menú: ¡Dona ahora! (PayPal), web de Mendikat, Buscar actualizaciones y Acerca de (versión, autor) |

## Documentación (Spec-Driven Development)

| Documento | Contenido |
|-----------|-----------|
| [specs/PROJECT.md](specs/PROJECT.md) | Visión, objetivos, alcance, glosario y decisiones |
| [specs/REQUIREMENTS.md](specs/REQUIREMENTS.md) | Requisitos con identificador y criterios de aceptación |
| [specs/ACCEPTANCE.md](specs/ACCEPTANCE.md) | Escenarios Given/When/Then |
| [specs/ROADMAP.md](specs/ROADMAP.md) | Incrementos, dependencias y definición de terminado |
| [specs/TRACEABILITY.md](specs/TRACEABILITY.md) | Requisito → código → prueba |
| [specs/DECISIONS/](specs/DECISIONS/) | ADR: plataforma, UI, coordenadas, cartografía, GPX, DEM, algoritmos, estructura (WPT retirado) |

## Requisitos

- Windows 10 versión 2004 o posterior / Windows 11, x64 (exigido por SkiaSharp, usado por el mapa; ADR-0009).
- Desarrollo: SDK de .NET 10 (`global.json` fija 10.0.402).
- Ejecución: [runtime de escritorio .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) (x64), instalado una vez.
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

## Distribuir

La aplicación se distribuye dependiente del runtime (ADR-0001): cada usuario instala una vez el runtime de escritorio
.NET 10 (x64) y recibe una carpeta de unos 48 MB.

```powershell
dotnet publish src\Mendikat.TrailLab.App -p:PublishProfile=win-x64   # genera artifacts\publish\win-x64\
```

Se comprime esa carpeta y el usuario ejecuta `Mendikat.TrailLab.exe`. Si falta el runtime, Windows muestra un aviso con el
enlace de descarga.

## Arquitectura

WPF + MVVM sin framework externo, documentos en pestañas con un mapa compartido (ADR-0002, ADR-0009).

| Proyecto | Contenido |
|----------|-----------|
| `src/Mendikat.TrailLab.Domain` | Modelo: documentos, tracks, segmentos, puntos, waypoints. Sin dependencias de UI |
| `src/Mendikat.TrailLab.Gpx` | Lector y escritor GPX 1.0/1.1 |
| `src/Mendikat.TrailLab.Mapping` | Catálogo de cartografías, descarga de mosaicos, capas del mapa, formato UTM/DMS |
| `src/Mendikat.TrailLab.App` | WPF: ventana, ViewModels, `MapController`, diálogos, configuración, logging, textos en `Resources/Strings.resx` |
| `tests/Mendikat.TrailLab.Tests` | xUnit v3 con fixtures propios en `Fixtures/`; sin acceso a la red |

## Formatos admitidos

- **GPX 1.1** y **1.0**: lectura y escritura. Se guarda siempre GPX 1.1 válido (un GPX 1.0 se convierte, con aviso)
  conservando metadatos, campos desconocidos y extensiones con sus prefijos. Horas sin zona → UTC con aviso.
- Los waypoints (`wpt`) del GPX se dibujan como triángulos del color del documento, con su nombre al acercarse.
- **KML** y **KMZ** (Google Earth) y **TCX** (Garmin): sólo lectura. Se abren como un GPX nuevo sin guardar
  («ruta.kml» → «ruta.gpx»; el original no se modifica) o se convierten por lotes con *Herramientas › Convertir a GPX…*.
  El formato se reconoce por el contenido, no por la extensión.
  - KML: cada punto → waypoint (nombre, descripción, altitud si la tiene); las líneas (`LineString`, `gx:Track` con horas)
    de cada marca → un track con un segmento por línea; se recorren todas las carpetas. Polígonos, superposiciones y
    modelos no tienen equivalente en GPX: se cuentan en un aviso.
  - KMZ: se lee `doc.kml` (o el primer `.kml`); las imágenes se ignoran. Máximo 256 MB descomprimido.
  - TCX: cada actividad → un track con un segmento por vuelta; pulso y cadencia → extensión `gpxtpx` de Garmin
    (la que leen Strava, Garmin y la mayoría de programas). Distancia, velocidad y potencia no se convierten (aviso).
- Los archivos **WPT** (OziExplorer, CompeGPS) ya no se admiten (ADR-0006, retirada el 2026-10-26).

## Cartografía

Predeterminada: Mapa IGN (España). Disponibles: Mapa IGN (España), CyclOSM, OpenTopoMap, OpenStreetMap,
Mapa satélite (ESRI), Mapa Topográfico (ESRI), Géoportail IGN (France, SCAN 25) y MtbMap. Ninguna requiere clave personal (Géoportail usa la clave pública del IGN).
La elegida se recuerda entre sesiones. Detalles, atribuciones y condiciones en
[ADR-0004](specs/DECISIONS/ADR-0004-map-providers.md).

## Coordenadas

Barra de estado: UTM WGS84 `30T 575700 4760048` (zona y banda, Este, Norte en metros) y WGS84 en grados,
minutos y segundos `42º 59' 21,60'' N 2º 04' 17,11'' W`. Cálculo con GeographicLib (Karney), con las zonas
especiales de Noruega y Svalbard; fuera de UTM se muestra UPS ([ADR-0003](specs/DECISIONS/ADR-0003-coordinates-geodesy.md)).
También muestra el datum del archivo activo (WGS84 en GPX, KML y TCX) y, al detener el puntero, la altitud bajo él:
la del punto del archivo si hay uno con elevación (`Alt. 1.234 m (archivo)`) o, si no, la del terreno según el MDT
del IGN (`Alt. 846 m (MDT IGN 5 m)`, sólo España y con conexión; sin dato, «—»).

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
  si algo cambiará en el archivo (GPX 1.0 → 1.1). Los números se escriben con su valor exacto, pero su texto puede cambiar (-2.0610 → -2.061).
- El perfil mide la distancia horizontal y, por defecto, no cuenta el hueco entre segmentos (casilla para incluirlo).
- Tras cada edición se redibuja el documento completo en el mapa: unos 0,2 s con 100 000 puntos y casi 1 s con 500 000.
  Evitarlo exige actualizar el mapa por partes (rediseño de las capas); queda fuera de este plan.
- Con muchos waypoints juntos, sus nombres se solapan en el mapa al acercarse.
- MDT del IGN: sólo España. El servicio no marca «sin dato» (devuelve 0 en el mar y fuera de España), así que las celdas a 0,000 m se tratan como sin dato y esos puntos no cambian. No se aplica ninguna corrección geoidal (ADR-0007).
- Un GPX de 50 MB (500 000 puntos) tarda unos 5 s en abrirse y ocupa unos 190 MB de memoria. Guardarlo prepara el
  archivo en la ventana principal (~2 s sin respuesta); la verificación y la escritura van en segundo plano. Guardar no se puede cancelar.
- El lector de pantalla no anuncia por sí solo los cambios de la barra de estado: hay que ir a ella para leerlos.
- El comportamiento sin conexión (MAP-07) está probado con red simulada; falta la comprobación manual sin red.
- Las pruebas de interfaz crean las ventanas reales sin mostrarlas (enlaces, nombres accesibles, cierre); el uso con ratón
  y el dibujo del mapa se comprueban a mano.
- Condiciones de uso de Esri, OSM y del SCAN 25 del IGN francés a revisar antes de distribuir públicamente.
- MtbMap y CyclOSM son servidores comunitarios sin garantía de disponibilidad.
