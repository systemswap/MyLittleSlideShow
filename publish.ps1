<#
    MyLittleSlideShow - Publish-Skript fuer Windows (PowerShell 5.1 / 7.x)

    Aufruf:
        .\publish.ps1                 -> interaktives Menue
        .\publish.ps1 -Choice 2       -> direkt Punkt 2 bauen
        .\publish.ps1 -Choice 9 -SingleFile:$false

    Die Menuepunkte sind mit publish.sh identisch nummeriert.
    AppImage (7/8) laesst sich nur unter Linux bzw. im WSL bauen, dort mit
    ./publish.sh 7 bzw. ./publish.sh 8.
#>

[CmdletBinding()]
param(
    [int]    $Choice = -1,
    [switch] $SingleFile = $true,
    [switch] $KeepExternalLibsSeparate = $true,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

# Keine Telemetrie von .NET-CLI und MSBuild (gilt nur fuer diesen Lauf).
# Avalonias Build-Telemetrie ist im csproj abgeschaltet.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:MSBUILD_TELEMETRY_OPTOUT    = '1'
$env:DOTNET_NOLOGO               = '1'

$Root       = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project    = Join-Path $Root 'src\MyLittleSlideShow\MyLittleSlideShow.csproj'
$OutputRoot = Join-Path $Root 'publish'

function Write-Head($text) {
    Write-Host ''
    Write-Host "=== $text ===" -ForegroundColor Cyan
}

function Assert-Dotnet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "Das .NET SDK wurde nicht gefunden. Bitte SDK 10.0 installieren: https://dotnet.microsoft.com/download"
    }
    $sdkVersion = (& dotnet --version).Trim()
    Write-Host "Verwendetes SDK: $sdkVersion"
    if ($sdkVersion -notmatch '^(1[0-9]|[2-9][0-9])\.') {
        Write-Warning "Das Projekt zielt auf net10.0. Mit SDK $sdkVersion schlaegt der Build vermutlich fehl."
    }
}

