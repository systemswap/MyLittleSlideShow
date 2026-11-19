using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyLittleSlideShow.Platform;

namespace MyLittleSlideShow.Localization;

/// <summary>Wie die Flagge einer Sprache gezeichnet wird.</summary>
public enum FlagKind
{
    /// <summary>Gleich breite Streifen in <see cref="Flag.Colors"/>.</summary>
    Stripes,

    /// <summary>Union Jack (eingebaut fuer Englisch).</summary>
    UnionJack,

    /// <summary>Bilddatei (PNG) im Ordner languages.</summary>
    Image,

    /// <summary>Nicht angegeben: graues Feld mit dem Sprachcode.</summary>
    None,
}

/// <summary>Flagge einer Sprache in der Sprachauswahl.</summary>
/// <param name="Vertical">Senkrechte Streifen (Frankreich) statt waagerechter (Deutschland).</param>
/// <param name="Colors">Streifenfarben wie "#DD0000".</param>
/// <param name="ImagePath">Voller Pfad der Bilddatei.</param>
public sealed record Flag(FlagKind Kind, bool Vertical = false, IReadOnlyList<string>? Colors = null, string? ImagePath = null)
{
    public static readonly Flag None = new(FlagKind.None);
}

/// <summary>
/// Eine Sprache: eingebaut (Deutsch, Englisch) oder aus einer JSON-Datei.
/// <see cref="Name"/> steht in der Sprache selbst ("Français") - zusammen mit
/// der Flagge findet man die eigene Sprache auch, wenn gerade eine fremde eingestellt ist.
/// </summary>
/// <param name="Code">Sprachcode in Kleinbuchstaben, z. B. "fr".</param>
/// <param name="Texts">Uebersetzungen, Schluessel ist der deutsche Text; leer fuer Deutsch.</param>
/// <param name="File">Pfad der JSON-Datei; null fuer eingebaute Sprachen.</param>
public sealed record Language(string Code, string Name, Flag Flag, IReadOnlyDictionary<string, string> Texts, string? File = null)
{
    public static readonly Language German = new("de", "Deutsch",
        new Flag(FlagKind.Stripes, Colors: ["#000000", "#DD0000", "#FFCE00"]),
        new Dictionary<string, string>());

    public bool IsGerman => Code == "de";

    public override string ToString() => Name;
}

/// <summary>
/// Findet alle Sprachen: die eingebauten und die JSON-Dateien im Ordner
/// <c>languages</c> neben dem Programm.
/// </summary>
/// <remarks>
/// Aufbau einer Sprachdatei (UTF-8), z. B. <c>languages/fr.json</c>:
/// <code>
/// {
///   "code": "fr",
///   "name": "Français",
///   "flag": { "stripes": "vertical", "colors": ["#002395", "#FFFFFF", "#ED2939"] },
///   "texts": { "Einstellungen": "Paramètres", … }
/// }
/// </code>
/// Statt Streifen geht auch eine Bilddatei: <c>"flag": "fr.png"</c>. Schluessel
/// ist immer der deutsche Text; fehlende Texte erscheinen auf Englisch. Dateien,
/// deren Name mit einem Unterstrich beginnt (Beispieldatei), werden nicht geladen.
/// </remarks>
public static class Languages
{
    public const string FolderName = "languages";

    public const string SampleFileName = "_beispiel.json";

    /// <summary>
    /// Ordner languages neben dem Programm - bei einer AppImage neben der
    /// .AppImage-Datei, denn ihr Inneres ist schreibgeschuetzt.
    /// </summary>
    public static string Folder =>
        Path.Combine(Path.GetDirectoryName(AppLocations.LaunchPath) ?? AppContext.BaseDirectory, FolderName);

    /// <summary>
    /// Deutsch, Englisch (sofern keine Datei es ersetzt) und die gueltigen Dateien
    /// in Dateinamen-Reihenfolge. Fehlerhafte Dateien landen in <paramref name="problems"/>.
    /// </summary>
    public static List<Language> All(out List<string> problems)
    {
        problems = [];
        var list = new List<Language> { Language.German, English.Language };
        var folder = Folder;
        if (!Directory.Exists(folder)) return list;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.Add(e.Message);
            return list;
        }

