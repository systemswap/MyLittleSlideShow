namespace MyLittleSlideShow.Services;

/// <summary>Abschnitte der Zusammenfassung, in Anzeigereihenfolge.</summary>
public enum MetadataSection
{
    /// <summary>Name, Ordner, Format, Groesse, Bildmasse, Aufloesung, Datum.</summary>
    File,

    /// <summary>Kamera, Objektiv, Software.</summary>
    Camera,

    /// <summary>Aufnahmedatum und Belichtung.</summary>
    Capture,

    /// <summary>GPS-Position.</summary>
    Location,

    /// <summary>Urheber, Copyright, Titel, Beschreibung, Stichwoerter, Bewertung.</summary>
    Description,

    /// <summary>Eingebettetes ICC-Farbprofil.</summary>
    ColourProfile,
}

/// <summary>Ein beschrifteter, bereits fuer die Anzeige formatierter Wert.</summary>
public sealed record MetadataField(string Label, string Value);

/// <summary>Ein Abschnitt der Zusammenfassung mit seinen Feldern (nie leer).</summary>
public sealed record MetadataGroup(MetadataSection Section, string Title, IReadOnlyList<MetadataField> Fields);

/// <summary>Ein Roheintrag, wie er in der Datei steht, fuer die vollstaendige Liste.</summary>
/// <param name="Directory">Wo er steht, z. B. "Exif IFD0", "GPS", "IPTC", "XMP".</param>
public sealed record MetadataTag(string Directory, string Name, string Value);

/// <summary>
/// Alles Lesbare ueber eine Bilddatei: eine kurze Zusammenfassung in
/// Abschnitten und die vollstaendige Liste der Eintraege.
/// </summary>
/// <param name="Groups">Abschnitte mit mindestens einem Wert.</param>
/// <param name="Tags">Alle Eintraege, hoechstens <see cref="ImageMetadataReader.MaxTags"/>.</param>
/// <param name="Truncated">Es gab mehr Eintraege, als gelistet sind.</param>
/// <param name="Problem">Warum das Lesen scheiterte oder unvollstaendig war, sonst null.</param>
public sealed record ImageMetadata(IReadOnlyList<MetadataGroup> Groups, IReadOnlyList<MetadataTag> Tags,
                                   bool Truncated, string? Problem);
