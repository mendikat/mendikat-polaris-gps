using Mendikat.TrailLab.Domain.Geo;

namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// What a calibration file (.map, .imp) says: the image, its coordinate system and the calibration points.
/// <see cref="ImageWidth"/> and <see cref="ImageHeight"/> are those written in the file, when it has them.
/// </summary>
public sealed record MapCalibration(
    string Format, string Title, string ImagePath, Crs Crs, IReadOnlyList<ControlPoint> Points, int? ImageWidth = null, int? ImageHeight = null);

/// <summary>A calibrated image ready to be drawn: pixel ↔ map coordinates ↔ WGS84 (LMAP-03, LMAP-06).</summary>
public sealed class GeoreferencedMap
{
    private GeoreferencedMap(MapCalibration calibration, int width, int height, PixelTransform transform, GeoBounds bounds)
    {
        Calibration = calibration;
        ImageWidth = width;
        ImageHeight = height;
        Transform = transform;
        Bounds = bounds;
    }

    public MapCalibration Calibration { get; }

    public Crs Crs => Calibration.Crs;

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    public PixelTransform Transform { get; }

    /// <summary>WGS84 extent of the image (its edges sampled through the projection).</summary>
    public GeoBounds Bounds { get; }

    /// <exception cref="LocalMapException">Invalid calibration.</exception>
    /// <exception cref="UnsupportedCrsException">A datum without transformation for this area.</exception>
    public static GeoreferencedMap Create(MapCalibration calibration, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, $"imagen de {imageWidth} × {imageHeight} píxeles");
        }

        var transform = PixelTransform.Fit(calibration.Points);
        var edge = new List<GeoPosition>();
        const int Steps = 16;
        for (var i = 0; i <= Steps; i++)
        {
            var t = (double)i / Steps;
            foreach (var (px, py) in new[] { (t * imageWidth, 0.0), (t * imageWidth, (double)imageHeight), (0.0, t * imageHeight), ((double)imageWidth, t * imageHeight) })
            {
                var (x, y) = transform.ToMap(px, py);
                edge.Add(calibration.Crs.ToWgs84(x, y));
            }
        }

        if (edge.Any(p => !GeoPosition.IsValidLatitude(p.Latitude) || !GeoPosition.IsValidLongitude(p.Longitude)))
        {
            throw new LocalMapException(LocalMapError.InvalidCalibration, "la imagen queda fuera de la Tierra con estos puntos");
        }

        return new GeoreferencedMap(calibration, imageWidth, imageHeight, transform, GeoBounds.FromPositions(edge)!.Value);
    }

    /// <summary>WGS84 position of an image pixel.</summary>
    public GeoPosition ToWgs84(double pixelX, double pixelY)
    {
        var (x, y) = Transform.ToMap(pixelX, pixelY);
        return Crs.ToWgs84(x, y);
    }

    /// <summary>Image pixel of a WGS84 position (may be outside the image).</summary>
    public (double PixelX, double PixelY) ToPixel(GeoPosition position)
    {
        var (x, y) = Crs.FromWgs84(position);
        return Transform.ToPixel(x, y);
    }
}
