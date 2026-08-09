using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MyLittleSlideShow.Platform;

/// <summary>Oeffnet URLs, Dateien und Ordner plattformuebergreifend.</summary>
public sealed class ShellService : IShellService
{
    public void OpenUrl(string url) => OpenWithDefaultHandler(url);

    public void OpenFile(string path)
    {
        if (File.Exists(path)) OpenWithDefaultHandler(path);
    }

    public void RevealInFileManager(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (directory is null || !Directory.Exists(directory)) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // explorer markiert die Datei direkt
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Einige Dateimanager koennen die Datei markieren, andere nicht.
            if (ProcessRunner.Exists("nautilus") && ProcessRunner.Run("nautilus", "--select", path)) return;
            if (ProcessRunner.Exists("dolphin") && ProcessRunner.Run("dolphin", "--select", path)) return;
            if (ProcessRunner.Exists("nemo") && ProcessRunner.Run("nemo", path)) return;
            OpenWithDefaultHandler(directory);
            return;
        }

        OpenWithDefaultHandler(directory);
    }

    private static void OpenWithDefaultHandler(string target)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(new ProcessStartInfo("xdg-open", target) { UseShellExecute = false });
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo("open", target) { UseShellExecute = false });
                return;
            }

            // Windows: UseShellExecute laesst die Shell den Handler waehlen
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            // Bewusst still: das Oeffnen eines Links darf die App nicht abbrechen.
        }
    }
}

public sealed class UnsupportedWallpaperService : IWallpaperService
{
    public bool IsSupported => false;
    public void SetWallpaper(string path, WallpaperStyle style) =>
        throw new PlatformNotSupportedException("Desktophintergrund wird auf diesem System nicht unterstuetzt.");
}

public sealed class UnsupportedAutostartService : IAutostartService
{
    public bool IsSupported => false;
    public bool IsEnabled() => false;
    public void Enable() { }
    public void Disable() { }
}

public sealed class UnsupportedSleepInhibitor : ISleepInhibitor
{
    public bool IsSupported => false;
    public void SetKeepAwake(bool keepAwake) { }
    public void Dispose() { }
}

public sealed class UnsupportedTrashService : ITrashService
{
    public bool IsSupported => false;
    public bool MoveToTrash(string path) => false;
}
