# Polaris GPS

Aplicación de escritorio Windows para abrir, visualizar, inspeccionar, editar y guardar archivos **GPX** (tracks, rutas
y waypoints) sobre cartografía online (IGN España por defecto).

> **Estado: versión 0.5.0.**
> Se abren archivos GPX, y KML, KMZ, TCX, OziExplorer (PLT, WPT) y CompeGPS/TwoNav (TRK, WPT) convertidos a GPX, se dibujan sobre 8 cartografías (IGN por defecto), se pueden seleccionar, borrar, insertar,
> mover y editar puntos con deshacer/rehacer, y se guardan de forma segura. Los waypoints del GPX se ven en el mapa con su nombre.
> También muestra el perfil de elevación sincronizado con el mapa, y permite simplificar tracks, regenerar sus tiempos y ajustar sus altitudes con el MDT del IGN.
> Los archivos grandes se abren con porcentaje de progreso y se pueden cancelar.

## Uso rápido

| Acción | Cómo |
|--------|------|
| Ver los puntos de un track, ruta o waypoints | Seleccionarlo en **Capas** (panel izquierdo); la casilla lo muestra u oculta en el mapa. Un track en varias partes lista sus segmentos: clic selecciona sus puntos y ✕ elimina ese segmento (con confirmación; se deshace) |
| Seleccionar un punto | Clic en la lista o en el mapa (cerca del punto); Ctrl/Mayús + clic en la lista para varios |
| Eliminar | **Supr**. Sin puntos seleccionados, elimina el track o ruta (con confirmación) |
| Insertar un punto intermedio | **Insert**: punto medio entre el seleccionado y el siguiente |
| Menú contextual | Con un punto de un track seleccionado, clic derecho en el mapa: **Insertar punto** (en el sitio del clic, como Ctrl+clic; sobre un punto, el punto medio tras él), **Eliminar punto**, las herramientas del menú *Herramientas* y **Añadir waypoint** |
| Waypoints | Clic derecho en el mapa › **Añadir waypoint…** (nombre, comentario, descripción y **símbolo**: cima, collado, aparcamiento, refugio, fuente…, guardado como `sym` de Garmin y dibujado con su icono). Clic derecho sobre un waypoint › **Editar waypoint…** o **Eliminar waypoint**; **Supr** con el waypoint seleccionado también lo elimina, siempre con confirmación. Con la capa de waypoints seleccionada, la casilla sobre la lista filtra por nombre. Los nombres que se solaparían se ocultan hasta acercarse |
| Mover un punto | Seleccionarlo y arrastrarlo en el mapa, o cambiar latitud/longitud en el detalle y pulsar **Aplicar** (Intro) |
| Editar nombre, descripción, elevación, hora | Panel «Punto seleccionado» › **Aplicar**; **Descartar** (Esc) deshace lo escrito |
| Deshacer / rehacer | **Ctrl+Z** / **Ctrl+Y** (menú Edición) |
| Perfil de elevación | Seleccionar un track o ruta en **Capas** › botón **Perfil** (o Ver › Perfil de elevación). Muestra distancia, desnivel positivo y negativo, elevación mínima y máxima, y tiempo total, en movimiento y detenido. Puntero sobre el perfil: marca el punto en el mapa; clic: lo selecciona; ← →: punto anterior / siguiente. Selector de gráfica: altitud, altitud coloreada por pendiente, pendiente (%) o velocidad (km/h); lista de subidas de más de 50 m |
| Simplificar un track o ruta | Seleccionarlo en **Capas** › *Herramientas › Simplificar track…*; tolerancia en metros con vista previa del número de puntos |
| Regenerar tiempos | *Herramientas › Regenerar tiempos…*: fecha, hora y zona horaria de inicio, y velocidad (km/h, m/s, min/km) o intervalo fijo; vista previa en hora local y UTC |
| Invertir el sentido | *Herramientas › Invertir track*: invierte puntos y segmentos del track o ruta seleccionado; las horas se reflejan (mismo inicio y fin, mismos intervalos). Se deshace con Ctrl+Z |
| Unir tracks | *Herramientas › Unir tracks…*: marcar los tracks (de todos los documentos abiertos), el hueco máximo dentro de un segmento y si se permite invertir alguno; se encadenan por el extremo más cercano, con vista previa del orden y de cada hueco. El resultado se abre en un GPX nuevo sin guardar y los originales no cambian |
| Ajustar altitudes con el MDT | *Herramientas › Ajustar altitudes con el MDT del IGN…* › **Consultar** (5 m o 25 m; requiere Internet) › revisar cuántos puntos cambian y cuántos no tienen dato › **Aplicar**. Sólo España; el origen de cada elevación se ve en el detalle del punto |
| Limpiar un track | *Herramientas › Limpiar track…*: quita saltos del GPS (velocidad máxima, necesita horas), picos (el punto se separa y la línea vuelve) y paradas (quedan el primer y el último punto), y aplica un suavizado gaussiano de la altitud con la ventana elegida (100 m por defecto, como mendikat.net); vista previa y un solo paso de deshacer |
| Cortar | Con un punto seleccionado: *Herramientas › Partir en el punto seleccionado* (dos tracks o rutas; la segunda empieza en una copia del punto), *Eliminar los puntos anteriores / posteriores*; *Separar los segmentos en tracks*. También en el menú contextual |
| Dibujar | **✎ Dibujar** (o *Edición › Modo dibujo*, Ctrl+D): cada clic añade un punto tras el seleccionado; Esc termina. *Edición › Nuevo track / Nueva ruta* en el documento abierto. Opciones de la barra: **Seguir caminos** (BRouter, por sendas y pistas de OSM, con tipo de recorrido), **Ajustar a puntos** (toma la posición y altitud de un punto cercano) y **Altitud del MDT** (MDT locales o IGN 5 m). La barra de estado muestra distancia, desnivel y tiempo MIDE |
| Cerrar en circular, ida y vuelta | *Herramientas › Cerrar en circular* (vuelve al inicio, por caminos si están activados) e *Ida y vuelta* (añade la vuelta por los mismos puntos) |
| Tiempos por desnivel | *Herramientas › Regenerar tiempos…* › **Según la distancia y el desnivel (método MIDE)**: 4 km/h en llano, 400 m/h de subida, 600 m/h de bajada (la velocidad escrita escala las tres) |
| Enviar o recibir del GPS | *Comunicaciones › Enviar al GPS…* copia el documento como GPX en un Garmin (`Garmin\GPX`) o TwoNav (`TwoNavData\Data`) conectado como unidad USB; *Recibir del GPS…* abre sus tracks, rutas y waypoints |
| Abrir arrastrando | Soltar sobre la ventana uno o varios archivos GPX, KML, KMZ, TCX, PLT, WPT, TRK (se abren como documentos) o `.map`, `.imp`, `.rmap`, `.asc`, `.hgt` (como cartografía de usuario) |
| Abrir KML, KMZ, TCX, PLT, WPT o TRK | *Archivo › Abrir archivo…* (Ctrl+O): se abre como un GPX nuevo «nombre.gpx» sin guardar; **Guardar** pide dónde guardarlo |
| Convertir varios archivos a GPX | *Archivo › Convertir a GPX…*: elegir los KML, KMZ, TCX, PLT, WPT o TRK, la carpeta de destino (por defecto la de cada archivo) y qué hacer si el GPX ya existe › **Convertir**; informe por archivo |
| Ver un mapa escaneado o un MDT propio | *Archivo › Abrir cartografía…* (o soltarlos sobre la ventana): mapas `.map` (OziExplorer) e `.imp` (CompeGPS/TwoNav) con su imagen JPG, PNG, BMP, GIF o TIFF, RMAP de TwoNav (`.rmap`), GeoTIFF, ECW e imágenes con `.tfw`/`.jgw`/`.pgw` y `.prj`, y MDT `.asc` (con su `.prj`), `.hgt` y GeoTIFF. ECW servidos por internet: *Archivo › Abrir cartografía en línea (ECWP)…*. Se listan en **Cartografías Usuario (n)** de la barra de herramientas: arrastrar ≡ (o Ctrl+↑/↓) para decidir cuál queda encima (la de arriba de la lista; la cartografía en línea siempre al fondo), deslizador de opacidad para ver la de debajo, casilla para mostrar u ocultar (en un MDT, para usarlo), ⤢ para ir a ella y ✕ para quitarla |
| Cambiar el idioma | *Archivo › Configuración…* › **Idioma de la aplicación**: español (por defecto), català, euskara, français o português, con su bandera. El instalador `.msi` también lo pregunta y es el idioma de la primera ejecución. Se aplica al volver a abrir la aplicación, también a la guía de ayuda (F1). Algunos detalles técnicos de los errores de cartografía siguen en español; los números mantienen la coma decimal |
| Modo claro u oscuro | La aplicación sigue el modo de Windows (*Configuración de Windows › Personalización › Colores*), también en la barra de título; el mapa y su regla de escala no cambian |
| Mostrar u ocultar la regla de escala | *Archivo › Configuración…* › **Visualización** › **Mostrar la regla de escala en el mapa** (activada por defecto); el cambio se ve al aceptar |
| Cuadrícula UTM | *Archivo › Configuración…* › **Visualización** › **Mostrar la cuadrícula UTM en el mapa** (desactivada por defecto), en el huso del centro y el datum elegido |
| Datum ED50 | *Archivo › Configuración…* › **Coordenadas** › **Datum**: WGS84 (por defecto) o ED50 para la barra de estado, la lista de puntos, el detalle y la cuadrícula (husos 29 a 31; los archivos siguen en WGS84) |
| Colorear los tracks | *Ver › Colorear tracks por* › Altitud, Pendiente o Velocidad, con leyenda en el mapa; Ninguno vuelve al color del documento |
| Cancelar la apertura de un archivo grande | Botón **Cancelar** de la barra de estado, o **Esc** |
| Usar sólo el teclado | **Alt** + letra subrayada abre los menús; **Tab** recorre paneles y campos; flechas en las listas |
| Ayuda | **F1** o *Ayuda › Ayuda de Polaris GPS* (guía HTML en el navegador, en el idioma de la aplicación; arriba enlaza a los demás idiomas). En el mismo menú: ¡Dona ahora! (PayPal), web de Mendikat, Buscar actualizaciones (compara con la última release de `mendikat/polaris-gps-releases` y ofrece abrir su página) y Acerca de (versión, autor) |

