#!/usr/bin/env bash
# Entfernt den Menüeintrag und das Symbol von ./einrichten.sh. Die AppImage
# und die Einstellungen (~/.config/MyLittleSlideShow)
# bleiben unberührt; einen Autostart-Eintrag schaltet man im Programm selbst aus.
set -euo pipefail

DATA="${XDG_DATA_HOME:-$HOME/.local/share}"

rm -f "$DATA/applications/MyLittleSlideShow.desktop" "$DATA/applications/mylittleslideshow.desktop" \
  "$DATA/icons/hicolor/256x256/apps/mylittleslideshow.png"

command -v update-desktop-database > /dev/null 2>&1 && update-desktop-database "$DATA/applications" || true
command -v gtk-update-icon-cache > /dev/null 2>&1 && gtk-update-icon-cache -f -t "$DATA/icons/hicolor" > /dev/null 2>&1 || true

echo "Menüeintrag entfernt."
