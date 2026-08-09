using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace MyLittleSlideShow.Platform;

internal static class ProcessRunner
{
    /// <summary>Startet ein Kommando und wartet. Gibt true bei ExitCode 0 zurueck.</summary>
    public static bool Run(string fileName, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(10_000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool Exists(string command) => Run("/bin/sh", "-c", $"command -v {command} >/dev/null 2>&1");
}

/// <summary>
/// Setzt den Desktophintergrund. Da Linux keine einheitliche API dafuer hat,
/// wird die laufende Desktopumgebung erkannt und das passende Werkzeug genutzt.
/// </summary>
public sealed class LinuxWallpaperService : IWallpaperService
{
    private static string Desktop =>
        (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")
         ?? Environment.GetEnvironmentVariable("DESKTOP_SESSION")
         ?? string.Empty).ToLowerInvariant();

    public bool IsSupported =>
        ProcessRunner.Exists("gsettings")
        || ProcessRunner.Exists("xfconf-query")
        || ProcessRunner.Exists("plasma-apply-wallpaperimage")
        || ProcessRunner.Exists("feh")
        || ProcessRunner.Exists("swaybg");

    public void SetWallpaper(string path, WallpaperStyle style)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Hintergrundbild nicht gefunden.", path);

        var uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        var desktop = Desktop;

        if (desktop.Contains("gnome") || desktop.Contains("unity") || desktop.Contains("budgie"))
        {
            var option = style switch
            {
                WallpaperStyle.Zentriert => "centered",
                WallpaperStyle.Gestreckt => "stretched",
                _ => "zoom"
            };
            ProcessRunner.Run("gsettings", "set", "org.gnome.desktop.background", "picture-uri", uri);
            // Ab GNOME 42 gibt es einen eigenen Schluessel fuer den Dunkelmodus.
            ProcessRunner.Run("gsettings", "set", "org.gnome.desktop.background", "picture-uri-dark", uri);
            ProcessRunner.Run("gsettings", "set", "org.gnome.desktop.background", "picture-options", option);
            return;
        }

        if (desktop.Contains("cinnamon"))
        {
            ProcessRunner.Run("gsettings", "set", "org.cinnamon.desktop.background", "picture-uri", uri);
            return;
        }

        if (desktop.Contains("mate"))
        {
            ProcessRunner.Run("gsettings", "set", "org.mate.background", "picture-filename", Path.GetFullPath(path));
            return;
        }

        if (desktop.Contains("kde") || desktop.Contains("plasma"))
        {
            if (ProcessRunner.Run("plasma-apply-wallpaperimage", Path.GetFullPath(path))) return;
            // Fallback fuer aeltere Plasma-Versionen
            var script = $$"""
                var allDesktops = desktops();
                for (i = 0; i < allDesktops.length; i++) {
                    d = allDesktops[i];
                    d.wallpaperPlugin = "org.kde.image";
                    d.currentConfigGroup = ["Wallpaper", "org.kde.image", "General"];
                    d.writeConfig("Image", "{{uri}}");
                }
                """;
            ProcessRunner.Run("qdbus", "org.kde.plasmashell", "/PlasmaShell",
                              "org.kde.PlasmaShell.evaluateScript", script);
            return;
        }

        if (desktop.Contains("xfce"))
        {
            var full = Path.GetFullPath(path);
            // Alle Monitore/Arbeitsflaechen durchgehen
            ProcessRunner.Run("/bin/sh", "-c",
                $"xfconf-query -c xfce4-desktop -l | grep -E 'last-image$' | " +
                $"while read prop; do xfconf-query -c xfce4-desktop -p \"$prop\" -s \"{full}\"; done");
            return;
        }

        // Generische Fallbacks fuer schlanke WMs
        var fehMode = style switch
        {
            WallpaperStyle.Zentriert => "--bg-center",
            WallpaperStyle.Gestreckt => "--bg-scale",
            _ => "--bg-fill"
        };
        if (ProcessRunner.Run("feh", fehMode, Path.GetFullPath(path))) return;

        throw new PlatformNotSupportedException(
            "Der Desktophintergrund konnte nicht gesetzt werden. " +
            "Die Desktopumgebung wird nicht unterstuetzt oder gsettings/feh fehlt.");
    }
}

/// <summary>Autostart ueber eine .desktop-Datei nach XDG Autostart Spec.</summary>
public sealed class LinuxAutostartService : IAutostartService
{
    private const string FileName = "mylittleslideshow.desktop";

