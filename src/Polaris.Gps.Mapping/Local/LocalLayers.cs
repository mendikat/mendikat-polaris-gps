using BruTile;
using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Geo;

namespace Polaris.Gps.Mapping.Local;

/// <summary>A local DEM whatever its format (.asc, .hgt, GeoTIFF): an elevation source with a known extent.</summary>
public interface ILocalDem : IElevationSource
{
    /// <summary>WGS84 extent.</summary>
    GeoBounds Bounds { get; }

    int Columns { get; }

    int Rows { get; }

    /// <summary>Coordinate system, e.g. "UTM 30N ETRS89".</summary>
    string CrsDescription { get; }

    /// <summary>Elevation at a WGS84 position, or null without data (never 0 m).</summary>
    double? ElevationAt(GeoPosition position);
}

/// <summary>A local map drawn as Web Mercator tiles, whatever its format (calibrated image, GeoTIFF, ECW).</summary>
public interface ILocalMap
{
    /// <summary>WGS84 extent.</summary>
    GeoBounds Bounds { get; }

    /// <summary>Tiles for the map view, made on demand on background threads.</summary>
    ILocalTileSource CreateTileSource(string name);
}
