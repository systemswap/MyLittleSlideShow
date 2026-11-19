using System.ComponentModel;
using System.Globalization;

namespace MyLittleSlideShow.Localization;

/// <summary>
/// Uebersetzung der Oberflaeche. Der deutsche Text ist zugleich der Schluessel:
/// im Code <c>Tr.T("Einstellungen")</c>, in XAML <c>{l:T 'Einstellungen'}</c>.
/// Deutsch braucht deshalb keine Tabelle, Englisch steht im Code
/// (<see cref="English"/>), weitere Sprachen kommen als JSON-Dateien aus dem
/// Ordner <c>languages</c> neben dem Programm (<see cref="Languages"/>).
/// </summary>
/// <remarks>
/// Fehlt ein Text in einer Sprache, gilt der englische, sonst der deutsche.
/// Beim Umschalten meldet <see cref="Version"/> eine Aenderung; die Bindungen
/// der Oberflaeche haengen daran, die Texte wechseln also sofort. Im Code
/// gesetzte Texte frischen ihre Fenster ueber <see cref="LanguageChanged"/> auf.
/// </remarks>
public sealed class Tr : INotifyPropertyChanged
{
    /// <summary>Einzige Instanz, Bindungsquelle fuer <see cref="Version"/>.</summary>
    public static Tr Instance { get; } = new();

    private Language _current = Language.German;
    private CultureInfo _culture = CultureInfo.GetCultureInfo("de");

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Nach jedem Sprachwechsel, auf dem UI-Thread.</summary>
    public static event Action? LanguageChanged;

    /// <summary>Zaehler, der bei jedem Sprachwechsel steigt (Ausloeser fuer Bindungen).</summary>
    public int Version { get; private set; }

    /// <summary>Die eingestellte Sprache (Deutsch, bis <see cref="Use"/> aufgerufen wird).</summary>
    public static Language Current => Instance._current;

    /// <summary>Kultur der Sprache fuer Datum und Zahlen; unbekannte Codes ergeben die neutrale Kultur.</summary>
    public static CultureInfo Culture => Instance._culture;

    /// <summary>Schaltet die Sprache der Oberflaeche um.</summary>
    public static void Use(Language language)
    {
        var tr = Instance;
        if (ReferenceEquals(tr._current, language)) return;
        tr._current = language;
        try { tr._culture = CultureInfo.GetCultureInfo(language.Code); }
        catch (CultureNotFoundException) { tr._culture = CultureInfo.InvariantCulture; }
        tr.Version++;
        tr.PropertyChanged?.Invoke(tr, new PropertyChangedEventArgs(nameof(Version)));
        LanguageChanged?.Invoke();
    }

    /// <summary>Text in der eingestellten Sprache.</summary>
    /// <param name="german">Der deutsche Text, zugleich der Schluessel.</param>
    public static string T(string german)
    {
        var lang = Instance._current;
        if (lang.Texts.TryGetValue(german, out var text) && !string.IsNullOrEmpty(text)) return text;
        if (!lang.IsGerman && English.Texts.TryGetValue(german, out var en)) return en;
        return german;
    }

    /// <summary>Text mit Platzhaltern {0}, {1} …; Zahlen im Format der Sprache.</summary>
    public static string T(string german, params object?[] args) =>
        string.Format(Culture, T(german), args);
}
