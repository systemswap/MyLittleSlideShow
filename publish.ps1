<#
.SYNOPSIS
    Baut MyLittleSlideShow fuer Windows - Pendant zu publish.sh (Linux/AppImage).

.DESCRIPTION
    Ergebnis in published\windows-x64\:
        MyLittleSlideShow.exe     eine einzige Datei: Programm, .NET und alle Bibliotheken
        LICENSE.txt               eigene Lizenz (MIT)
        THIRD-PARTY-NOTICES.md    verwendete Fremdbestandteile
        licenses\                 deren Lizenztexte
    dazu published\MyLittleSlideShow-<Version>-windows-x64.zip (aeltere Archive werden ersetzt).
    Die EXE traegt keine Versionsnummer im Namen, damit ein Autostart-Eintrag nach dem
    naechsten Bau weiter stimmt.

    Die Versionsnummer steht nur im csproj (<Version>).

.EXAMPLE
    .\publish.ps1
#>

[CmdletBinding()]
param(
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

# Keine Telemetrie von .NET-CLI und MSBuild (gilt nur fuer diesen Lauf).
# Avalonias Build-Telemetrie ist im csproj abgeschaltet.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:MSBUILD_TELEMETRY_OPTOUT    = '1'
$env:AVALONIA_TELEMETRY_OPTOUT  = '1'
$env:DO_NOT_TRACK               = '1'
$env:DOTNET_NOLOGO               = '1'

$Root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root 'src\MyLittleSlideShow\MyLittleSlideShow.csproj'
$Published = Join-Path $Root 'published'
$OutDir  = Join-Path $Published 'windows-x64'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Das .NET SDK wurde nicht gefunden. Bitte SDK 10 installieren: https://dotnet.microsoft.com/download'
}

$version = ([xml](Get-Content $Project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "MyLittleSlideShow $version fuer Windows -> $OutDir" -ForegroundColor Cyan

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

# Eine Datei mit integriertem .NET; native Bibliotheken (Skia, HarfBuzz, ANGLE)
# werden beim Start entpackt.
& dotnet publish $Project `
    -c $Configuration -r win-x64 -o $OutDir --nologo `
    -p:SelfContained=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ist fehlgeschlagen.' }

# Debug-Symbole der nativen Bibliotheken (SkiaSharp 3 legt sie daneben) gehoeren nicht dazu.
Get-ChildItem $OutDir -Filter *.pdb -File | Remove-Item -Force

# Lizenztexte gehoeren zur Weitergabe dazu (u. a. Apache-2.0 fuer MetadataExtractor).
# Im Programm stehen sie zusaetzlich unter Einstellungen > Infos > Lizenzen.
Copy-Item (Join-Path $Root 'LICENSE') (Join-Path $OutDir 'LICENSE.txt') -Force
Copy-Item (Join-Path $Root 'THIRD-PARTY-NOTICES.md') $OutDir -Force
Copy-Item (Join-Path $Root 'packaging\licenses') (Join-Path $OutDir 'licenses') -Recurse -Force

Get-ChildItem $Published -Filter 'MyLittleSlideShow-*-windows-x64.zip' -File | Remove-Item -Force
$archive = Join-Path $Published "MyLittleSlideShow-$version-windows-x64.zip"
Compress-Archive -Path $OutDir -DestinationPath $archive

Write-Host ''
Get-ChildItem $OutDir -File | Sort-Object Length -Descending |
    ForEach-Object { '  {0,10:N0} KB  {1}' -f ($_.Length / 1KB), $_.Name }
Write-Host ''
Write-Host "Fertig: $OutDir\MyLittleSlideShow.exe, $archive" -ForegroundColor Green
