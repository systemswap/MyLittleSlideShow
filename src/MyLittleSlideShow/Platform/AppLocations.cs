namespace MyLittleSlideShow.Platform;

/// <summary>
/// Wo das Programm liegt. Bei einer AppImage ist <see cref="Environment.ProcessPath"/> das
/// schreibgeschuetzte Innere, das bei jedem Start unter einem anderen Pfad haengt
/// (/tmp/.mount_...) und ausgehaengt wird, sobald das Programm endet.
/// Siehe docs/FEHLERGEDAECHTNIS.md, "Autostart zeigte bei der AppImage ins Leere".
/// </summary>
public static class AppLocations
{
    /// <summary>
    /// Was ein Autostart-Eintrag oder ein Neustart starten soll: die .AppImage-Datei selbst
    /// (Umgebungsvariable APPIMAGE der AppImage-Laufzeit), sonst das laufende Programm.
    /// </summary>
    public static string LaunchPath =>
        Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage
            ? appImage
            : Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "MyLittleSlideShow");
}
