using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace MyLittleSlideShow.Services;

public enum Rotation
{
    Left,
    Right
}

/// <summary>
/// Ersetzt die alte ZZZ.ImageProcessing-Klasse. System.Drawing (GDI+) ist unter
/// .NET seit Version 7 nur noch unter Windows unterstuetzt, deshalb laeuft die
/// gesamte Bildverarbeitung jetzt ueber ImageSharp - rein verwaltet und
/// identisch auf Windows und Linux.
/// </summary>
public static class ImageProcessing
{
    public static readonly string[] SupportedExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".tif", ".tiff", ".bmp", ".webp"];

    /// <summary>
    /// Laedt ein Bild von der Platte, richtet es anhand der EXIF-Orientierung aus,
    /// skaliert es auf die Zielgroesse und schaerft es optional.
    /// </summary>
    public static Bitmap LoadForDisplay(string path, int maxWidth, int maxHeight,
                                        bool sharpen, int sharpenValue,
                                        int sharpenThreshold = 0)
    {
        using var image = LoadResized(path, maxWidth, maxHeight);

        if (sharpen && sharpenValue > 0)
            UnsharpMask(image, sharpenValue, sharpenThreshold);

        return ToAvaloniaBitmap(image);
    }

    /// <summary>
    /// Laedt, richtet aus und skaliert - aber schaerft noch nicht. Fuer die
    /// Live-Vorschau der Einstellungen: das teure Dekodieren passiert einmal,
    /// danach wird pro Reglerbewegung nur noch <see cref="Render"/> gerechnet.
    /// Der Aufrufer muss das Ergebnis freigeben.
    /// </summary>
    public static Image<Bgra32> LoadResized(string path, int maxWidth, int maxHeight)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var image = ImageSharpImage.Load<Bgra32>(fs);

        try
        {
            // AutoOrient wertet alle acht EXIF-Orientierungen aus - die alte
            // Implementierung behandelte nur die Faelle 6 und 8.
            image.Mutate(ctx => ctx.AutoOrient());
            ResizeToFit(image, maxWidth, maxHeight);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <summary>Schaerft eine Kopie des bereits skalierten Bilds und liefert die Avalonia-Bitmap.</summary>
    public static Bitmap Render(Image<Bgra32> resized, bool sharpen, int sharpenValue,
                                int sharpenThreshold = 0)
    {
        if (!sharpen || sharpenValue <= 0)
            return ToAvaloniaBitmap(resized);

        using var copy = resized.Clone();
        UnsharpMask(copy, sharpenValue, sharpenThreshold);
        return ToAvaloniaBitmap(copy);
    }

    private static void ResizeToFit(Image<Bgra32> image, int maxWidth, int maxHeight)
    {
        if (maxWidth <= 0 || maxHeight <= 0) return;

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(maxWidth, maxHeight),
            Mode = ResizeMode.Max,             // Seitenverhaeltnis bleibt erhalten
            Sampler = KnownResamplers.Lanczos3 // Qualitaet vergleichbar HighQualityBicubic
        }));
    }

    /// <summary>Bild aus den eingebetteten Avalonia-Ressourcen laden.</summary>
    public static Bitmap LoadAsset(string assetPath)
    {
        // assetPath z. B. "Images/Play.png"
        var uri = new Uri($"avares://MyLittleSlideShow/Assets/{assetPath.TrimStart('/')}");
        using var stream = AssetLoader.Open(uri);
        return new Bitmap(stream);
    }

    /// <summary>Eingebettete Ressource laden, skalieren und schaerfen (Standardhintergrund).</summary>
    public static Bitmap LoadAssetProcessed(string assetPath, int maxWidth, int maxHeight,
                                            bool sharpen, int sharpenValue,
                                            int sharpenThreshold = 0)
    {
        var uri = new Uri($"avares://MyLittleSlideShow/Assets/{assetPath.TrimStart('/')}");
        using var stream = AssetLoader.Open(uri);
        using var image = ImageSharpImage.Load<Bgra32>(stream);

        ResizeToFit(image, maxWidth, maxHeight);

        if (sharpen && sharpenValue > 0)
            UnsharpMask(image, sharpenValue, sharpenThreshold);

        return ToAvaloniaBitmap(image);
    }

    /// <summary>Dreht die Datei auf der Platte und speichert sie im Originalformat.</summary>
    public static void RotateAndSaveImageFile(string imagePath, Rotation rotation)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return;

        var mode = rotation == Rotation.Right ? RotateMode.Rotate90 : RotateMode.Rotate270;

        using var image = ImageSharpImage.Load(imagePath);
        image.Mutate(ctx => ctx.Rotate(mode));

        // Nach dem Drehen der Pixel muss das EXIF-Orientierungsflag neutral sein,
        // sonst drehen andere Betrachter das Bild ein zweites Mal.
        image.Metadata.ExifProfile?.SetValue(
            SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, (ushort)1);

        var ext = Path.GetExtension(imagePath).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg")
            image.Save(imagePath, new JpegEncoder { Quality = 100 });
        else
            image.Save(imagePath);   // Encoder wird anhand der Dateiendung gewaehlt
    }

    /// <summary>Konvertiert ein beliebiges Bild nach JPEG (fuer Hintergrundbilder noetig).</summary>
    public static string ConvertToJpg(string sourcePath, string targetPath)
    {
        using var image = ImageSharpImage.Load<Rgba32>(sourcePath);
        image.Mutate(ctx => ctx.AutoOrient().BackgroundColor(Color.White));
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        image.Save(targetPath, new JpegEncoder { Quality = 95 });
        return targetPath;
    }

    /// <summary>Ermittelt den tatsaechlichen MIME-Typ anhand des Dateiinhalts, nicht der Endung.</summary>
    public static string GetMimeType(string path)
    {
        try
        {
            var format = ImageSharpImage.DetectFormat(path);
            return format.DefaultMimeType;
        }
        catch
        {
            return "image/unknown";
        }
    }

    /// <summary>Liefert die passende Dateiendung zum echten Bildformat, z. B. ".jpg".</summary>
    public static string GetCanonicalExtension(string path)
    {
        try
        {
            var format = ImageSharpImage.DetectFormat(path);
            var ext = format.FileExtensions.FirstOrDefault() ?? "bin";
            return "." + ext.ToLowerInvariant();
        }
        catch
        {
            return Path.GetExtension(path).ToLowerInvariant();
        }
    }

    /// <summary>Alle EXIF-Felder als lesbare "Name: Wert"-Zeilen.</summary>
    public static IReadOnlyList<string> ReadExif(string path)
    {
        var lines = new List<string>();
        try
        {
            var info = ImageSharpImage.Identify(path);

            lines.Add($"Abmessungen: {info.Width} x {info.Height} Pixel");

            var exif = info.Metadata.ExifProfile;
            if (exif is null)
            {
                lines.Add("Es konnten keine Exif Daten eingelesen werden.");
                return lines;
            }

            foreach (var value in exif.Values.OrderBy(v => v.Tag.ToString(), StringComparer.Ordinal))
            {
                var name = value.Tag.ToString();
                if (name.Contains("MakerNote", StringComparison.OrdinalIgnoreCase)) continue;

                var raw = value.GetValue();
                if (raw is null) continue;

                lines.Add($"{name}: {Render(raw)}");
            }
        }
        catch
        {
            lines.Add("Es konnten keine Exif Daten eingelesen werden.");
        }

        return lines;
    }

    private static string Render(object value)
    {
        if (value is Array array)
        {
            if (array.Length > 20 && array.GetType().GetElementType() == typeof(byte))
                return "0x" + Convert.ToHexString(array.Cast<byte>().ToArray());

            return string.Join(", ", array.Cast<object?>().Select(x => x?.ToString() ?? string.Empty));
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Unschaerfemaskierung, wie sie das WPF-Original gerechnet hat:
    ///
    ///     Ergebnis = Original + k * (Original - Weichzeichnung)
    ///
    /// Der alte Code kam ueber die Faltungsmatrix { 1 2 1 / 2 4 2 / 1 2 1 }
    /// (Summe 16) und die Gewichte bias = 1 - strength, factor = strength / 16
    /// bei strength = -Wert/10 auf genau diese Formel mit k = Wert / 10.
    /// Der Standardwert 25 entspricht also k = 2,5.
    ///
    /// Die Weichzeichnung ist bewusst genau dieses 3x3-Binomialfilter und nicht
    /// ImageSharps GaussianBlur: dessen Kern reicht bei gleichem Sigma ueber
    /// sieben Pixel und daempft feine Strukturen mit, die das enge 3x3-Fenster
    /// stehen laesst. Daran haengt der Detailreichtum bei kleinen Bildern.
    ///
    /// Gerechnet wird separierbar ([1 2 1] waagerecht, dann senkrecht). Die
    /// Zwischenwerte bleiben unnormalisiert als ushort stehen und werden erst
    /// ganz am Ende einmal durch 16 geteilt - ohne das wuerde die doppelte
    /// Rundung auf Byte Abweichungen von bis zu 2 Stufen gegenueber dem
    /// Original erzeugen.
    /// </summary>
    private static void UnsharpMask(Image<Bgra32> image, int sharpenValue, int threshold)
    {
        var amount = Math.Clamp(sharpenValue, 0, 99) / 10f;
        if (amount <= 0f) return;

        // "Ruhiges Schaerfen": Unterschiede unterhalb der Schwelle bleiben
        // unangetastet. Damit wird Rauschen in Himmel, Haut und anderen
        // glatten Flaechen nicht mitverstaerkt, Kanten aber weiterhin voll.
        var limit = Math.Clamp(threshold, 0, 64);

        var width = image.Width;
        var height = image.Height;
        if (width < 2 || height < 2) return;

        // Waagerechte Teilsummen, hoechstens 4 * 255 = 1020.
        var rowSumR = new ushort[width * height];
        var rowSumG = new ushort[width * height];
        var rowSumB = new ushort[width * height];

        image.ProcessPixelRows(accessor =>
        {
            // Durchgang 1: waagerecht [1 2 1]
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var offset = y * width;

                for (var x = 0; x < width; x++)
                {
                    // Am Rand wird das aeusserste Pixel gespiegelt, damit auch
                    // dort geschaerft wird. Im Original blieb ein 1 px breiter
                    // Rand unbearbeitet.
                    var left = row[x > 0 ? x - 1 : 0];
                    var mid = row[x];
                    var right = row[x < width - 1 ? x + 1 : width - 1];

                    rowSumR[offset + x] = (ushort)(left.R + 2 * mid.R + right.R);
                    rowSumG[offset + x] = (ushort)(left.G + 2 * mid.G + right.G);
                    rowSumB[offset + x] = (ushort)(left.B + 2 * mid.B + right.B);
                }
            }

            // Durchgang 2: senkrecht [1 2 1] und sofort mischen
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var above = (y > 0 ? y - 1 : 0) * width;
                var mid = y * width;
                var below = (y < height - 1 ? y + 1 : height - 1) * width;

                for (var x = 0; x < width; x++)
                {
                    var pixel = row[x];

                    row[x] = new Bgra32(
                        Mix(pixel.R, rowSumR[above + x] + 2 * rowSumR[mid + x] + rowSumR[below + x], amount, limit),
                        Mix(pixel.G, rowSumG[above + x] + 2 * rowSumG[mid + x] + rowSumG[below + x], amount, limit),
                        Mix(pixel.B, rowSumB[above + x] + 2 * rowSumB[mid + x] + rowSumB[below + x], amount, limit),
                        pixel.A);
                }
            }
        });
    }

    /// <summary>
    /// Mischt einen Kanal. <paramref name="blurSum16"/> ist die noch nicht
    /// normalisierte Summe der 3x3-Faltung (also das 16-fache des Mittelwerts).
    /// </summary>
    private static byte Mix(byte original, int blurSum16, float amount, int threshold)
    {
        var difference = original - blurSum16 / 16f;

        // Unterhalb der Schwelle bleibt der Pixel unveraendert. Der weiche
        // Uebergang (statt eines harten Schnitts) verhindert sichtbare Kanten
        // zwischen geschaerften und ungeschaerften Bereichen.
        if (threshold > 0)
        {
            var magnitude = Math.Abs(difference);
            if (magnitude <= threshold) return original;

            difference *= Math.Min(1f, (magnitude - threshold) / threshold);
        }

        var value = original + amount * difference;
        return (byte)(value < 0f ? 0 : value > 255f ? 255 : value);
    }

    /// <summary>Kopiert ImageSharp-Pixel ohne Umweg ueber PNG direkt in eine Avalonia-Bitmap.</summary>
    private static Bitmap ToAvaloniaBitmap(Image<Bgra32> image)
    {
        var writeable = new WriteableBitmap(
            new PixelSize(image.Width, image.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using (var buffer = writeable.Lock())
        {
            var destinationAddress = buffer.Address;
            var destinationStride = buffer.RowBytes;
            var rowBytes = image.Width * 4;

            image.ProcessPixelRows(accessor =>
            {
                unsafe
                {
                    var destination = (byte*)destinationAddress;

                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        fixed (Bgra32* src = row)
                        {
                            Buffer.MemoryCopy(src,
                                              destination + (long)y * destinationStride,
                                              destinationStride,
                                              rowBytes);
                        }
                    }
                }
            });
        }

        return writeable;
    }
}
