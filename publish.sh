#!/usr/bin/env bash
#
# Baut MyLittleSlideShow als Linux-AppImage - Pendant zu publish.ps1 (Windows).
#
#   ./publish.sh
#
# Ergebnis: publish/linux-x64/MyLittleSlideShow-<Version>-x86_64.AppImage
# Die Versionsnummer steht nur im csproj (<Version>).
#
# Gebraucht werden das .NET 10 SDK und appimagetool (wird bei Bedarf nach
# .tools/ geladen) sowie FUSE zum Ausfuehren der AppImage.

set -euo pipefail

# Keine Telemetrie von .NET-CLI und MSBuild (gilt nur fuer diesen Lauf).
# Avalonias Build-Telemetrie ist im csproj abgeschaltet.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILD_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/src/MyLittleSlideShow/MyLittleSlideShow.csproj"
OUT="$ROOT/publish/linux-x64"
APP="MyLittleSlideShow"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1)"

command -v dotnet >/dev/null 2>&1 || {
    echo "Das .NET SDK wurde nicht gefunden: https://dotnet.microsoft.com/download"
    exit 1
}

echo "=== $APP $VERSION fuer Linux -> $OUT"
rm -rf "$OUT"
payload="$OUT/payload"
appdir="$OUT/AppDir"
mkdir -p "$payload"

# Eine Datei mit integriertem .NET; native Bibliotheken (Skia, HarfBuzz)
# werden beim Start entpackt.
dotnet publish "$PROJECT" \
    -c Release -r linux-x64 -o "$payload" --nologo \
    -p:SelfContained=true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none

# Debug-Symbole der nativen Bibliotheken (SkiaSharp 3 legt sie daneben) gehoeren nicht dazu.
find "$payload" -name '*.pdb' -delete

# Lizenztexte gehoeren zur Weitergabe dazu (u. a. Apache-2.0 fuer MetadataExtractor).
# Im Programm stehen sie zusaetzlich unter Einstellungen > Infos > Lizenzen.
cp "$ROOT/LICENSE" "$payload/LICENSE.txt"
cp "$ROOT/THIRD-PARTY-NOTICES.md" "$payload/"
cp -r "$ROOT/licenses" "$payload/"

# ---------------------------------------------------------------- AppDir
mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" "$appdir/usr/share/icons/hicolor/256x256/apps"
cp -a "$payload/." "$appdir/usr/bin/"
chmod +x "$appdir/usr/bin/$APP"

icon="$ROOT/src/MyLittleSlideShow/Assets/Images/MyIcon.png"
cp "$icon" "$appdir/usr/share/icons/hicolor/256x256/apps/$APP.png"
cp "$icon" "$appdir/$APP.png"

cat > "$appdir/$APP.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=MyLittleSlideShow
Comment=Kleine Diashow fuer den Desktop
Exec=$APP
Icon=$APP
Categories=Graphics;Viewer;
Terminal=false
StartupNotify=false
EOF
cp "$appdir/$APP.desktop" "$appdir/usr/share/applications/"

cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
export PATH="$HERE/usr/bin:$PATH"
exec "$HERE/usr/bin/MyLittleSlideShow" "$@"
EOF
chmod +x "$appdir/AppRun"

# ----------------------------------------------------------- appimagetool
tool="$(command -v appimagetool || true)"
if [ -z "$tool" ]; then
    tool="$ROOT/.tools/appimagetool-x86_64.AppImage"
    if [ ! -x "$tool" ]; then
        echo "appimagetool fehlt - wird nach $tool geladen."
        mkdir -p "$ROOT/.tools"
        curl -fLsS -o "$tool" \
            https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
        chmod +x "$tool"
    fi
fi

target="$OUT/$APP-$VERSION-x86_64.AppImage"
ARCH=x86_64 "$tool" "$appdir" "$target"
rm -rf "$payload" "$appdir"

ls -lh "$target"
echo "Fertig: $target"
