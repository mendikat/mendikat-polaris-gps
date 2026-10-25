# ADR-0007 — Fuente DEM para el ajuste de altitudes

- Estado: **Aceptada** (2026-10-24): **IGN WCS, sólo España** (opción A). Se implementa en el incremento 7.
- Fecha: 2026-10-24

## Fuentes evaluadas (verificadas el 2026-10-24)

| Opción | Cobertura | Resolución | Licencia / límites | Datum vertical | Online/offline |
|--------|-----------|------------|--------------------|----------------|----------------|
| **A. IGN — WCS «Modelos Digitales del Terreno de España»** `https://servicios.idee.es/wcs-inspire/mdt` | España (península, Baleares, Canarias, Ceuta, Melilla) | 5 m, 25 m, 200 m, 500 m, 1000 m (coberturas `Elevacion4258_5`, `…_25`, …) | **CC BY 4.0 scne.es**, sin tasas (según GetCapabilities). Sin límite publicado; uso razonable. | Altitudes ortométricas sobre el geoide oficial español (EGM08-REDNAP en península/Baleares; REDCAN en Canarias). A confirmar en metadatos del producto en el incremento 7. | Online. Formatos `application/asc` (ArcGrid, texto — no requiere librería GeoTIFF) y GeoTIFF. Petición por bbox del track. |
| B. Open Topo Data (API pública) `https://api.opentopodata.org/v1/{dataset}` | Global/Europa según dataset (EU-DEM 25 m, SRTM 30 m, ASTER, …) | 25–90 m | Servicio gratuito **sin garantía**: máx. 100 puntos/petición, 1 petición/s, 1000 peticiones/día. Licencia según dataset (EU-DEM: Copernicus; SRTM: dominio público). | EU-DEM: EVRS (EVRF2000); SRTM: EGM96. | Online. JSON. |
| C. Archivos locales SRTM `.hgt` (o Copernicus GLO-30 GeoTIFF) | Donde el usuario tenga archivos | 30 m / 90 m | SRTM dominio público; GLO-30 licencia Copernicus | EGM96 | **Offline**. HGT es trivial de leer; GeoTIFF requiere librería. |

## Propuesta

- **Implementar A (IGN WCS) como fuente principal** para España: mejor resolución, licencia abierta y clara, sin clave.
- **Diseño abierto** `IElevationSource` para añadir C (HGT local, offline) después, y B sólo como fuente global opcional, avisando de sus límites.
- Puntos fuera de cobertura o con *nodata* → «sin dato», conservan la elevación original (DEM-04).
- Interpolación bilineal en la malla; se documentará.
- **Sin corrección geoidal implícita** (DEM-05). La elevación de GPX 1.1 no declara datum; se indica en la vista previa el datum vertical de la fuente y se avisa de que la elevación original puede estar en otra referencia.
- Atribución CC BY 4.0 en el diálogo del ajuste y en el README.

## Decisión solicitada

1. ¿Aceptar IGN WCS (A) como fuente única inicial (sólo España)?
2. ¿Se necesita cobertura fuera de España en v1? Si es así, ¿B (online, límites estrictos) o C (archivos locales)?

## Implementación (incremento 7, 2026-10-25)

Comportamiento del servicio verificado el 2026-10-25 (`DescribeCoverage` y `GetCoverage` reales):

- `GetCoverage` con `FORMAT=application/asc` devuelve una respuesta `multipart/related` (frontera `wcs`) que contiene
  un ArcGrid en grados, remuestreado al recuadro pedido. Coberturas `Elevacion4258_5` (5 m) y `Elevacion4258_25` (25 m).
- **No hay valor NODATA**: el mar y las zonas fuera de España devuelven exactamente `0.000`. Decisión: los ceros
  exactos se tratan como «sin dato» y esos puntos conservan su elevación (un punto real a 0,000 m tampoco cambia,
  que es el resultado seguro).
- Fuera de la envolvente de la cobertura el servicio devuelve un `ows:ExceptionReport` con `ExtentError`: sin dato.
- Comprobación independiente de ejes y datum: máximo del MDT 5 m en el Aneto 3402,0 m (oficial 3404) y en el
  Mulhacén 3476,7 m (oficial 3479).
- Datum vertical, según los metadatos INSPIRE del servicio y del MDS05: altitudes ortométricas (PNOA-LiDAR) sobre el
  nivel medio del mar en Alicante (Península) y nivel medio local (Baleares, Canarias), red REDNAP. El sistema
  horizontal es ETRS89; la diferencia con WGS84 (< 1 m en 2026) es despreciable frente a la celda de 5 m.

Diseño:

- `Domain/Elevation/IElevationSource` (interfaz) y `ElevationAdjustment` (plan, recuentos, diferencias y
  `SetElevationsCommand` deshacible). `TrailPoint.ElevationSource` registra en la sesión el origen de cada elevación
  (archivo, interpolada, MDT, editada) y el panel de detalle lo muestra (DEM-06).
- `Mapping/Elevation/IgnWcsElevationSource`: puntos agrupados en cuadrículas de 0,005° (≈ 0,5 km) con un margen de
  una celda; una petición por cuadrícula, 4 en paralelo, `HttpClient` con 30 s de tiempo de espera, progreso y
  cancelación. Interpolación bilineal entre centros de celda; si alguna celda con peso distinto de cero no tiene dato,
  el punto queda sin dato. No se creó un proyecto aparte: la fuente usa la misma infraestructura HTTP que la cartografía.
- Las pruebas usan un servidor simulado que reproduce las respuestas reales (multipart, ceros, `ExtentError`);
  nunca acceden a la red.
