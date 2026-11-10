# Migration: WPF/WinForms (net7.0-windows) → Avalonia 11 (net10.0)

Dieses Dokument beschreibt, was beim Portieren von MyLittleSlideShow 1.9.1.8
geändert wurde und worauf beim Weiterentwickeln zu achten ist.

## 1. Projekt und Zielplattform

| Alt | Neu |
|---|---|
| `net7.0-windows`, `UseWPF`, `UseWindowsForms` | `net10.0`, Avalonia 11.3 |
| `RuntimeIdentifier win-x64` | `win-x64`, `linux-x64`, `linux-arm64` |
| ClickOnce-Publish-Eigenschaften in der csproj | entfernt |
| `COMReference IWshRuntimeLibrary` | entfällt (Autostart ohne COM) |
| `Properties/Settings.settings` | `Services/SettingsManager.cs` (JSON) |

## 2. UI-Mapping WPF → Avalonia

| WPF / WinForms | Avalonia |
|---|---|
| `.xaml` | `.axaml` (Namespace `https://github.com/avaloniaui`) |
| `Window.DragMove()` | `BeginMoveDrag(pointerPressedEventArgs)` |
| `MouseWheel` | `PointerWheelChanged` (`e.Delta.Y`, `e.KeyModifiers`) |
| `MouseLeftButtonDown` | `PointerPressed` + `e.GetCurrentPoint(this).Properties` |
| `MouseMove`/`MouseLeave`-Handler für Hover | Styles mit `:pointerover` (ca. 24 Handler entfallen) |
| `Label` | `TextBlock` |
| `Visibility.Visible/Collapsed` | `IsVisible` (bool) |
| `Window.Clip` mit `RectangleGeometry` | `Border` mit `CornerRadius` + `ClipToBounds` |
| `AllowsTransparency`/`WindowStyle=None` | `WindowDecorations="None"` (bis Avalonia 11: `SystemDecorations`), `TransparencyLevelHint` |
| `Topmost` | `Topmost` (unverändert) |
| WinForms `NotifyIcon` + `ContextMenuStrip` | `TrayIcon` + `NativeMenu` in `App.axaml` |
| WinForms `Timer` | `DispatcherTimer` |
| `FolderBrowserDialog` | `StorageProvider.OpenFolderPickerAsync` (async!) |
| `MessageBox.Show` | `MyMessageBox.ShowDialogAsync` → `Task<bool>` |
| `System.Windows.Forms.Screen.AllScreens` | `Window.Screens.All` |
| Manuelles Strg-Tracking über KeyDown/KeyUp | `KeyModifiers.Control` im Wheel-Event |
| `UserControl_InfoWindow` | eigenständiges `AboutWindow` mit `TabControl` |

Wichtig: Avalonia-Dialoge sind ausschließlich asynchron. Aufrufer, die früher
blockierend `ShowDialog()` benutzt haben, sind auf `async void`-Handler mit
`await` umgestellt worden.

## 3. Bildverarbeitung

`ZZZ_ImageProcessing.cs` (GDI+/System.Drawing) wurde vollständig durch
`Services/ImageProcessing.cs` auf Basis von **SixLabors.ImageSharp 3.1.5**
ersetzt:

| Alt (GDI+) | Neu (ImageSharp) |
|---|---|
| `Graphics.DrawImage` + `InterpolationMode.HighQualityBicubic` | `Mutate(x => x.Resize(..., KnownResamplers.Lanczos3))` |
| Eigener Faltungskern zum Schärfen | Unschärfemaskierung über `GaussianBlur` + eigene Mischung |
| `RotateFlip` anhand EXIF-Tag 274 (nur Fall 6 und 8) | `AutoOrient()` (alle 8 Orientierungen) |
| `ImageCodecInfo` / Encoder-Parameter | `JpegEncoder { Quality = ... }` |
| `ZZZ_GetKindOfFile.cs` (urlmon + Registry-MIME) | `Image.DetectFormat` |
| `ExifReader.cs` + `ExifTags.cs` (976 + 310 Zeilen) | `ImageMetadata.ExifProfile` |

Die Übergabe an Avalonia erfolgt direkt über `WriteableBitmap` und
`DangerousGetPixelRowMemory` — kein PNG-Roundtrip über einen MemoryStream mehr.

