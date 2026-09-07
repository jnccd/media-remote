<#
.SYNOPSIS
  One-shot desktop release: publish the server for a RID, bundle it into the Tauri
  resources, then build the Tauri app.

.DESCRIPTION
  - Publishes the server (../publish.ps1) for the target RID and bundles the frontend.
  - Copies the published server into src-tauri/resources/server, renaming the host
    binary to MediaControlServer(.exe) (the name the Rust code looks for).
  - Runs `npm run tauri build`.

  Use: ./build.ps1 (defaults to win-x64), or ./build.ps1 -Rid linux-x64.
#>
param([string]$Rid = 'win-x64')

$ErrorActionPreference = 'Stop'

$root     = Split-Path -Parent $PSScriptRoot
$dist     = Join-Path $root "dist/$Rid"
$resServer = Join-Path $PSScriptRoot 'src-tauri/resources/server'

# Always re-publish so the bundled server binary and the frontend reflect the latest source.
Write-Host "==> Publishing server ($Rid)..."
& (Join-Path $root 'publish.ps1') -Rids @($Rid)
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }

Write-Host "==> Bundling $dist into $resServer"
Remove-Item -Recurse -Force $resServer -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $resServer | Out-Null
Copy-Item -Recurse -Force (Join-Path $dist '*') $resServer

# Rename the published host to the name the Rust code expects.
$win = Join-Path $resServer 'Server.exe'
$lin = Join-Path $resServer 'Server'
if (Test-Path $win) {
    Rename-Item -LiteralPath $win -NewName 'MediaControlServer.exe'
} elseif (Test-Path $lin) {
    Rename-Item -LiteralPath $lin -NewName 'MediaControlServer'
}

Write-Host '==> Building Tauri app'
Push-Location $PSScriptRoot
try {
    if (-not (Test-Path 'node_modules')) { & npm install }
    & npm run tauri build
    if ($LASTEXITCODE -ne 0) { throw 'tauri build failed' }
}
finally { Pop-Location }

Write-Host '==> Done. App is in desktop/src-tauri/target/release/.'
