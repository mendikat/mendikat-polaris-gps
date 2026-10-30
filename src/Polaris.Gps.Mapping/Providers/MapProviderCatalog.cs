namespace Polaris.Gps.Mapping.Providers;

/// <summary>
/// The only place where tile URLs are defined (MAP-02). Endpoints, attributions and limits were verified on
/// 2026-10-24; see ADR-0004 for terms of use. None needs a personal API key: Géoportail uses the public shared key of IGN.
/// </summary>
public static class MapProviderCatalog
{
    public const string DefaultId = "ign-es";

    /// <summary>Public key published by IGN for its SCAN maps on the Géoplateforme; not personal, not secret (ADR-0004).</summary>
    public const string IgnScanPublicKey = "ign_scan_ws";

    private static readonly string[] Abc = ["a", "b", "c"];

    public static IReadOnlyList<MapProvider> All { get; } =
    [
        new(
            "ign-es",
            "Mapa IGN (España)",
            "https://www.ign.es/wmts/mapa-raster?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=MTN&STYLE=default"
                + "&TILEMATRIXSET=GoogleMapsCompatible&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&FORMAT=image/jpeg",
            "© Instituto Geográfico Nacional de España (CC BY 4.0 scne.es)",
            "https://www.scne.es/",
            MaxZoom: 20,
            UsageNote: "Cobertura: España."),
        new(
            "cyclosm",
            "CyclOSM",
            "https://{s}.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png",
            "© Colaboradores de OpenStreetMap · Estilo CyclOSM, alojado por OpenStreetMap France",
            "https://www.cyclosm.org/",
            MaxZoom: 20,
            ServerNodes: Abc,
            UsageNote: "Servidor comunitario sin garantía de disponibilidad."),
        new(
            "opentopomap",
            "OpenTopoMap",
            "https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png",
            "Datos © Colaboradores de OpenStreetMap, SRTM · Estilo © OpenTopoMap (CC-BY-SA)",
            "https://opentopomap.org/",
            MaxZoom: 17,
            ServerNodes: Abc),
        new(
            "osm",
            "OpenStreetMap",
            "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
            "© Colaboradores de OpenStreetMap",
            "https://www.openstreetmap.org/copyright",
            MaxZoom: 19),
        new(
            "esri-imagery",
            "Mapa satélite (ESRI)",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
            "Tiles © Esri — Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community",
            "https://www.esri.com/",
            MaxZoom: 19),
        new(
            "esri-topo",
            "Mapa Topográfico (ESRI)",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}",
            "Tiles © Esri — Esri, HERE, Garmin, Intermap, increment P Corp., GEBCO, USGS, FAO, NPS, NRCAN, GeoBase, IGN, Kadaster NL, Ordnance Survey, Esri Japan, METI, Esri China (Hong Kong), © OpenStreetMap contributors, and the GIS User Community",
            "https://www.esri.com/",
            MaxZoom: 19),
        new(
            "geoportail-fr",
            "Géoportail IGN (France)",
            // SCAN 25 (classic 1:25 000 topographic map; smaller scales of the same series when zoomed out). It is
            // served by the "private" flow with ign_scan_ws, the public shared key IGN publishes for its SCAN maps.
            "https://data.geopf.fr/private/wmts?apikey=" + IgnScanPublicKey
                + "&SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=GEOGRAPHICALGRIDSYSTEMS.MAPS"
                + "&STYLE=normal&TILEMATRIXSET=PM_0_18&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&FORMAT=image/jpeg",
            "© IGN-F / Géoportail – SCAN 25®",
            "https://cartes.gouv.fr/cgu/",
            MaxZoom: 18,
            UsageNote: "Cobertura: Francia. Licencia del SCAN 25 por confirmar antes de distribuir (ADR-0004)."),
        new(
            "mtbmap",
            "MtbMap",
            "https://tile.mtbmap.cz/mtbmap_tiles/{z}/{x}/{y}.png",
            "© Colaboradores de OpenStreetMap y USGS · Estilo MtbMap.cz",
            "https://mtbmap.cz/",
            MaxZoom: 18,
            UsageNote: "Proyecto personal sin garantía de disponibilidad; cobertura principalmente Europa."),
    ];

    public static MapProvider Default => Find(DefaultId)!;

    public static MapProvider? Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    /// <summary>The provider with <paramref name="id"/>, or the default one if it is unknown (MAP-04).</summary>
    public static MapProvider FindOrDefault(string? id) => Find(id) ?? Default;
}
