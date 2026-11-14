$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = if (Test-Path "C:\Program Files\dotnet\dotnet.exe") { "C:\Program Files\dotnet\dotnet.exe" } else { "dotnet" }
$buildRoot = Join-Path $root "build"
$releaseDir = Join-Path $root "build\STS2-LongDistancePlay"
$dllSource = Join-Path $root "src\STS2-LongDistancePlay\bin\Release\STS2-LongDistancePlay.dll"
$pckSource = Join-Path $root "src\STS2-LongDistancePlay\bin\Release\STS2-LongDistancePlay.pck"
if (-not (Test-Path $pckSource)) {
    $pckSource = Join-Path $root "build\STS2-LongDistancePlay.pck"
}
$manifestPath = Join-Path $root "STS2-LongDistancePlay.json"
if (-not (Test-Path $manifestPath)) {
    $manifestPath = Join-Path $root "src\STS2-LongDistancePlay\STS2-LongDistancePlay.json"
}

Write-Host "Building STS2-LongDistancePlay (.NET Release)..." -ForegroundColor Cyan
& $dotnet build (Join-Path $root "src\STS2-LongDistancePlay\STS2-LongDistancePlay.csproj") -c Release

$py = Get-Command python -ErrorAction SilentlyContinue
if (-not $py) { $py = Get-Command python3 -ErrorAction SilentlyContinue }
if ($py) {
    Write-Host "Packing PCK via Python..." -ForegroundColor Cyan
    & $py.Source (Join-Path $root "tools\pack_pck.py") $pckSource
}

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Get-ChildItem -Path $releaseDir -Force | Remove-Item -Recurse -Force
Get-ChildItem -Path $buildRoot -Filter "STS2-LongDistancePlay-*.zip" -File -ErrorAction SilentlyContinue | Remove-Item -Force

Copy-Item $dllSource -Destination (Join-Path $releaseDir "STS2-LongDistancePlay.dll") -Force
Copy-Item $pckSource -Destination (Join-Path $releaseDir "STS2-LongDistancePlay.pck") -Force
Copy-Item $manifestPath -Destination (Join-Path $releaseDir "STS2-LongDistancePlay.json") -Force

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$version = [string]$manifest.version
$modFolderName = "STS2-LongDistancePlay"

# Helper function to write LF scripts without BOM
function Write-UnixScript($filePath, $content) {
    $normalized = $content.Replace("`r`n", "`n")
    [System.IO.File]::WriteAllText($filePath, $normalized, [System.Text.UTF8Encoding]::new($false))
}

# --- Script Definitions ---

$windowsBat = @'
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0helper.ps1"
pause
'@

$windowsPs1 = @'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$host.UI.RawUI.WindowTitle = 'STS2-LongDistancePlay Installer'

Write-Host '============================================'
Write-Host '  Long Distance Play (by 0xb1)'
Write-Host '  One-Click Windows Mod Installer'
Write-Host '============================================'
Write-Host ''

$src = $PSScriptRoot
$modFolder = Join-Path $src 'STS2-LongDistancePlay'
$dll  = Join-Path $modFolder 'STS2-LongDistancePlay.dll'
$pck  = Join-Path $modFolder 'STS2-LongDistancePlay.pck'
$json = Join-Path $modFolder 'STS2-LongDistancePlay.json'

$missing = @()
if (-not (Test-Path $dll))  { $missing += 'STS2-LongDistancePlay.dll' }
if (-not (Test-Path $pck))  { $missing += 'STS2-LongDistancePlay.pck' }
if (-not (Test-Path $json)) { $missing += 'STS2-LongDistancePlay.json' }

if ($missing.Count -gt 0) {
    Write-Host '[ERROR] Missing mod files:' -ForegroundColor Red
    foreach ($f in $missing) { Write-Host "  - $f" -ForegroundColor Red }
    Write-Host ''
    Write-Host 'Please ensure this script is in the same directory as the STS2-LongDistancePlay folder.'
    exit 1
}

Write-Host 'Locating Slay the Spire 2 installation...'
Write-Host ''

