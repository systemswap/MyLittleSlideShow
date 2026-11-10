using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace MyLittleSlideShow.Services;

public enum Rotation
{
    Left,
    Right
}

/// <summary>
/// Bildfunktionen fuer die Oberflaeche. Gerechnet wird mit SkiaSharp (siehe
/// <see cref="SkiaImaging"/>), Metadaten liest <see cref="ImageMetadataReader"/>.
/// Beides laeuft identisch unter Windows und Linux.
/// </summary>
public static class ImageProcessing
{
    /// <summary>
    /// TIFF fehlt seit 2.1.1: Skia dekodiert es nicht. Die TIFF-Decoder waren
    /// zudem die Quelle der meisten ImageSharp-Sicherheitsluecken.
    /// </summary>
    public static readonly string[] SupportedExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"];

    /// <summary>
    /// Laedt ein Bild von der Platte, richtet es anhand der EXIF-Orientierung aus,
    /// rechnet die Farben nach sRGB um, skaliert es auf die Zielgroesse und
    /// schaerft es optional.
    /// </summary>
    /// <param name="maxWidth">Zielbreite in physischen Bildschirmpixeln.</param>
    /// <param name="maxHeight">Zielhoehe in physischen Bildschirmpixeln.</param>
    /// <param name="scaling">Bildschirmskalierung (1 = 100 %, 1.5 = 150 %).</param>
    public static Bitmap LoadForDisplay(string path, int maxWidth, int maxHeight,
                                        bool sharpen, int sharpenValue,
                                        int sharpenThreshold = 0, double scaling = 1)
    {
        using var resized = LoadResized(path, maxWidth, maxHeight);
        return Render(resized, sharpen, sharpenValue, sharpenThreshold, scaling);
    }

    /// <summary>
    /// Laedt, richtet aus und skaliert - aber schaerft noch nicht. Fuer die
    /// Live-Vorschau der Einstellungen: das teure Dekodieren passiert einmal,
    /// danach wird pro Reglerbewegung nur noch <see cref="Render"/> gerechnet.
    /// Der Aufrufer muss das Ergebnis freigeben.
    /// </summary>
    public static SKBitmap LoadResized(string path, int maxWidth, int maxHeight)
    {
        var (codec, data) = SkiaImaging.Open(File.ReadAllBytes(path));
        using (data)
        using (codec)
            return SkiaImaging.LoadFitted(codec, maxWidth, maxHeight);
    }

    /// <summary>
    /// Kopiert das skalierte Bild in eine Avalonia-Bitmap und schaerft es dort.
    /// Die Bitmap bekommt 96 dpi x Skalierung: so zeigt Avalonia jedes Pixel
    /// auf genau einem Bildschirmpixel, auch bei 125 % oder 150 %.
    /// </summary>
    public static Bitmap Render(SKBitmap resized, bool sharpen, int sharpenValue,
                                int sharpenThreshold = 0, double scaling = 1)
        => ToAvaloniaBitmap(resized, sharpen ? sharpenValue : 0, sharpenThreshold, scaling);

    /// <summary>Bild aus den eingebetteten Avalonia-Ressourcen laden.</summary>
    public static Bitmap LoadAsset(string assetPath)
    {
        // assetPath z. B. "Images/Play.png"
        using var stream = AssetLoader.Open(AssetUri(assetPath));
        return new Bitmap(stream);
    }

    /// <summary>Eingebettete Ressource laden, skalieren und schaerfen (Standardhintergrund).</summary>
    public static Bitmap LoadAssetProcessed(string assetPath, int maxWidth, int maxHeight,
                                            bool sharpen, int sharpenValue,
                                            int sharpenThreshold = 0, double scaling = 1)
    {
        using var stream = AssetLoader.Open(AssetUri(assetPath));
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var (codec, data) = SkiaImaging.Open(buffer.ToArray());
        using (data)
        using (codec)
        using (var resized = SkiaImaging.LoadFitted(codec, maxWidth, maxHeight))
            return Render(resized, sharpen, sharpenValue, sharpenThreshold, scaling);
    }

    private static Uri AssetUri(string assetPath) =>
        new($"avares://MyLittleSlideShow/Assets/{assetPath.TrimStart('/')}");

