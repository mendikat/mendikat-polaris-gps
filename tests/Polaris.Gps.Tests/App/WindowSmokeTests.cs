using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Windows;
using Polaris.Gps.App;
using Polaris.Gps.App.Map;
using Polaris.Gps.App.Services;
using Polaris.Gps.App.ViewModels;
using Polaris.Gps.App.Views;
using Polaris.Gps.Domain.Documents;
using Polaris.Gps.Domain.Elevation;
using Polaris.Gps.Domain.Geo;
using Polaris.Gps.Domain.Model;
using Polaris.Gps.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Polaris.Gps.Tests.App;

/// <summary>
/// Builds every secondary window with its view model. XAML binding mistakes (e.g. a two-way binding to a read-only
/// property, found in increment 7) throw while the window is created, so these tests catch them without showing anything.
/// </summary>
public sealed class WindowSmokeTests
{
    internal static Track SampleTrack(double latitude)
    {
        var segment = new TrackSegment();
        segment.Points.AddRange(Enumerable.Range(0, 3).Select(i => new TrailPoint(new GeoPosition(latitude + i * 0.001, -1.6))));
        var track = new Track { Name = "T" };
        track.Segments.Add(segment);
        return track;
    }

    internal static LayerItemViewModel TrackLayer()
    {
        var data = new TrailData();
        var segment = new TrackSegment();
        segment.Points.AddRange(Enumerable.Range(0, 5).Select(i => new TrailPoint(new GeoPosition(42 + i * 0.001, -1.6)) { Elevation = 500 + i }));
        var track = new Track { Name = "T" };
        track.Segments.Add(segment);
        data.Tracks.Add(track);
        return new DocumentViewModel(new TrailDocument(@"C:\data\t.gpx", DocumentFormat.Gpx, data)).Layers[0];
    }

    /// <summary>WPF needs a single-threaded apartment; exceptions are rethrown on the test thread.</summary>
    internal static void OnStaThread(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    [Fact]
    public void Secondary_windows_can_be_created_with_their_view_models()
    {
        OnStaThread(() =>
        {
            var layer = TrackLayer();
            var document = new DocumentViewModel(new TrailDocument(@"C:\data\p.gpx", DocumentFormat.Gpx));
            Window[] windows =
            [
                new SimplifyWindow(new SimplifyViewModel(layer)),
                new CleanWindow(new CleanViewModel(layer)),
                new JoinTracksWindow(new JoinTracksViewModel([(SampleTrack(42.8), "a.gpx"), (SampleTrack(42.9), "b.gpx")])),
                new TimesWindow(new TimesViewModel(layer)),
                new DemWindow(new DemViewModel(layer, _ => new NoDataSource())),
                new ProfileWindow(new ProfileViewModel(document, layer)),
                new ConvertWindow(new ConvertViewModel(new FakeDialogService(), [])),
            ];
            foreach (var window in windows)
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Window_icon_is_an_embedded_resource_that_wpf_can_decode()
    {
        // MainWindow uses Icon="Assets/icon.ico": WPF looks it up in the assembly's "g" resources at run time.
        // Without <Resource Include="Assets\icon.ico" /> the app failed on start-up.
        OnStaThread(() =>
        {
            var resources = new System.Resources.ResourceManager("PolarisGps.g", typeof(Polaris.Gps.App.App).Assembly);
            using var stream = resources.GetStream("assets/icon.ico");
            Assert.NotNull(stream);

            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

            Assert.NotEmpty(decoder.Frames);
        });
    }

    [Fact]
    public void Main_window_without_changes_closes_at_the_first_attempt()
    {
        // Regression: Close() was called again while the window was closing, which throws (log of 2026-10-25).
        OnStaThread(() =>
        {
            var window = CreateMainWindow();
            var closed = false;
            window.Closed += (_, _) => closed = true;

            window.Close();

            Assert.True(closed);
        });
    }

    [Fact]
    public void Main_window_with_changes_stays_open_when_the_user_cancels()
    {
        OnStaThread(() =>
        {
            var dialogs = new FakeDialogService { SaveChangesAnswer = SaveChangesChoice.Cancel };
            var window = CreateMainWindow(dialogs);
            var viewModel = (MainViewModel)window.DataContext;
            viewModel.NewGpx();
            viewModel.ActiveDocument!.Document.MarkChanged();
            var closed = false;
            window.Closed += (_, _) => closed = true;

            window.Close();

            Assert.False(closed);
            Assert.Single(dialogs.SaveChangesQuestions);

            dialogs.SaveChangesAnswer = SaveChangesChoice.Discard;
            window.Close();

            Assert.True(closed);
        });
    }

    internal static MainWindow CreateMainWindow(FakeDialogService? dialogs = null)
    {
        var viewModel = new MainViewModel(
            dialogs ?? new FakeDialogService(), new FakeDocumentStore(), new FakeSettingsStore(), NullLogger<MainViewModel>.Instance);
        var httpClient = new HttpClient(new OfflineHandler());
        return new MainWindow(viewModel, map => new MapController(map, httpClient, NullLogger<MapController>.Instance));
    }

    /// <summary>The tests never reach the network.</summary>
    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    internal sealed class NoDataSource : IElevationSource
    {
        public string Name => "none";

        public string VerticalDatum => "none";

        public string Attribution => "none";

        public Task<IReadOnlyList<double?>> GetElevationsAsync(IReadOnlyList<GeoPosition> positions, IProgress<double>? progress, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<double?>>(positions.Select(_ => (double?)null).ToList());
    }
}
