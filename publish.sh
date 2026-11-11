#!/usr/bin/env bash
set -Eeuo pipefail

# Erstellt die fertigen Programme in published/:
#
#   published/linux-x64/MyLittleSlideShow-x86_64.AppImage   eine Datei: Programm, .NET, Avalonia, SkiaSharp
#   published/windows-x64/MyLittleSlideShow.exe             eine Datei: Programm, .NET, Avalonia, SkiaSharp
#   published/MyLittleSlideShow-<Version>-linux-x64.tar.gz, MyLittleSlideShow-<Version>-windows-x64.zip
#                                                           (aeltere Archive derselben Plattform werden ersetzt)
#
# Die Programmdatei traegt keine Versionsnummer im Namen, damit der Menueeintrag und
# ein Autostart-Eintrag nach dem naechsten Bau weiter stimmen; die Version steht im
# Programm und im Namen der Archive. Neben jedes Programm kommen LICENSE.txt,
# THIRD-PARTY-NOTICES.md und licenses/, unter Linux ausserdem einrichten.sh /
# entfernen.sh fuer den Menueeintrag (bewusst ohne "Oeffnen mit ..."). Nach einem
# Linux-Bau traegt das Skript das Programm im Menue ein. Die Einstellungen liegen
# unter ~/.config und werden von einem Bau nicht beruehrt.
#
# Paketform:
#   eine Datei (Standard)  Linux AppImage, Windows EXE; .NET und alle Bibliotheken darin
#   einzeln                alle Bibliotheken lose im Ordner, ohne AppImage -
#                          falls eine Datei zu gross wird oder zur Fehlersuche
#
# Aufruf ohne Argumente fragt erst nach der Paketform, dann nach dem Ziel.
# Ohne Rueckfrage (Reihenfolge egal, Paketform sonst "eine Datei"):
#   ./publish.sh beides
#   ./publish.sh linux
#   ./publish.sh windows
#   ./publish.sh linux einzeln
#
# Zwischenstaende und appimagetool liegen unter ~/.cache/mylittleslideshow-build/.
# Benoetigt: .NET SDK 10, wget (laedt appimagetool beim ersten Mal), zip

# Keine Nutzungsdaten an Hersteller senden (.NET, MSBuild, Avalonia), auch wenn es im
# System nicht abgeschaltet ist. Avalonias Build-Telemetrie ist zusaetzlich im csproj aus.
export DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILD_TELEMETRY_OPTOUT=1 AVALONIA_TELEMETRY_OPTOUT=1 DO_NOT_TRACK=1
export DOTNET_NOLOGO=1

PROJECT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
PROJECT="$PROJECT_DIR/src/MyLittleSlideShow/MyLittleSlideShow.csproj"
OUT_DIR="$PROJECT_DIR/published"
LINUX_DIR="$OUT_DIR/linux-x64"
WINDOWS_DIR="$OUT_DIR/windows-x64"
CACHE_DIR="${XDG_CACHE_HOME:-$HOME/.cache}/mylittleslideshow-build"
WORK_DIR="$CACHE_DIR/publish"
ARTIFACTS="$CACHE_DIR/artifacts"
APPIMAGETOOL="$CACHE_DIR/appimagetool-x86_64.AppImage"
ICON="$PROJECT_DIR/packaging/linux/mylittleslideshow.png"

TARGET=""
FORM=""
for argument in "$@"; do
  case "$argument" in
    linux|windows) TARGET="$argument" ;;
    beides|all) TARGET=all ;;
    eine-datei|datei|standard) FORM=eine-datei ;;
    einzeln|ordner) FORM=einzeln ;;
    *) echo "Aufruf: $0 [linux|windows|beides] [eine-datei|einzeln]" >&2; exit 1 ;;
  esac
done

[ -n "$FORM" ] || [ -z "$TARGET" ] || FORM=eine-datei

