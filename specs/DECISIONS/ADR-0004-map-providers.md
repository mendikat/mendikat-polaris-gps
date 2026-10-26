# ADR-0004 — Proveedores de cartografía

- Estado: **Aceptada** (Thunderforest eliminado, D3; condiciones de uso a revisar antes de distribuir)
- Fecha: 2026-10-24

## Contexto

Se piden nueve fuentes (Thunderforest se descartó después, ver abajo). Cada una se verificó el 2026-10-24 solicitando el mosaico z=10 x=505 y=376
(Navarra/Gipuzkoa) con un `User-Agent` propio. Todas respondieron HTTP 200 con imagen.

## Decisión — Catálogo inicial

Las definiciones viven sólo en `Mendikat.TrailLab.Mapping/Providers/MapProviderCatalog.cs` (MAP-02).

| Id | Nombre visible | Tipo / plantilla | Atribución obligatoria | Clave | Notas de uso |
|----|----------------|------------------|------------------------|-------|--------------|
| `ign-es` | Mapa IGN (España) **(predeterminado)** | WMTS KVP `https://www.ign.es/wmts/mapa-raster?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=MTN&STYLE=default&TILEMATRIXSET=GoogleMapsCompatible&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&FORMAT=image/jpeg` | «© Instituto Geográfico Nacional de España» (CC BY 4.0, scne.es) | No | Cobertura España; z 0–20. Fuera de España, mosaicos vacíos. |
| `cyclosm` | CyclOSM | XYZ `https://{a-c}.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png` | «© OpenStreetMap contributors, estilo CyclOSM, alojado por OpenStreetMap France» | No | Servidor comunitario: uso moderado. |
| `opentopomap` | OpenTopoMap | XYZ `https://{a-c}.tile.opentopomap.org/{z}/{x}/{y}.png` | «Datos © OpenStreetMap contributors, SRTM · Estilo © OpenTopoMap (CC-BY-SA)» | No | z máx. 17. |
| `osm` | OpenStreetMap | XYZ `https://tile.openstreetmap.org/{z}/{x}/{y}.png` | «© OpenStreetMap contributors» | No | **Tile Usage Policy**: User-Agent identificativo obligatorio, sin descarga masiva, respetar caché HTTP. |
| `esri-imagery` | Mapa satélite (ESRI) | XYZ `https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}` (¡orden y/x!) | «Tiles © Esri — Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community» | No (hoy) | **Condiciones Esri a revisar** para distribución: los servicios básicos públicos admiten uso no comercial; uso comercial puede requerir cuenta ArcGIS. |
| `esri-topo` | Mapa Topográfico (ESRI) | XYZ `…/World_Topo_Map/MapServer/tile/{z}/{y}/{x}` | «Tiles © Esri — Esri, HERE, Garmin, … » (texto completo del servicio) | No (hoy) | Ídem. Esri lo marca como *mature support*. |
| `geoportail-fr` | Géoportail IGN (France) | WMTS KVP `https://data.geopf.fr/private/wmts?apikey=ign_scan_ws&SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=GEOGRAPHICALGRIDSYSTEMS.MAPS&STYLE=normal&TILEMATRIXSET=PM_0_18&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&FORMAT=image/jpeg` (desde 2026-10-26; antes Plan IGN v2) | «© IGN-F / Géoportail – SCAN 25®» | Clave **pública** compartida `ign_scan_ws` (no personal) | SCAN 25 del IGN francés (escalas menores al alejarse), zoom 0–18. Gratuito según la Géoplateforme («salvo licencia específica»); la ficha del SCAN 25 indica «Atribución» y remite a las condiciones del IGN: **confirmar la licencia antes de distribuir**. |
| `mtbmap` | MtbMap | XYZ `https://tile.mtbmap.cz/mtbmap_tiles/{z}/{x}/{y}.png` | «© OpenStreetMap contributors & USGS, estilo MtbMap.cz» | No | Proyecto personal sin SLA; cobertura principalmente Europa. Se marca «disponibilidad no garantizada». |

## Thunderforest — descartado (2026-10-24)

El documento base lo incluía, pero exige API key según sus condiciones. Por decisión del responsable
del producto (D3) **se elimina del catálogo**. Con ello ningún proveedor requiere clave y no se
implementa configuración de claves (MAP-05 retirado). Si en el futuro se añade un proveedor con clave,
se especificará de nuevo (clave en la configuración local de usuario, nunca en el repositorio).

## Configuración

`%APPDATA%\Mendikat\TrailLab\settings.json` (nunca se versiona):

```json
{ "map": { "provider": "ign-es" } }
```

## Otras decisiones

- **Componente de mapa: Mapsui.Wpf 5.1.0** (MIT) — capas de mosaicos XYZ (BruTile, Apache-2.0),
  capas vectoriales en memoria, renderizado SkiaSharp (MIT), proyección 3857 nativa.
  *Alternativas descartadas*: control propio (más código y riesgo en proyección/caché), GMap.NET WPF
  (mantenimiento escaso), WebView2 + Leaflet (JS embebido, dos modelos de datos).
- Caché de mosaicos en memoria + caché HTTP de sesión; sin descarga masiva (fuera de alcance).
- Peticiones con timeout (10 s), `User-Agent` `MendikatTrailLab/<versión>` y en segundo plano; los fallos se
  registran una vez por cambio de estado (fallando / funcionando) y se avisan en la barra de estado (MAP-07).
- **Atribución** (MAP-06): la muestra la aplicación como enlace sobre el mapa, a partir de `MapProvider`. No se
  pasa a la fuente de mosaicos de Mapsui, que si no dibuja una segunda copia superpuesta (comprobado el 2026-10-24).
- Antes de distribuir públicamente la aplicación se revisarán de nuevo las condiciones de cada proveedor (especialmente Esri y OSM).

## Géoportail: SCAN 25 en lugar del Plan IGN (2026-10-26, a petición del responsable)

El responsable quiere ver en Géoportail el mapa topográfico clásico (SCAN 25), no el Plan IGN v2. El SCAN 25 no está en
el flujo abierto `data.geopf.fr/wmts`; está en el flujo «private» con `apikey=ign_scan_ws`, la clave pública y compartida
que el IGN usa para sus SCAN (comprobado: GetCapabilities y mosaicos de los zooms 6 a 18 en el Mont Baïgura). Se relaja la
regla «sin clave de API»: se prohíben las claves personales o secretas, no esta clave pública
(`MapProviderCatalog.IgnScanPublicKey`, prueba `Every_provider_has_attribution_https_and_tile_placeholders`).
Pendiente: confirmar en las condiciones del IGN que el uso del SCAN 25 en una aplicación distribuida está permitido.
