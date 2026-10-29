namespace Mendikat.TrailLab.Mapping.Local;

/// <summary>
/// A decoded image in memory, 32-bit BGRA (not premultiplied), row 0 at the top. A large image may have been
/// decoded at a smaller size: <see cref="Scale"/> is decoded pixels per original pixel (1 = full size; LMAP-09).
/// </summary>
public sealed class RasterImage
{
    public RasterImage(int width, int height, double scale, byte[] bgra)
    {
        if (width <= 0 || height <= 0 || bgra.Length != width * height * 4)
        {
            throw new ArgumentException("Pixel buffer does not match the size.", nameof(bgra));
        }

        Width = width;
        Height = height;
        Scale = scale;
        Bgra = bgra;
    }

    public int Width { get; }

    public int Height { get; }

    public double Scale { get; }

    public byte[] Bgra { get; }
}

/// <summary>Decodes calibrated images (JPG, PNG, BMP, GIF, TIFF). Implemented with Windows imaging in the application.</summary>
public interface IRasterImageDecoder
{
    /// <summary>Pixel size of the image, reading only its header.</summary>
    /// <exception cref="LocalMapException">Not a decodable image.</exception>
    (int Width, int Height) ReadSize(string path);

    /// <summary>
    /// Decodes the image, at a reduced size if it has more than <paramref name="maxPixels"/> pixels, so that a huge
    /// scan never takes all the memory (LMAP-09).
    /// </summary>
    /// <exception cref="LocalMapException">Not a decodable image.</exception>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    RasterImage Decode(string path, long maxPixels, CancellationToken cancellationToken);

    /// <summary>Decodes a small image held in memory at full size (a JPEG tile of an RMAP).</summary>
    /// <exception cref="LocalMapException">Not a decodable image; <paramref name="name"/> is the detail.</exception>
    RasterImage Decode(byte[] data, string name);
}
