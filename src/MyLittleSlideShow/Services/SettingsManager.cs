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

    /// <summary>
    /// Uebernimmt einmalig die Werte einer alten WPF-Installation
    /// (user.config unter %LOCALAPPDATA%), falls noch keine settings.json existiert.
    /// </summary>
    public void MigrateFromLegacyIfNeeded()
    {
        if (File.Exists(_filePath)) return;
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MTS-Solutions.de");
            if (!Directory.Exists(root)) return;

            var config = Directory.EnumerateFiles(root, "user.config", SearchOption.AllDirectories)
                                  .OrderByDescending(File.GetLastWriteTimeUtc)
                                  .FirstOrDefault();
            if (config is null) return;

            var doc = System.Xml.Linq.XDocument.Load(config);
            var values = doc.Descendants("setting")
                            .ToDictionary(
                                e => (string?)e.Attribute("name") ?? string.Empty,
                                e => e.Element("value")?.Value ?? string.Empty);

            string Get(string key) => values.TryGetValue(key, out var v) ? v : string.Empty;
            bool GetBool(string key) => bool.TryParse(Get(key), out var b) && b;
            int GetInt(string key, int fallback) => int.TryParse(Get(key), out var i) ? i : fallback;
            double GetDouble(string key) => double.TryParse(Get(key),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;

            Settings.FolderPath = Get("FolderPath");
            Settings.LastLoadedFile = Get("LastLoadedFile");
            Settings.ScanFolderRecursive = GetBool("ScanFolderRekursiv");
            Settings.ImageChangeIntervalSeconds = GetInt("ImageChangeIntervall_Seconds", 120);
            Settings.PositionTop = GetDouble("PositionTop");
            Settings.PositionLeft = GetDouble("PositionLeft");
            Settings.SharpenValue = GetInt("SharpenValue", 25);
            Settings.SharpImages = GetBool("SharpImages");
            Settings.WithAmbiFrame = GetBool("WithAmbiFrame");
            Settings.OpacityOfAmbiFrame = GetInt("OpacityOfAmbiFrame", 90);
            Settings.ActivateSlideShow = GetBool("ActivateSlideShow");
            Settings.RandomImages = GetBool("RandomImages");
            Settings.SetEveryImageAsWallpaper = GetBool("SetEveryImageAsWallpaper");
            Settings.DisplayAlwaysOn = GetBool("DisplayAllwaysOn");
            Settings.MainWindowHeight = GetInt("MainWindowHeight", 315);
            Settings.MainWindowWidth = GetInt("MainWindowWidth", 386);

            if (Enum.TryParse<WallpaperStyle>(Get("WallpaperStyle"), out var style))
                Settings.WallpaperStyle = style;

            Save();
        }
        catch
        {
            // Migration ist optional.
        }
    }
}
