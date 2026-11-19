#!/usr/bin/env python3
"""Prueft die englische Uebersetzung (Localization/English.cs).

Sammelt alle uebersetzbaren deutschen Texte:
  - C#:   Tr.T("...")  (auch "a" + "b" ueber mehrere Zeilen)
  - XAML: {l:T '...'}  und  <l:T Text="..." />
  - die Zeilen von Infos/HiddenOptions.txt (jede Zeile ist ein Schluessel)
und vergleicht sie mit den Schluesseln in English.cs.

Meldet Texte ohne englische Uebersetzung (Fehler, Rueckgabewert 1) und
Eintraege in English.cs, die im Code nicht mehr vorkommen (nur Hinweis).

    python3 tools/check-translations.py
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "src" / "MyLittleSlideShow"
ENGLISH = SRC / "Localization" / "English.cs"

LITERAL = r'"((?:[^"\\]|\\.)*)"'
CS_CALL = re.compile(r'Tr\.T\(\s*(' + LITERAL + r'(?:\s*\+\s*' + LITERAL + r')*)', re.S)
CS_PART = re.compile(LITERAL)
XAML_SHORT = re.compile(r"\{l:T '([^']*)'\}")
XAML_ELEMENT = re.compile(r'<l:T\s+Text="([^"]*)"')
ENGLISH_KEY = re.compile(r'^\s*\[' + LITERAL + r'\]\s*=', re.M)


ESCAPES = {'"': '"', "\\": "\\", "t": "\t", "n": "\n", "r": "\r", "0": "\0"}


def unescape(text: str) -> str:
    """C#-Escapes eines normalen Stringliterals (\\" \\\\ \\t \\n ...)."""
    return re.sub(r"\\(.)", lambda m: ESCAPES.get(m.group(1), m.group(0)), text)


def xml_unescape(text: str) -> str:
    return (text.replace("&quot;", '"').replace("&lt;", "<").replace("&gt;", ">")
                .replace("&apos;", "'").replace("&amp;", "&"))


def used_texts() -> dict[str, str]:
    found: dict[str, str] = {}
    for path in SRC.rglob("*.cs"):
        if "obj" in path.parts or "bin" in path.parts or path == ENGLISH:
            continue
        for match in CS_CALL.finditer(path.read_text(encoding="utf-8-sig")):
            key = "".join(unescape(p) for p in CS_PART.findall(match.group(1)))
            found.setdefault(key, str(path.relative_to(ROOT)))
    for path in SRC.rglob("*.axaml"):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        text = path.read_text(encoding="utf-8-sig")
        for key in XAML_SHORT.findall(text) + XAML_ELEMENT.findall(text):
            found.setdefault(xml_unescape(key), str(path.relative_to(ROOT)))
    hidden = SRC / "Infos" / "HiddenOptions.txt"
    for line in hidden.read_text(encoding="utf-8-sig").splitlines():
        if line.strip():
            found.setdefault(line, str(hidden.relative_to(ROOT)))
    return found


def main() -> int:
    used = used_texts()
    english = {unescape(k) for k in ENGLISH_KEY.findall(ENGLISH.read_text(encoding="utf-8-sig"))}

    missing = sorted(k for k in used if k not in english)
    leftovers = sorted(k for k in english if k not in used)

    for key in missing:
        print(f"FEHLT  {used[key]}: {key!r}")
    for key in leftovers:
        print(f"UNBENUTZT  {key!r}")
    print(f"{len(used)} Texte, {len(missing)} ohne Englisch, {len(leftovers)} unbenutzte Einträge")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
