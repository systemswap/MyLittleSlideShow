using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MyLittleSlideShow.Localization;
using MyLittleSlideShow.Platform;
using MyLittleSlideShow.Services;

namespace MyLittleSlideShow.Views;

/// <summary>Darstellungswerte, die das Hauptfenster vor dem Speichern live anzeigt.</summary>
public sealed record DisplayPreview(
    bool Sharpen,
    int SharpenValue,
    bool QuietSharpen,
    int SharpenThreshold,
    bool WithAmbiFrame,
    int AmbiOpacity);

public partial class SettingsWindow : Window
{
    /// <summary>
    /// Stufen fuer den Bildwechsel-Regler. Ein linearer Regler ueber
    /// 1..86400 Sekunden waere im kurzen Bereich nicht bedienbar.
    /// </summary>
    private static readonly int[] IntervalSteps =
    [
        5, 10, 15, 20, 30, 45,
        60, 90, 120, 180, 300, 600, 900, 1200, 1800,
        3600, 7200, 10800, 21600, 43200, 86400
    ];

    private readonly SettingsManager _settingsManager;
    private AppSettings S => _settingsManager.Settings;

    private string _folderPath = string.Empty;
    private bool _initialized;

    // Bleibt beim gespeicherten Wert, bis der Regler bewegt wird - ein frei
    // gewaehlter Wert wie 100 s wird so nicht still auf eine Stufe gerundet.
    private int _intervalSeconds;

    // Sprache beim Oeffnen: "Abbrechen" (oder Schliessen ohne Speichern) stellt sie wieder her.
    private readonly Language _languageAtOpen = Tr.Current;

    /// <summary>Meldet dem Hauptfenster, ob es sich neu laden muss.</summary>
    public bool SettingsSaved { get; private set; }

    /// <summary>Wird bei jeder Aenderung an Schaerfe oder AmbiFrame ausgeloest.</summary>
    public event Action<DisplayPreview>? PreviewChanged;

    /// <summary>Nur fuer den XAML-Designer.</summary>
    public SettingsWindow() : this(App.SettingsManager) { }

    public SettingsWindow(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        InitializeComponent();
        ScreenFit.Attach(this);
        Loaded += OnLoaded;
        Tr.LanguageChanged += RefreshCodeTexts;
        Closed += (_, _) =>
        {
            Tr.LanguageChanged -= RefreshCodeTexts;
            if (!SettingsSaved) Tr.Use(_languageAtOpen);
        };
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var platform = PlatformServices.Current;

        Autostart_CheckBox.IsChecked = platform.Autostart.IsEnabled();
        Autostart_CheckBox.IsEnabled = platform.Autostart.IsSupported;

        DisplayAlwaysOn_CheckBox.IsChecked = S.DisplayAlwaysOn;
        DisplayAlwaysOn_CheckBox.IsEnabled = platform.SleepInhibitor.IsSupported;

        Sharpen_CheckBox.IsChecked = S.SharpImages;
        SharpenValue_Slider.Value = S.SharpenValue;
        QuietSharpen_CheckBox.IsChecked = S.QuietSharpen;
        SharpenThreshold_Slider.Value = S.SharpenThreshold;
        StretchWhileZooming_CheckBox.IsChecked = S.StretchWhileZooming;

        AmbiFrame_CheckBox.IsChecked = S.WithAmbiFrame;
        AmbiFrameOpacity_Slider.Value = S.OpacityOfAmbiFrame;

        SlideShow_CheckBox.IsChecked = S.ActivateSlideShow;
        _intervalSeconds = S.ImageChangeIntervalSeconds;
        Interval_Slider.Maximum = IntervalSteps.Length - 1;
        Interval_Slider.Value = NearestIntervalStep(_intervalSeconds);
        Random_CheckBox.IsChecked = S.RandomImages;

        EveryImageAsWallpaper_CheckBox.IsChecked = S.SetEveryImageAsWallpaper;
        FillWallpaperStyles(S.WallpaperStyle);

        if (!platform.Wallpaper.IsSupported)
        {
            EveryImageAsWallpaper_CheckBox.IsEnabled = false;
            WallpaperStyle_ComboBox.IsEnabled = false;
            WallpaperHint.IsVisible = true;
        }

        _folderPath = S.FolderPath;
        Recursive_CheckBox.IsChecked = S.ScanFolderRecursive;

        FillLanguages();
        Language_ComboBox.SelectionChanged += (_, _) =>
        {
            if (Language_ComboBox.SelectedItem is Language language) Tr.Use(language);
        };

        RefreshCodeTexts();

        // Erst nach dem Befuellen anhaengen, sonst feuert jede Zuweisung oben
        // eine Vorschau mit halb gesetzten Werten.
        foreach (var slider in new[] { SharpenValue_Slider, SharpenThreshold_Slider,
                                       AmbiFrameOpacity_Slider, Interval_Slider })
            slider.PropertyChanged += OnControlPropertyChanged;

        foreach (var box in new[] { Sharpen_CheckBox, QuietSharpen_CheckBox,
                                    AmbiFrame_CheckBox, SlideShow_CheckBox })
            box.PropertyChanged += OnControlPropertyChanged;

        _initialized = true;
        UpdateDependentControls();
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty && e.Property != ToggleButton.IsCheckedProperty)
            return;