## Requisitos

- Windows 10 versión 2004 o posterior / Windows 11, x64 (exigido por SkiaSharp, usado por el mapa).
- Desarrollo: SDK de .NET 10 (`global.json` fija 10.0.402).
- Ejecución: [runtime de escritorio .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) (x64), instalado una vez.
- Internet sólo para la cartografía: abrir y ver archivos funciona sin conexión.

## Compilar, probar y ejecutar

Desde la carpeta del repositorio:

```powershell
dotnet build                                   # compila la solución (advertencias = errores)
dotnet test                                    # ejecuta las pruebas (Microsoft.Testing.Platform)
dotnet run --project src\Polaris.Gps.App  # compila y arranca la aplicación
```

`dotnet run` sin `--project` no funciona en la raíz, porque allí está la solución y no el proyecto de la aplicación.
Tras compilar, también se puede abrir directamente
`src\Polaris.Gps.App\bin\Debug\net10.0-windows10.0.19041.0\PolarisGps.exe`.

Archivos de usuario:
- Configuración: `%APPDATA%\Polaris GPS\settings.json` (cartografía elegida, idioma y regla de escala).
- Registro técnico: `%LOCALAPPDATA%\Polaris GPS\logs\polarisgps-AAAAMMDD.log` (se conservan los 10 últimos;
  la carpeta del perfil de usuario se sustituye por `%USERPROFILE%`).

