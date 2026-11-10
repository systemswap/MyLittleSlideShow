#!/usr/bin/env bash
#
# MyLittleSlideShow - Publish-Skript fuer Linux
#
#   ./publish.sh          -> interaktives Menue
#   ./publish.sh 4        -> direkt Punkt 4 bauen
#
# Umgebungsvariablen:
#   SINGLE_FILE=0         Einzeldatei-Bundle abschalten
#   KEEP_EXTERNAL_LIBS=0  Fremdbibliotheken mit ins Bundle packen
#   CONFIG=Debug          andere Konfiguration

set -euo pipefail

# Keine Telemetrie von .NET-CLI und MSBuild (gilt nur fuer diesen Lauf).
# Avalonias Build-Telemetrie ist im csproj abgeschaltet.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILD_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/src/MyLittleSlideShow/MyLittleSlideShow.csproj"
OUTPUT_ROOT="$ROOT/publish"

SINGLE_FILE="${SINGLE_FILE:-1}"
KEEP_EXTERNAL_LIBS="${KEEP_EXTERNAL_LIBS:-1}"
CONFIG="${CONFIG:-Release}"

APP_NAME="MyLittleSlideShow"
APP_VERSION="2.3.0"

C_CYAN=$'\033[36m'; C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'
C_GRAY=$'\033[90m'; C_OFF=$'\033[0m'

# Nicht "head" nennen: das wuerde das gleichnamige Kommando ueberdecken, das
# weiter unten zum Kuerzen der Dateiliste gebraucht wird.
section() { printf '\n%s=== %s ===%s\n' "$C_CYAN" "$1" "$C_OFF"; }
warn() { printf '%s%s%s\n' "$C_YELLOW" "$1" "$C_OFF"; }
ok()   { printf '%s%s%s\n' "$C_GREEN" "$1" "$C_OFF"; }

assert_dotnet() {
    if ! command -v dotnet >/dev/null 2>&1; then
        echo "Das .NET SDK wurde nicht gefunden."
        echo "Installation: https://dotnet.microsoft.com/download  oder  sudo apt install dotnet-sdk-10.0"
        exit 1
    fi
    local v; v="$(dotnet --version)"
    echo "Verwendetes SDK: $v"
    case "$v" in
        1[0-9].*|[2-9][0-9].*) ;;
        *) warn "Das Projekt zielt auf net10.0. Mit SDK $v schlaegt der Build vermutlich fehl." ;;
    esac
}

# publish <rid> <self_contained:0|1> <label>
do_publish() {
    local rid="$1" self="$2" label="$3"
    local mode; [ "$self" = "1" ] && mode="standalone" || mode="normal"
    local out="$OUTPUT_ROOT/$rid-$mode"

    section "$label  ->  $out"
    rm -rf "$out"; mkdir -p "$out"

    local args=(
        publish "$PROJECT"
        -c "$CONFIG"
        -r "$rid"
        -o "$out"
        --nologo
        -p:DebugType=none
        -p:PublishReadyToRun=false
        -p:PublishTrimmed=false
    )
    [ "$self" = "1" ] && args+=(-p:SelfContained=true) || args+=(-p:SelfContained=false)

    if [ "$SINGLE_FILE" = "1" ]; then
        # Native Begleitdateien (libSkiaSharp, libHarfBuzzSharp) wandern mit in
        # die Programmdatei und werden zur Laufzeit entpackt.
        args+=(-p:PublishSingleFile=true)
        args+=(-p:IncludeNativeLibrariesForSelfExtract=true)
        # Komprimierung erlaubt das SDK ausschliesslich bei self-contained
        # (sonst: error NETSDK1176).
        [ "$self" = "1" ] && args+=(-p:EnableCompressionInSingleFile=true)
        [ "$KEEP_EXTERNAL_LIBS" = "1" ] && args+=(-p:KeepExternalLibsSeparate=true)
    fi

    dotnet "${args[@]}"

    find "$out" \( -name '*.pdb' -o -name '*.xml' \) -delete 2>/dev/null || true

    # Lizenztexte gehoeren zur Weitergabe dazu (Apache-2.0 verlangt es fuer
    # MetadataExtractor). Im Programm stehen sie zusaetzlich unter Infos > Lizenzen.
    cp "$ROOT/LICENSE" "$out/LICENSE.txt"
    cp "$ROOT/THIRD-PARTY-NOTICES.md" "$out/"
    cp -r "$ROOT/licenses" "$out/"
    chmod +x "$out/$APP_NAME" 2>/dev/null || true

    local count size
    count="$(find "$out" -maxdepth 1 -type f | wc -l)"
    size="$(du -sh "$out" | cut -f1)"
    printf '\n'; ok "Fertig: $count Datei(en), $size"
    # awk statt "head -n 11": head beendet sich vorzeitig, ls laeuft dann in ein
    # SIGPIPE und wegen "set -o pipefail" wuerde das ganze Skript abbrechen.
    ls -lhS "$out" | awk 'NR > 1 && NR <= 11 {printf "  %8s  %s\n", $5, $9}'

    [ "$self" = "1" ] || warn $'\nHinweis: Diese Variante braucht eine installierte .NET-10-Laufzeit auf dem Zielrechner.'

    LAST_OUT="$out"
}

