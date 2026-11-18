# Hinweise zum Weiterentwickeln

Worauf beim Ändern zu achten ist. Wie das Projekt dahin gekommen ist
(WPF → Avalonia, ImageSharp → SkiaSharp, Avalonia 11 → 12), steht in der
Git-Historie und im Changelog (`src/MyLittleSlideShow/Infos/Changelog.txt`).

## Abhängigkeiten aktualisieren

- **SkiaSharp** muss genau die Version haben, die `Avalonia.Skia` verlangt
  (verwalteter und nativer Teil müssen zusammenpassen). Bei jedem
  Avalonia-Update im csproj mitziehen – nachsehen in der `.nuspec` von
  `Avalonia.Skia`.
- **Avalonia.BuildServices** steht nur im csproj, um Avalonias Build-Telemetrie
  per `ExcludeAssets="all"` abzuschalten. Die Version muss der von Avalonia
  geforderten entsprechen, sonst bricht `restore` mit NU1605 ab.
- **Lizenzen bei jedem Update von Hand prüfen:** Neue oder geänderte Pakete
  (auch indirekt mitkommende wie ANGLE mit Avalonia) in ihrer `.nuspec` bzw.
  `LICENSE` ansehen und `packaging/licenses/` sowie `THIRD-PARTY-NOTICES.md`
  nachziehen.

## Bilder

- Gerechnet wird mit SkiaSharp (`Services/SkiaImaging.cs`, ohne Avalonia,
  dadurch einzeln testbar). Alle Bitmaps sind Bgra8888, vormultipliziertes Alpha.
- Für die Anzeige werden Farben anhand des ICC-Profils nach sRGB umgerechnet,
  beim Drehen nicht – dort wird das Originalprofil wieder ins JPEG geschrieben
  (`JpegMetadata.cs`, samt EXIF mit Orientierung 1).
- Das Bild wird für genau die sichtbare Fläche in physischen Pixeln berechnet
  (`MainWindow.ImageTargetSize`); `ShowImage` verkleinert es um die
  Bildschirmskalierung – so wird es 1:1 angezeigt und die Schärfung nicht durch
  Nachskalieren abgeschwächt. Die Bitmap hat immer 96 dpi: mit 96 dpi ×
  Skalierung zeichnet Avalonia 12 nur einen Ausschnitt (`docs/FEHLERGEDAECHTNIS.md`).
- Formate: jpg, png, gif, bmp, webp. Kein TIFF (Skia dekodiert es nicht), GIF
  lässt sich nicht drehen (kein Encoder).
- Dekodiert wird synchron auf dem UI-Thread; bei sehr großen Bildern wäre
  `Task.Run` die naheliegende Verbesserung.

## Fenster

- Das Hauptfenster liegt immer vollständig im Arbeitsbereich eines Bildschirms
  (`PlaceFullyVisible`, auch bei `Screens.Changed`). Position in physischen
  Pixeln, Größe geräteunabhängig – umgerechnet mit der Skalierung des Bildschirms.
- Dialoge binden `ScreenFit.Attach(this)` ein: höchstens 85 % Höhe / 90 % Breite
  des Arbeitsbereichs, Rahmen eingerechnet.
- Avalonia-Dialoge sind nur asynchron (`await ShowDialog`).

## Plattformen

Alles Betriebssystemspezifische liegt in `Platform/` hinter Interfaces;
nicht unterstützte Funktionen melden `IsSupported = false` und die Bedienelemente
werden ausgeblendet.

| Funktion | Windows | Linux |
|---|---|---|
| Hintergrundbild | `SystemParametersInfo` + Registry | `gsettings`, `plasma-apply-wallpaperimage`, `xfconf-query`, `feh` |
| Autostart | Registry-Run-Key | `~/.config/autostart/*.desktop` |
| Standby verhindern | `SetThreadExecutionState` | `systemd-inhibit`, sonst `xdg-screensaver reset` |
| Papierkorb | `SHFileOperation` | `gio trash`, sonst freedesktop-Trash-Spec |

Unter Linux hängt die Transparenz des rahmenlosen Fensters vom Compositor ab.

## Release auf GitHub

1. `<Version>` in `src/MyLittleSlideShow/MyLittleSlideShow.csproj` anheben –
   die einzige Stelle mit der Versionsnummer – und `Infos/Changelog.txt` ergänzen.
2. Committen und auf `main` pushen. Der Workflow baut Windows und Linux zur Probe.
3. Nur bei einem abgenommenen Meilenstein (Version ohne `-beta`, siehe
   `docs/ARBEITSREGELN.md`): `git tag vX.Y.Z && git push origin vX.Y.Z` – der
   Workflow baut erneut und legt das Release mit EXE und AppImage an. Passt das
   Tag nicht zur Version im csproj, bricht er ab.

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
docs/                           Fehlergedächtnis, Arbeitsregeln
```

## Bilder der Startseite

`docs/bilder/readme/` enthält Banner, Höhepunkte und Fensteraufnahmen für die
README. Sie sind unsichtbar mit Avalonia Headless gerendert, die Beispielmotive
selbst gezeichnet – keine echten Fotos, keine persönlichen Pfade (im
Einstellungsfenster steht ein neutraler Beispielpfad).