$sp = $null
try { $sp = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' -EA Stop).InstallPath } catch {}
if (-not $sp) {
    try { $sp = (Get-ItemProperty 'HKCU:\SOFTWARE\Valve\Steam' -EA Stop).SteamPath } catch {}
}

$gp = $null
if ($sp) {
    $vdf = Join-Path $sp 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
        foreach ($line in Get-Content $vdf) {
            if ($line -match '"path"\s+"([^"]+)"') {
                $p = $Matches[1].Replace('\\', '\')
                $c = Join-Path $p 'steamapps\common\Slay the Spire 2'
                if (Test-Path $c) { $gp = $c; break }
            }
        }
    }
    if (-not $gp) {
        $c = Join-Path $sp 'steamapps\common\Slay the Spire 2'
        if (Test-Path $c) { $gp = $c }
    }
}

if ($gp) {
    Write-Host "Found game directory: $gp" -ForegroundColor Green
    Write-Host ''

    $dest = Join-Path $gp 'mods\STS2-LongDistancePlay'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $modFolder '*') -Destination $dest -Recurse -Force

    Write-Host '============================================'
    Write-Host '  Installation successful!' -ForegroundColor Green
    Write-Host '============================================'
    Write-Host ''
    Write-Host "Installed to: $dest"
} else {
    Write-Host '============================================'
    Write-Host '  Auto-detection failed' -ForegroundColor Red
    Write-Host '============================================'
    Write-Host ''
    Write-Host 'Could not locate Slay the Spire 2 automatically.'
    Write-Host 'Please copy the "STS2-LongDistancePlay" directory manually to:'
    Write-Host '  <Slay the Spire 2>\mods\STS2-LongDistancePlay\'
}

Write-Host ''
'@

$macInstallSh = @'
#!/usr/bin/env bash
set -e

cd "$(dirname "$0")"

echo "============================================"
echo "  Long Distance Play (by 0xb1)"
echo "  macOS Mod Installer"
echo "============================================"
echo ""

MOD_SRC="STS2-LongDistancePlay"
if [ ! -d "$MOD_SRC" ] || [ ! -f "$MOD_SRC/STS2-LongDistancePlay.dll" ]; then
    echo "[ERROR] Missing STS2-LongDistancePlay folder or binaries."
    echo "Please ensure this script is run from the extracted zip archive."
    exit 1
fi

echo "Locating Slay the Spire 2 installation..."

DEFAULT_STEAM_PATH="$HOME/Library/Application Support/Steam"
GAME_DIR=""

if [ -d "$DEFAULT_STEAM_PATH/steamapps/common/Slay the Spire 2" ]; then
    GAME_DIR="$DEFAULT_STEAM_PATH/steamapps/common/Slay the Spire 2"
fi