## Distribuir

La aplicación se distribuye dependiente del runtime: cada usuario instala una vez el runtime de escritorio
.NET 10 (x64) y recibe una carpeta de unos 140 MB (unos 90 MB son GDAL, para GeoTIFF y ECW).

```powershell
dotnet publish src\Polaris.Gps.App -p:PublishProfile=win-x64   # genera artifacts\publish\win-x64\
```

Se comprime esa carpeta y el usuario ejecuta `PolarisGps.exe`. Si falta el runtime, Windows muestra un aviso con el
enlace de descarga.

### Instalador (.msi)

El proyecto `installer\Polaris.Gps.Installer` (WiX Toolset 5, se descarga como paquete NuGet; no está en la solución
para no alargar cada compilación) publica la aplicación y la empaqueta en un instalador de unos 38 MB:

```powershell
dotnet build installer\Polaris.Gps.Installer -c Release   # genera artifacts\installer\PolarisGPS-<versión>-win-x64.msi
```

- Páginas: portada con la imagen de inicio, **idioma de la aplicación** (español, català, euskara, français,
  português; propone el de la instalación anterior o el de Windows), **contrato de licencia** en ese idioma (hay que
  aceptarlo), **Mendikat** (el proyecto altruista, su autor y un botón a la página de donaciones), **Mendikat
  Navigator** (la app para Android, con enlace a Google Play), carpeta de destino y progreso; estas dos últimas, de WiX,
  en español. Las imágenes salen de `src\Polaris.Gps.App\Assets` (`splash.jpg`, `icon.png`, `author.png`,
  `mendikat-navigator-icon.png`, `mendikat-navigator-app.png`).
