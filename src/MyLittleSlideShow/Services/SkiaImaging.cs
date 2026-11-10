using SkiaSharp;

namespace MyLittleSlideShow.Services;

/// <summary>
/// Bildverarbeitung auf Basis von SkiaSharp. SkiaSharp steckt ohnehin in
/// Avalonia (gleiche Version, gleiche nativen Bibliotheken unter Windows und
/// Linux) und steht unter der MIT-Lizenz - anders als ImageSharp ab 3.2, das
/// fuer Release-Builds einen Lizenzschluessel verlangt.
///
/// Diese Klasse kennt Avalonia bewusst nicht, damit sie sich isoliert testen
/// laesst. Alle Bitmaps sind Bgra8888 mit vormultipliziertem Alpha.
/// </summary>
internal static class SkiaImaging
{
    /// <summary>
    /// Obergrenze fuer die zu dekodierende Pixelzahl (~1 GB Speicher).
    /// Schuetzt vor manipulierten Dateien, die riesige Abmessungen angeben.
    /// </summary>
    private const long MaxDecodedPixels = 250_000_000;

    public static readonly SKColorType ColorType = SKColorType.Bgra8888;
    public static readonly SKAlphaType AlphaType = SKAlphaType.Premul;

    // ---------------------------------------------------------- Dekodieren

    /// <summary>Oeffnet eine Bilddatei. Der Aufrufer gibt Codec und Daten frei.</summary>
    public static (SKCodec Codec, SKData Data) Open(byte[] bytes)
    {
        var data = SKData.CreateCopy(bytes);
        var codec = SKCodec.Create(data);
        if (codec is null)
        {
            data.Dispose();
            throw new InvalidDataException("Unbekanntes oder beschädigtes Bildformat.");
        }
        return (codec, data);
    }

    /// <summary>
    /// Dekodiert, richtet anhand der EXIF-Orientierung aus und skaliert so,
    /// dass das Bild in <paramref name="maxWidth"/> x <paramref name="maxHeight"/>
    /// passt (Seitenverhaeltnis bleibt, kleine Bilder werden vergroessert -
    /// wie ResizeMode.Max bei ImageSharp). 0 bei einer Grenze = Originalgroesse.
    /// </summary>
    public static SKBitmap LoadFitted(SKCodec codec, int maxWidth, int maxHeight)
    {
        var oriented = DecodeOriented(codec, maxWidth, maxHeight);
        var fitted = ResizeToFit(oriented, maxWidth, maxHeight);
        if (!ReferenceEquals(fitted, oriented)) oriented.Dispose();
        return fitted;
    }

    /// <summary>
    /// Dekodiert das (erste) Bild und wendet die EXIF-Orientierung an. Ist ein
    /// Zielrahmen angegeben, dekodiert JPEG direkt verkleinert (1/2, 1/4, 1/8),
    /// aber nie kleiner als das Doppelte des Ziels - die eigentliche
    /// Verkleinerung macht danach <see cref="ResizeToFit"/> in guter Qualitaet.
    /// </summary>
    /// <param name="toSrgb">
    /// Farben anhand des eingebetteten ICC-Profils nach sRGB umrechnen. Noetig
    /// fuer die Anzeige: Fotos in Display P3 (iPhone) oder Adobe RGB wirken
    /// sonst blass. Beim Drehen aus bleiben, weil dort das Originalprofil
    /// wieder in die Datei geschrieben wird.
    /// </param>
    public static SKBitmap DecodeOriented(SKCodec codec, int boxWidth = 0, int boxHeight = 0,
                                          bool toSrgb = true)
    {
        var info = codec.Info;
        var origin = codec.EncodedOrigin;
        var size = new SKSizeI(info.Width, info.Height);

        if (boxWidth > 0 && boxHeight > 0 && info.Width > 0 && info.Height > 0)
        {
            // Der Rahmen gilt fuer das ausgerichtete Bild, dekodiert wird roh.
            var swap = SwapsAxes(origin);
            var bw = swap ? boxHeight : boxWidth;
            var bh = swap ? boxWidth : boxHeight;
            var fit = Math.Min((double)bw / info.Width, (double)bh / info.Height);

            foreach (var scale in new[] { 0.125f, 0.25f, 0.5f })
            {
                if (scale < fit * 2) continue;
                var dims = codec.GetScaledDimensions(scale);
                if (dims.Width >= Math.Ceiling(info.Width * fit * 2) &&
                    dims.Height >= Math.Ceiling(info.Height * fit * 2))
                {
                    size = dims;
                    break;
                }
            }
        }

        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * size.Height > MaxDecodedPixels)
            throw new InvalidDataException($"Bild ist zu groß oder ungültig ({size.Width} x {size.Height}).");