if [ -z "$GAME_DIR" ] && [ -f "$DEFAULT_STEAM_PATH/steamapps/libraryfolders.vdf" ]; then
    while IFS= read -r line; do
        if [[ $line =~ \"path\"[[:space:]]+\"([^\"]+)\" ]]; then
            CANDIDATE="${BASH_REMATCH[1]}/steamapps/common/Slay the Spire 2"
            if [ -d "$CANDIDATE" ]; then
                GAME_DIR="$CANDIDATE"
                break
            fi
        fi
    done < "$DEFAULT_STEAM_PATH/steamapps/libraryfolders.vdf"
fi

if [ -n "$GAME_DIR" ]; then
    echo "Found game directory: $GAME_DIR"

    APP_BUNDLE="$GAME_DIR/SlayTheSpire2.app"
    if [ -d "$APP_BUNDLE" ]; then
        DEST_DIR="$APP_BUNDLE/Contents/MacOS/mods/STS2-LongDistancePlay"
        STEAM_APPID_FILE="$APP_BUNDLE/Contents/MacOS/steam_appid.txt"
    else
        DEST_DIR="$GAME_DIR/mods/STS2-LongDistancePlay"
        STEAM_APPID_FILE="$GAME_DIR/steam_appid.txt"
    fi

    mkdir -p "$DEST_DIR"
    cp -R "$MOD_SRC/"* "$DEST_DIR/"

    if [ ! -f "$STEAM_APPID_FILE" ]; then
        echo "2868840" > "$STEAM_APPID_FILE"
        echo "Created steam_appid.txt (bypasses direct launch Steam errors)."
    fi

    echo ""
    echo "============================================"
    echo "  Installation successful!"
    echo "============================================"
    echo "Installed to: $DEST_DIR"
    echo ""
    echo "Note: On Apple Silicon, ensure the game is opened using Rosetta 2:"
    echo "  Right-click SlayTheSpire2.app > Get Info > check 'Open using Rosetta'"
else
    echo "============================================"
    echo "  Auto-detection failed"
    echo "============================================"
    echo ""
    echo "Could not locate Slay the Spire 2 automatically."
    echo "Please copy the 'STS2-LongDistancePlay' folder manually to:"
    echo "  <Slay the Spire 2>/SlayTheSpire2.app/Contents/MacOS/mods/STS2-LongDistancePlay/"
fi

echo ""
read -n 1 -s -r -p "Press any key to close..." || true
echo ""
'@

$macInstallCmd = @'
#!/usr/bin/env bash
cd "$(dirname "$0")"
chmod +x ./install.sh 2>/dev/null || true
./install.sh
'@

$linuxInstallSh = @'
#!/usr/bin/env bash
set -e

cd "$(dirname "$0")"

echo "============================================"
echo "  Long Distance Play (by 0xb1)"
echo "  Linux / Steam Deck Mod Installer"
echo "============================================"
echo ""

MOD_SRC="STS2-LongDistancePlay"
if [ ! -d "$MOD_SRC" ] || [ ! -f "$MOD_SRC/STS2-LongDistancePlay.dll" ]; then
    echo "[ERROR] Missing STS2-LongDistancePlay folder or binaries."
    echo "Please ensure this script is run from the extracted zip archive."
    exit 1
fi

echo "Locating Slay the Spire 2 installation..."

CANDIDATES=(
    "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2"
    "$HOME/.steam/steam/steamapps/common/Slay the Spire 2"
    "$HOME/.steam/root/steamapps/common/Slay the Spire 2"
    "$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/Slay the Spire 2"
    "/run/media/mmcblk0p1/steamapps/common/Slay the Spire 2"
)

GAME_DIR=""
for c in "${CANDIDATES[@]}"; do
    if [ -d "$c" ]; then
        GAME_DIR="$c"
        break
    fi
done

if [ -z "$GAME_DIR" ]; then
    VDF_PATHS=(
        "$HOME/.local/share/Steam/steamapps/libraryfolders.vdf"
        "$HOME/.steam/steam/steamapps/libraryfolders.vdf"
        "$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/libraryfolders.vdf"
    )
    for vdf in "${VDF_PATHS[@]}"; do
        if [ -f "$vdf" ]; then
            while IFS= read -r line; do
                if [[ $line =~ \"path\"[[:space:]]+\"([^\"]+)\" ]]; then
                    cand="${BASH_REMATCH[1]}/steamapps/common/Slay the Spire 2"
                    if [ -d "$cand" ]; then
                        GAME_DIR="$cand"
                        break 2
                    fi
                fi
            done < "$vdf"
        fi
    done
fi

if [ -n "$GAME_DIR" ]; then
    echo "Found game directory: $GAME_DIR"
    DEST_DIR="$GAME_DIR/mods/STS2-LongDistancePlay"
    mkdir -p "$DEST_DIR"
    cp -R "$MOD_SRC/"* "$DEST_DIR/"

    echo ""
    echo "============================================"
    echo "  Installation successful!"
    echo "============================================"
    echo "Installed to: $DEST_DIR"
else
    echo "============================================"
    echo "  Auto-detection failed"
    echo "============================================"
    echo ""
    echo "Could not locate Slay the Spire 2 automatically."
    echo "Please copy the 'STS2-LongDistancePlay' folder manually to:"
    echo "  <Slay the Spire 2>/mods/STS2-LongDistancePlay/"
fi

echo ""
read -n 1 -s -r -p "Press any key to close..." || true
echo ""
'@

# --- Package 1: Windows ---
$winZipName = "STS2-LongDistancePlay-v$version-Windows.zip"
$winZipPath = Join-Path $buildRoot $winZipName
$stageWin = Join-Path $buildRoot "_zip_stage_win"
if (Test-Path $stageWin) { Remove-Item $stageWin -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stageWin $modFolderName) | Out-Null
Copy-Item (Join-Path $releaseDir "*") -Destination (Join-Path $stageWin $modFolderName) -Recurse -Force
Set-Content (Join-Path $stageWin "Install.bat") -Value $windowsBat -Encoding ASCII
Set-Content (Join-Path $stageWin "helper.ps1") -Value $windowsPs1 -Encoding UTF8
if (Test-Path $winZipPath) { Remove-Item $winZipPath -Force }
Compress-Archive -Path (Join-Path $stageWin "*") -DestinationPath $winZipPath -CompressionLevel Optimal
Remove-Item $stageWin -Recurse -Force
Write-Host "Built Windows package: $winZipPath" -ForegroundColor Green

# --- Package 2: macOS ---
$macZipName = "STS2-LongDistancePlay-v$version-macOS.zip"
$macZipPath = Join-Path $buildRoot $macZipName
$stageMac = Join-Path $buildRoot "_zip_stage_mac"
if (Test-Path $stageMac) { Remove-Item $stageMac -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stageMac $modFolderName) | Out-Null
Copy-Item (Join-Path $releaseDir "*") -Destination (Join-Path $stageMac $modFolderName) -Recurse -Force
Write-UnixScript (Join-Path $stageMac "install.sh") $macInstallSh
Write-UnixScript (Join-Path $stageMac "Install.command") $macInstallCmd
if (Test-Path $macZipPath) { Remove-Item $macZipPath -Force }
Compress-Archive -Path (Join-Path $stageMac "*") -DestinationPath $macZipPath -CompressionLevel Optimal
Remove-Item $stageMac -Recurse -Force
Write-Host "Built macOS package:   $macZipPath" -ForegroundColor Green

# --- Package 3: Linux ---
$linuxZipName = "STS2-LongDistancePlay-v$version-Linux.zip"
$linuxZipPath = Join-Path $buildRoot $linuxZipName
$stageLinux = Join-Path $buildRoot "_zip_stage_linux"
if (Test-Path $stageLinux) { Remove-Item $stageLinux -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stageLinux $modFolderName) | Out-Null
Copy-Item (Join-Path $releaseDir "*") -Destination (Join-Path $stageLinux $modFolderName) -Recurse -Force
Write-UnixScript (Join-Path $stageLinux "install.sh") $linuxInstallSh
if (Test-Path $linuxZipPath) { Remove-Item $linuxZipPath -Force }
Compress-Archive -Path (Join-Path $stageLinux "*") -DestinationPath $linuxZipPath -CompressionLevel Optimal
Remove-Item $stageLinux -Recurse -Force
Write-Host "Built Linux package:   $linuxZipPath" -ForegroundColor Green

# --- Package 4: Universal (raw folder) ---
$univZipName = "STS2-LongDistancePlay-v$version.zip"
$univZipPath = Join-Path $buildRoot $univZipName
$stageUniv = Join-Path $buildRoot "_zip_stage_univ"
if (Test-Path $stageUniv) { Remove-Item $stageUniv -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stageUniv $modFolderName) | Out-Null
Copy-Item (Join-Path $releaseDir "*") -Destination (Join-Path $stageUniv $modFolderName) -Recurse -Force
if (Test-Path $univZipPath) { Remove-Item $univZipPath -Force }
Compress-Archive -Path (Join-Path $stageUniv "*") -DestinationPath $univZipPath -CompressionLevel Optimal
Remove-Item $stageUniv -Recurse -Force
Write-Host "Built Universal package: $univZipPath" -ForegroundColor Green
