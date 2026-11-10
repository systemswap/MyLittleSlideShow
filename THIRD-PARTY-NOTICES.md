# Fremde Bestandteile und ihre Lizenzen

MyLittleSlideShow selbst steht unter der MIT-Lizenz (siehe `LICENSE`). Mit dem
Programm werden Bestandteile anderer Projekte weitergegeben; ihre Rechte
liegen bei den jeweiligen Autoren. Hier steht, was dabei ist, unter welcher
Lizenz es steht und wo der Lizenztext liegt.

Alle Bestandteile stehen unter freien Lizenzen ohne Nutzungsgebühr. Der
Workflow prüft das bei jedem Build (`tools/check-licenses.py`): kommt ein Paket
mit einer anderen Lizenz hinzu oder ändert ein Paket seine Lizenz, schlägt der
Build fehl.

## Im Programm (MyLittleSlideShow.exe bzw. AppImage)

| Bestandteil | Version | Lizenz | Projekt |
|---|---|---|---|
| .NET-Laufzeit (nur in der Standalone-Fassung) | 10 | MIT | https://github.com/dotnet/runtime |
| Avalonia (UI, Fluent-Design, Skia-, X11-, Win32- und FreeDesktop-Anbindung) | 11.3.23 | MIT | https://github.com/AvaloniaUI/Avalonia |
| Avalonia.Fonts.Inter | 11.3.23 | MIT | https://github.com/AvaloniaUI/Avalonia |
| Schrift Inter (in Avalonia.Fonts.Inter) | | SIL Open Font License 1.1 – `licenses/inter-OFL.txt` | https://github.com/rsms/inter |
| ANGLE (nur Windows, über Avalonia.Angle.Windows.Natives) | 2.1.25547 | BSD-3-Clause – `licenses/angle-BSD-3-Clause.txt` | https://chromium.googlesource.com/angle/angle |
| SkiaSharp / Skia (Bilder dekodieren, skalieren, drehen, speichern) mit libjpeg-turbo, libpng, libwebp, zlib, FreeType, expat u. a. | 2.88.9 | MIT / BSD-3-Clause und weitere – `licenses/skiasharp-THIRD-PARTY-NOTICES.txt` | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp / HarfBuzz | 8.3.1.1 | MIT / „Old MIT“ – `licenses/skiasharp-THIRD-PARTY-NOTICES.txt` | https://github.com/mono/SkiaSharp, https://github.com/harfbuzz/harfbuzz |
| MetadataExtractor (Dateiinfos: EXIF, IPTC, XMP, ICC, Herstellerdaten) | 2.9.3 | Apache-2.0 – `licenses/Apache-2.0.txt` | https://github.com/drewnoakes/metadata-extractor-dotnet |
| XmpCore (XMP lesen, von MetadataExtractor benutzt) | 6.1.10.1 | BSD-3-Clause (Adobe XMP) – `licenses/xmpcore-BSD-3-Clause.txt` | https://github.com/drewnoakes/xmp-core-dotnet |
| Tmds.DBus.Protocol (nur Linux) | 0.21.3 | MIT | https://github.com/tmds/Tmds.DBus |
| MicroCom.Runtime | 0.11.0 | MIT | https://github.com/kekekeks/MicroCom |

Für die MIT-lizenzierten Bestandteile gilt der folgende Text, jeweils mit dem
Copyright-Vermerk des Projekts:

> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to deal
> in the Software without restriction, including without limitation the rights
> to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
> copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all
> copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

## Nur beim Bauen, nicht im Programm

| Bestandteil | Hinweis |
|---|---|
| Avalonia.BuildServices | Kommt mit Avalonia und würde bei jedem Build Nutzungsdaten an Avalonia senden. Ist im Projekt per `ExcludeAssets` abgeschaltet und wird nicht ausgeführt. |
| Avalonia.Diagnostics | Entwicklerwerkzeug, nur in Debug-Builds. |

## Netzzugriffe

MyLittleSlideShow sendet keine Nutzungsdaten und baut selbst keine
Netzverbindung auf. Nur der Knopf „Spenden“ öffnet die PayPal-Seite im
Browser. Auch beim Bauen ist Telemetrie abgeschaltet: .NET-CLI und MSBuild
über `DOTNET_CLI_TELEMETRY_OPTOUT` / `MSBUILD_TELEMETRY_OPTOUT` in den
Publish-Skripten und im Workflow, Avalonia wie oben beschrieben.
