using System.Runtime.InteropServices;

namespace MyLittleSlideShow.Platform;

public enum WallpaperStyle
{
    Zentriert,
    Gestreckt,
    Angepasst
}

/// <summary>Desktophintergrund setzen.</summary>
public interface IWallpaperService
{
    bool IsSupported { get; }
    void SetWallpaper(string path, WallpaperStyle style);
}

/// <summary>Automatischer Start bei Anmeldung.</summary>
public interface IAutostartService
{
    bool IsSupported { get; }
    bool IsEnabled();
    void Enable();
    void Disable();
}

/// <summary>Verhindert, dass Bildschirm/System in den Standby geht.</summary>
public interface ISleepInhibitor : IDisposable
{
    bool IsSupported { get; }
    /// <summary>Sperre aktivieren oder aufheben. Idempotent.</summary>
    void SetKeepAwake(bool keepAwake);
}

/// <summary>Datei in den Papierkorb verschieben (kein hartes Loeschen).</summary>
public interface ITrashService
{
    bool IsSupported { get; }
    bool MoveToTrash(string path);
}

/// <summary>Dateien, Ordner und URLs mit der Standardanwendung oeffnen.</summary>
public interface IShellService
{
    void OpenUrl(string url);
    void OpenFile(string path);
    /// <summary>Oeffnet den Ordner und markiert - wenn moeglich - die Datei.</summary>
    void RevealInFileManager(string path);
}

/// <summary>Sammelt alle plattformabhaengigen Dienste an einer Stelle.</summary>
public sealed class PlatformServices
{
    public required IWallpaperService Wallpaper { get; init; }
    public required IAutostartService Autostart { get; init; }
    public required ISleepInhibitor SleepInhibitor { get; init; }
    public required ITrashService Trash { get; init; }
    public required IShellService Shell { get; init; }

    private static PlatformServices? _current;
    public static PlatformServices Current => _current ??= Create();

    private static PlatformServices Create()
    {
        var shell = new ShellService();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new PlatformServices
            {
                Wallpaper = new WindowsWallpaperService(),
                Autostart = new WindowsAutostartService(),
                SleepInhibitor = new WindowsSleepInhibitor(),
                Trash = new WindowsTrashService(),
                Shell = shell
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new PlatformServices
            {
                Wallpaper = new LinuxWallpaperService(),
                Autostart = new LinuxAutostartService(),
                SleepInhibitor = new LinuxSleepInhibitor(),
                Trash = new LinuxTrashService(),
                Shell = shell
            };
        }

        // macOS oder Unbekannt: Kernfunktionen laufen, Systemintegration ist deaktiviert.
        return new PlatformServices
        {
            Wallpaper = new UnsupportedWallpaperService(),
            Autostart = new UnsupportedAutostartService(),
            SleepInhibitor = new UnsupportedSleepInhibitor(),
            Trash = new UnsupportedTrashService(),
            Shell = shell
        };
    }
}
