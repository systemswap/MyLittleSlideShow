# MyLittleSlideShow

Kleine Diaschau für den Desktop – .NET 10 und Avalonia 12, läuft auf
**Windows** und **Linux** aus derselben Codebasis.

## Bauen und starten

Voraussetzung: .NET 10 SDK.

```bash
dotnet run --project src/MyLittleSlideShow
```

## Veröffentlichen

| System | Befehl | Ergebnis |
|---|---|---|
| Linux | `./publish.sh linux` | `published/linux-x64/MyLittleSlideShow-x86_64.AppImage` – eine Datei, .NET und Avalonia enthalten |
| Windows (unter Linux gebaut) | `./publish.sh windows` | `published/windows-x64/MyLittleSlideShow.exe` – eine Datei |
| Windows (unter Windows gebaut) | `.\publish.ps1` | `published\windows-x64\MyLittleSlideShow.exe` |

`./publish.sh` ohne Argumente fragt nach Paketform (eine Datei oder alle
Bibliotheken einzeln) und Ziel (Linux, Windows, beides). Neben dem Programm
liegen jeweils `LICENSE.txt`, `THIRD-PARTY-NOTICES.md` und `licenses/`, daneben
die Archive `published/MyLittleSlideShow-<Version>-linux-x64.tar.gz` bzw.
`-windows-x64.zip`. Die Programmdatei trägt keine Versionsnummer, damit
Menü- und Autostart-Eintrag nach dem nächsten Bau weiter stimmen.

Unter Linux trägt `publish.sh` das Programm danach ins Anwendungsmenü ein
(`published/linux-x64/einrichten.sh`, rückgängig mit `entfernen.sh`).

## Release auf GitHub

1. `<Version>` in `src/MyLittleSlideShow/MyLittleSlideShow.csproj` anheben –
   die einzige Stelle mit der Versionsnummer – und `Infos/Changelog.txt` ergänzen.
2. Committen und auf `main` pushen. Der Workflow baut Windows und Linux zur Probe
   und prüft die Lizenzen.
3. `git tag vX.Y.Z && git push origin vX.Y.Z` – der Workflow baut erneut und
   legt das Release mit EXE und AppImage an. Passt das Tag nicht zur Version im
   csproj, bricht er ab.

Sicherheitsupdates für Pakete schlägt Dependabot automatisch als Pull Request vor.

## Laufzeitabhängigkeiten unter Linux

| Funktion | Benötigt | Fällt zurück auf |
|---|---|---|
| Hintergrundbild | `gsettings` (GNOME/Cinnamon/MATE), `plasma-apply-wallpaperimage` (KDE), `xfconf-query` (XFCE) | `feh` |
| Papierkorb | `gio` (glib2) | `trash-put`, sonst eigene Umsetzung der freedesktop-Trash-Spec |
| Standby verhindern | `systemd-inhibit` | `xdg-screensaver reset` |
| Datei/Ordner öffnen | `xdg-open` (xdg-utils) | – |
| Tray-Icon | StatusNotifierItem; unter GNOME die Erweiterung *AppIndicator and KStatusNotifierItem Support* | – |

Fehlt ein Werkzeug, wird die betroffene Schaltfläche ausgeblendet – die App startet trotzdem.

## Einstellungen

- Windows: `%APPDATA%\MyLittleSlideShow\settings.json`
- Linux: `~/.config/MyLittleSlideShow/settings.json`

## Projektstruktur

```
src/MyLittleSlideShow/
├── Program.cs, App.axaml       Start, Tray-Icon
├── Views/
│   ├── MainWindow              Diaschau-Fenster, Platzierung auf dem Bildschirm
│   ├── SettingsWindow          Einstellungen mit Live-Vorschau
│   ├── InfoWindow              Dateiinfos (EXIF, IPTC, XMP, ICC)
│   ├── AboutWindow             Changelog, versteckte Optionen, Lizenzen
│   ├── MyMessageBox            Ja/Nein-Dialog
│   └── ScreenFit.cs            Dialoge passen immer auf den Bildschirm
├── Themes/FineTheme.axaml      gemeinsames Design der Dialoge
├── Services/
│   ├── SkiaImaging.cs          Bildkern: dekodieren, skalieren, schärfen, kodieren
│   ├── ImageProcessing.cs      Bildfunktionen für die Oberfläche
│   ├── JpegMetadata.cs         EXIF/ICC beim Drehen übernehmen
│   ├── ImageMetadataReader.cs  Dateiinfos (EXIF, IPTC, XMP, ICC)
│   └── SettingsManager.cs      JSON-Einstellungen
└── Platform/                   Windows/Linux: Hintergrund, Autostart, Papierkorb, Standby
packaging/linux/                Menüeintrag, Symbol, einrichten.sh / entfernen.sh
packaging/licenses/             Lizenztexte der Fremdbestandteile
tools/check-licenses.py         Lizenzprüfung im Workflow
docs/                           Fehlergedächtnis, Arbeitsregeln
```

## Lizenz und Telemetrie

MyLittleSlideShow steht unter der [MIT-Lizenz](LICENSE). Alle Fremdbestandteile
stehen unter freien Lizenzen (MIT, Apache-2.0, BSD-3-Clause, OFL-1.1) – Übersicht
in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), im Programm unter
*Einstellungen › Infos › Lizenzen*. Ändert ein Paket seine Lizenz, schlägt der
Build fehl (`tools/check-licenses.py`).

Das Programm sendet nichts. Beim Bauen ist Telemetrie ebenfalls aus: Avalonias
Build-Telemetrie im csproj, .NET-CLI und MSBuild über
`DOTNET_CLI_TELEMETRY_OPTOUT` / `MSBUILD_TELEMETRY_OPTOUT` in den Skripten und
im Workflow. Für eigene `dotnet`-Aufrufe einmalig dauerhaft setzen:
`setx DOTNET_CLI_TELEMETRY_OPTOUT 1` und `setx MSBUILD_TELEMETRY_OPTOUT 1`.

Hinweise zum Weiterentwickeln: [ENTWICKLUNG.md](ENTWICKLUNG.md), Regeln für die
Arbeit am Projekt: [docs/ARBEITSREGELN.md](docs/ARBEITSREGELN.md).