if [ -z "$FORM" ]; then
  echo
  echo "Wie soll das Paket aussehen?"
  echo "  1) Eine Datei (Standard) – Linux: AppImage, Windows: EXE; .NET und Avalonia darin"
  echo "  2) Einzeln               – alle Bibliotheken lose im Ordner"
  echo
  read -r -p "Auswahl [1]: " selection
  case "${selection:-1}" in
    1) FORM=eine-datei ;;
    2) FORM=einzeln ;;
    *) echo "Fehler: Ungültige Auswahl." >&2; exit 1 ;;
  esac
fi

if [ -z "$TARGET" ]; then
  echo
  echo "Wofür soll gebaut werden?"
  echo "  1) Linux"
  echo "  2) Windows"
  echo "  3) beides"
  echo
  read -r -p "Auswahl [3]: " selection
  case "${selection:-3}" in
    1) TARGET=linux ;;
    2) TARGET=windows ;;
    3) TARGET=all ;;
    *) echo "Fehler: Ungültige Auswahl." >&2; exit 1 ;;
  esac
fi

if [ "$FORM" = eine-datei ]; then SINGLE_FILE=true; else SINGLE_FILE=false; fi
echo "Paketform: $FORM, Ziel: $TARGET"

command -v dotnet > /dev/null || { echo "Fehler: .NET SDK fehlt: https://dotnet.microsoft.com/download" >&2; exit 1; }

# Nicht, solange das Programm aus published/ läuft.
if pgrep -f "$LINUX_DIR/MyLittleSlideShow(-x86_64\.AppImage)?( |$)" > /dev/null; then
  echo "Fehler: MyLittleSlideShow läuft gerade (aus published/) – bitte erst beenden, dann neu bauen." >&2
  exit 1
fi

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -1)"
echo "MyLittleSlideShow $VERSION"

rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR" "$OUT_DIR"

# Leert einen Ausgabeordner (die Einstellungen liegen nicht dort, sondern in ~/.config).
clear_program_folder() {
  mkdir -p "$1"
  find "$1" -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +
}

# Lizenz, Hinweise auf fremde Bestandteile und deren Lizenztexte neben das Programm.
# Im Programm stehen sie zusaetzlich unter Einstellungen > Infos > Lizenzen.
notices() {
  local dest="$1"
  install -m 0644 "$PROJECT_DIR/LICENSE" "$dest/LICENSE.txt"
  install -m 0644 "$PROJECT_DIR/THIRD-PARTY-NOTICES.md" "$dest/THIRD-PARTY-NOTICES.md"
  mkdir -p "$dest/licenses"
  install -m 0644 "$PROJECT_DIR/packaging/licenses/"* "$dest/licenses/"
}

# Eine eigenständige Programmdatei für eine Plattform (bei „einzeln“ ein Ordner
# mit allen Bibliotheken). Native Bibliotheken (Skia, HarfBuzz, ANGLE) werden
# beim Start entpackt.
publish() {
  local rid="$1" dest="$2"

  dotnet publish "$PROJECT" -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=$SINGLE_FILE \
    -p:IncludeNativeLibrariesForSelfExtract=$SINGLE_FILE \
    -p:EnableCompressionInSingleFile=$SINGLE_FILE \
    -p:DebugType=None -p:DebugSymbols=false \
    -p:UseArtifactsOutput=true -p:ArtifactsPath="$ARTIFACTS" \
    -o "$dest" > "$WORK_DIR/publish-$rid.log" 2>&1 \
    || { tail -20 "$WORK_DIR/publish-$rid.log" >&2; echo "Fehler: dotnet publish für $rid fehlgeschlagen." >&2; exit 1; }
  # Debug-Symbole der nativen Bibliotheken (SkiaSharp 3 legt sie daneben) gehoeren nicht dazu.
  find "$dest" -name '*.pdb' -delete
}

# Archiv mit der Version im Namen; aeltere Archive derselben Plattform werden vorher entfernt.
package() {
  local folder="$1" kind="$2"
  local archive="MyLittleSlideShow-$VERSION-$folder.$kind"
  rm -f "$OUT_DIR"/MyLittleSlideShow-*"$folder.$kind"
  case "$kind" in
    tar.gz) tar -C "$OUT_DIR" -czf "$OUT_DIR/$archive" "$folder" ;;
    zip) (cd "$OUT_DIR" && zip -qr "$archive" "$folder") ;;
  esac
}