    /// <summary>
    /// Dreht die Datei auf der Platte um 90 Grad und speichert sie im
    /// Originalformat. Bei JPEG bleiben EXIF und Farbprofil erhalten.
    /// GIF kann Skia nicht schreiben - dann passiert nichts und es kommt
    /// false zurueck.
    /// </summary>
    public static bool RotateAndSaveImageFile(string imagePath, Rotation rotation)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return false;

        var original = File.ReadAllBytes(imagePath);
        var (codec, data) = SkiaImaging.Open(original);
        using (data)
        using (codec)
        {
            var format = codec.EncodedFormat;
            if (format is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png
                               or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Bmp))
                return false;

            // Erst die bisherige EXIF-Orientierung in die Pixel uebernehmen,
            // dann um 90 Grad drehen. So ist das Ergebnis immer genau eine
            // Vierteldrehung gegenueber dem, was der Nutzer gesehen hat.
            // Ohne Farbumrechnung: das ICC-Profil wird unveraendert mit uebernommen.
            using var oriented = SkiaImaging.DecodeOriented(codec, toSrgb: false);
            using var rotated = SkiaImaging.Transform(oriented,
                rotation == Rotation.Right ? SKEncodedOrigin.RightTop : SKEncodedOrigin.LeftBottom);

            var encoded = format switch
            {
                SKEncodedImageFormat.Jpeg => JpegMetadata.CarryOver(
                    original, SkiaImaging.EncodeJpeg(rotated, quality: 100, fullChroma: true)),
                SKEncodedImageFormat.Png => SkiaImaging.EncodePng(rotated),
                SKEncodedImageFormat.Webp => SkiaImaging.EncodeWebpLossless(rotated),
                _ => SkiaImaging.EncodeBmp(rotated)
            };

            WriteReplacing(imagePath, encoded);
            return true;
        }
    }

    /// <summary>
    /// Schreibt erst in eine Nachbardatei und tauscht dann aus: bricht etwas
    /// ab, bleibt das Originalfoto unversehrt. File.Replace erhaelt unter
    /// Windows Erstellungsdatum und Attribute der Originaldatei.
    /// </summary>
    private static void WriteReplacing(string path, byte[] content)
    {
        var temp = path + ".mlss.tmp";
        File.WriteAllBytes(temp, content);
        try
        {
            File.Replace(temp, path, destinationBackupFileName: null);
        }
        catch (PlatformNotSupportedException)
        {
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>Konvertiert ein beliebiges Bild nach JPEG (fuer Hintergrundbilder noetig).</summary>
    public static string ConvertToJpg(string sourcePath, string targetPath)
    {
        var (codec, data) = SkiaImaging.Open(File.ReadAllBytes(sourcePath));
        using (data)
        using (codec)
        using (var oriented = SkiaImaging.DecodeOriented(codec))
        using (var flat = SkiaImaging.FlattenOnWhite(oriented))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllBytes(targetPath, SkiaImaging.EncodeJpeg(flat, quality: 95, fullChroma: false));
        }
        return targetPath;
    }

    /// <summary>
    /// Kopiert die Skia-Pixel ohne Umweg ueber PNG direkt in eine
    /// Avalonia-Bitmap und schaerft dort im Speicher.
    /// </summary>
    private static unsafe Bitmap ToAvaloniaBitmap(SKBitmap source, int sharpenValue, int sharpenThreshold,
                                                  double scaling)
    {
        var converted = source.ColorType == SkiaImaging.ColorType && source.AlphaType == SkiaImaging.AlphaType
            ? null
            : source.Copy(SkiaImaging.ColorType);
        var pixels = converted ?? source;

        try
        {
            var writeable = new WriteableBitmap(
                new PixelSize(pixels.Width, pixels.Height),
                new Vector(96 * scaling, 96 * scaling),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using var buffer = writeable.Lock();

            var source0 = (byte*)pixels.GetPixels();
            var destination0 = (byte*)buffer.Address;
            var rowBytes = pixels.Width * 4;

            for (var y = 0; y < pixels.Height; y++)
            {
                Buffer.MemoryCopy(source0 + (long)y * pixels.RowBytes,
                                  destination0 + (long)y * buffer.RowBytes,
                                  buffer.RowBytes,
                                  rowBytes);
            }

            if (sharpenValue > 0)
                SkiaImaging.UnsharpMask(destination0, pixels.Width, pixels.Height,
                                        buffer.RowBytes, sharpenValue, sharpenThreshold);

            return writeable;
        }
        finally
        {
            converted?.Dispose();
        }
    }
}
