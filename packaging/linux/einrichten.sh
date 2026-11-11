#!/usr/bin/env bash
# Trägt MyLittleSlideShow für den angemeldeten Benutzer ins Anwendungsmenü ein –
# ohne Root-Rechte. Kopiert werden nur Menüeintrag und Symbol; der Eintrag zeigt
# auf die AppImage in diesem Ordner (bei der Paketform „einzeln“ auf das
# Programm im Ordner). Bewusst ohne „Öffnen mit …“ im Dateimanager: Es ist eine
# Diashow, kein Bildbetrachter für einzelne Dateien.
#
# GNOME zeigt das Programmsymbol in der Leiste nur mit so einem Eintrag
# (StartupWMClass = Fensterklasse). Dateiname = Fensterklasse; den früheren
# mylittleslideshow.desktop entfernt es, denn GNOME hält an einem geänderten Exec
# unter altem Namen fest und startet sonst das alte Programm.
#
# Wird der Ordner verschoben: hier erneut ausführen. Rückgängig: ./entfernen.sh
set -euo pipefail

HERE="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
PROG="$HERE/MyLittleSlideShow-x86_64.AppImage"
[ -f "$PROG" ] || PROG="$HERE/MyLittleSlideShow"
DATA="${XDG_DATA_HOME:-$HOME/.local/share}"

[ -f "$PROG" ] || { echo "Fehler: $PROG fehlt." >&2; exit 1; }
chmod +x "$PROG"

install -Dm644 "$HERE/share/icons/hicolor/256x256/apps/mylittleslideshow.png" "$DATA/icons/hicolor/256x256/apps/mylittleslideshow.png"

# Exec in Anführungszeichen (Pfade mit Leerzeichen), Sonderzeichen so maskiert,
# wie die Desktop-Entry-Spezifikation es verlangt.
escaped="$(printf '%s' "$PROG" | sed -e 's/\\/\\\\\\\\/g' -e 's/["`$]/\\\\&/g' -e 's/%/%%/g')"

mkdir -p "$DATA/applications"
rm -f "$DATA/applications/mylittleslideshow.desktop"

# Nur schreiben, wenn sich der Inhalt ändert: GNOME merkt sich Sortierung und
# Ordner am Dateinamen; ein unveränderter Eintrag löst nichts aus.
ENTRY="$DATA/applications/MyLittleSlideShow.desktop"
NEW="$(mktemp)"
trap 'rm -f "$NEW"' EXIT
EXEC_LINE="Exec=\"$escaped\"" awk '/^Exec=/ { print ENVIRON["EXEC_LINE"]; next } { print }' \
  "$HERE/share/applications/MyLittleSlideShow.desktop" > "$NEW"

if cmp -s "$NEW" "$ENTRY"; then
  echo "Menüeintrag unverändert."
  exit 0
fi
install -m 644 "$NEW" "$ENTRY"

command -v update-desktop-database > /dev/null 2>&1 && update-desktop-database "$DATA/applications" || true
command -v gtk-update-icon-cache > /dev/null 2>&1 && gtk-update-icon-cache -f -t "$DATA/icons/hicolor" > /dev/null 2>&1 || true

echo "Eingetragen: MyLittleSlideShow steht jetzt im Anwendungsmenü."
echo "Rückgängig: ./entfernen.sh"
