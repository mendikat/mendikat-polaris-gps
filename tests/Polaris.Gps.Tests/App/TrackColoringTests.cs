using Polaris.Gps.App.ViewModels;
using Polaris.Gps.Mapping.Layers;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>View › Colour tracks by: the choice is remembered and the legend follows it.</summary>
public sealed class TrackColoringTests
{
    [Fact]
    public void The_colouring_is_saved_restored_and_explained_by_the_legend()
    {
        var settings = new FakeSettingsStore();
        var vm = new MainViewModel(new FakeDialogService(), new FakeDocumentStore(), settings, NullLogger<MainViewModel>.Instance);
        Assert.Equal(TrackColorMode.None, vm.TrackColoring);
        Assert.False(vm.HasLegend);

        vm.TrackColoringCommand.Execute("Slope");

        Assert.True(vm.IsColoringSlope);
        Assert.Equal("Slope", settings.Current.Map.TrackColoring);
        Assert.Equal(TrackColoring.SlopeClasses.Count, vm.Legend.Count);
        Assert.Equal("< −15 %", vm.Legend[0].Label);
        Assert.Equal("> 25 %", vm.Legend[^1].Label);

        var restored = new MainViewModel(new FakeDialogService(), new FakeDocumentStore(), settings, NullLogger<MainViewModel>.Instance);
        Assert.Equal(TrackColorMode.Slope, restored.TrackColoring);

        settings.Current = settings.Current with { Map = settings.Current.Map with { TrackColoring = "42" } };
        Assert.Equal(TrackColorMode.None, new MainViewModel(new FakeDialogService(), new FakeDocumentStore(), settings, NullLogger<MainViewModel>.Instance).TrackColoring);
    }
}
