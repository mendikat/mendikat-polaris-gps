namespace Mendikat.TrailLab.Gpx;

/// <summary>
/// Reports reading progress as a fraction from 0 to 1 and checks for cancellation (NFR-02).
/// Only advances of at least 1 % are reported, so a large file does not flood the UI thread.
/// </summary>
internal sealed class LoadProgress(IProgress<double>? progress, CancellationToken cancellationToken)
{
    private const double MinimumStep = 0.01;
    private double _last = double.NegativeInfinity;

    public CancellationToken CancellationToken => cancellationToken;

    public void Report(double fraction)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (progress is null)
        {
            return;
        }

        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction - _last >= MinimumStep || (fraction >= 1 && _last < 1))
        {
            _last = fraction;
            progress.Report(fraction);
        }
    }
}

/// <summary>Read-only pass-through stream that reports the fraction of a seekable stream read so far.</summary>
internal sealed class ProgressStream(Stream inner, Action<double> onRead) : Stream
{
    private readonly long _length = inner.Length;

    public override bool CanRead => true;

    public override bool CanSeek => inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => Report(inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Report(inner.Read(buffer));

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Report(int read)
    {
        if (_length > 0)
        {
            onRead((double)inner.Position / _length);
        }

        return read;
    }
}