        var target = new SKImageInfo(size.Width, size.Height, ColorType, AlphaType);
        var bitmap = new SKBitmap(target);
        if (bitmap.GetPixels() == IntPtr.Zero)
        {
            bitmap.Dispose();
            throw new OutOfMemoryException($"Kein Speicher für {size.Width} x {size.Height} Pixel.");
        }

        // Die Bitmap selbst bleibt ohne Farbraum ("sRGB angenommen"), damit
        // spaeteres Zeichnen und Skalieren nichts mehr umrechnet. Nur dem
        // Dekoder wird sRGB als Ziel genannt - er rechnet vom eingebetteten
        // Profil um; Dateien ohne Profil gelten als sRGB und bleiben gleich.
        var decodeInfo = toSrgb ? target.WithColorSpace(SKColorSpace.CreateSrgb()) : target;
        var result = codec.GetPixels(decodeInfo, bitmap.GetPixels());

        // Abgeschnittene Dateien zeigen, was lesbar ist - wie ein Bildbetrachter.
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new InvalidDataException($"Bild konnte nicht dekodiert werden ({result}).");
        }

        var oriented = Transform(bitmap, origin);
        if (!ReferenceEquals(oriented, bitmap)) bitmap.Dispose();
        return oriented;
    }

    // ------------------------------------------------------------ Geometrie

    public static bool SwapsAxes(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
               or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// Wendet eine EXIF-Orientierung an. RightTop entspricht einer Drehung um
    /// 90 Grad im Uhrzeigersinn, LeftBottom 90 Grad dagegen. Liefert bei
    /// TopLeft dieselbe Instanz zurueck.
    /// </summary>
    public static SKBitmap Transform(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;

        // Abbildung Quellpixel (x, y) -> Ziel (x', y'):
        //   x' = ScaleX * x + SkewX * y + TransX
        //   y' = SkewY  * x + ScaleY * y + TransY
        SKMatrix matrix;
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:    matrix = Affine(-1, 0, w, 0, 1, 0); break;   // waagerecht spiegeln
            case SKEncodedOrigin.BottomRight: matrix = Affine(-1, 0, w, 0, -1, h); break;  // 180 Grad
            case SKEncodedOrigin.BottomLeft:  matrix = Affine(1, 0, 0, 0, -1, h); break;   // senkrecht spiegeln
            case SKEncodedOrigin.LeftTop:     matrix = Affine(0, 1, 0, 1, 0, 0); break;    // transponieren
            case SKEncodedOrigin.RightTop:    matrix = Affine(0, -1, h, 1, 0, 0); break;   // 90 Grad rechts
            case SKEncodedOrigin.RightBottom: matrix = Affine(0, -1, h, -1, 0, w); break;  // transversal
            case SKEncodedOrigin.LeftBottom:  matrix = Affine(0, 1, 0, -1, 0, w); break;   // 90 Grad links
            default: return source;
        }

        var swap = SwapsAxes(origin);
        var result = new SKBitmap(new SKImageInfo(
            swap ? source.Height : source.Width,
            swap ? source.Width : source.Height,
            ColorType, AlphaType));

        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(matrix);
        // Ganzzahlige Abbildung: jedes Pixel landet exakt auf einem Pixel,
        // gefiltert wird nichts.
        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return result;
    }

    private static SKMatrix Affine(float scaleX, float skewX, float transX,
                                   float skewY, float scaleY, float transY) =>
        new(scaleX, skewX, transX, skewY, scaleY, transY, 0, 0, 1);

    /// <summary>
    /// Skaliert auf die groesste Groesse, die in den Rahmen passt. Verkleinert
    /// wird erst in exakten Halbierungen (je 2x2 Pixel gemittelt, kein
    /// Aliasing), dann in einem letzten bikubischen Schritt auf das Ziel -
    /// das kommt Lanczos3 sehr nahe. Liefert die Quelle selbst zurueck, wenn
    /// sie schon passt.
    /// </summary>
    public static SKBitmap ResizeToFit(SKBitmap source, int maxWidth, int maxHeight)
    {
        if (maxWidth <= 0 || maxHeight <= 0) return source;

        var ratio = Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * ratio));
        var height = Math.Max(1, (int)Math.Round(source.Height * ratio));
        if (width == source.Width && height == source.Height) return source;

        var current = source;
        while (current.Width >= width * 2 && current.Height >= height * 2)
        {
            var half = Scale(current, current.Width / 2, current.Height / 2, SKFilterQuality.Low);
            if (!ReferenceEquals(current, source)) current.Dispose();
            current = half;
        }

        var result = Scale(current, width, height, SKFilterQuality.High);
        if (!ReferenceEquals(current, source)) current.Dispose();
        return result;
    }

    private static SKBitmap Scale(SKBitmap source, int width, int height, SKFilterQuality quality)
    {
        var result = new SKBitmap(new SKImageInfo(width, height, ColorType, AlphaType));
        using var destination = result.PeekPixels();
        if (!source.ScalePixels(destination, quality))
        {
            result.Dispose();
            throw new InvalidOperationException("Skalieren fehlgeschlagen.");
        }
        return result;
    }

    // --------------------------------------------------------------- Schaerfen

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
    /// Die Weichzeichnung ist bewusst genau dieses 3x3-Binomialfilter und kein
    /// Gauss mit groesserem Kern: das enge Fenster laesst feine Strukturen
    /// stehen. Daran haengt der Detailreichtum bei kleinen Bildern.
    ///
    /// Gerechnet wird separierbar ([1 2 1] waagerecht, dann senkrecht). Die
    /// Zwischenwerte bleiben unnormalisiert als ushort stehen und werden erst
    /// ganz am Ende einmal durch 16 geteilt - ohne das wuerde die doppelte
    /// Rundung auf Byte Abweichungen von bis zu 2 Stufen erzeugen.
    ///
    /// Arbeitet direkt auf BGRA-Pixeln (vormultipliziertes Alpha) im
    /// uebergebenen Speicher.
    /// </summary>
    public static unsafe void UnsharpMask(byte* pixels, int width, int height, int stride,
                                          int sharpenValue, int threshold)
    {
        var amount = Math.Clamp(sharpenValue, 0, 99) / 10f;
        if (amount <= 0f) return;

        // "Ruhiges Schaerfen": Unterschiede unterhalb der Schwelle bleiben
        // unangetastet. Damit wird Rauschen in Himmel, Haut und anderen
        // glatten Flaechen nicht mitverstaerkt, Kanten aber weiterhin voll.
        var limit = Math.Clamp(threshold, 0, 64);

        if (width < 2 || height < 2) return;

        // Waagerechte Teilsummen, hoechstens 4 * 255 = 1020.
        var count = width * height;
        var sumB = new ushort[count];
        var sumG = new ushort[count];
        var sumR = new ushort[count];

        // Durchgang 1: waagerecht [1 2 1]. Am Rand wird das aeusserste Pixel
        // wiederholt, damit auch dort geschaerft wird.
        for (var y = 0; y < height; y++)
        {
            var row = pixels + (long)y * stride;
            var offset = y * width;

            for (var x = 0; x < width; x++)
            {
                var left = row + (x > 0 ? x - 1 : 0) * 4;
                var mid = row + x * 4;
                var right = row + (x < width - 1 ? x + 1 : width - 1) * 4;

                sumB[offset + x] = (ushort)(left[0] + 2 * mid[0] + right[0]);
                sumG[offset + x] = (ushort)(left[1] + 2 * mid[1] + right[1]);
                sumR[offset + x] = (ushort)(left[2] + 2 * mid[2] + right[2]);
            }
        }

        // Durchgang 2: senkrecht [1 2 1] und sofort mischen. Die Summen
        // stammen alle aus Durchgang 1, das Schreiben in die Zeile ist also
        // unkritisch.
        for (var y = 0; y < height; y++)
        {
            var row = pixels + (long)y * stride;
            var above = (y > 0 ? y - 1 : 0) * width;
            var mid = y * width;
            var below = (y < height - 1 ? y + 1 : height - 1) * width;

            for (var x = 0; x < width; x++)
            {
                var p = row + x * 4;
                var alpha = p[3];   // vormultipliziert: kein Kanal darf ueber Alpha liegen

                p[0] = Mix(p[0], sumB[above + x] + 2 * sumB[mid + x] + sumB[below + x], amount, limit, alpha);
                p[1] = Mix(p[1], sumG[above + x] + 2 * sumG[mid + x] + sumG[below + x], amount, limit, alpha);
                p[2] = Mix(p[2], sumR[above + x] + 2 * sumR[mid + x] + sumR[below + x], amount, limit, alpha);
            }
        }
    }

    /// <summary>
    /// Mischt einen Kanal. <paramref name="blurSum16"/> ist die noch nicht
    /// normalisierte Summe der 3x3-Faltung (also das 16-fache des Mittelwerts).
    /// </summary>
    private static byte Mix(byte original, int blurSum16, float amount, int threshold, byte max)
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
        return (byte)(value < 0f ? 0 : value > max ? max : value);
    }

    // ------------------------------------------------------------- Kodieren

    public static byte[] EncodeJpeg(SKBitmap bitmap, int quality, bool fullChroma)
    {
        using var pixmap = bitmap.PeekPixels();
        var options = new SKJpegEncoderOptions(
            quality,
            fullChroma ? SKJpegEncoderDownsample.Downsample444 : SKJpegEncoderDownsample.Downsample420,
            SKJpegEncoderAlphaOption.Ignore);
        using var data = pixmap.Encode(options)
                         ?? throw new InvalidOperationException("JPEG-Kodierung fehlgeschlagen.");
        return data.ToArray();
    }

    public static byte[] EncodePng(SKBitmap bitmap)
    {
        using var pixmap = bitmap.PeekPixels();
        using var data = pixmap.Encode(SKPngEncoderOptions.Default)
                         ?? throw new InvalidOperationException("PNG-Kodierung fehlgeschlagen.");
        return data.ToArray();
    }

    public static byte[] EncodeWebpLossless(SKBitmap bitmap)
    {
        using var pixmap = bitmap.PeekPixels();
        using var data = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100))
                         ?? throw new InvalidOperationException("WebP-Kodierung fehlgeschlagen.");
        return data.ToArray();
    }

    /// <summary>
    /// Schreibt eine 24-Bit-BMP (Skia kann BMP nur lesen). Transparenz wird
    /// auf Weiss gelegt, BMP-Betrachter ignorieren Alpha ohnehin meist.
    /// </summary>
    public static unsafe byte[] EncodeBmp(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var rowSize = (width * 3 + 3) & ~3;
        var imageSize = rowSize * height;
        const int headerSize = 14 + 40;

        var output = new byte[headerSize + imageSize];
        var span = output.AsSpan();

        // BITMAPFILEHEADER
        span[0] = (byte)'B';
        span[1] = (byte)'M';
        BitConverter.TryWriteBytes(span[2..], output.Length);
        BitConverter.TryWriteBytes(span[10..], headerSize);

        // BITMAPINFOHEADER
        BitConverter.TryWriteBytes(span[14..], 40);
        BitConverter.TryWriteBytes(span[18..], width);
        BitConverter.TryWriteBytes(span[22..], height);     // positiv = von unten nach oben
        BitConverter.TryWriteBytes(span[26..], (short)1);   // Ebenen
        BitConverter.TryWriteBytes(span[28..], (short)24);  // Bit pro Pixel
        BitConverter.TryWriteBytes(span[34..], imageSize);
        BitConverter.TryWriteBytes(span[38..], 3780);       // 96 dpi in Pixel pro Meter
        BitConverter.TryWriteBytes(span[42..], 3780);

        var source = (byte*)bitmap.GetPixels();
        var stride = bitmap.RowBytes;

        for (var y = 0; y < height; y++)
        {
            var src = source + (long)(height - 1 - y) * stride;
            var dst = headerSize + y * rowSize;

            for (var x = 0; x < width; x++)
            {
                var p = src + x * 4;
                var background = 255 - p[3];   // vormultipliziert: Weiss darunterlegen
                output[dst + x * 3 + 0] = (byte)(p[0] + background);
                output[dst + x * 3 + 1] = (byte)(p[1] + background);
                output[dst + x * 3 + 2] = (byte)(p[2] + background);
            }
        }

        return output;
    }

    /// <summary>Legt transparente Bereiche auf Weiss (fuer JPEG).</summary>
    public static SKBitmap FlattenOnWhite(SKBitmap source)
    {
        var result = new SKBitmap(new SKImageInfo(source.Width, source.Height, ColorType, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return result;
    }
}