- El idioma elegido se guarda en `HKLM\SOFTWARE\Mendikat\Polaris GPS\Language`; la aplicación lo usa la primera vez que
  la abre cada usuario y después manda *Configuración*. Sin interfaz: `msiexec /i PolarisGPS-….msi /qn APPLANGUAGE=eu`.
- La licencia está en `installer\Polaris.Gps.Installer\License\licencia.<idioma>.txt` (texto: `# ` título, `## `
  apartado, el resto párrafos); `make-assets.ps1` la convierte a RTF y saca de `splash.jpg` las imágenes del
  instalador al compilar.
- Instala para todos los usuarios en `Archivos de programa\Polaris GPS`, con acceso directo en el menú Inicio; pide
  permisos de administrador.
- Una versión nueva sustituye a la instalada (la versión sale de `Directory.Build.props`); no deja instalar una más
  antigua encima. Se desinstala desde *Configuración › Aplicaciones*; la configuración del usuario (`%APPDATA%`) se
  conserva.
- El `.msi` no instala el runtime de .NET: lo hace el programa de instalación (abajo). Con el `.msi` solo o con el zip,
  si falta, Windows lo avisa al abrir la aplicación.
- El `UpgradeCode` de `Package.wxs` identifica el producto entre versiones: no se debe cambiar.

### Programa de instalación con .NET (setup.exe)

`installer\Polaris.Gps.Setup` (bundle Burn de WiX 5) genera `artifacts\installer\PolarisGPS-<versión>-setup.exe` (unos 100 MB), lo
que se publica para los usuarios:

```powershell
dotnet build installer\Polaris.Gps.Setup -c Release   # compila antes el .msi
```

- Si falta el runtime de escritorio de .NET 10 (x64), una página (en español, `Prereq.es.wxl`) pide instalarlo y su
  instalador muestra el progreso; después se abre el asistente del `.msi` con sus páginas. Si ya está, se abre directamente.
- El instalador del runtime se descarga una vez de `https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe`
  (último 10.0.x) a `obj\runtime` y va dentro del `.exe`, que funciona sin conexión. Para actualizarlo, borrar `obj\runtime`.
- En *Aplicaciones instaladas* aparece sólo el programa de instalación; al desinstalarlo se quita Polaris GPS y se
  conserva el runtime. Su `UpgradeCode` (`Bundle.wxs`) tampoco se debe cambiar.
- Sin firmar: Windows SmartScreen puede avisar al descargarlo («Más información › Ejecutar de todas formas»).

## Arquitectura

WPF + MVVM sin framework externo, documentos en pestañas con un mapa compartido.

| Proyecto | Contenido |
|----------|-----------|
| `src/Polaris.Gps.Domain` | Modelo: documentos, tracks, segmentos, puntos, waypoints. Sin dependencias de UI |
| `src/Polaris.Gps.Gpx` | Lector y escritor GPX 1.0/1.1 |
| `src/Polaris.Gps.Mapping` | Catálogo de cartografías, descarga de mosaicos, capas del mapa, formato UTM/DMS |
| `src/Polaris.Gps.App` | WPF: ventana, ViewModels, `MapController`, diálogos, configuración, logging, textos en `Resources/Strings.resx` (español) y `Strings.ca/eu/fr/pt.resx` |
| `tests/Polaris.Gps.Tests` | xUnit v3 con fixtures propios en `Fixtures/`; sin acceso a la red |

## Formatos admitidos

- **GPX 1.1** y **1.0**: lectura y escritura. Se guarda siempre GPX 1.1 válido (un GPX 1.0 se convierte, con aviso)
  conservando metadatos, campos desconocidos y extensiones con sus prefijos. Horas sin zona → UTC con aviso.
