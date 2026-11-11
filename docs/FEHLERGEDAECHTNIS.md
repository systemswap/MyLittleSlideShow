# Fehlergedächtnis

Behobene Fehler und Fallen, zeitlich nach der Behebung geordnet, neue Einträge
ans Ende. Vor Änderungen an einer der genannten Stellen hier nachlesen.
Einträge bleiben stehen, auch wenn der Code inzwischen anders aussieht.

Die ersten vier Einträge (Versionen 2.0.0.x) entstanden in einer Fassung, die
eine Zeit lang getrennt von diesem Repository geführt wurde; die dort genannten
Commit-Kennungen gibt es hier nicht. Was davon heute gilt, steht im Eintrag
„Zwei getrennt geführte Fassungen“.

## Telemetrie: Avalonia meldete jeden Build (Avalonia.BuildServices)

- **Entdeckt:** 07.10.2026, bei der Durchsicht aller Projekte nach
  Telemetrie (vor dem ersten Commit). **Behoben:** 07.10.2026, im
  Ausgangsstand (2.0.0.0) schon enthalten.
- **Was:** Das Paket `Avalonia.BuildServices` (kommt automatisch mit Avalonia)
  führt vor jedem Kompilieren die `AvaloniaStatsTask` aus und meldet Projekt
  (gehasht), Avalonia-Version, Zielsystem, IDE, Betriebssystem und eine
  Geräte-Kennung an `av-build-tel-api-v1.avaloniaui.net`. Gefragt wird nicht.
- **Situation:** jeder Build in Rider, Visual Studio oder mit `dotnet` auf
  einem Rechner ohne `AVALONIA_TELEMETRY_OPTOUT=1`.
- **Wie behoben:** `Directory.Build.targets` mit leerem Target
  `AvaloniaStats` (MSBuild lädt die Datei nach den Paket-Targets, die letzte
  Definition gilt); die Build-Skripte setzen außerdem
  `DOTNET_CLI_TELEMETRY_OPTOUT`, `AVALONIA_TELEMETRY_OPTOUT`,
  `ORT_DISABLE_TELEMETRY`, `TESTINGPLATFORM_TELEMETRY_OPTOUT`.
- **Nicht tun:** die Datei löschen oder eine zweite `Directory.Build.targets`
  in einem Unterordner anlegen (MSBuild nimmt nur die nächstgelegene). Nach
  einem Avalonia-Update prüfen, ob das Target noch `AvaloniaStats` heißt.
- **Wo:** `Directory.Build.targets`, `publish.sh`, `publish.ps1`.
- **Nachweis:** `dotnet build -v:d` meldet „Das Ziel "AvaloniaStats" … wird
  durch das Ziel … aus Directory.Build.targets außer Kraft gesetzt“ (ohne Netz
  geprüft).

## Autostart zeigte bei der AppImage ins Leere

- **Entdeckt:** 2.0.0.0 (Commit `29a130a`, 07.10.2026), beim Umstellen auf die
  AppImage (nicht beim Benutzer aufgetreten). **Behoben:** 2.0.0.1.
- **Was:** Der Autostart-Eintrag (`~/.config/autostart/mylittleslideshow.desktop`)
  bekam `Environment.ProcessPath` als Programm. In einer AppImage ist das das
  Innere unter `/tmp/.mount_…/usr/bin/MyLittleSlideShow`, das bei jedem Start
  anders heißt – beim nächsten Anmelden hätte der Autostart ins Leere gezeigt,
  und `IsEnabled()` hätte den Haken nach einem Neustart als aus gemeldet.
- **Wie behoben:** `AppLocations.LaunchPath` liefert bei einer AppImage die
  .AppImage-Datei selbst (`$APPIMAGE`), sonst das laufende Programm.
- **Nicht tun:** `Environment.ProcessPath` oder `AppContext.BaseDirectory` für
  Pfade benutzen, die über den Programmstart hinaus gelten (Autostart,
  Menüeintrag, Dateien neben dem Programm).
- **Wo:** `Platform/AppLocations.cs`, `Platform/LinuxServices.cs`
  (`LinuxAutostartService.ExecutablePath`).
- **Nachweis:** von Hand – Autostart in der AppImage einschalten und in
  `~/.config/autostart/mylittleslideshow.desktop` prüfen, dass `Exec` auf
  `…/published/linux-x64/MyLittleSlideShow-x86_64.AppImage` zeigt.

## ImageSharp in eigenem Ordner: verwaltete DLL aus Unterordner laden

- **Neu in:** 2.0.0.1 (07.10.2026). Kein Fehler, aber eine Falle.
- **Was:** ImageSharp liegt nicht mehr direkt neben dem Programm, sondern in
  `Tools/ImageSharp/` (Wunsch: fremde Bibliotheken je in eigenem Ordner neben
  EXE/AppImage). Eine *verwaltete* DLL findet .NET dort nicht von selbst.
- **Wie gelöst:** Das Build-Target `KeepExternalLibsOutOfBundle` legt die DLL
  mit `ExcludeFromSingleFile` nach `Tools/ImageSharp`;
  `AppLocations.UseToolLibraries()` (erste Zeile in `Main`) lädt sie über
  `AssemblyLoadContext.Default.Resolving`. Fehlt der Ordner (Start aus der
  IDE), gilt die normale Suche im Ausgabeordner.