# ---------------------------------------------------------------- Linux
if [ "$TARGET" = all ] || [ "$TARGET" = linux ]; then
  echo "== Linux ($FORM)"
  publish linux-x64 "$WORK_DIR/linux"
  clear_program_folder "$LINUX_DIR"

  if [ "$SINGLE_FILE" = true ]; then
    APPDIR="$WORK_DIR/AppDir"
    mkdir -p "$APPDIR/usr/bin"
    install -m 0755 "$WORK_DIR/linux/MyLittleSlideShow" "$APPDIR/usr/bin/MyLittleSlideShow"
    install -m 0644 "$PROJECT_DIR/packaging/linux/MyLittleSlideShow.desktop" "$APPDIR/MyLittleSlideShow.desktop"
    install -m 0644 "$ICON" "$APPDIR/mylittleslideshow.png"

    cat > "$APPDIR/AppRun" << 'APPRUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/MyLittleSlideShow" "$@"
APPRUN
    chmod 0755 "$APPDIR/AppRun"

    if [ ! -x "$APPIMAGETOOL" ]; then
      mkdir -p "$CACHE_DIR"
      wget -nv -O "$APPIMAGETOOL" "https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage"
      chmod +x "$APPIMAGETOOL"
    fi

    ARCH=x86_64 "$APPIMAGETOOL" --no-appstream "$APPDIR" "$LINUX_DIR/MyLittleSlideShow-x86_64.AppImage" > "$WORK_DIR/appimagetool.log" 2>&1 \
      || { tail -20 "$WORK_DIR/appimagetool.log" >&2; echo "Fehler: appimagetool fehlgeschlagen." >&2; exit 1; }
  else
    cp -a "$WORK_DIR/linux/." "$LINUX_DIR/"
    chmod 0755 "$LINUX_DIR/MyLittleSlideShow"
  fi

  notices "$LINUX_DIR"

  # Menüeintrag (GNOME zeigt das Symbol in der Leiste nur mit einem Eintrag)
  install -Dm644 "$ICON" "$LINUX_DIR/share/icons/hicolor/256x256/apps/mylittleslideshow.png"
  install -Dm644 "$PROJECT_DIR/packaging/linux/MyLittleSlideShow.desktop" "$LINUX_DIR/share/applications/MyLittleSlideShow.desktop"
  install -m 0755 "$PROJECT_DIR/packaging/linux/einrichten.sh" "$PROJECT_DIR/packaging/linux/entfernen.sh" "$LINUX_DIR/"

  package linux-x64 tar.gz
fi

# ---------------------------------------------------------------- Windows
if [ "$TARGET" = all ] || [ "$TARGET" = windows ]; then
  echo "== Windows ($FORM)"
  publish win-x64 "$WORK_DIR/windows"
  clear_program_folder "$WINDOWS_DIR"
  if [ "$SINGLE_FILE" = true ]; then
    install -m 0644 "$WORK_DIR/windows/MyLittleSlideShow.exe" "$WINDOWS_DIR/MyLittleSlideShow.exe"
  else
    cp -a "$WORK_DIR/windows/." "$WINDOWS_DIR/"
  fi
  notices "$WINDOWS_DIR"
  package windows-x64 zip
fi

echo
echo "Fertig (MyLittleSlideShow $VERSION):"
find "$OUT_DIR" -maxdepth 2 -type f \( -name "*.AppImage" -o -name "*.exe" -o -name MyLittleSlideShow -o -name "*.tar.gz" -o -name "*.zip" \) -printf "  %P  (%s Bytes)\n" | sort

# Menüeintrag auf das neue Programm setzen (fester Pfad). Nicht auf dem Build-Server.
if { [ "$TARGET" = all ] || [ "$TARGET" = linux ]; } && [ -z "${CI:-}" ]; then
  echo
  "$LINUX_DIR/einrichten.sh"
fi