function Invoke-Publish {
    param(
        [Parameter(Mandatory)] [string] $Rid,
        [Parameter(Mandatory)] [bool]   $SelfContained,
        [Parameter(Mandatory)] [string] $Label
    )

    $modeName = if ($SelfContained) { 'standalone' } else { 'normal' }
    $outDir   = Join-Path $OutputRoot "$Rid-$modeName"

    Write-Head "$Label  ->  $outDir"

    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    $arguments = @(
        'publish', $Project,
        '-c', $Configuration,
        '-r', $Rid,
        '-o', $outDir,
        "-p:SelfContained=$($SelfContained.ToString().ToLower())",
        "-p:PublishSingleFile=$($SingleFile.ToString().ToLower())",
        '-p:DebugType=none',
        '-p:PublishReadyToRun=false',
        '-p:PublishTrimmed=false',
        '--nologo'
    )

    if ($SingleFile) {
        # Native Begleitdateien wandern mit in die EXE und werden zur Laufzeit
        # entpackt. Ohne das liegen libSkiaSharp und libHarfBuzzSharp daneben.
        $arguments += '-p:IncludeNativeLibrariesForSelfExtract=true'

        # Komprimierung erlaubt das SDK ausschliesslich bei self-contained
        # (sonst: error NETSDK1176).
        if ($SelfContained) {
            $arguments += '-p:EnableCompressionInSingleFile=true'
        }

        if ($KeepExternalLibsSeparate) {
            $arguments += '-p:KeepExternalLibsSeparate=true'
        }
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen (RID $Rid)." }

    Get-ChildItem $outDir -Include *.pdb, *.xml -Recurse -File -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue

    # Lizenztexte gehoeren zur Weitergabe dazu (Apache-2.0 verlangt es fuer
    # MetadataExtractor). Im Programm stehen sie zusaetzlich unter Infos > Lizenzen.
    Copy-Item (Join-Path $Root 'LICENSE') (Join-Path $outDir 'LICENSE.txt') -Force
    Copy-Item (Join-Path $Root 'THIRD-PARTY-NOTICES.md') $outDir -Force
    Copy-Item (Join-Path $Root 'licenses') $outDir -Recurse -Force

    $files = Get-ChildItem $outDir -File
    $size  = [math]::Round((($files | Measure-Object Length -Sum).Sum) / 1MB, 1)

    Write-Host ''
    Write-Host "Fertig: $($files.Count) Datei(en), $size MB" -ForegroundColor Green
    $files | Sort-Object Length -Descending | Select-Object -First 10 |
        ForEach-Object { '  {0,10:N0} KB  {1}' -f ($_.Length / 1KB), $_.Name }

    if (-not $SelfContained) {
        Write-Host ''
        Write-Host 'Hinweis: Diese Variante braucht eine installierte .NET-10-Laufzeit auf dem Zielrechner.' -ForegroundColor Yellow
    }
}

# Nummerierung identisch zu publish.sh
$menu = @(
    @{ N = 1; Rid = 'win-x64';     Sc = $false; Text = 'Windows x64   - normal      (setzt .NET 10 auf dem Zielrechner voraus)' }
    @{ N = 2; Rid = 'win-x64';     Sc = $true;  Text = 'Windows x64   - standalone  (.NET ist integriert)' }
    @{ N = 3; Rid = 'linux-x64';   Sc = $false; Text = 'Linux x64     - normal' }
    @{ N = 4; Rid = 'linux-x64';   Sc = $true;  Text = 'Linux x64     - standalone' }
    @{ N = 5; Rid = 'linux-arm64'; Sc = $false; Text = 'Linux arm64   - normal' }
    @{ N = 6; Rid = 'linux-arm64'; Sc = $true;  Text = 'Linux arm64   - standalone' }
)

function Show-Menu {
    Write-Host ''
    Write-Host '  MyLittleSlideShow - Publish' -ForegroundColor White
    Write-Host '  ---------------------------'
    foreach ($m in $menu) { Write-Host ("  {0})  {1}" -f $m.N, $m.Text) }
    Write-Host '  7)  Linux x64     - AppImage    (nur unter Linux/WSL: ./publish.sh 7)' -ForegroundColor DarkGray
    Write-Host '  8)  Linux arm64   - AppImage    (nur unter Linux/WSL: ./publish.sh 8)' -ForegroundColor DarkGray
    Write-Host '  9)  Alles bauen (ohne AppImage)'
    Write-Host '  0)  Abbrechen'
    Write-Host ''
    Write-Host ("  Einzeldatei: {0}   Fremdbibliotheken separat: {1}" -f $SingleFile, $KeepExternalLibsSeparate) -ForegroundColor DarkGray
    Write-Host ''
}

Assert-Dotnet

if ($Choice -lt 0) {
    Show-Menu
    $answer = Read-Host 'Auswahl'
    if (-not [int]::TryParse($answer, [ref]$Choice)) { Write-Host 'Ungueltige Eingabe.'; exit 1 }
}

switch ($Choice) {
    0 { Write-Host 'Abgebrochen.'; exit 0 }
    { $_ -in 7, 8 } {
        Write-Warning 'AppImage kann nur unter Linux gebaut werden (appimagetool und FUSE werden gebraucht).'
        Write-Host    'Nutze dort:  ./publish.sh 7   bzw.   ./publish.sh 8'
        exit 1
    }
    9 { foreach ($m in $menu) { Invoke-Publish -Rid $m.Rid -SelfContained $m.Sc -Label $m.Text } }
    default {
        $sel = $menu | Where-Object { $_.N -eq $Choice }
        if (-not $sel) { Write-Host 'Ungueltige Auswahl.'; exit 1 }
        Invoke-Publish -Rid $sel.Rid -SelfContained $sel.Sc -Label $sel.Text
    }
}

Write-Host ''
Write-Host "Alle Ergebnisse liegen unter: $OutputRoot" -ForegroundColor Green