        foreach (var file in files)
        {
            if (Path.GetFileName(file).StartsWith('_')) continue;
            try
            {
                var lang = Load(file);
                if (lang.IsGerman)
                {
                    problems.Add(Tr.T("{0}: Deutsch ist eingebaut und lässt sich nicht ersetzen.", Path.GetFileName(file)));
                    continue;
                }
                // Eine Datei fuer eine eingebaute Sprache (Englisch) ersetzt diese.
                list.RemoveAll(l => l.Code == lang.Code);
                list.Add(lang);
            }
            catch (Exception e) when (e is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                problems.Add($"{Path.GetFileName(file)}: {e.Message}");
            }
        }
        return list;
    }

    /// <summary>Laedt eine Sprache aus einer JSON-Datei; Code ersatzweise aus dem Dateinamen.</summary>
    public static Language Load(string file)
    {
        var root = JsonNode.Parse(System.IO.File.ReadAllText(file),
                       documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
                   as JsonObject ?? throw new InvalidDataException(Tr.T("kein JSON-Objekt"));

        var code = ((string?)root["code"] ?? Path.GetFileNameWithoutExtension(file)).Trim().ToLowerInvariant();
        var name = (string?)root["name"] ?? code;
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root["texts"] is JsonObject t)
            foreach (var (key, value) in t)
                if (value is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0)
                    texts[key] = s;

        return new Language(code, name, ReadFlag(root["flag"], Path.GetDirectoryName(file)!), texts, file);
    }

    private static Flag ReadFlag(JsonNode? node, string folder)
    {
        switch (node)
        {
            case JsonValue v when v.TryGetValue<string>(out var image):
                var path = Path.Combine(folder, Path.GetFileName(image));
                return System.IO.File.Exists(path) ? new Flag(FlagKind.Image, ImagePath: path) : Flag.None;
            case JsonObject o when o["colors"] is JsonArray colors:
                var list = colors.Select(c => c is JsonValue cv && cv.TryGetValue<string>(out var s) ? s : null)
                                 .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
                var vertical = string.Equals((string?)o["stripes"], "vertical", StringComparison.OrdinalIgnoreCase);
                return list.Count > 0 ? new Flag(FlagKind.Stripes, vertical, list) : Flag.None;
            default:
                return Flag.None;
        }
    }

    /// <summary>Sprache des Systems, sonst Englisch (Deutsch also nur bei deutschem System).</summary>
    public static Language ForSystem(IReadOnlyList<Language> available)
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return available.FirstOrDefault(l => l.Code == code) ?? available.First(l => l.Code == "en");
    }

    /// <summary>
    /// Schreibt die Beispieldatei in den Ordner languages: alle Texte, vorbelegt
    /// mit dem englischen Text zum Uebersetzen. Schluessel ist der deutsche Text.
    /// </summary>
    /// <returns>Pfad der geschriebenen Datei.</returns>
    public static string WriteSample()
    {
        var file = Path.Combine(Folder, SampleFileName);
        Directory.CreateDirectory(Folder);

        var texts = new JsonObject();
        foreach (var (german, english) in English.Texts.OrderBy(e => e.Key, StringComparer.Ordinal))
            texts[german] = english;

        var root = new JsonObject
        {
            ["_hinweis"] = "Kopieren als z. B. fr.json (Dateien mit _ am Anfang werden nicht geladen), " +
                           "code, name und flag anpassen und die Texte rechts übersetzen. Links steht immer " +
                           "der deutsche Text als Schlüssel. Platzhalter wie {0} müssen erhalten bleiben. " +
                           "Flagge: Streifen von oben nach unten (\"horizontal\") oder von links nach rechts " +
                           "(\"vertical\"), oder eine PNG-Datei im selben Ordner: \"flag\": \"fr.png\". / " +
                           "Copy as e.g. fr.json (files starting with _ are not loaded), adjust code, name and " +
                           "flag and translate the texts on the right. The key on the left is always the German " +
                           "text. Keep placeholders such as {0}.",
            ["code"] = "fr",
            ["name"] = "Français",
            ["flag"] = new JsonObject
            {
                ["stripes"] = "vertical",
                ["colors"] = new JsonArray("#002395", "#FFFFFF", "#ED2939"),
            },
            ["texts"] = texts,
        };

        System.IO.File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // Umlaute lesbar lassen
        }));
        return file;
    }
}
