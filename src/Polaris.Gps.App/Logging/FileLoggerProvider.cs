using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.Logging;

/// <summary>
/// Minimal daily-rotating file logger (NFR-06). One file per day, the most recent
/// <see cref="RetainedFiles"/> are kept. Text passes through <see cref="PathRedactor"/>.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const int RetainedFiles = 10;
    private const string FilePrefix = "polarisgps-";

    private readonly string _directory;
    private readonly PathRedactor _redactor;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Lock _sync = new();

    public FileLoggerProvider(string directory, PathRedactor redactor, Func<DateTimeOffset>? clock = null)
    {
        _directory = directory;
        _redactor = redactor;
        _clock = clock ?? (() => DateTimeOffset.Now);
        Directory.CreateDirectory(_directory);
        DeleteOldFiles();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = _clock();
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(level).Append("] ")
            .Append(category).Append(": ")
            .Append(message);
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        var text = _redactor.Redact(line.ToString()) + Environment.NewLine;
        var path = Path.Combine(_directory, $"{FilePrefix}{now:yyyyMMdd}.log");
        lock (_sync)
        {
            try
            {
                File.AppendAllText(path, text, Encoding.UTF8);
            }
            catch (IOException ex)
            {
                // Logging must never break the application; report to the debugger output only.
                System.Diagnostics.Debug.WriteLine($"Log write failed: {ex.Message}");
            }
        }
    }

    private void DeleteOldFiles()
    {
        var oldFiles = new DirectoryInfo(_directory)
            .GetFiles(FilePrefix + "*.log")
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(RetainedFiles);
        foreach (var file in oldFiles)
        {
            try
            {
                file.Delete();
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Old log not deleted: {ex.Message}");
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