- **Nicht tun:** in `Main` (oder statischen Feldern von `Program`) schon
  ImageSharp-Typen benutzen – der Resolver muss vorher stehen.
- **Nachweis:** Testprogramm (Single-File, DLL nur in `Tools/ImageSharp`):
  lädt die DLL von dort.

## „Neu starten“ im Tray-Menü bei der AppImage

- **Entdeckt:** 2.0.0.3 (07.10.2026), beim Durchsehen von `App.axaml.cs`
  (nicht beim Benutzer aufgetreten). **Behoben:** 2.0.0.4.
- **Was:** „Neu starten“ startete `Environment.ProcessPath` – in einer AppImage
  das Innere unter `/tmp/.mount_…`. Beendet sich das alte Programm danach,
  hängt die AppImage-Laufzeit dieses Innere aus; der neue Prozess hätte seine
  Dateien verloren.
- **Wie behoben:** wie beim Autostart `AppLocations.LaunchPath` (die
  .AppImage-Datei selbst).
- **Nicht tun:** siehe Eintrag „Autostart zeigte bei der AppImage ins Leere“.
- **Wo:** `App.axaml.cs` (`Tray_Restart_Click`).

## Zwei getrennt geführte Fassungen: AppImage-Korrekturen fehlten, Menüeintrag zeigte auf den alten Stand

- **Entdeckt:** 2.3.0 (Commit `44a91f8`), beim Zusammenführen mit dem Stand auf
  GitHub. **Behoben:** 2.3.1-beta.
- **Wie bemerkt:** Nach dem Bau mit dem damaligen `publish.sh` startete der
  Menüeintrag weiter das alte Programm; beim Abgleich der beiden Fassungen fielen
  die übrigen Punkte auf.
- **Was:** Die auf GitHub weiterentwickelte Fassung (2.1 bis 2.3) und eine lokal
  geführte Fassung (2.0.0.x) hatten keine gemeinsame Historie. In der
  GitHub-Fassung fehlten deshalb Korrekturen der lokalen:
  - `publish.sh` baute nach `publish/linux-x64/MyLittleSlideShow-<Version>-x86_64.AppImage`
    – mit Versionsnummer im Namen und ohne Menüeintrag. Der vorhandene
    Menüeintrag zeigte weiter auf das alte Programm in `published/`, und ein
    Eintrag auf die neue Datei wäre nach dem nächsten Bau ins Leere gelaufen.
  - Autostart (`LinuxAutostartService`) und „Neu starten“ im Tray benutzten
    wieder `Environment.ProcessPath` – siehe die Einträge „Autostart zeigte bei
    der AppImage ins Leere“ und „Neu starten im Tray-Menü bei der AppImage“.
  - Die Firmenangabe im csproj und die Übernahme der Einstellungen der früheren
    WPF-Fassung (sie suchte unter einem Firmenordner) waren wieder da.
- **Warum:** An zwei Rechnern wurde unabhängig weitergearbeitet, ohne vorher den
  Stand des anderen zu holen.
- **Lösung:** Der GitHub-Stand ist die Grundlage. Darauf:
  - `publish.sh` baut nach `published/linux-x64/MyLittleSlideShow-x86_64.AppImage`
    (fester Name), legt Lizenzen, `einrichten.sh`/`entfernen.sh` und die
    Archive mit Versionsnummer daneben und trägt das Programm danach ins Menü
    ein; `publish.ps1` und der Workflow wurden auf `published/` umgestellt.
  - `Platform/AppLocations.LaunchPath` (bei einer AppImage `$APPIMAGE`) für
    Autostart und Neustart.
  - Firmenangabe und WPF-Übernahme entfernt.
  - Die Avalonia-Build-Telemetrie ist heute über `ExcludeAssets="all"` für
    `Avalonia.BuildServices` im csproj abgeschaltet, nicht mehr über
    `Directory.Build.targets` (siehe ersten Eintrag).
- **Nicht tun:** Versionsnummern in den Namen der Programmdatei schreiben; für
  Pfade, die über den Programmstart hinaus gelten, `Environment.ProcessPath`
  benutzen; auf einem Rechner weiterarbeiten, ohne vorher `git pull` zu machen
  (Regeln: `docs/ARBEITSREGELN.md`).
- **Wo:** `publish.sh`, `publish.ps1`, `.github/workflows/build-release.yml`,
  `packaging/linux/`, `Platform/AppLocations.cs`, `App.axaml.cs`
  (`Tray_Restart_Click`), `Platform/LinuxServices.cs` (`LinuxAutostartService`).
- **Test:** kein automatischer (das Projekt hat keine Testsuite). Von Hand:
  `./publish.sh linux`, danach zeigt `Exec` in
  `~/.local/share/applications/MyLittleSlideShow.desktop` auf
  `…/published/linux-x64/MyLittleSlideShow-x86_64.AppImage`; Autostart in der
  AppImage einschalten, dann muss `Exec` in
  `~/.config/autostart/mylittleslideshow.desktop` auf dieselbe Datei zeigen.