get_appimagetool() {
    if command -v appimagetool >/dev/null 2>&1; then
        APPIMAGETOOL="$(command -v appimagetool)"
        return 0
    fi

    local cache="$ROOT/.tools"
    local arch; arch="$(uname -m)"
    local tool="$cache/appimagetool-$arch.AppImage"

    if [ -x "$tool" ]; then APPIMAGETOOL="$tool"; return 0; fi

    warn "appimagetool wurde nicht gefunden."
    read -r -p "Soll es nach $tool heruntergeladen werden? [j/N] " a
    case "$a" in j|J|y|Y) ;; *) return 1 ;; esac

    mkdir -p "$cache"
    local url="https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-$arch.AppImage"
    if command -v curl >/dev/null 2>&1; then
        curl -fL "$url" -o "$tool"
    elif command -v wget >/dev/null 2>&1; then
        wget -O "$tool" "$url"
    else
        echo "Weder curl noch wget vorhanden."; return 1
    fi
    chmod +x "$tool"
    APPIMAGETOOL="$tool"
}

# build_appimage <rid>
build_appimage() {
    local rid="$1"
    local arch
    case "$rid" in
        linux-x64)   arch="x86_64" ;;
        linux-arm64) arch="aarch64" ;;
        *) echo "AppImage nur fuer linux-x64 und linux-arm64."; return 1 ;;
    esac

    # AppImage ergibt nur als standalone Sinn, sonst fehlt .NET auf dem Zielsystem.
    do_publish "$rid" 1 "AppImage-Basis ($rid, standalone)"
    local payload="$LAST_OUT"

    get_appimagetool || { warn "AppImage uebersprungen."; return 1; }

    section "AppImage wird gepackt"
    local appdir="$OUTPUT_ROOT/$rid-appimage/AppDir"
    rm -rf "$OUTPUT_ROOT/$rid-appimage"
    mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" \
             "$appdir/usr/share/icons/hicolor/256x256/apps"

    cp -a "$payload/." "$appdir/usr/bin/"
    chmod +x "$appdir/usr/bin/$APP_NAME"

    # Icon: AppImage erwartet ein PNG im Wurzelverzeichnis des AppDir
    local icon_src="$ROOT/src/MyLittleSlideShow/Assets/Images/MyIcon.png"
    if [ -f "$icon_src" ]; then
        cp "$icon_src" "$appdir/usr/share/icons/hicolor/256x256/apps/$APP_NAME.png"
        cp "$icon_src" "$appdir/$APP_NAME.png"
    else
        warn "Kein Icon gefunden ($icon_src) - AppImage bekommt keins."
    fi

    cat > "$appdir/usr/share/applications/$APP_NAME.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=MyLittleSlideShow
Comment=Kleine Diashow fuer den Desktop
Exec=$APP_NAME
Icon=$APP_NAME
Categories=Graphics;Viewer;
Terminal=false
StartupNotify=false
EOF
    cp "$appdir/usr/share/applications/$APP_NAME.desktop" "$appdir/$APP_NAME.desktop"

    cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
export PATH="$HERE/usr/bin:$PATH"
exec "$HERE/usr/bin/MyLittleSlideShow" "$@"
EOF
    chmod +x "$appdir/AppRun"

    local target="$OUTPUT_ROOT/$rid-appimage/$APP_NAME-$APP_VERSION-$arch.AppImage"
    ARCH="$arch" "$APPIMAGETOOL" "$appdir" "$target"

    ok "AppImage erstellt: $target"
    warn "Auf sehr alten Systemen braucht die AppImage libfuse2 (sudo apt install libfuse2)."
}

show_menu() {
    cat <<EOF

  ${APP_NAME} - Publish
  ---------------------------
  1)  Windows x64   - normal      (setzt .NET 10 auf dem Zielrechner voraus)
  2)  Windows x64   - standalone  (.NET ist integriert)
  3)  Linux x64     - normal
  4)  Linux x64     - standalone
  5)  Linux arm64   - normal
  6)  Linux arm64   - standalone
  7)  Linux x64     - AppImage
  8)  Linux arm64   - AppImage
  9)  Alles bauen (ohne AppImage)
  0)  Abbrechen

EOF
    printf '%s  Einzeldatei: %s   Fremdbibliotheken separat: %s%s\n\n' \
        "$C_GRAY" "$SINGLE_FILE" "$KEEP_EXTERNAL_LIBS" "$C_OFF"
}

assert_dotnet

choice="${1:-}"
if [ -z "$choice" ]; then
    show_menu
    read -r -p "Auswahl: " choice
fi

case "$choice" in
    0) echo "Abgebrochen."; exit 0 ;;
    1) do_publish win-x64     0 "Windows x64 - normal" ;;
    2) do_publish win-x64     1 "Windows x64 - standalone" ;;
    3) do_publish linux-x64   0 "Linux x64 - normal" ;;
    4) do_publish linux-x64   1 "Linux x64 - standalone" ;;
    5) do_publish linux-arm64 0 "Linux arm64 - normal" ;;
    6) do_publish linux-arm64 1 "Linux arm64 - standalone" ;;
    7) build_appimage linux-x64 ;;
    8) build_appimage linux-arm64 ;;
    9)
        do_publish win-x64     0 "Windows x64 - normal"
        do_publish win-x64     1 "Windows x64 - standalone"
        do_publish linux-x64   0 "Linux x64 - normal"
        do_publish linux-x64   1 "Linux x64 - standalone"
        do_publish linux-arm64 0 "Linux arm64 - normal"
        do_publish linux-arm64 1 "Linux arm64 - standalone"
        ;;
    *) echo "Ungueltige Auswahl."; exit 1 ;;
esac

printf '\n'; ok "Alle Ergebnisse liegen unter: $OUTPUT_ROOT"
