namespace MyLittleSlideShow.Services;

/// <summary>
/// Spendenseite. Geoeffnet wird sie nur auf Klick auf "Spenden", im Browser des
/// Systems (ShellService) - kein Fenster im Programm, damit Zahlungsdaten und
/// Anmeldungen im gewohnten Browser bleiben. Vorher oder von selbst wird nichts gesendet.
/// </summary>
public static class Donation
{
    public const string Address = "https://ko-fi.com/systemswap";
}