- Los waypoints (`wpt`) del GPX se dibujan como triángulos del color del documento, con su nombre al acercarse.
- **KML** y **KMZ** (Google Earth) y **TCX** (Garmin): sólo lectura. Se abren como un GPX nuevo sin guardar
  («ruta.kml» → «ruta.gpx»; el original no se modifica) o se convierten por lotes con *Archivo › Convertir a GPX…*.
  El formato se reconoce por el contenido, no por la extensión.
  - KML: cada punto → waypoint (nombre, descripción, altitud si la tiene); las líneas (`LineString`, `gx:Track` con horas)
    de cada marca → un track con un segmento por línea; se recorren todas las carpetas. Polígonos, superposiciones y
    modelos no tienen equivalente en GPX: se cuentan en un aviso.
  - KMZ: se lee `doc.kml` (o el primer `.kml`); las imágenes se ignoran. Máximo 256 MB descomprimido.
  - TCX: cada actividad → un track con un segmento por vuelta; pulso y cadencia → extensión `gpxtpx` de Garmin
    (la que leen Strava, Garmin y la mayoría de programas). Distancia, velocidad y potencia no se convierten (aviso).
- **OziExplorer** (`.plt` tracks, `.wpt` waypoints) y **CompeGPS/TwoNav** (`.trk` tracks, `.wpt` waypoints): sólo lectura,
  convertidos a GPX como los KML. Los dos `.wpt` se distinguen por el contenido. Se leen como mendikat.net:
  - Datum WGS84 o ED50 (se convierte a WGS84, sólo España y Portugal); otro datum o coordenadas UTM dan un error claro.
  - OziExplorer: un track con un segmento; altitudes en pies → metros (−777 = sin altitud); hora en UTC (aviso).
    Waypoints con nombre, descripción, altitud y hora.
  - CompeGPS/TwoNav: un track con un segmento y su nombre (línea `N`); waypoints con nombre, descripción y símbolo.
    Las horas se pasan a UTC con la zona del archivo (línea `L`); sin ella se toman como UTC (aviso).

### Cartografía y MDT locales (*Archivo › Abrir cartografía…*)

Sólo lectura, con lectores propios y sin dependencias nuevas. No son documentos: no se guardan ni se
recuerdan al cerrar la aplicación.

| Formato | Qué se lee | Limitaciones |
|---------|-----------|--------------|
| OziExplorer `.map` (2.x) | Imagen, datum, proyección, puntos de calibración (grados y minutos o cuadrícula UTM) o, si faltan, las esquinas `MMPLL` | Imagen JPG, PNG, BMP, GIF o TIFF; **OZF2/OZFX3 no** (formato propietario) |
| CompeGPS/TwoNav `.imp` | Imagen (`Bitmap=`), datum y puntos `Pn` en UTM o longitud/latitud (`A`) | Probado con archivos de referencia propios; faltan muestras reales |
| TwoNav `.rmap` | Imagen en teselas JPEG con varios niveles de zoom y la calibración incrustada (las mismas claves que un `.imp`); se carga el nivel más grande que quepa en 50 megapíxeles. Estructura según el lector de QMapShack (no hay especificación pública) | Sólo teselas JPEG (tipo 10.7): los RMAP de paleta, 16 bits o MDT se rechazan con un aviso. Probado con archivos generados por los tests; faltan muestras reales |
| SRTM `.hgt` | 1″ (3601²) y 3″ (1201²), esquina por el nombre (`N42W001`), huecos −32768 = sin dato | Alturas sobre el geoide EGM96, sin convertir |
| ESRI ASCII Grid `.asc` | `xllcorner`/`xllcenter`, `NODATA_value`; sistema del `.prj` (WKT, formato de ArcInfo o `EPSG:n`) o, en los MDT del IGN, del nombre (`MDT05-0112-H30-LIDAR`, `PNOA_MDT05_ETRS89_HU30_…`) | Sin `.prj` ni nombre del IGN no se abre; un MDT05 del IGN (170 MB) tarda unos 3 s y ocupa unos 90 MB |
| GeoTIFF `.tif`, `.tiff` y COG (`.cog`) (con GDAL) | Mapas (RGB, paleta o gris; transparencia por NoData o banda alfa) y MDT (una banda numérica, con NoData); cualquier sistema de coordenadas que conozca PROJ; los georreferenciados sólo con puntos de control se reproyectan al vuelo | Un TIFF sin georreferencia (p. ej. la imagen de un `.map`) se rechaza. Sin pirámides internas, alejarse mucho en un GeoTIFF enorme es lento; los COG las tienen siempre |
| ECW `.ecw` y ECWP (`ecwp://`, `ecwps://`) (con GDAL y el SDK ERDAS ECW/JP2) | Mapas de cualquier tamaño: se lee sólo la zona visible y a la resolución necesaria (un MTN25 de 13 000 × 9 000 píxeles se abre en 0,3 s) | La primera vez hay que aceptar la licencia de uso de ECW; sin ella, los ECW no se abren. ECWP no se ha probado con un servidor real |
| Imagen con archivo de georreferencia: `.tfw` (TIFF), `.jgw` (JPEG), `.pgw` (PNG), `.gfw`, `.bpw`, `.wld` | Posición y tamaño de píxel del archivo de georreferencia (lo lee GDAL) y sistema de coordenadas del `.prj` con el mismo nombre (WKT, formato de ArcInfo o `EPSG:n`). Se abre la imagen, no el `.tfw`; un GeoTIFF o COG ya lleva la georreferencia dentro y no lo necesita | Sin `.prj` no se abre: el archivo de georreferencia no dice el sistema de coordenadas |

