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
- **Lizenzprüfung** (`tools/check-licenses.py`): Pakete ohne SPDX-Kennung sind
  nur mit einem von Hand geprüften Eintrag für genau ihre Version erlaubt. Bringt
  ein Avalonia-Update eine neue ANGLE-Version mit, schlägt die Prüfung an: Lizenz
  in der `LICENSE` des Pakets ansehen, Eintrag in `REVIEWED` anpassen und
  `packaging/licenses/` bzw. `THIRD-PARTY-NOTICES.md` nachziehen.

## Bilder

- Gerechnet wird mit SkiaSharp (`Services/SkiaImaging.cs`, ohne Avalonia,
  dadurch einzeln testbar). Alle Bitmaps sind Bgra8888, vormultipliziertes Alpha.
- Für die Anzeige werden Farben anhand des ICC-Profils nach sRGB umgerechnet,
  beim Drehen nicht – dort wird das Originalprofil wieder ins JPEG geschrieben
  (`JpegMetadata.cs`, samt EXIF mit Orientierung 1).
- Das Bild wird für genau die sichtbare Fläche in physischen Pixeln berechnet
  (`MainWindow.ImageTargetSize`, Bitmap mit 96 dpi × Skalierung) – so wird es 1:1
  angezeigt und die Schärfung nicht durch Nachskalieren abgeschwächt.
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
