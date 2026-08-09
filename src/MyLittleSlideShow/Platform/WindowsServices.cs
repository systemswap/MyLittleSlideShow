using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MyLittleSlideShow.Platform;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsWallpaperService : IWallpaperService
{
    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    public bool IsSupported => true;

    public void SetWallpaper(string path, WallpaperStyle style)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Hintergrundbild nicht gefunden.", path);

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
        if (key is not null)
        {
            // WallpaperStyle: 0=Zentriert/Nebeneinander, 2=Gestreckt, 6=Anpassen, 10=Fuellen
            var (wallpaperStyle, tile) = style switch
            {
                WallpaperStyle.Zentriert => ("0", "0"),
                WallpaperStyle.Gestreckt => ("2", "0"),
                _ => ("10", "0")   // Angepasst -> Fuellen
            };
            key.SetValue("WallpaperStyle", wallpaperStyle);
            key.SetValue("TileWallpaper", tile);
        }

        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
    }
}

/// <summary>
/// Autostart ueber den Run-Registry-Key. Ersetzt die frueher genutzte
/// COM-Referenz IWshRuntimeLibrary (.lnk-Erzeugung), die nicht portierbar ist.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAutostartService : IAutostartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MyLittleSlideShow";

    public bool IsSupported => true;

    private static string ExecutablePath =>
        Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "MyLittleSlideShow.exe");

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        var value = key?.GetValue(ValueName) as string;
        if (string.IsNullOrEmpty(value)) return false;

        // Nur als aktiv melden, wenn der Eintrag auf die aktuelle exe zeigt.
        var registered = value.Trim('"');
        return string.Equals(registered, ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        key?.SetValue(ValueName, $"\"{ExecutablePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is not null)
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

[SupportedOSPlatform("windows")]
public sealed partial class WindowsSleepInhibitor : ISleepInhibitor
{
    [Flags]
    private enum ExecutionState : uint
    {
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
        Continuous = 0x80000000
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint SetThreadExecutionState(uint esFlags);

    private bool _active;

    public bool IsSupported => true;

    public void SetKeepAwake(bool keepAwake)
    {
        if (keepAwake == _active) return;

        if (keepAwake)
        {
            SetThreadExecutionState((uint)(ExecutionState.Continuous
                                           | ExecutionState.SystemRequired
                                           | ExecutionState.DisplayRequired));
        }
        else
        {
            SetThreadExecutionState((uint)ExecutionState.Continuous);
        }

        _active = keepAwake;
    }

    public void Dispose() => SetKeepAwake(false);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsTrashService : ITrashService
{
    private const int FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    public bool IsSupported => true;

    public bool MoveToTrash(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            // Doppeltes Null-Terminator-Ende ist Pflicht
            pFrom = path + '\0' + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING
        };

        return SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted;
    }
}
