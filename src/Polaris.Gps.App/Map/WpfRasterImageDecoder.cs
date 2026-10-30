using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Polaris.Gps.Mapping.Local;

namespace Polaris.Gps.App.Map;

/// <summary>
/// Decodes calibrated images with Windows Imaging Component (JPG, PNG, BMP, GIF, TIFF; D7). Large images are decoded
/// at a reduced size (JPEG decodes directly at the smaller size), so a big scan never takes all the memory (LMAP-09).
/// Safe to call from background threads: the bitmaps are frozen and never shown.
/// </summary>
public sealed class WpfRasterImageDecoder : IRasterImageDecoder
{
    public (int Width, int Height) ReadSize(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None).Frames[0];
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            throw new LocalMapException(LocalMapError.InvalidImage, Path.GetFileName(path), innerException: ex);
        }
    }

    public RasterImage Decode(string path, long maxPixels, CancellationToken cancellationToken)
    {
        var (width, height) = ReadSize(path);
        var scale = Math.Min(1, Math.Sqrt((double)maxPixels / ((double)width * height)));
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
            if (scale < 1)
            {
                bitmap.DecodePixelWidth = Math.Max(1, (int)(width * scale));
            }

            bitmap.EndInit();
            bitmap.Freeze();
            cancellationToken.ThrowIfCancellationRequested();

            var bgra = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            bgra.Freeze();
            var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
            bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
            return new RasterImage(bgra.PixelWidth, bgra.PixelHeight, (double)bgra.PixelWidth / width, pixels);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            throw new LocalMapException(LocalMapError.InvalidImage, Path.GetFileName(path), innerException: ex);
        }
        catch (OutOfMemoryException ex)
        {
            throw new LocalMapException(LocalMapError.TooLarge, $"{width} × {height}", innerException: ex);
        }
    }

    public RasterImage Decode(byte[] data, string name)
    {
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad).Frames[0];
            var bgra = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            bgra.Freeze();
            var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
            bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
            return new RasterImage(bgra.PixelWidth, bgra.PixelHeight, 1, pixels);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            throw new LocalMapException(LocalMapError.InvalidImage, name, innerException: ex);
        }
    }
}
