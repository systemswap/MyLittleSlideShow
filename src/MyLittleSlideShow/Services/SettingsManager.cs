using System.Text.Json;
using System.Text.Json.Serialization;
using MyLittleSlideShow.Platform;

namespace MyLittleSlideShow.Services;

/// <summary>
/// Ersetzt Properties.Settings (app.config / Settings.settings), das es unter
/// .NET 10 ohne WPF/WinForms nicht mehr gibt. Ablage als JSON im Benutzerprofil:
///   Windows: %APPDATA%\MyLittleSlideShow\settings.json
///   Linux:   ~/.config/MyLittleSlideShow/settings.json
/// </summary>
public sealed class AppSettings
{
    public bool Autostart { get; set; }
    public bool DisplayAlwaysOn { get; set; }
    public string LastPathOfApplication { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;
    public bool ScanFolderRecursive { get; set; }
    public int ImageChangeIntervalSeconds { get; set; } = 120;
    public string LastLoadedFile { get; set; } = string.Empty;

    public double PositionTop { get; set; }
    public double PositionLeft { get; set; }

    public int SharpenValue { get; set; } = 25;
    public bool SharpImages { get; set; } = true;

    /// <summary>"Ruhiges Schaerfen": Schwelle, ab der ueberhaupt geschaerft wird.</summary>
    public bool QuietSharpen { get; set; }
    public int SharpenThreshold { get; set; } = 6;

    /// <summary>Bild beim Zoomen mitziehen und nur zwischendurch neu berechnen.</summary>
    public bool StretchWhileZooming { get; set; } = true;

    public bool WithAmbiFrame { get; set; } = true;
    public int OpacityOfAmbiFrame { get; set; } = 90;

    public bool ActivateSlideShow { get; set; } = true;
    public bool RandomImages { get; set; }

    public bool SetEveryImageAsWallpaper { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WallpaperStyle WallpaperStyle { get; set; } = WallpaperStyle.Angepasst;

    public int MainWindowHeight { get; set; } = 315;
    public int MainWindowWidth { get; set; } = 386;
}

public sealed class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly string _filePath;
    private readonly Lock _gate = new();

    public AppSettings Settings { get; private set; } = new();

    public SettingsManager()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
                                      Environment.SpecialFolderOption.Create),
            "MyLittleSlideShow");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        Load();
    }

    public void Load()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                }
                else
                {
                    Settings = new AppSettings();
                }
            }
            catch
            {
                // Beschaedigte Datei darf den Start nicht verhindern.
                Settings = new AppSettings();
            }

            // Autostart-Status kommt vom System, nicht aus der Datei.
            Settings.Autostart = PlatformServices.Current.Autostart.IsEnabled();
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            try
            {
                // Atomar schreiben, damit ein Absturz die Datei nicht zerlegt.
                var tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Settings, JsonOptions));
                File.Move(tmp, _filePath, overwrite: true);
            }
            catch
            {
                // Speichern ist best effort.
            }
        }
    }
}
