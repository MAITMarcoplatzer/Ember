# Ember-Installer fuer Endanwender
#
#   MAIT-intern: irm https://git.mait.de/AI.Network/Ember/raw/branch/main/install.ps1 | iex
#   Extern:      irm https://raw.githubusercontent.com/MAITMarcoplatzer/Ember/main/install.ps1 | iex
#
# Laedt die aktuelle Ember.exe aus dem neuesten Release (MAIT-Gitea, Fallback
# GitHub), legt sie unter %LOCALAPPDATA%\Ember ab, erstellt eine Startmenue-
# Verknuepfung, aktiviert den Autostart und startet die App. Installiert
# codeburn automatisch, falls es fehlt.

$ErrorActionPreference = 'Stop'
$releaseApis = @(
    'https://git.mait.de/api/v1/repos/AI.Network/Ember/releases/latest',
    'https://api.github.com/repos/MAITMarcoplatzer/Ember/releases/latest'
)
$installDir = Join-Path $env:LOCALAPPDATA 'Ember'
$exePath = Join-Path $installDir 'Ember.exe'

Write-Host "Ember wird installiert..." -ForegroundColor Cyan

# 1. codeburn sicherstellen (Datenquelle von Ember)
if (-not (Get-Command codeburn -ErrorAction SilentlyContinue)) {
    if (Get-Command npm -ErrorAction SilentlyContinue) {
        Write-Host "codeburn fehlt - wird per npm installiert..."
        npm install -g codeburn
    } else {
        Write-Error ("codeburn benoetigt Node.js 20+. Bitte zuerst installieren: " +
            "winget install OpenJS.NodeJS.LTS - danach dieses Skript erneut ausfuehren.")
    }
}

# 2. Neueste Ember.exe aus dem letzten Release laden (erste erreichbare Quelle)
$release = $null
foreach ($apiUrl in $releaseApis) {
    try {
        $release = Invoke-RestMethod $apiUrl -TimeoutSec 15
        Write-Host "Release-Quelle: $apiUrl"
        break
    } catch {
        Write-Host "Quelle nicht erreichbar, versuche naechste... ($apiUrl)"
    }
}
if (-not $release) { Write-Error "Keine Release-Quelle erreichbar (Gitea/GitHub)." }
$asset = $release.assets | Where-Object name -eq 'Ember.exe' | Select-Object -First 1
if (-not $asset) { Write-Error "Im neuesten Release wurde keine Ember.exe gefunden." }

New-Item -ItemType Directory -Force $installDir | Out-Null
Get-Process Ember -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300
Write-Host "Lade Ember $($release.tag_name)..."
Invoke-WebRequest $asset.browser_download_url -OutFile $exePath

# 3. Startmenue-Verknuepfung
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut(
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Ember.lnk'))
$shortcut.TargetPath = $exePath
$shortcut.Description = 'Ember - CodeBurn fuer Windows'
$shortcut.Save()

# 4. Autostart aktivieren
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
    -Name 'Ember' -Value "`"$exePath`""

# 5. Starten
Start-Process $exePath

Write-Host ""
Write-Host "Ember $($release.tag_name) wurde installiert und gestartet." -ForegroundColor Green
Write-Host "Das Kosten-Icon erscheint im System-Tray (rechts unten, ggf. hinter dem Pfeil)."
