using System.Globalization;
using System.Text;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileType;
using MetadataExtractor.Formats.Icc;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Xmp;
using MxDirectory = MetadataExtractor.Directory;
using MxReader = MetadataExtractor.ImageMetadataReader;

namespace MyLittleSlideShow.Services;

/// <summary>
/// Liest die Metadaten einer Bilddatei fuer das Info-Fenster: EXIF (mit GPS
/// und Herstellerdaten), IPTC, XMP und das ICC-Profil. Einzige Stelle, die
/// MetadataExtractor benutzt; der Rest der App arbeitet mit <see cref="ImageMetadata"/>.
///
/// Uebernommen aus ImageConverter (ImageConverter.Core/Metadata/ImageMetadataReader.cs),
/// angepasst: deutsche Texte direkt statt Uebersetzungsfunktion, Bildmasse
/// ueber Skia statt ImageMagick.
/// </summary>
/// <remarks>
/// Metadaten stammen aus Dateien unbekannter Herkunft, deshalb ist das Ergebnis
/// begrenzt: hoechstens <see cref="MaxTags"/> Eintraege, Werte auf
/// <see cref="MaxValueLength"/> Zeichen gekuerzt, Steuerzeichen ersetzt. Lesen
/// wirft nie; eine beschaedigte Datei liefert, was lesbar war, plus
/// <see cref="ImageMetadata.Problem"/>. Nichts wird protokolliert oder gesendet.
/// </remarks>
public static class ImageMetadataReader
{
    /// <summary>Hoechstens so viele Eintraege (z. B. riesige Herstellerdaten werden gekappt).</summary>
    public const int MaxTags = 3000;