- Datums: WGS84, ETRS89 (igual a WGS84 a escala de mapa) y ED50 en los husos 29, 30 y 31, con la misma transformación
  de Helmert que mendikat.net. Proyecciones: geográficas, UTM y Transverse Mercator. Cualquier otro sistema se rechaza
  con un mensaje, en vez de colocar el mapa mal.
- Los mapas se dibujan sobre la cartografía en línea y bajo los tracks, reproyectados a Web Mercator. Las imágenes de
  más de 50 megapíxeles se cargan a tamaño reducido (se pierde detalle al acercarse mucho).
- Los MDT no se dibujan: dan la altitud bajo el puntero (antes que el MDT del IGN, sin conexión) y aparecen en
  *Ajustar altitudes* después de las resoluciones del IGN.
- GeoTIFF y ECW pasan de su datum a WGS84 con PROJ (incluido en GDAL). En ED50 da lo mismo que la transformación de mendikat.net con 2–7 cm de diferencia en los husos 29 y 30 y 0,9 m en el 31.

## Cartografía

Predeterminada: Mapa IGN (España). Disponibles: Mapa IGN (España), CyclOSM, OpenTopoMap, OpenStreetMap,
Mapa satélite (ESRI), Mapa Topográfico (ESRI), Géoportail IGN (France, SCAN 25) y MtbMap. Ninguna requiere clave personal (Géoportail usa la clave pública del IGN).
La elegida se recuerda entre sesiones; cada una muestra su atribución sobre el mapa.
En la esquina inferior derecha, encima de la atribución, una regla de escala (en m, o en km desde 1000 m) cambia con el zoom y la latitud; se oculta en *Archivo › Configuración…* › **Visualización**.

## Coordenadas

Barra de estado: UTM WGS84 `30T 575700 4760048` (zona y banda, Este, Norte en metros) y WGS84 en grados,
minutos y segundos `42º 59' 21,60'' N 2º 04' 17,11'' W`. Cálculo con GeographicLib (Karney), con las zonas
especiales de Noruega y Svalbard; fuera de UTM se muestra UPS.
También muestra el datum del archivo activo (WGS84 en GPX, KML y TCX) y, al detener el puntero, la altitud bajo él:
la del punto del archivo si hay uno con elevación (`Alt. 1.234 m (archivo)`) o, si no, la del terreno según el MDT
del IGN (`Alt. 846 m (MDT IGN 5 m)`, sólo España y con conexión; sin dato, «—»).

Con *Archivo › Configuración… › Coordenadas › Datum* = ED50, la UTM se calcula sobre el elipsoide Internacional 1924
con la transformación de Helmert de mendikat.net (`Ed50`), y lo mismo la latitud y longitud de la lista y del detalle
(lo escrito en ED50 se pasa a WGS84 al aplicar). Sólo en los husos 29 a 31; fuera, WGS84 y la barra de estado lo dice.
La cuadrícula UTM (`UtmGrid`) usa el huso del centro del mapa y el mismo datum; separación redonda de al menos 90 px.

## Dependencias y licencias

