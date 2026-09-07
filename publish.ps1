<#
.SYNOPSIS
  Builds the self-contained MediaControl server for each target RID and bundles the
  built frontend next to it. Produces release artifacts under ./dist/<rid>/.

.DESCRIPTION
  - Builds the frontend (pnpm build) unless -SkipFrontend is given.
  - Publishes the Server self-contained for each -Rids entry (win-x64, linux-x64).
  - Copies Frontend/dist into each publish dir so the published server can serve the UI.

  Run from a PowerShell prompt at the repo root, cross-platform (Windows pwsh / Linux pwsh).
  On NixOS prefer the Nix derivation instead of this script.
#>
param(
    [string[]]$Rids   = @('win-x64', 'linux-x64'),
    [string]$Runtime  = 'Release',
    [switch]$SkipFrontend
)

$ErrorActionPreference = 'Stop'

$root        = $PSScriptRoot
$serverProj  = Join-Path $root 'Server/Server.csproj'
$frontendDir = Join-Path $root 'Frontend'
$distRoot    = Join-Path $root 'dist'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet is required but was not found on PATH.'
}

# --- 1. Build the frontend ---------------------------------------------------
if (-not $SkipFrontend) {
    if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
        throw 'pnpm is required to build the frontend. Install it, or pass -SkipFrontend.'
    }
    Write-Host "==> Building frontend ($frontendDir)"
    Push-Location $frontendDir
    try {
        & pnpm install
        if ($LASTEXITCODE -ne 0) { throw 'pnpm install failed.' }
        & pnpm run build
        if ($LASTEXITCODE -ne 0) { throw 'pnpm build failed.' }
    }
    finally { Pop-Location }
}
else {
    Write-Host '==> Skipping frontend build (-SkipFrontend)'
}

if (-not (Test-Path (Join-Path $frontendDir 'dist'))) {
    throw "Frontend build not found at $frontendDir/dist. Run the script without -SkipFrontend."
}

# --- 2. Publish self-contained server for each RID ---------------------------
foreach ($rid in $Rids) {
    $outDir = Join-Path $distRoot $rid
    Write-Host "==> Publishing $rid -> $outDir"

    # Start from a clean output dir so stale files (e.g. old frontend bundles / old server
    # dlls) don't linger and end up in the bundle.
    if (Test-Path $outDir) {
        Remove-Item -Recurse -Force $outDir
    }

    & dotnet publish $serverProj `
        -c $Runtime `
        -r $rid `
        --self-contained true `
        -p:BuildFrontend=false `
        -o $outDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $rid. (If the runtime pack isn't cached, run once with network access.)"
    }

    # --- 3. Bundle the frontend where the server will look for it -------------
    $frontDest = Join-Path $outDir 'Frontend'
    $frontDestDist = Join-Path $frontDest 'dist'

    # Replace (not merge) so stale assets from a previous build don't persist.
    if (Test-Path $frontDestDist) {
        Remove-Item -Recurse -Force $frontDestDist
    }
    New-Item -ItemType Directory -Force -Path $frontDest | Out-Null
    Copy-Item -Recurse -Force (Join-Path $frontendDir 'dist') $frontDestDist
    Write-Host "    Frontend bundled at $frontDestDist"
}

Write-Host "==> Done. Artifacts in $distRoot"
