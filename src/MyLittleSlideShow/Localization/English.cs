namespace MyLittleSlideShow.Localization;

/// <summary>
/// Eingebautes Englisch. Schluessel ist der deutsche Text aus Code oder XAML;
/// <c>tools/check-translations.py</c> meldet fehlende und nicht mehr benutzte
/// Eintraege. Platzhalter {0}, {1} … muessen in der Uebersetzung erhalten bleiben.
/// </summary>
public static class English
{
    // Die Tabelle zuerst: statische Felder werden in Textreihenfolge belegt, Language unten braucht sie.
    private static readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal)
    {
        // ------------------------------------------------------------ Hauptfenster
        ["vorheriges Bild"] = "previous image",
        ["DiaShow Start/Stop"] = "Slide show start/stop",
        ["nächstes Bild"] = "next image",
        ["Größe ändern: links = größer, rechts = kleiner"] = "Resize: left = larger, right = smaller",
        ["Exif Daten"] = "Exif data",
        ["Aktuelles Bild als Desktophintergrund verwenden"] = "Use the current image as desktop background",
        ["Bild in den Papierkorb verschieben"] = "Move the image to the recycle bin",
        ["90° gegen Uhrzeigersinn drehen"] = "Rotate 90° counter-clockwise",
        ["90° im Uhrzeigersinn drehen"] = "Rotate 90° clockwise",
        ["Einstellungen"] = "Settings",
        ["MyLittleSlideShow beenden"] = "Quit MyLittleSlideShow",
        ["Datei: {0} / {1}"] = "File: {0} / {1}",
        ["Ladezeit [ms]:"] = "Load [ms]:",
        ["Timer:"] = "Timer:",
        ["angehalten"] = "stopped",
        ["Desktophintergrund ändern"] = "Change desktop background",
        ["Soll dieses Bild als Desktophintergrund genutzt werden?"] = "Use this image as desktop background?",
        ["Spenden"] = "Donate",
        ["MyLittleSlideShow unterstützen – öffnet die Spendenseite auf Ko-fi im Browser"] =
            "Support MyLittleSlideShow – opens the donation page on Ko-fi in the browser",

        // ------------------------------------------------------------ Tray-Menue
        ["Anzeigen"] = "Show",
        ["Minimieren"] = "Minimize",
        ["Neu starten"] = "Restart",
        ["Beenden"] = "Quit",

        // ------------------------------------------------------------ Dialoge
        ["Ja"] = "Yes",
        ["Nein"] = "No",
        ["Schließen"] = "Close",
        ["Abbrechen"] = "Cancel",
        ["Speichern"] = "Save",

        // ------------------------------------------------------------ Einstellungen
        ["MyLittleSlideShow Einstellungen"] = "MyLittleSlideShow settings",
        ["Infos"] = "About",
        ["SPRACHE"] = "LANGUAGE",
        ["Weitere Sprachen als Datei im Ordner languages"] = "More languages as files in the folder languages",
        ["Beispieldatei erstellen"] = "Create sample file",
        ["Ordner öffnen"] = "Open folder",
        ["Beispieldatei geschrieben: {0}"] = "Sample file written: {0}",
        ["Die Beispieldatei konnte nicht geschrieben werden: {0}"] = "The sample file could not be written: {0}",
        ["Der Ordner {0} konnte nicht angelegt werden: {1}"] = "The folder {0} could not be created: {1}",
        ["{0}: Deutsch ist eingebaut und lässt sich nicht ersetzen."] = "{0}: German is built in and cannot be replaced.",
        ["kein JSON-Objekt"] = "not a JSON object",
        ["ALLGEMEIN"] = "GENERAL",
        ["Automatisch beim Anmelden starten"] = "Start automatically at sign-in",
        ["Bildschirm nicht abschalten, solange die Diaschau läuft"] = "Keep the screen on while the slide show is running",
        ["DARSTELLUNG"] = "DISPLAY",
        ["Bilder schärfen"] = "Sharpen images",
        ["Stärke 0–99 · Standard 25"] = "Strength 0–99 · default 25",
        ["Ruhiges Schärfen ab Schwelle"] = "Calm sharpening above threshold",
        ["Schwelle 0–64 · Standard 6"] = "Threshold 0–64 · default 6",
        ["Verstärkt kein Rauschen in glatten Flächen wie Himmel oder Haut."] =
            "Does not amplify noise in smooth areas such as sky or skin.",
        ["Beim Zoomen mitstrecken"] = "Stretch while zooming",
        ["Neu berechnet und geschärft wird erst, wenn das Mausrad steht."] =
            "Recalculated and sharpened only once the mouse wheel stops.",
        ["AmbiFrame anzeigen"] = "Show AmbiFrame",
        ["Deckkraft 0–99 · Standard 90"] = "Opacity 0–99 · default 90",
        ["DIASCHAU"] = "SLIDE SHOW",
        ["Bilder automatisch wechseln"] = "Change images automatically",
        ["Zeit bis zum nächsten Bild, 5 Sekunden bis 24 Stunden"] = "Time until the next image, 5 seconds to 24 hours",
        ["Zufällige Reihenfolge"] = "Random order",
        ["DESKTOPHINTERGRUND"] = "DESKTOP BACKGROUND",
        ["Jedes Bild beim Wechsel auch als Hintergrund setzen"] = "Also set every new image as background",
        ["Anordnung"] = "Arrangement",
        ["Zentriert"] = "Centered",
        ["Gestreckt"] = "Stretched",
        ["Angepasst"] = "Fit",
        ["Für diese Desktopumgebung ist kein Werkzeug zum Setzen des Hintergrundbilds verfügbar (erwartet werden gsettings, plasma-apply-wallpaperimage, xfconf-query oder feh)."] =
            "No tool for setting the background image is available for this desktop environment (expected: gsettings, plasma-apply-wallpaperimage, xfconf-query or feh).",
        ["BILDERORDNER"] = "IMAGE FOLDER",
        ["(kein Pfad gewählt)"] = "(no folder selected)",
        ["Unterordner einbeziehen"] = "Include subfolders",
        ["Zurücksetzen"] = "Reset",
        ["Ordner wählen …"] = "Choose folder …",
        ["Ordner mit Bildern wählen"] = "Choose a folder with images",
        ["Unterstützte Formate: jpg, jpeg, png, gif, bmp, webp"] = "Supported formats: jpg, jpeg, png, gif, bmp, webp",

        // ------------------------------------------------------------ Infos
        ["Versteckte Optionen"] = "Hidden options",
        ["Changelog"] = "Changelog",
        ["Lizenzen"] = "Licenses",
        ["Datei konnte nicht gelesen werden."] = "The file could not be read.",
        ["Befindet sich die Maus über dem gezeigten Bild sind folgende Optionen möglich."] =
            "With the mouse over the image, the following options are available.",
        ["-\tBildwechsel durch Drehen des Mausrades"] = "-\tChange the image by turning the mouse wheel",
        ["-\tVergrößern und Verkleinern der Anwendung durch"] = "-\tEnlarge or shrink the window by",
        ["\tgedrückt halten der rechten Maustaste oder Strg (ctrl) und drehen des Mausrades"] =
            "\tholding the right mouse button or Ctrl and turning the mouse wheel",
        ["Bild in Standard Bildbetrachter öffnen"] = "Open the image in the default image viewer",
        ["- doppel Klick auf das Bild"] = "- double-click the image",
        ["Die Anwendung muß aktiv bzw. einmal mit der linken Maustaste angeklickt wurden sein."] =
            "The window must be active, i.e. clicked once with the left mouse button.",

        // ------------------------------------------------------------ Dateiinfos
        ["Dateiinfos"] = "File info",
        ["Verzeichnis öffnen"] = "Open folder",
        ["Nicht alles war lesbar: {0}"] = "Not everything could be read: {0}",
        ["Alle Einträge anzeigen ({0})"] = "Show all entries ({0})",
        ["Alle Einträge ausblenden"] = "Hide all entries",
        ["… weitere Einträge ausgelassen (mehr als {0})."] = "… further entries omitted (more than {0}).",
        ["Datei"] = "File",
        ["Name"] = "Name",
        ["Ordner"] = "Folder",
        ["Format"] = "Format",
        ["Dateigröße"] = "File size",
        ["Bildmaße"] = "Dimensions",
        ["{0} × {1} Pixel ({2:0.#} MP)"] = "{0} × {1} pixels ({2:0.#} MP)",
        ["Auflösung"] = "Resolution",
        ["Erstellt"] = "Created",
        ["Geändert"] = "Modified",
        ["Kamera"] = "Camera",
        ["Hersteller"] = "Make",
        ["Modell"] = "Model",
        ["Objektiv"] = "Lens",
        ["Software"] = "Software",
        ["Aufnahme"] = "Capture",
        ["Aufnahmedatum"] = "Date taken",
        ["Belichtungszeit"] = "Exposure time",
        ["Blende"] = "Aperture",
        ["Brennweite"] = "Focal length",
        ["entspricht {0} mm Kleinbild"] = "{0} mm in 35 mm terms",
        ["Belichtungskorrektur"] = "Exposure compensation",
        ["Programm"] = "Program",
        ["Manuell"] = "Manual",
        ["Programmautomatik"] = "Program AE",
        ["Zeitautomatik (Blende vorgewählt)"] = "Aperture priority",
        ["Blendenautomatik (Zeit vorgewählt)"] = "Shutter priority",
        ["Kreativprogramm (Schärfentiefe)"] = "Creative program (depth of field)",
        ["Actionprogramm (kurze Zeit)"] = "Action program (fast shutter)",
        ["Porträt"] = "Portrait",
        ["Landschaft"] = "Landscape",
        ["Blitz"] = "Flash",
        ["ausgelöst"] = "fired",
        ["nicht ausgelöst"] = "did not fire",
        ["Weißabgleich"] = "White balance",
        ["automatisch"] = "auto",
        ["manuell"] = "manual",
        ["Ort"] = "Location",
        ["Koordinaten"] = "Coordinates",
        ["O"] = "E",
        ["Höhe"] = "Altitude",
        ["Urheber und Beschreibung"] = "Author and description",
        ["Urheber"] = "Author",
        ["Copyright"] = "Copyright",
        ["Titel"] = "Title",
        ["Beschreibung"] = "Description",
        ["Stichwörter"] = "Keywords",
        ["Bewertung"] = "Rating",
        ["Farbprofil"] = "Colour profile",
        ["Profil"] = "Profile",
    };

    public static IReadOnlyDictionary<string, string> Texts => _texts;

    public static readonly Language Language = new("en", "English", new Flag(FlagKind.UnionJack), _texts);
}
