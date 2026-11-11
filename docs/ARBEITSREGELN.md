# Arbeitsregeln

Gilt für jede Arbeit an diesem Projekt, auf jedem Rechner und mit jedem Werkzeug.
Wer hier etwas ändert, liest diese Seite vorher einmal ganz.

## Vor dem Arbeiten

- **Erst holen, dann ändern:** `git pull` vor jeder Änderung. An mehreren
  Rechnern parallel weiterzuarbeiten hat schon einmal zu zwei Fassungen ohne
  gemeinsame Historie geführt (`docs/FEHLERGEDAECHTNIS.md`, „Zwei getrennt
  geführte Fassungen“).
- **Neutrale Git-Identität**, nur für dieses Repository (ohne `--global`):

  ```
  git config user.name  "MyLittleSlideShow"
  git config user.email "mylittleslideshow@localhost"
  ```

  Vor dem Push prüfen, dass kein persönlicher Name in den neuen Commits steht:
  `git log --format='%an <%ae> | %cn <%ce>' origin/main..`
- **Nie erzwingen:** kein `git push --force`, keine Zweige oder Tags auf GitHub
  löschen. Lehnt GitHub einen Push ab, erst `git pull`.
- Vor Änderungen an einer Stelle im Fehlergedächtnis nachlesen.

## Keine persönlichen Daten

Nichts davon in Dateien, Commits (Nachricht, Autor), Kommentare, Doku,
Testdaten, Screenshots oder gebaute Programme:

- Namen, E-Mail-Adressen, Benutzer- und Rechnernamen, Konten, Firmenangaben;
- Pfade, die einen Benutzernamen enthalten – stattdessen `~/…` oder neutral
  `/home/user/…`; im Code nur Pfade innerhalb des Projekts und vom
  System vorgegebene Orte (`~/.config/MyLittleSlideShow`, `%APPDATA%`), nie feste
  Pfade eines Rechners, auch nicht als Voreinstellung;
- Namen von Ordnern oder Dateien aus dem eigenen Bildbestand – Testbilder
  technisch beschreiben („ein JPEG mit 24 Megapixeln und ICC-Profil“);
- Zugangsschlüssel, Tokens, Passwörter, Zertifikate, `.env`-Dateien. Gerät so
  etwas doch hinein: als verbrannt ansehen und neu erzeugen.
- Bilder, die ins Repository kommen, auf Metadaten (EXIF, Autor) und sichtbare
  Pfade prüfen.

**Ausnahme:** Die PayPal-Empfängeradresse in `Services/Donation.cs` gehört zur
Spendenfunktion und bleibt.

Ältere Commits auf GitHub tragen noch persönliche Autorenangaben. Die Historie
wird nur auf ausdrücklichen Wunsch umgeschrieben (Force-Push).

## Weitere Regeln für Inhalte

- **Keine Verweise auf andere eigene Projekte** – weder in Code, Kommentaren,
  Doku noch in Commit-Nachrichten („übernommen aus …“, „wie im …“). Das Projekt
  beschreibt, was es selbst tut und warum.
- **Keine Vergleiche mit fremden Programmen** („wie in …“). Erlaubt sind
  benutzte Bibliotheken und Werkzeuge, Lizenz- und Herkunftsangaben.
- **Nicht angeben, welches KI-Werkzeug geholfen hat:** keine Mitautor-Zeilen
  für Werkzeuge in Commit-Nachrichten, keine Hinweise auf das Werkzeug, keine
  Sitzungslinks.
  Anweisungsdateien solcher Werkzeuge bleiben lokal (`.git/info/exclude`, nicht
  `.gitignore`).
- **Kommentare** beschreiben den heutigen Stand und das Warum, nicht die
  Entstehung (die steht in Git und im Changelog). Stil wie im vorhandenen Code.

## Versionen

- Die Versionsnummer steht nur in `<Version>` in
  `src/MyLittleSlideShow/MyLittleSlideShow.csproj`.
- Jede Programmänderung bekommt im selben Commit eine neue Patch-Version mit
  `-beta`: `2.3.1-beta` → `2.3.2-beta`. Reine Doku-Änderungen brauchen keine.
- Eine Version ohne `-beta` (nächste Nebenversion, z. B. `2.4.0`) gibt es nur,
  wenn ein Stand ausdrücklich als Meilenstein abgenommen ist. Nur dann ein Tag
  `v2.4.0` – das Tag löst auf GitHub das Release aus.
- `Infos/Changelog.txt` bei sichtbaren Änderungen ergänzen.

## Keine Daten an Hersteller

- Telemetrie ist überall aus: im Programm gibt es keine, beim Bauen setzen
  `publish.sh`, `publish.ps1` und der Workflow die Schalter selbst
  (`DOTNET_CLI_TELEMETRY_OPTOUT`, `MSBUILD_TELEMETRY_OPTOUT`,
  `AVALONIA_TELEMETRY_OPTOUT`); Avalonias Build-Telemetrie ist im csproj
  abgeschaltet (`ENTWICKLUNG.md`).
- Vor jedem neuen Paket oder Werkzeug prüfen, ob es Daten sendet, und es im
  Projekt selbst abschalten.

## Fremder Code und Bibliotheken

Im selben Commit, in dem etwas Fremdes dazukommt (Paket, Code aus einem
Repository, aus Foren oder Artikeln, fremde Daten oder Schriften):

- Eintrag in `THIRD-PARTY-NOTICES.md`: Name, Version, Webadresse, Lizenz, wo es
  im Code steckt;
- vollständiger Lizenztext nach `packaging/licenses/` (wird mit ausgeliefert und
  im Programm angezeigt);
- an der Stelle im Code ein kurzer Herkunftskommentar.

Vorher die Lizenz prüfen. GPL/AGPL, „nur privat“, „nicht kommerziell“,
CC BY-SA (z. B. Stack Overflow) oder unklar: erst klären, nicht einbauen.
`tools/check-licenses.py` prüft die Pakete im Workflow.

## Bauen

| Befehl | Ergebnis |
|---|---|
| `./publish.sh linux` | `published/linux-x64/MyLittleSlideShow-x86_64.AppImage`, danach Menüeintrag |
| `./publish.sh windows` | `published/windows-x64/MyLittleSlideShow.exe` |
| `./publish.sh beides` | beides |
| `.\publish.ps1` (Windows) | `published\windows-x64\MyLittleSlideShow.exe` |

- Programmdateien ohne Versionsnummer im Namen, sonst zeigen Menü- und
  Autostart-Eintrag nach dem nächsten Bau ins Leere. Die Version steht im
  Programm und im Namen der Archive.
- Nicht bauen, während das Programm aus `published/` läuft (`publish.sh` bricht
  dann ab).
- `published/` steht in `.gitignore` und kommt nie ins Repository.

## Fehlergedächtnis

Nach jedem behobenen Fehler ein Eintrag in `docs/FEHLERGEDAECHTNIS.md` (neue ans
Ende): entdeckt und behoben in welcher Version, wie bemerkt, was passiert ist und
wo, warum, Lösung und warum diese, was man nicht tun sollte, wo im Code, welcher
Test bzw. welche Prüfung es absichert.

## Fertig ist eine Änderung, wenn …

1. sie ohne neue Warnungen baut;
2. sie ausprobiert ist (eine Testsuite gibt es nicht);
3. Doku, Kommentare und Fehlergedächtnis stimmen;
4. die Version erhöht ist;
5. der Stand durchgesehen (keine persönlichen Daten, siehe oben), committet und
   gepusht ist.