**Lizenz:** ImageSharp steht unter der Six Labors Split License. Für
Open-Source-/private Nutzung ist sie kostenlos; kommerzielle Nutzung oberhalb
der Freigrenze erfordert eine kostenpflichtige Lizenz. Alternative wäre
SkiaSharp (MIT), das aber weniger Bildoperationen mitbringt.

### 3a. Wechsel von ImageSharp auf SkiaSharp (Version 2.1.1)

ImageSharp 3.1.x hatte mehrere bekannte Sicherheitslücken (u. a.
GHSA-v76p-62qx-wwq2, GHSA-j9gm-c75j-xc9q – überwiegend in den TIFF-Codecs).
Behoben sind sie erst ab 3.2.0, und ab dieser Version bricht jeder
Release-Build ohne Six-Labors-Lizenzschlüssel ab. Deshalb wurde ImageSharp
vollständig ersetzt:

| Aufgabe | jetzt |
|---|---|
| Dekodieren, EXIF-Ausrichtung | SkiaSharp `SKCodec`, `EncodedOrigin` (alle 8 Fälle); große JPEGs werden schon beim Dekodieren verkleinert |
| Skalieren | Halbierungsschritte (2×2-Mittel) + abschließend bikubisch – visuell gleichwertig zu Lanczos3, etwa doppelt so schnell |
| Schärfen | unverändert der eigene 3×3-Unschärfemaskierungs-Algorithmus, jetzt direkt im Bildspeicher der Avalonia-Bitmap |
| Drehen + Speichern | Skia-Encoder (JPEG q100 4:4:4, PNG, WebP verlustfrei, BMP selbst geschrieben); EXIF und ICC-Profil werden bei JPEG übernommen, die Orientierung auf 1 gesetzt (`JpegMetadata.cs`) |
| EXIF-Anzeige | **MetadataExtractor** (Apache-2.0) |

SkiaSharp kommt ohnehin mit Avalonia (MIT); die Paketversion im csproj muss
der von Avalonia.Skia entsprechen.

### 3b. Avalonia 12 und SkiaSharp 3 (Version 2.3.0)

Avalonia 11.3 → 12.1, SkiaSharp 2.88 → 3.119. Der Code übersetzte ohne
Fehler; angepasst wurden nur als veraltet markierte Stellen:

| Vorher | Jetzt |
|---|---|
| `SystemDecorations="None"` | `WindowDecorations="None"` |
| `ScalePixels(…, SKFilterQuality.High)` (Mitchell) | `ScalePixels(…, new SKSamplingOptions(SKCubicResampler.CatmullRom))` – näher an Lanczos3 |
| `Avalonia.Diagnostics` (nur Debug, wurde nie angehängt) | entfernt; der Nachfolger `AvaloniaUI.DiagnosticsSupport` gehört zum kommerziellen Accelerate-Umfeld |

Beim Testen auf einem Laptop (1920 × 1080, 125 %) fielen zwei Dinge auf, die
unabhängig von Avalonia 12 sind und mitbehoben wurden: Das Hauptfenster
wurde nur anhand seiner linken oberen Ecke geprüft (jetzt immer vollständig
sichtbar, auch bei Monitorwechsel im Betrieb), und das Einstellungsfenster
war höher als der Bildschirm (jetzt zweispaltig, alle Dialoge höchstens
85 % der Bildschirmhöhe).

Wegfall: **TIFF** wird nicht mehr angezeigt (Skia hat keinen TIFF-Decoder),
und **GIF** kann nicht mehr gedreht werden (Skia hat keinen GIF-Encoder).

## 4. Einstellungen

`Properties.Settings.Default` gibt es nicht mehr. Stattdessen
`SettingsManager` mit JSON-Datei:

- Windows: `%APPDATA%\MyLittleSlideShow\settings.json`
- Linux: `~/.config/MyLittleSlideShow/settings.json`

Geschrieben wird atomar (Temp-Datei + Replace). Beim ersten Start wird einmalig
versucht, die alte `user.config` unter `%LOCALAPPDATA%\MTS-Solutions.de` zu
lesen und die 20 bekannten Werte zu übernehmen.

## 5. Plattformabstraktion

