using System.Globalization;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.Domain.Geo;
using Mendikat.TrailLab.Mapping.Local;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>
/// A local map or DEM in the "Local maps" list (LMAP-01, LDEM-01). A map can be shown or hidden; a DEM is not drawn
/// and its check box says whether it is used for elevations (status bar and "Adjust elevations").
/// </summary>
public sealed class LocalMapItemViewModel : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private bool _isVisible = true;
    private double _opacity = 1;

    public LocalMapItemViewModel(LocalMapContent content)
    {
        Content = content;
    }

    /// <summary>Raised when <see cref="IsVisible"/> changes.</summary>
    public event EventHandler? VisibilityChanged;

    /// <summary>Raised when <see cref="Opacity"/> changes.</summary>
    public event EventHandler? OpacityChanged;

    public LocalMapContent Content { get; }

    public string Name => Content.FileName;

    public bool IsDem => Content is LocalDem;

    public ElevationGrid? Dem => (Content as LocalDem)?.Grid;

    public GeoBounds Bounds => Content switch
    {
        LocalRasterMap raster => raster.Map.Bounds,
        LocalDem dem => dem.Grid.Bounds,
        _ => throw new InvalidOperationException(),
    };

    /// <summary>Map: drawn on the map. DEM: used for elevations.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                VisibilityChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Opacity of a drawn map, 0 (invisible) to 1 (opaque), to see the maps below it (LMAP-11).</summary>
    public double Opacity
    {
        get => _opacity;
        set
        {
            if (SetProperty(ref _opacity, Math.Clamp(value, 0, 1)))
            {
                OnPropertyChanged(nameof(OpacityText));
                OpacityChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>"75 %".</summary>
    public string OpacityText => (Opacity * 100).ToString("0", Spanish) + " %";

    /// <summary>Accessible name of the slider, e.g. "Opacidad de Baztan.map".</summary>
    public string OpacityName => string.Format(Strings.LocalOpacityName, Name);

    /// <summary>Only drawn maps have an opacity; DEMs are not drawn.</summary>
    public bool HasOpacity => !IsDem;

    /// <summary>"Mapa" or "MDT", shown before the name.</summary>
    public string Kind => IsDem ? Strings.LocalKindDem : Strings.LocalKindMap;

    public string VisibilityToolTip => IsDem ? Strings.LocalDemUseToolTip : Strings.LocalMapShowToolTip;

    /// <summary>Format, coordinate system, size and calibration error (tool tip).</summary>
    public string Details => Content switch
    {
        LocalRasterMap raster => string.Format(
            Spanish,
            Strings.LocalMapDetails,
            raster.Map.Calibration.Format,
            raster.Map.Crs,
            raster.Map.ImageWidth.ToString("N0", Spanish),
            raster.Map.ImageHeight.ToString("N0", Spanish),
            raster.Map.Calibration.Points.Count,
            raster.Map.Transform.MaxResidualPixels.ToString("0.0", Spanish),
            raster.Image.Scale < 1 ? string.Format(Spanish, Strings.LocalMapReduced, (raster.Image.Scale * 100).ToString("0", Spanish)) : string.Empty),
        LocalDem dem => string.Format(
            Spanish,
            Strings.LocalDemDetails,
            dem.Grid.Columns.ToString("N0", Spanish),
            dem.Grid.Rows.ToString("N0", Spanish),
            dem.Grid.Crs,
            dem.Grid.VerticalDatum),
        _ => string.Empty,
    };
}
