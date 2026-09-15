<#
.SYNOPSIS
    Build and run the media-remote desktop wrapper from source on Windows.

.DESCRIPTION
    Windows counterpart to start_desktop_app.sh: the same two `dotnet build -c
    Release` invocations, the same MEDIA_CONTROL_SERVER / MEDIA_CONTROL_SERVER_CWD
    contract with DesktopApp/ServerProcess.cs, and then the app itself in the
    foreground so its stdout (every line the server prints) lands in this console.

    The build steps are identical because the csproj files do the work: the
    DesktopApp is a plain build, and the Server csproj builds the frontend itself
    on Release (its BuildFrontend target runs `pnpm i` + `pnpm run build` in
    ../Frontend), so the two builds cover Server + Frontend/dist + the Avalonia
    app.

    Unlike the shell script, nothing here needs a Nix dev shell: there is no
    LD_LIBRARY_PATH to populate for SkiaSharp, and the SDK's native apphost works
    on Windows, so this runs the built .exe directly instead of going through the
    `dotnet` muxer.

    There is no single-instance locking or app.log here either - on NixOS that
    lives in the autostart launcher (lib/service.nix in nixos-config), which has
    no Windows equivalent. Run this from one console; a second instance starts but
    its server dies on the port being taken.

.PARAMETER SkipBuild
    Start the app without rebuilding. Equivalent to running the shell script with
    NIXOS_JNCCD_GUI_STARTER_UNCHANGED=1; requires a previous build to exist.

.PARAMETER Port
    Port for the server to listen on. Defaults to $env:PORT if set, otherwise the
    server's own default (7779, see Server/Src/Configuration.cs), which is also
    what the window shows.

.PARAMETER AppArgs
    Any remaining arguments are forwarded to the app.

.EXAMPLE
    ./start_desktop_app.ps1
    Build both projects and run the tray app.

.EXAMPLE
    ./start_desktop_app.ps1 -SkipBuild -Port 8080
    Reuse the existing build, serve on http://localhost:8080.

.NOTES
    PowerShell 5.1 compatible, so it also runs under `powershell.exe`. If running
    the .ps1 directly is blocked by the execution policy, use
    start_desktop_app.cmd, which bypasses it.

    To rebuild, the app has to be quit first: Windows locks the files of a running
    process, so `dotnet build` cannot replace them while an instance is up. The
    shell script has no such constraint.
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [int]$Port,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

$ErrorActionPreference = 'Stop'

# Anchor on this script's own location, so it works from any working directory -
# including a double-click through start_desktop_app.cmd, where the cwd is
# whatever Explorer hands over.
$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
Set-Location -LiteralPath $root

# The artifact paths the launcher contract in start_desktop_app.sh pins down are
# kept identical here, so "already built" means the same thing on both platforms.
$appDll    = 'DesktopApp/bin/Release/net8.0/media-control-desktop.dll'
$appExe    = 'DesktopApp/bin/Release/net8.0/media-control-desktop.exe'
$serverDll = 'Server/bin/Release/net8.0/Server.dll'

# The variables this script exports. Saved first and put back on the way out:
# unlike the shell script, which always runs as its own process, a .ps1 runs
# inside the caller's session, so it would otherwise leave CI=true and a
# MEDIA_CONTROL_SERVER pointing at this checkout behind in the user's prompt.
$touchedVariables = @('CI', 'MEDIA_CONTROL_SERVER', 'MEDIA_CONTROL_SERVER_CWD', 'PORT')
$previousValues = @{}
foreach ($name in $touchedVariables) {
    $previousValues[$name] = [Environment]::GetEnvironmentVariable($name)
}

function Restore-Environment {
    foreach ($name in $touchedVariables) {
        if ($null -eq $previousValues[$name]) {
            Remove-Item -Path "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path "Env:$name" -Value $previousValues[$name]
        }
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet is required but was not found on PATH. Install the .NET 8 SDK.'
}

# --- Windows-specific: the instance a rebuild would trip over -----------------
#
# Windows locks the files of a running process, so a build cannot overwrite
# media-control-desktop.dll while the tray app is up, nor Server.dll while the
# server child it spawned is up. MSBuild's error for that (MSB3026/MSB3021,
# "being used by another process") points at the copy step and never mentions the
# app, so detect the instance and say what to do. start_desktop_app.sh needs no
# equivalent: on Linux the running binary is simply unlinked and replaced.

# Whether a build could still overwrite this file. Opening it for write with no
# sharing fails exactly when the build's copy step would. Sturdier than looking
# for the owning process: the server is a plain `dotnet` host, and identifying it
# would need WMI command lines (Get-CimInstance), which are not always available.
function Test-ArtifactInUse {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        return $true
    }
    catch [System.UnauthorizedAccessException] {
        return $true
    }

    $stream.Dispose()
    return $false
}