        if (sender == Interval_Slider)
        {
            var index = Math.Clamp((int)Math.Round(Interval_Slider.Value), 0, IntervalSteps.Length - 1);
            _intervalSeconds = IntervalSteps[index];
        }

        UpdateDependentControls();

        if (sender != Interval_Slider && sender != SlideShow_CheckBox)
            PreviewChanged?.Invoke(CurrentPreview());
    }

    /// <summary>Wertanzeigen und Freigaben der Regler nachfuehren.</summary>
    private void UpdateDependentControls()
    {
        if (!_initialized) return;

        var sharpen = Sharpen_CheckBox.IsChecked == true;

        SharpenValue_Slider.IsEnabled = sharpen;
        QuietSharpen_CheckBox.IsEnabled = sharpen;
        SharpenThreshold_Slider.IsEnabled = sharpen && QuietSharpen_CheckBox.IsChecked == true;
        AmbiFrameOpacity_Slider.IsEnabled = AmbiFrame_CheckBox.IsChecked == true;
        Interval_Slider.IsEnabled = SlideShow_CheckBox.IsChecked == true;

        SharpenValue_Label.Text = ((int)SharpenValue_Slider.Value).ToString();
        SharpenThreshold_Label.Text = ((int)SharpenThreshold_Slider.Value).ToString();
        AmbiFrameOpacity_Label.Text = $"{(int)AmbiFrameOpacity_Slider.Value} %";
        Interval_Label.Text = FormatInterval(_intervalSeconds);
    }

    private DisplayPreview CurrentPreview() => new(
        Sharpen_CheckBox.IsChecked == true,
        (int)SharpenValue_Slider.Value,
        QuietSharpen_CheckBox.IsChecked == true,
        (int)SharpenThreshold_Slider.Value,
        AmbiFrame_CheckBox.IsChecked == true,
        (int)AmbiFrameOpacity_Slider.Value);

    private static int NearestIntervalStep(int seconds)
    {
        var best = 0;
        for (var i = 1; i < IntervalSteps.Length; i++)
        {
            if (Math.Abs(IntervalSteps[i] - seconds) < Math.Abs(IntervalSteps[best] - seconds))
                best = i;
        }
        return best;
    }

    private static string FormatInterval(int seconds) => seconds switch
    {
        < 60 => $"{seconds} s",
        < 3600 when seconds % 60 == 0 => $"{seconds / 60} min",
        < 3600 => $"{seconds / 60.0:0.#} min",
        _ => $"{seconds / 3600} h"
    };

    private async void SelectFolder_Click(object? sender, RoutedEventArgs e)
    {
        // Ersetzt System.Windows.Forms.FolderBrowserDialog durch den
        // plattformunabhaengigen StorageProvider von Avalonia.
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Tr.T("Ordner mit Bildern wählen"),
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null) return;

        var path = folder.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        _folderPath = path;
        RefreshCodeTexts();
    }

    private void ResetPath_Click(object? sender, RoutedEventArgs e)
    {
        _folderPath = string.Empty;
        RefreshCodeTexts();
        S.LastLoadedFile = string.Empty;
    }

    // ---------------------------------------------------------------- Sprache

    /// <summary>Im Code gesetzte Texte; die XAML-Texte wechseln ueber ihre Bindung von selbst.</summary>
    private void RefreshCodeTexts()
    {
        FolderPath_Label.Text = string.IsNullOrEmpty(_folderPath) ? Tr.T("(kein Pfad gewählt)") : _folderPath;
        WallpaperHint.Text = Tr.T("Für diese Desktopumgebung ist kein Werkzeug zum Setzen des Hintergrundbilds " +
                                  "verfügbar (erwartet werden gsettings, plasma-apply-wallpaperimage, xfconf-query oder feh).");
        if (WallpaperStyle_ComboBox.SelectedItem is WallpaperChoice choice) FillWallpaperStyles(choice.Style);
    }

    /// <summary>Eintrag der Auswahl "Anordnung" mit uebersetztem Namen.</summary>
    private sealed record WallpaperChoice(WallpaperStyle Style)
    {
        public override string ToString() => Style switch
        {
            WallpaperStyle.Zentriert => Tr.T("Zentriert"),
            WallpaperStyle.Gestreckt => Tr.T("Gestreckt"),
            _ => Tr.T("Angepasst"),
        };
    }

    private void FillWallpaperStyles(WallpaperStyle selected)
    {
        var choices = Enum.GetValues<WallpaperStyle>().Select(s => new WallpaperChoice(s)).ToList();
        WallpaperStyle_ComboBox.ItemsSource = choices;
        WallpaperStyle_ComboBox.SelectedItem = choices.First(c => c.Style == selected);
    }

    private void FillLanguages(string? select = null)
    {
        var languages = Languages.All(out var problems);
        Language_ComboBox.ItemsSource = languages;
        var code = select ?? Tr.Current.Code;
        Language_ComboBox.SelectedItem = languages.FirstOrDefault(l => l.Code == code) ?? languages[0];
        ShowLanguageHint(problems.Count > 0 ? string.Join(Environment.NewLine, problems) : null, warning: true);
    }

    private void ShowLanguageHint(string? text, bool warning)
    {
        LanguageHint.Text = text;
        LanguageHint.IsVisible = text is not null;
        if (warning) LanguageHint.Bind(TextBlock.ForegroundProperty, LanguageHint.GetResourceObservable("Fine.Warning"));
        else LanguageHint.ClearValue(TextBlock.ForegroundProperty);
    }

    private void CreateSampleLanguage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = Languages.WriteSample();
            ShowLanguageHint(Tr.T("Beispieldatei geschrieben: {0}", file), warning: false);
            PlatformServices.Current.Shell.RevealInFileManager(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowLanguageHint(Tr.T("Die Beispieldatei konnte nicht geschrieben werden: {0}", ex.Message), warning: true);
        }
    }

    private void OpenLanguageFolder_Click(object? sender, RoutedEventArgs e)
    {
        var folder = Languages.Folder;
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowLanguageHint(Tr.T("Der Ordner {0} konnte nicht angelegt werden: {1}", folder, ex.Message), warning: true);
            return;
        }
        // Neu hineinkopierte Dateien erscheinen beim naechsten Oeffnen der Einstellungen.
        PlatformServices.Current.Shell.OpenFile(folder);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var autostart = PlatformServices.Current.Autostart;
        if (autostart.IsSupported)
        {
            var wanted = Autostart_CheckBox.IsChecked == true;
            if (wanted) autostart.Enable(); else autostart.Disable();
            S.Autostart = autostart.IsEnabled();
        }

        S.DisplayAlwaysOn = DisplayAlwaysOn_CheckBox.IsChecked == true;

        S.SharpImages = Sharpen_CheckBox.IsChecked == true;
        S.SharpenValue = (int)SharpenValue_Slider.Value;
        S.QuietSharpen = QuietSharpen_CheckBox.IsChecked == true;
        S.SharpenThreshold = (int)SharpenThreshold_Slider.Value;
        S.StretchWhileZooming = StretchWhileZooming_CheckBox.IsChecked == true;

        S.WithAmbiFrame = AmbiFrame_CheckBox.IsChecked == true;
        S.OpacityOfAmbiFrame = (int)AmbiFrameOpacity_Slider.Value;

        S.ActivateSlideShow = SlideShow_CheckBox.IsChecked == true;
        S.ImageChangeIntervalSeconds = _intervalSeconds;
        S.RandomImages = Random_CheckBox.IsChecked == true;

        S.SetEveryImageAsWallpaper = EveryImageAsWallpaper_CheckBox.IsChecked == true;
        if (WallpaperStyle_ComboBox.SelectedItem is WallpaperChoice style)
            S.WallpaperStyle = style.Style;

        S.Language = Tr.Current.Code;

        S.FolderPath = _folderPath;
        S.ScanFolderRecursive = Recursive_CheckBox.IsChecked == true;

        _settingsManager.Save();
        SettingsSaved = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    private void Donate_Click(object? sender, RoutedEventArgs e)
        => PlatformServices.Current.Shell.OpenUrl(Donation.Address);

    private void Info_Click(object? sender, RoutedEventArgs e)
        => new AboutWindow().ShowDialog(this);
}
