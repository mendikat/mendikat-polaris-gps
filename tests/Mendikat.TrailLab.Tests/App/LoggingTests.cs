using Mendikat.TrailLab.App.Logging;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.Tests.App;

public sealed class LoggingTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("traillab-logs-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [Trait("Req", "NFR-06")]
    [InlineData(@"Could not find file 'C:\Users\ana\Documents\ruta.gpx'.", @"Could not find file '%USERPROFILE%\Documents\ruta.gpx'.")]
    [InlineData(@"c:\users\ANA\x.gpx", @"%USERPROFILE%\x.gpx")]
    [InlineData(@"D:\tracks\x.gpx", @"D:\tracks\x.gpx")]
    public void Redactor_hides_the_user_profile_path(string input, string expected)
    {
        var redactor = new PathRedactor(@"C:\Users\ana\");

        Assert.Equal(expected, redactor.Redact(input));
    }

    [Fact]
    [Trait("Req", "NFR-06")]
    public void File_logger_writes_redacted_lines_to_a_daily_file()
    {
        var clock = new DateTimeOffset(2026, 10, 24, 18, 30, 0, TimeSpan.FromHours(2));
        using var provider = new FileLoggerProvider(_directory, new PathRedactor(@"C:\Users\ana"), () => clock);
        var logger = provider.CreateLogger("Test");

        logger.LogInformation("Opened {Path}", @"C:\Users\ana\ruta.gpx");
        logger.LogDebug("Not written");

        var text = File.ReadAllText(Path.Combine(_directory, "traillab-20261024.log"));
        Assert.Contains(@"[Information] Test: Opened %USERPROFILE%\ruta.gpx", text);
        Assert.DoesNotContain("ana", text);
        Assert.DoesNotContain("Not written", text);
    }

    [Fact]
    [Trait("Req", "NFR-06")]
    public void Only_the_most_recent_log_files_are_kept()
    {
        for (var day = 1; day <= 15; day++)
        {
            File.WriteAllText(Path.Combine(_directory, $"traillab-202610{day:00}.log"), "x");
        }

        using var provider = new FileLoggerProvider(_directory, new PathRedactor(@"C:\Users\ana"));

        var remaining = Directory.GetFiles(_directory).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(FileLoggerProvider.RetainedFiles, remaining.Count);
        Assert.Equal("traillab-20261006.log", remaining[0]);
    }
}