    private static string AutostartDir
    {
        get
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configHome))
                configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, "autostart");
        }
    }

    private static string FilePath => Path.Combine(AutostartDir, FileName);

    private static string ExecutablePath =>
        Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "MyLittleSlideShow");

    public bool IsSupported => true;

    public bool IsEnabled()
    {
        if (!File.Exists(FilePath)) return false;
        // Nur als aktiv melden, wenn der Eintrag auf die aktuelle Binaerdatei zeigt.
        var content = File.ReadAllText(FilePath);
        return content.Contains(ExecutablePath, StringComparison.Ordinal);
    }

    public void Enable()
    {
        Directory.CreateDirectory(AutostartDir);
        var sb = new StringBuilder();
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine("Type=Application");
        sb.AppendLine("Name=MyLittleSlideShow");
        sb.AppendLine("Comment=Kleine Diaschau fuer den Desktop");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Exec=\"{ExecutablePath}\"");
        sb.AppendLine("Terminal=false");
        sb.AppendLine("X-GNOME-Autostart-enabled=true");
        // Ein paar Sekunden Vorlauf: Startet die Anwendung gleichzeitig mit der
        // Arbeitsflaeche, ist der Systemabschnitt der Leiste oft noch nicht
        // bereit und das Symbol taucht nicht auf.
        sb.AppendLine("X-GNOME-Autostart-Delay=3");
        File.WriteAllText(FilePath, sb.ToString());
    }

    public void Disable()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}

/// <summary>
/// Haelt Bildschirm und System wach. Nutzt systemd-inhibit als langlaufenden
/// Kindprozess; die Sperre endet automatisch, wenn dieser beendet wird.
/// Ersetzt den alten Trick, alle 120 Sekunden den Mauszeiger zu bewegen.
/// </summary>
public sealed class LinuxSleepInhibitor : ISleepInhibitor
{
    private Process? _inhibitor;
    private Timer? _screensaverPoke;

    public bool IsSupported => ProcessRunner.Exists("systemd-inhibit") || ProcessRunner.Exists("xdg-screensaver");

    public void SetKeepAwake(bool keepAwake)
    {
        if (keepAwake)
        {
            if (_inhibitor is { HasExited: false }) return;
            StartInhibitor();
        }
        else
        {
            StopInhibitor();
        }
    }

    private void StartInhibitor()
    {
        if (ProcessRunner.Exists("systemd-inhibit"))
        {
            try
            {
                var psi = new ProcessStartInfo("systemd-inhibit")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("--what=idle:sleep");
                psi.ArgumentList.Add("--who=MyLittleSlideShow");
                psi.ArgumentList.Add("--why=Diaschau laeuft");
                psi.ArgumentList.Add("--mode=block");
                psi.ArgumentList.Add("sleep");
                psi.ArgumentList.Add("infinity");

                _inhibitor = Process.Start(psi);
                if (_inhibitor is not null) return;
            }
            catch
            {
                // faellt unten auf den Screensaver-Reset zurueck
            }
        }

        // Fallback: Bildschirmschoner regelmaessig zuruecksetzen
        _screensaverPoke = new Timer(_ =>
        {
            if (!ProcessRunner.Run("xdg-screensaver", "reset"))
                ProcessRunner.Run("xset", "s", "reset");
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(50));
    }

    private void StopInhibitor()
    {
        try
        {
            if (_inhibitor is { HasExited: false })
            {
                _inhibitor.Kill(entireProcessTree: true);
                _inhibitor.WaitForExit(2000);
            }
        }
        catch { /* Prozess bereits weg */ }
        finally
        {
            _inhibitor?.Dispose();
            _inhibitor = null;
        }

        _screensaverPoke?.Dispose();
        _screensaverPoke = null;
    }

    public void Dispose() => StopInhibitor();
}

/// <summary>
/// Papierkorb nach freedesktop.org Trash Spec. Bevorzugt "gio trash",
/// implementiert die Spezifikation sonst selbst.
/// </summary>
public sealed class LinuxTrashService : ITrashService
{
    public bool IsSupported => true;

    public bool MoveToTrash(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        var full = Path.GetFullPath(path);

        if (ProcessRunner.Exists("gio") && ProcessRunner.Run("gio", "trash", full))
            return true;

        if (ProcessRunner.Exists("trash-put") && ProcessRunner.Run("trash-put", full))
            return true;

        return MoveToTrashManually(full);
    }

    private static bool MoveToTrashManually(string full)
    {
        try
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(dataHome))
                dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                        ".local", "share");

            var trashFiles = Path.Combine(dataHome, "Trash", "files");
            var trashInfo = Path.Combine(dataHome, "Trash", "info");
            Directory.CreateDirectory(trashFiles);
            Directory.CreateDirectory(trashInfo);

            // Eindeutigen Zielnamen finden
            var name = Path.GetFileName(full);
            var target = Path.Combine(trashFiles, name);
            var counter = 1;
            while (File.Exists(target) || File.Exists(Path.Combine(trashInfo, Path.GetFileName(target) + ".trashinfo")))
            {
                name = $"{Path.GetFileNameWithoutExtension(full)}.{counter++}{Path.GetExtension(full)}";
                target = Path.Combine(trashFiles, name);
            }

            var info = new StringBuilder();
            info.AppendLine("[Trash Info]");
            info.AppendLine(CultureInfo.InvariantCulture, $"Path={Uri.EscapeDataString(full).Replace("%2F", "/")}");
            info.AppendLine(CultureInfo.InvariantCulture,
                            $"DeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}");

            File.WriteAllText(Path.Combine(trashInfo, name + ".trashinfo"), info.ToString());
            File.Move(full, target);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
