using Mapsui;
using Mapsui.Layers;
using Mendikat.TrailLab.Domain.Model;

namespace Mendikat.TrailLab.Mapping.Layers;

/// <summary>Finds the point under the pointer (LAY-03): the nearest point feature within a tolerance in pixels.</summary>
public static class PointHitTester
{
    /// <summary>Default tolerance around a point, in screen pixels.</summary>
    public const double DefaultTolerancePixels = 8;

    /// <param name="world">Pointer position in Web Mercator meters.</param>
    /// <param name="resolution">Current map resolution (meters per pixel).</param>
    /// <returns>The nearest point and the layer that contains it, or null if none is close enough.</returns>
    public static (TrailPoint Point, ILayer Layer)? FindNearest(
        IEnumerable<ILayer> layers, MPoint world, double resolution, double tolerancePixels = DefaultTolerancePixels)
    {
        var tolerance = tolerancePixels * resolution;
        var box = new MRect(world.X - tolerance, world.Y - tolerance, world.X + tolerance, world.Y + tolerance);
        (TrailPoint Point, ILayer Layer)? best = null;
        var bestDistance = double.MaxValue;

        foreach (var layer in layers)
        {
            if (!layer.Enabled || layer is not MemoryLayer memory)
            {
                continue;
            }

            foreach (var feature in memory.Features)
            {
                if (feature is not PointFeature { Data: TrailPoint point } pointFeature || !box.Contains(pointFeature.Point))
                {
                    continue;
                }

                var distance = pointFeature.Point.Distance(world);
                if (distance <= tolerance && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (point, layer);
                }
            }
        }

        return best;
    }
}