    /// <summary>Laengster angezeigter Wert; laengere enden mit Auslassungszeichen.</summary>
    public const int MaxValueLength = 400;

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    /// <summary>Liest alles Lesbare aus der Datei.</summary>
    public static ImageMetadata Read(string path)
    {
        IReadOnlyList<MxDirectory> directories;
        string? problem = null;
        try
        {
            directories = MxReader.ReadMetadata(path);
        }
        catch (Exception e) when (e is ImageProcessingException or IOException or UnauthorizedAccessException
                                       or ArgumentException or InvalidOperationException or IndexOutOfRangeException
                                       or OverflowException or FormatException or NotSupportedException)
        {
            directories = [];
            problem = e.Message;
        }

        var groups = new List<MetadataGroup>();
        void Add(MetadataSection section, string title, IEnumerable<MetadataField?> fields)
        {
            var list = fields.Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Value)).Cast<MetadataField>().ToList();
            if (list.Count > 0) groups.Add(new MetadataGroup(section, title, list));
        }

        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var sub = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        var iptc = directories.OfType<IptcDirectory>().FirstOrDefault();
        var xmp = SafeXmp(directories.OfType<XmpDirectory>().FirstOrDefault());
        var icc = directories.OfType<IccDirectory>().FirstOrDefault();

        Add(MetadataSection.File, "Datei", FileFields(path, directories, ifd0));
        Add(MetadataSection.Camera, "Kamera",
        [
            Field("Hersteller", Text(ifd0, ExifDirectoryBase.TagMake)),
            Field("Modell", Text(ifd0, ExifDirectoryBase.TagModel)),
            Field("Objektiv", Text(sub, ExifDirectoryBase.TagLensModel) ?? Text(sub, ExifDirectoryBase.TagLensMake)),
            Field("Software", Text(ifd0, ExifDirectoryBase.TagSoftware) ?? Xmp(xmp, "xmp:CreatorTool")),
        ]);
        Add(MetadataSection.Capture, "Aufnahme",
        [
            Field("Aufnahmedatum", CaptureDate(sub, ifd0)),
            Field("Belichtungszeit", ExposureTime(sub)),
            Field("Blende", Aperture(sub)),
            Field("ISO", Integer(sub, ExifDirectoryBase.TagIsoEquivalent)),
            Field("Brennweite", FocalLength(sub)),
            Field("Belichtungskorrektur", ExposureBias(sub)),
            Field("Programm", ExposureProgram(sub)),
            Field("Blitz", Flash(sub)),
            Field("Weißabgleich", WhiteBalance(sub)),
        ]);
        Add(MetadataSection.Location, "Ort",
        [
            Field("Koordinaten", Coordinates(gps)),
            Field("Höhe", Altitude(gps)),
            Field("Ort", Join(Text(iptc, IptcDirectory.TagCity), Text(iptc, IptcDirectory.TagCountryOrPrimaryLocationName))),
        ]);
        Add(MetadataSection.Description, "Urheber und Beschreibung",
        [
            Field("Urheber", Text(ifd0, ExifDirectoryBase.TagArtist) ?? Text(iptc, IptcDirectory.TagByLine) ?? Xmp(xmp, "dc:creator[1]")),
            Field("Copyright", Text(ifd0, ExifDirectoryBase.TagCopyright) ?? Text(iptc, IptcDirectory.TagCopyrightNotice)
                               ?? Xmp(xmp, "dc:rights[1]")),
            Field("Titel", Text(iptc, IptcDirectory.TagObjectName) ?? Xmp(xmp, "dc:title[1]")),
            Field("Beschreibung", Text(ifd0, ExifDirectoryBase.TagImageDescription) ?? Text(iptc, IptcDirectory.TagCaption)
                                  ?? Xmp(xmp, "dc:description[1]")),
            Field("Stichwörter", Keywords(iptc, xmp)),
            Field("Bewertung", Rating(xmp)),
        ]);
        Add(MetadataSection.ColourProfile, "Farbprofil",
        [
            Field("Profil", Text(icc, IccDirectory.TagTagDesc) ?? Text(icc, IccDirectory.TagProfileClass)),
        ]);

        var (tags, truncated) = AllTags(directories);
        if (problem is null)
        {
            var errors = directories.SelectMany(d => d.Errors).Take(3).ToList();
            if (errors.Count > 0) problem = Clean(string.Join(" · ", errors));
        }
        return new ImageMetadata(groups, tags, truncated, problem);
    }

    // ------------------------------------------------------------ Datei

    private static IEnumerable<MetadataField?> FileFields(string path, IReadOnlyList<MxDirectory> directories,
                                                          ExifIfd0Directory? ifd0)
    {
        FileInfo? file = null;
        try
        {
            file = new FileInfo(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Dateiangaben unbekannt
        }

        yield return Field("Name", file?.Name);
        yield return Field("Ordner", file?.DirectoryName);

        var type = directories.OfType<FileTypeDirectory>().FirstOrDefault();
        yield return Field("Format", Text(type, FileTypeDirectory.TagDetectedFileTypeName));

        long bytes = 0;
        try
        {
            bytes = file?.Length ?? 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Groesse unbekannt
        }
        if (bytes > 0) yield return Field("Dateigröße", FormatBytes(bytes));

        // Bildmasse so, wie die Diaschau das Bild zeigt (EXIF-Drehung beruecksichtigt)
        var size = PixelSize(path);
        if (size is { } s)
            yield return Field("Bildmaße", string.Format(Culture, "{0} × {1} Pixel ({2:0.#} MP)", s.Width, s.Height,
                                                         s.Width * (double)s.Height / 1e6));

        yield return Field("Auflösung", Resolution(ifd0));

        DateTime? created = null, modified = null;
        try
        {
            created = file?.CreationTime;
            modified = file?.LastWriteTime;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Datum unbekannt
        }
        yield return Field("Erstellt", created?.ToString("G", Culture));
        yield return Field("Geändert", modified?.ToString("G", Culture));
    }

    private static (int Width, int Height)? PixelSize(string path)
    {
        try
        {
            var (codec, data) = SkiaImaging.Open(File.ReadAllBytes(path));
            using (data)
            using (codec)
            {
                var swap = SkiaImaging.SwapsAxes(codec.EncodedOrigin);
                var (w, h) = (codec.Info.Width, codec.Info.Height);
                return w > 0 && h > 0 ? (swap ? (h, w) : (w, h)) : null;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static string? Resolution(ExifIfd0Directory? ifd0)
    {
        if (ifd0 is null || !ifd0.TryGetRational(ExifDirectoryBase.TagXResolution, out var r) || r.Denominator == 0)
            return null;
        var value = r.ToDouble();
        if (!double.IsFinite(value) || value <= 0 || value > 100_000) return null;
        // ResolutionUnit: 2 = Zoll (Standard), 3 = Zentimeter
        var perCm = ifd0.TryGetInt32(ExifDirectoryBase.TagResolutionUnit, out var unit) && unit == 3;
        return string.Format(Culture, "{0:0} DPI", perCm ? value * 2.54 : value);
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024 ? string.Format(Culture, "{0:0.0} MB", bytes / 1048576.0)
        : bytes >= 1024 ? string.Format(Culture, "{0:0} KB", bytes / 1024.0)
        : string.Format(Culture, "{0} Bytes", bytes);

    // ------------------------------------------------------------ Aufnahme

    private static string? CaptureDate(ExifSubIfdDirectory? sub, ExifIfd0Directory? ifd0)
    {
        DateTime date = default;
        var found = sub is not null && (sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out date)
                                        || sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out date));
        if (!found && ifd0 is not null) found = ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out date);
        return found ? date.ToString("G", Culture) : null;
    }

    private static string? ExposureTime(ExifSubIfdDirectory? sub)
    {
        if (sub is null || !sub.TryGetRational(ExifDirectoryBase.TagExposureTime, out var r) || r.Numerator <= 0 || r.Denominator <= 0)
            return null;
        var seconds = r.ToDouble();
        return seconds < 1
            ? string.Format(Culture, "1/{0:0} s", 1 / seconds)
            : string.Format(Culture, "{0:0.#} s", seconds);
    }

    private static string? Aperture(ExifSubIfdDirectory? sub) =>
        sub is not null && sub.TryGetRational(ExifDirectoryBase.TagFNumber, out var r) && r.Denominator != 0 && r.ToDouble() > 0
            ? string.Format(Culture, "f/{0:0.0#}", r.ToDouble())
            : null;

    private static string? FocalLength(ExifSubIfdDirectory? sub)
    {
        if (sub is null) return null;
        string? mm = sub.TryGetRational(ExifDirectoryBase.TagFocalLength, out var r) && r.Denominator != 0 && r.ToDouble() > 0
            ? string.Format(Culture, "{0:0.#} mm", r.ToDouble())
            : null;
        string? kb = sub.TryGetInt32(ExifDirectoryBase.Tag35MMFilmEquivFocalLength, out var e) && e > 0
            ? string.Format(Culture, "entspricht {0} mm Kleinbild", e)
            : null;
        return mm is null ? kb : kb is null ? mm : $"{mm} ({kb})";
    }

    private static string? ExposureBias(ExifSubIfdDirectory? sub) =>
        sub is not null && sub.TryGetRational(ExifDirectoryBase.TagExposureBias, out var r) && r.Denominator != 0
            ? string.Format(Culture, "{0:+0.0#;−0.0#;0} EV", r.ToDouble())
            : null;

    private static string? ExposureProgram(ExifSubIfdDirectory? sub) =>
        sub is not null && sub.TryGetInt32(ExifDirectoryBase.TagExposureProgram, out var p)
            ? p switch
            {
                1 => "Manuell",
                2 => "Programmautomatik",
                3 => "Zeitautomatik (Blende vorgewählt)",
                4 => "Blendenautomatik (Zeit vorgewählt)",
                5 => "Kreativprogramm (Schärfentiefe)",
                6 => "Actionprogramm (kurze Zeit)",
                7 => "Porträt",
                8 => "Landschaft",
                _ => null,
            }
            : null;

    private static string? Flash(ExifSubIfdDirectory? sub) =>
        sub is not null && sub.TryGetInt32(ExifDirectoryBase.TagFlash, out var f)
            ? (f & 1) != 0 ? "ausgelöst" : "nicht ausgelöst"
            : null;

    private static string? WhiteBalance(ExifSubIfdDirectory? sub) =>
        sub is not null && sub.TryGetInt32(ExifDirectoryBase.TagWhiteBalanceMode, out var w)
            ? w == 0 ? "automatisch" : w == 1 ? "manuell" : null
            : null;

    // ------------------------------------------------------------ Ort

    private static string? Coordinates(GpsDirectory? gps)
    {
        if (gps?.GetGeoLocation() is not { IsZero: false } g || !double.IsFinite(g.Latitude) || !double.IsFinite(g.Longitude)
            || Math.Abs(g.Latitude) > 90 || Math.Abs(g.Longitude) > 180)
            return null;
        return string.Format(Culture, "{0:0.00000}° {1}, {2:0.00000}° {3}",
            Math.Abs(g.Latitude), g.Latitude >= 0 ? "N" : "S",
            Math.Abs(g.Longitude), g.Longitude >= 0 ? "O" : "W");
    }

    private static string? Altitude(GpsDirectory? gps)
    {
        if (gps is null || !gps.TryGetRational(GpsDirectory.TagAltitude, out var r) || r.Denominator == 0) return null;
        var below = gps.TryGetInt32(GpsDirectory.TagAltitudeRef, out var reference) && reference == 1;
        var metres = r.ToDouble();
        if (!double.IsFinite(metres) || Math.Abs(metres) > 100_000) return null;
        return string.Format(Culture, "{0:0} m", below ? -metres : metres);
    }

    // ------------------------------------------------------------ Beschreibung

    private static string? Keywords(IptcDirectory? iptc, IReadOnlyDictionary<string, string>? xmp)
    {
        var words = new List<string>();
        if (iptc?.GetStringArray(IptcDirectory.TagKeywords) is { } k) words.AddRange(k);
        if (xmp is not null)
            words.AddRange(xmp.Where(p => p.Key.StartsWith("dc:subject[", StringComparison.Ordinal)).Select(p => p.Value));
        var distinct = words.Select(w => w.Trim()).Where(w => w.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList();
        return distinct.Count == 0 ? null : Clean(string.Join(", ", distinct));
    }

    private static string? Rating(IReadOnlyDictionary<string, string>? xmp)
    {
        if (Xmp(xmp, "xmp:Rating") is not { } text
            || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stars) || stars < 0 || stars > 5)
            return null;
        return new string('★', stars) + new string('☆', 5 - stars);
    }

    /// <summary>XMP-Eigenschaften nach Pfad ("dc:creator[1]" ...), oder null ohne bzw. bei unlesbarem XMP.</summary>
    private static IReadOnlyDictionary<string, string>? SafeXmp(XmpDirectory? xmp)
    {
        if (xmp is null) return null;
        try
        {
            return new Dictionary<string, string>(xmp.GetXmpProperties());
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static string? Xmp(IReadOnlyDictionary<string, string>? xmp, string path) =>
        xmp is not null && xmp.TryGetValue(path, out var v) && !string.IsNullOrWhiteSpace(v) ? Clean(v) : null;

    // ------------------------------------------------------------ Vollstaendige Liste

    private static (IReadOnlyList<MetadataTag> Tags, bool Truncated) AllTags(IReadOnlyList<MxDirectory> directories)
    {
        var tags = new List<MetadataTag>();
        foreach (var directory in directories)
        foreach (var tag in directory.Tags)
        {
            if (tags.Count >= MaxTags) return (tags, true);
            string? description;
            try
            {
                description = tag.Description;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                description = null;   // ein kaputter Wert darf nicht den Rest der Liste kosten
            }
            if (string.IsNullOrWhiteSpace(description)) continue;
            tags.Add(new MetadataTag(directory.Name, tag.Name, Clean(description)));
        }
        return (tags, false);
    }

    // ------------------------------------------------------------ Hilfen

    private static MetadataField? Field(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new MetadataField(label, Clean(value));

    private static string? Text(MxDirectory? directory, int tag)
    {
        if (directory is null || !directory.ContainsTag(tag)) return null;
        string? text;
        try
        {
            text = directory.GetDescription(tag) ?? directory.GetString(tag);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
        return string.IsNullOrWhiteSpace(text) ? null : Clean(text);
    }

    private static string? Integer(MxDirectory? directory, int tag) =>
        directory is not null && directory.TryGetInt32(tag, out var v) && v > 0 ? v.ToString(Culture) : null;

    private static string? Join(params string?[] parts)
    {
        var present = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return present.Count == 0 ? null : string.Join(", ", present);
    }

    /// <summary>Gekuerzt auf <see cref="MaxValueLength"/>, Steuerzeichen durch Leerzeichen ersetzt.</summary>
    internal static string Clean(string value)
    {
        var text = new StringBuilder(Math.Min(value.Length, MaxValueLength + 1));
        foreach (var c in value)
        {
            if (text.Length >= MaxValueLength) break;
            text.Append(char.IsControl(c) || c is '\u2028' or '\u2029' ? ' ' : c);
        }
        var cleaned = text.ToString().Trim();
        return value.Length > MaxValueLength ? cleaned + "…" : cleaned;
    }
}
