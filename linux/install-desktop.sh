#!/bin/sh
# Adds a "Flato3D (Linux)" launcher to the app menu and the desktop.
set -eu
here=$(cd "$(dirname "$0")" && pwd)
script="$here/flato3d-linux.py"
icon="$here/Flato3D_icon.png"
[ -f "$icon" ] || icon="$here/../Flato3D Icon.png"
[ -f "$icon" ] || icon=applications-games
chmod +x "$script"
entry="[Desktop Entry]
Type=Application
Name=Flato3D (Linux)
Comment=Stereo depth for flat 2D Steam games
Exec=python3 \"$script\"
Icon=$icon
Terminal=true
Categories=Game;Utility;"
apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
mkdir -p "$apps"
printf '%s\n' "$entry" > "$apps/flato3d-linux.desktop"
desktop=$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")
if [ -d "$desktop" ]; then
    printf '%s\n' "$entry" > "$desktop/flato3d-linux.desktop"
    chmod +x "$desktop/flato3d-linux.desktop"
fi
echo "Added Flato3D (Linux) to the app menu${desktop:+ and $desktop}."
