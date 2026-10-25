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
