# MyLittleSlideShow – .NET 10 / Avalonia

Portierung von WPF + WinForms (net7.0-windows) auf **.NET 10 + Avalonia 11**.
Läuft auf **Windows** und **Linux** aus derselben Codebasis.

## Bauen

Voraussetzung: .NET 10 SDK.

```bash
dotnet restore
dotnet build -c Release
dotnet run --project src/MyLittleSlideShow
```

## Veröffentlichen

Self-contained, damit auf dem Zielrechner keine Runtime nötig ist:

```bash
# Windows
dotnet publish src/MyLittleSlideShow -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true

# Linux
dotnet publish src/MyLittleSlideShow -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true
```

> `PublishTrimmed` ist bewusst **nicht** aktiviert. Avalonia und MetadataExtractor
> nutzen Reflection; Trimming ist möglich, erfordert aber Testen und ggf.
> `TrimmerRootDescriptor`-Einträge.

## Laufzeitabhängigkeiten unter Linux

Die App selbst ist verwaltet, aber die Systemintegration ruft Standardwerkzeuge auf:

| Funktion | Benötigt | Fällt zurück auf |
|---|---|---|
| Hintergrundbild | `gsettings` (GNOME/Cinnamon/MATE), `plasma-apply-wallpaperimage` (KDE), `xfconf-query` (XFCE) | `feh` |
| Papierkorb | `gio` (glib2) | `trash-put`, sonst eigene Umsetzung der freedesktop-Trash-Spec |
| Standby verhindern | `systemd-inhibit` | `xdg-screensaver reset` |
| Datei/Ordner öffnen | `xdg-open` (xdg-utils) | – |
| Tray-Icon | StatusNotifierItem-Unterstützung der Desktopumgebung | – |

Fehlt ein Werkzeug, wird die betroffene Schaltfläche ausgeblendet bzw. der
Menüpunkt deaktiviert – die App startet trotzdem.

Unter reinem GNOME braucht das Tray-Icon die Erweiterung
*AppIndicator and KStatusNotifierItem Support*.

## Wo liegen die Einstellungen?

`Properties.Settings` gibt es nicht mehr. Stattdessen JSON:

- Windows: `%APPDATA%\MyLittleSlideShow\settings.json`
- Linux: `~/.config/MyLittleSlideShow/settings.json`

Beim ersten Start unter Windows werden die Werte einer vorhandenen alten
Installation (`user.config`) automatisch übernommen.

## Projektstruktur

```
src/MyLittleSlideShow/
├── Program.cs                 Einstiegspunkt (ersetzt StartupUri)
├── App.axaml(.cs)             Theme, Tray-Icon, Lebenszyklus
├── Views/
│   ├── MainWindow             Diaschau-Fenster
│   ├── SettingsWindow         Einstellungen
│   ├── InfoWindow             EXIF-Anzeige
│   ├── AboutWindow            Changelog + versteckte Optionen
│   └── MyMessageBox           Ja/Nein-Dialog
├── Services/
│   ├── SettingsManager.cs     JSON-Einstellungen + Migration
│   ├── ImageProcessing.cs     Laden, Skalieren, Schärfen, Drehen, EXIF
│   ├── SkiaImaging.cs         Bildkern auf SkiaSharp (ohne Avalonia)
│   ├── JpegMetadata.cs        EXIF/ICC beim Drehen übernehmen
│   └── Donation.cs            PayPal-Link
└── Platform/
    ├── IPlatformServices.cs   Interfaces + Auswahl zur Laufzeit
    ├── WindowsServices.cs     Registry, SHFileOperation, P/Invoke
    ├── LinuxServices.cs       gsettings, gio, systemd-inhibit, XDG
    └── ShellService.cs        Öffnen von Dateien/URLs + Fallbacks
```

Details zur Umstellung siehe [MIGRATION.md](MIGRATION.md).
