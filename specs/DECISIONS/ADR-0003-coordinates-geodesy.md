# ADR-0003 — Coordenadas, proyección y geodesia

- Estado: **Aceptada** (D5 aceptada el 2026-10-24: latitud primero)
- Fecha: 2026-10-24

## Contexto

Hay tres sistemas que no deben confundirse:

1. **Geográficas WGS84** (EPSG:4326): lat/lon en grados decimales. Es el sistema del modelo de datos (GPX lo exige).
2. **Proyectadas Web Mercator** (EPSG:3857): sólo para dibujar sobre los mosaicos.
3. **Pantalla** (píxeles del control): las gestiona el control de mapa a partir de 3857.

Además, UTM (EPSG:326xx/327xx) sólo para mostrar en la barra de estado.

## Decisión

- **Modelo**: siempre `GeoPosition(double Latitude, double Longitude)` WGS84 — tipo explícito, nunca `Point`/`(x,y)` sin nombre, para evitar inversiones lat/lon.
- **Dibujo**: conversión 4326→3857 en un único adaptador (`Mapping`), usando la función de Mapsui (`SphericalMercator.FromLonLat`). Las distancias **nunca** se calculan en 3857.
- **Geodesia**: **GeographicLib.NET 2.3.2** (MIT), port del algoritmo de Karney:
  - `Geodesic.WGS84.Inverse` para distancias (precisión ~15 nm) y punto medio (`Direct` con la mitad de la distancia).
  - `UTMUPS.Forward` para UTM/UPS: aplica automáticamente las excepciones de Noruega/Svalbard y cambia a UPS fuera de [80° S, 84° N].
  - La **banda** (letra de latitud, C–X sin I ni O, 8° cada una y X de 72° a 84°) se calcula a partir de la latitud en `CoordinateFormatter.Band`; es distinta de la zona (número de huso), que da GeographicLib.
- **Formato UTM**: `ZZB EEEEEE NNNNNNN` — zona+banda, **Este** y luego **Norte**, metros enteros (truncados como en MGRS/GeoConvert), datum WGS84. Ejemplo `30T 555555 4750000`.
- **Formato DMS (D5, aceptado)**: latitud primero, después longitud, hemisferio tras el valor:
  `43º 22' 23,11'' N 1º 45' 10,00'' W`. El ejemplo del documento base (`43º … W 1º … N`) intercambia los
  hemisferios; se interpreta como errata. Se usa `º` (ordinal) tal como en el ejemplo; *alternativa*: símbolo de grado `°`.
- Cultura `es-ES` para formateo (coma decimal). Redondeo de segundos con propagación (59,995″ → minuto siguiente).

## Alternativas descartadas

- **Implementación propia** de Krüger/Haversine: Haversine (esfera) tiene error de hasta ~0,5 %; implementar Krüger es viable pero duplica una librería probada.
- **ProjNET** (LGPL 2.1): más general, pero no hace falta reproyección arbitraria; licencia más restrictiva.
- **NetTopologySuite** para geodesia: no hace cálculos elipsoidales.

## Pruebas

Valores de referencia (ACC-COORD-01..04) generados con GeoConvert y fijados con tolerancias explícitas.
