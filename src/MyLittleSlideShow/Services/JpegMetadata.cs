using System.Buffers.Binary;
using System.Text;

namespace MyLittleSlideShow.Services;

/// <summary>
/// Der JPEG-Encoder von Skia schreibt keine Metadaten. Damit beim Drehen
/// Aufnahmedatum, Kamera, GPS usw. nicht verloren gehen, werden die EXIF-
/// (APP1) und ICC-Farbprofil-Segmente (APP2) aus der Originaldatei in die
/// neu kodierte Datei uebernommen. Die EXIF-Orientierung wird dabei auf 1
/// ("normal") gesetzt, weil die Pixel ja schon gedreht sind.
/// </summary>
internal static class JpegMetadata
{
    private static readonly byte[] ExifId = "Exif\0\0"u8.ToArray();
    private static readonly byte[] IccId = Encoding.ASCII.GetBytes("ICC_PROFILE\0");

    private const byte App0 = 0xE0;
    private const byte App1 = 0xE1;
    private const byte App2 = 0xE2;

    /// <summary>
    /// Liefert <paramref name="encoded"/> mit den Metadaten aus
    /// <paramref name="original"/>. Ist eine der Dateien nicht lesbar oder
    /// laesst sich die Orientierung nicht sicher zuruecksetzen, wird EXIF
    /// weggelassen - lieber ohne Metadaten als ein falsch gedrehtes Bild.
    /// </summary>
    public static byte[] CarryOver(byte[] original, byte[] encoded)
    {
        var sourceSegments = ReadSegments(original, out _);
        var targetSegments = ReadSegments(encoded, out var scanStart);
        if (scanStart < 0) return encoded;

        var carried = new List<byte[]>();
        foreach (var (marker, start, length) in sourceSegments)
        {
            var segment = original.AsSpan(start, length);

            if (marker == App1 && HasId(segment, ExifId))
            {
                var copy = segment.ToArray();
                if (ResetOrientation(copy)) carried.Add(copy);
            }
            else if (marker == App2 && HasId(segment, IccId))
            {
                carried.Add(segment.ToArray());
            }
        }

        if (carried.Count == 0) return encoded;

        using var output = new MemoryStream(encoded.Length + carried.Sum(c => c.Length));
        output.Write(encoded, 0, 2);   // SOI

        // Reihenfolge: JFIF (APP0) zuerst, dann EXIF/ICC, dann der Rest.
        foreach (var (marker, start, length) in targetSegments)
            if (marker == App0) output.Write(encoded, start, length);

        foreach (var segment in carried)
            output.Write(segment);

        foreach (var (marker, start, length) in targetSegments)
        {
            if (marker == App0) continue;
            var segment = encoded.AsSpan(start, length);
            if (marker == App1 && HasId(segment, ExifId)) continue;
            if (marker == App2 && HasId(segment, IccId)) continue;
            output.Write(segment);
        }

        output.Write(encoded, scanStart, encoded.Length - scanStart);
        return output.ToArray();
    }

    /// <summary>
    /// Zerlegt den Kopf einer JPEG-Datei in Segmente (Marker, Start, Laenge
    /// inklusive FFxx und Laengenfeld) bis zum Beginn der Bilddaten (SOS).
    /// </summary>
    private static List<(byte Marker, int Start, int Length)> ReadSegments(byte[] data, out int scanStart)
    {
        var segments = new List<(byte, int, int)>();
        scanStart = -1;

        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return segments;

        var pos = 2;
        while (pos + 4 <= data.Length)
        {
            if (data[pos] != 0xFF) break;

            var marker = data[pos + 1];
            if (marker == 0xFF) { pos++; continue; }          // Fuellbytes
            if (marker == 0xDA) { scanStart = pos; break; }   // Start of Scan
            if (marker == 0xD9) break;                        // End of Image
            if (marker is (>= 0xD0 and <= 0xD7) or 0x01) { pos += 2; continue; }   // ohne Laenge

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos + 2));
            if (length < 2 || pos + 2 + length > data.Length) break;

            segments.Add((marker, pos, length + 2));
            pos += 2 + length;
        }

        return segments;
    }

    private static bool HasId(ReadOnlySpan<byte> segment, byte[] id) =>
        segment.Length >= 4 + id.Length && segment.Slice(4, id.Length).SequenceEqual(id);

    /// <summary>
    /// Setzt das Orientation-Tag (0x0112) in IFD0 auf 1. Gibt false zurueck,
    /// wenn die Struktur nicht sicher lesbar ist; fehlt das Tag, ist nichts zu tun.
    /// </summary>
    private static bool ResetOrientation(byte[] segment)
    {
        // FFE1, Laenge (2), "Exif\0\0" (6) -> TIFF-Kopf ab Offset 10
        const int tiff = 10;
        if (segment.Length < tiff + 8) return false;

        bool littleEndian;
        if (segment[tiff] == 'I' && segment[tiff + 1] == 'I') littleEndian = true;
        else if (segment[tiff] == 'M' && segment[tiff + 1] == 'M') littleEndian = false;
        else return false;

        ushort U16(int offset) => littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(segment.AsSpan(offset))
            : BinaryPrimitives.ReadUInt16BigEndian(segment.AsSpan(offset));

        uint U32(int offset) => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(segment.AsSpan(offset))
            : BinaryPrimitives.ReadUInt32BigEndian(segment.AsSpan(offset));

        if (U16(tiff + 2) != 42) return false;

        var ifd = tiff + (long)U32(tiff + 4);
        if (ifd + 2 > segment.Length) return false;

        int entries = U16((int)ifd);
        for (var i = 0; i < entries; i++)
        {
            var entry = (int)ifd + 2 + i * 12;
            if (entry + 12 > segment.Length) return false;
            if (U16(entry) != 0x0112) continue;

            // Typ SHORT, ein Wert, steht direkt im Eintrag
            if (U16(entry + 2) != 3 || U32(entry + 4) != 1) return false;

            var value = segment.AsSpan(entry + 8, 2);
            if (littleEndian) BinaryPrimitives.WriteUInt16LittleEndian(value, 1);
            else BinaryPrimitives.WriteUInt16BigEndian(value, 1);
            return true;
        }

        return true;
    }
}