# Describes whatever a rebuild would trip over, or nothing if the way is clear.
function Get-BuildBlocker {
    $apps = @(Get-Process -Name 'media-control-desktop' -ErrorAction SilentlyContinue)
    if ($apps.Count -gt 0) {
        return @("the app (PID $($apps.Id -join ', '))")
    }

    # No app under its own name, but the build output may still be held: by an app
    # started through the `dotnet` muxer fallback, or by an orphaned server left
    # behind when a console was killed instead of quit - the case the tray's Quit
    # cannot fix.
    $locked = @(
        @($appDll, $serverDll) |
            Where-Object { Test-ArtifactInUse -Path (Join-Path $root $_) }
    )
    if ($locked.Count -gt 0) {
        return @("a stray media-remote process still holding $($locked -join ' and ')")
    }

    return @()
}

function Invoke-DotnetBuild {
    param([Parameter(Mandatory)][string]$Project)

    & dotnet build -c Release $Project
    if ($LASTEXITCODE -ne 0) {
        # A build that fails while an instance is up is almost always the file
        # lock - either the check below raced with a start, or the instance came up
        # during the build - so name the instance instead of leaving the raw MSB
        # copy error to be puzzled over.
        $blockers = @(Get-BuildBlocker)
        if ($blockers.Count -gt 0) {
            throw ("dotnet build failed for $Project (exit code $LASTEXITCODE) while " +
                   "$($blockers -join ' and ') is still running. Windows will not let a build " +
                   'replace files an instance holds: quit the app from its tray icon (or stop ' +
                   'that process) and run this again.')
        }

        throw "dotnet build failed for $Project (exit code $LASTEXITCODE)."
    }
}

# --- Build --------------------------------------------------------------------
if (-not $SkipBuild) {
    $blockers = @(Get-BuildBlocker)
    if ($blockers.Count -gt 0) {
        throw ("media-remote is already running: $($blockers -join ' and '). " +
               'Quit it from the tray icon (or stop that process) first, then run this again. ' +
               '(-SkipBuild skips the build only; it does not let a build replace a running exe.)')
    }

    Write-Host 'media-remote: building the desktop wrapper from source'

    # The server csproj runs `pnpm i` for us on Release (its BuildFrontend target).
    # pnpm refuses to purge a stale node_modules when it has no TTY
    # (ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY), which is exactly the case in a
    # non-interactive build, so behave like CI. Same reason as start_desktop_app.sh.
    # Scoped to the builds (child processes inherit it, which is all that needs
    # it), so a failed build cannot leave CI=true behind in an interactive session.
    $env:CI = 'true'
    try {
        Invoke-DotnetBuild 'DesktopApp/DesktopApp.csproj'
        Invoke-DotnetBuild 'Server/Server.csproj'
    }
    finally {
        Restore-Environment
    }
}

foreach ($artifact in @($appDll, $serverDll)) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $artifact))) {
        throw "$artifact is missing. Run without -SkipBuild to build it first."
    }
}

# --- Run ----------------------------------------------------------------------
# The wrapper normally finds the server next to itself. Here the two are built
# into separate directories instead, so point at the server we just built and
# make it run from the repo's Server directory: the server looks for
# Frontend/dist relative to its content root (RegisterStaticFiles.cs), and only
# under the repo does ../Frontend/dist resolve.
$env:MEDIA_CONTROL_SERVER     = Join-Path $root $serverDll
$env:MEDIA_CONTROL_SERVER_CWD = Join-Path $root 'Server'

# The server reads PORT from the environment (Configuration.cs) and the window
# shows the resulting URL (App.WebUiUrl). The server child inherits it, so both
# agree on the port.
if ($PSBoundParameters.ContainsKey('Port')) {
    $env:PORT = "$Port"
}

$effectivePort = if ($env:PORT) { $env:PORT } else { '7779' }
Write-Host "==> Starting media-control-desktop (web UI: http://localhost:$effectivePort)"

# Run the SDK's native apphost: on Windows it has none of the glibc problems the
# muxer works around on NixOS, and OutputType=Exe (see DesktopApp.csproj) means it
# stays attached to this console, so the server's log is visible here. The muxer
# is the fallback for layouts without an apphost.
try {
    if (Test-Path -LiteralPath (Join-Path $root $appExe)) {
        & (Join-Path $root $appExe) @AppArgs
    }
    else {
        & dotnet (Join-Path $root $appDll) @AppArgs
    }
}
finally {
    Restore-Environment
}

# Propagate the app's exit code (it shuts the server down on the way out).
exit $LASTEXITCODE