Alles Betriebssystemspezifische liegt hinter Interfaces in `Platform/`
(`IWallpaperService`, `IAutostartService`, `ISleepInhibitor`, `ITrashService`,
`IShellService`), die Auswahl erfolgt zur Laufzeit über `PlatformServices.Current`.

| Funktion | Windows | Linux |
|---|---|---|
| Wallpaper setzen | `SystemParametersInfo` + Registry (Stil) | `gsettings`, `plasma-apply-wallpaperimage`, `xfconf-query`, `feh` |
| Autostart | Registry-Run-Key (statt COM/.lnk) | XDG-Autostart `.desktop` in `~/.config/autostart` |
| Standby verhindern | `SetThreadExecutionState` | `systemd-inhibit` als Kindprozess, Fallback `xdg-screensaver reset` alle 50 s |
| Papierkorb | `SHFileOperation` mit `FOF_ALLOWUNDO` | `gio trash`, Fallback: eigene Umsetzung der freedesktop-Trash-Spec |
| Ordner im Dateimanager öffnen | `explorer.exe` | `xdg-open` |

Nicht unterstützte Funktionen werfen keine Ausnahme, sondern melden über
`IsSupported` zurück; die zugehörigen Bedienelemente werden ausgeblendet oder
deaktiviert.

Der alte Linux-untaugliche Trick, alle 120 s per `Cursor.Position` den
Mauszeiger zu bewegen und `SendKeys` zu schicken, entfällt ersatzlos.

## 6. Ersatzlos entfallene Dateien

| Datei | Grund |
|---|---|
| `ZZZ_ThumbnailProvider.cs` (203 Z.) | wurde nie aufgerufen, shell32/gdi32-P/Invoke |
| `ZZZ_GetKindOfFile.cs` (302 Z.) | ersetzt durch Formaterkennung des Decoders |
| `ExifReader.cs` (976 Z.) | ersetzt durch MetadataExtractor (zuvor ImageSharp-Metadaten) |
| `ExifTags.cs` (310 Z.) | dito |
| `ZZZ_PreventSleep.cs` | ersetzt durch `ISleepInhibitor` |
| `Properties/*` (Settings, Resources, AssemblyInfo) | ersetzt durch JSON bzw. SDK-Attribute |
| `ZZZ_StartUpManager.cs` (215 Z.) | ersetzt durch `IAutostartService` |
| `ZZZ_ChangeWindowsWallpaper.cs` | ersetzt durch `IWallpaperService` |

## 7. Verhaltensänderungen

- Fensterrahmenlose Transparenz (`TransparencyLevelHint`) hängt unter Linux vom
  Compositor ab; ohne Compositor fällt Avalonia auf undurchsichtig zurück.
- Das Tray-Icon benötigt unter GNOME eine AppIndicator-Erweiterung.
- Die EXIF-Ausrichtung wird jetzt für alle acht Orientierungswerte korrekt
  angewandt, nicht nur für 6 und 8 — Bilder können also anders herum erscheinen
  als in Version 1.9.1.8.
- Das Schärfen rechnet dieselbe Formel wie 1.9.1.8 (`Original + Wert/10 · (Original − Weichzeichnung)`),
  bearbeitet aber auch die Randpixel und bietet mit „Ruhiges Schärfen" optional
  einen Schwellwert gegen Rauschverstärkung.
- Bildskalierung nutzt Lanczos3 statt HighQualityBicubic; die Ergebnisse sehen
  minimal anders (schärfer) aus.
- Die Bilddekodierung läuft weiterhin synchron auf dem UI-Thread. Das wurde
  bewusst so übernommen, um die Ablauflogik unverändert zu lassen; eine
  Umstellung auf `Task.Run` wäre die naheliegende nächste Verbesserung.
- Das Drehen von JPEGs ist wie im Original verlustbehaftet (Neukodierung).

## 8. Was noch zu tun ist

Das Projekt konnte in der Portierungsumgebung **nicht kompiliert werden** (kein
.NET-SDK, kein Zugriff auf nuget.org). Erster Schritt lokal:

```bash
dotnet restore
dotnet build -c Release
dotnet run --project src/MyLittleSlideShow
```

Erwartbar sind noch kleinere Compilerfehler (Namensauflösungen, Nullable-Warnungen).
