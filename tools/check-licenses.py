#!/usr/bin/env python3
"""
Prueft die Lizenzen aller NuGet-Pakete, die im Programm landen.

Hintergrund: SixLabors.ImageSharp hat mit Version 3.2 seine Bedingungen
geaendert (Lizenzschluessel fuer Release-Builds). So etwas soll nicht erst
beim Veroeffentlichen auffallen, sondern beim naechsten Build.

Aufruf nach "dotnet restore":
    python3 tools/check-licenses.py [pfad/zu/project.assets.json]

Ergebnis: Exit-Code 0, wenn alle Pakete eine erlaubte Lizenz haben,
sonst 1 mit einer Liste der Abweichungen.
"""

import json
import os
import re
import sys

# Freie Lizenzen ohne Nutzungsgebuehr und ohne Copyleft fuer das Programm.
ALLOWED = {
    "MIT",
    "Apache-2.0",
    "BSD-2-Clause",
    "BSD-3-Clause",
    "BSL-1.0",
    "OFL-1.1",
    "Zlib",
    "ISC",
}

# Pakete, die keine SPDX-Kennung angeben, sondern nur eine URL. Von Hand
# geprueft - bewusst mit genauer Version: bei jedem Update muss die Lizenz
# erneut angesehen und der Eintrag angepasst werden.
REVIEWED = {
    # ANGLE (Chromium-Projekt), BSD-3-Clause; nur Windows, kommt mit Avalonia
    # Text: licenses/angle-BSD-3-Clause.txt (am 10.11.2026 im Paket geprueft,
    # Lizenztext unveraendert gegenueber 2.1.25547)
    "Avalonia.Angle.Windows.Natives/2.1.27548.20260419": "BSD-3-Clause",
    # Adobe XMP Toolkit, BSD-3-Clause; Text in licenses/xmpcore-BSD-3-Clause.txt
    "XmpCore/6.1.10.1": "BSD-3-Clause",
}


def license_of(nuspec_path):
    """SPDX-Ausdruck aus der .nuspec, oder None wenn nur eine URL angegeben ist."""
    with open(nuspec_path, encoding="utf-8") as f:
        text = f.read()
    m = re.search(r'<license\s+type="expression"\s*>([^<]+)</license>', text)
    return m.group(1).strip() if m else None


def allowed(expression):
    """Erlaubt, wenn jede Alternative bzw. jeder Teil eines Ausdrucks erlaubt ist.

    Bei "A OR B" reicht eine erlaubte Alternative, bei "A AND B" muessen
    beide erlaubt sein.
    """
    expression = expression.replace("(", " ").replace(")", " ")
    for alternative in re.split(r"\s+OR\s+", expression):
        parts = [p.strip() for p in re.split(r"\s+AND\s+", alternative) if p.strip()]
        if parts and all(p in ALLOWED for p in parts):
            return True
    return False


def main():
    assets = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        "src", "MyLittleSlideShow", "obj", "project.assets.json")
    with open(assets, encoding="utf-8") as f:
        data = json.load(f)

    folders = list(data.get("packageFolders", {}).keys())
    problems = []
    checked = 0

    for key, library in sorted(data["libraries"].items()):
        if library.get("type") != "package":
            continue
        name, version = key.split("/", 1)
        nuspec = None
        for folder in folders:
            candidate = os.path.join(folder, library["path"], name.lower() + ".nuspec")
            if os.path.exists(candidate):
                nuspec = candidate
                break
        if nuspec is None:
            problems.append(f"{key}: .nuspec nicht gefunden")
            continue

        checked += 1
        expression = license_of(nuspec) or REVIEWED.get(key)
        if expression is None:
            problems.append(f"{key}: keine Lizenzkennung - von Hand pruefen und in REVIEWED eintragen")
        elif not allowed(expression):
            problems.append(f"{key}: Lizenz '{expression}' ist nicht freigegeben")
        else:
            print(f"  ok  {key:60} {expression}")

    print(f"{checked} Pakete geprueft.")
    if problems:
        print("\nLIZENZPRUEFUNG FEHLGESCHLAGEN:")
        for p in problems:
            print("  - " + p)
        return 1
    print("Alle Lizenzen freigegeben.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