| Paquete | Licencia |
|---------|----------|
| Microsoft.Extensions.Logging 10.0.13 | MIT |
| GeographicLib.NET 2.3.2 | MIT |
| Mapsui, Mapsui.Tiling, Mapsui.Nts, Mapsui.Wpf 5.1.0 | MIT |
| ↳ BruTile 6.0 · NetTopologySuite 2.6 · SkiaSharp 3.119 · HarfBuzzSharp · Svg.Skia · Topten.RichTextKit | Apache-2.0 · BSD-3 · MIT · MIT · MIT · Apache-2.0 |
| GDAL 3.12.1 (bindings de C#) · GDAL.Native 3.12.1 (compilación GISInternals: GDAL, PROJ, GEOS, libtiff…) | MIT · licencias de cada biblioteca en `gdal*.rtf` y `gdalicense.txt` |
| SDK ERDAS ECW/JP2 Desktop Read-Only Redistributable 5 (paquete GDAL.Plugins: `gdal_ECW_JP2ECW.dll`, `NCSEcw.dll`) | Licencia propietaria de Intergraph/Hexagon, gratuita para aplicaciones de escritorio que sólo leen (`gdalCW5License.rtf`). Obliga a admitir ECWP, a que el usuario acepte una licencia de uso y a mostrar su aviso de copyright en «Acerca de»: los tres están hechos |
| xunit.v3 4.0.2 (sólo pruebas) | Apache-2.0 |

Datos: mosaicos y MDT del IGN España bajo CC BY 4.0 (scne.es); datos OSM bajo ODbL. Servicio de elevaciones: WCS «Modelos Digitales del Terreno de España» (servicios.idee.es/wcs-inspire/mdt).

## Limitaciones conocidas

- Cartografía local: imágenes OZF y RMAP que no sean de teselas JPEG no se admiten. Este producto contiene tecnología propiedad de Intergraph Corporation (ECW). En un `.map`, `.imp` o `.rmap`, una imagen enorme se
  carga reducida a 50 megapíxeles. ED50 sólo en los husos 29 a 31; su latitud difiere unos 0,3 m de la que da
  mendikat.net, que corta antes un cálculo iterativo (conviene corregirlo en el PHP).

- Guardar escribe primero un archivo temporal, lo verifica y sólo entonces sustituye el original. Al guardar se avisa
  si algo cambiará en el archivo (GPX 1.0 → 1.1). Los números se escriben con su valor exacto, pero su texto puede cambiar (-2.0610 → -2.061).
- El perfil mide la distancia horizontal y, por defecto, no cuenta el hueco entre segmentos (casilla para incluirlo).
- Tras cada edición se redibuja el documento completo en el mapa: unos 0,2 s con 100 000 puntos y casi 1 s con 500 000.
  Evitarlo exige actualizar el mapa por partes (rediseño de las capas); queda fuera de este plan.
- Con muchos waypoints juntos, sus nombres se solapan en el mapa al acercarse.
- MDT del IGN: sólo España. El servicio no marca «sin dato» (devuelve 0 en el mar y fuera de España), así que las celdas a 0,000 m se tratan como sin dato y esos puntos no cambian. No se aplica ninguna corrección geoidal.
- Un GPX de 50 MB (500 000 puntos) tarda unos 5 s en abrirse y ocupa unos 190 MB de memoria. Guardarlo prepara el
  archivo en la ventana principal (~2 s sin respuesta); la verificación y la escritura van en segundo plano. Guardar no se puede cancelar.
- El lector de pantalla no anuncia por sí solo los cambios de la barra de estado: hay que ir a ella para leerlos.
- El comportamiento sin conexión está probado con red simulada; falta la comprobación manual sin red.
- Las pruebas de interfaz crean las ventanas reales sin mostrarlas (enlaces, nombres accesibles, cierre); el uso con ratón
  y el dibujo del mapa se comprueban a mano.
- Condiciones de uso de Esri, OSM y del SCAN 25 del IGN francés a revisar antes de distribuir públicamente.
- MtbMap y CyclOSM son servidores comunitarios sin garantía de disponibilidad.
- «Buscar actualizaciones» consulta la API de GitHub sin credenciales (60 consultas por hora y dirección) en el
  repositorio público `mendikat/polaris-gps-releases`: hay que crearlo y publicar en él cada versión (el código sigue
  en el repositorio privado). Sin release publicada, avisa de que no se pudo comprobar.
- «Seguir caminos» usa el servidor público de BRouter (brouter.de), sin garantía de disponibilidad ni límites
  publicados; `BRouterClient` admite otro servidor (BRouter se puede instalar en uno propio).
- Comunicaciones sólo ve los GPS conectados como unidad USB; los que usan MTP (muchos relojes) no aparecen.
- El `.exe` y el `.msi` no están firmados.
