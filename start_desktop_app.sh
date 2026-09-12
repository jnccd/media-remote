#!/usr/bin/env bash
# Build and run the media-remote desktop wrapper from source.
#
# This is the client-side build path. The GUI autostart clones this repo, enters
# the flake's `desktop` dev shell (see ../flake.nix, which is what puts the
# SkiaSharp native libraries on LD_LIBRARY_PATH) and runs this script, so nothing
# goes through the Nix store: a new commit costs a `dotnet build` - seconds -
# instead of a Nix package build.
#
# Contract with the autostart launcher (lib/service.nix in nixos-config), which
# also handles the single-instance locking and the app's stdout log:
#   - run with NIXOS_JNCCD_GUI_STARTER_UNCHANGED=1 when this revision is already
#     built: then only start the app, never rebuild;
#   - otherwise build first and then run (for a .NET app the "build" step IS
#     running the app);
#   - the artifact the launcher checks to decide "already built" is
#     DesktopApp/bin/Release/net8.0/media-control-desktop.dll, so keep that path.
set -euo pipefail

# The server csproj runs `pnpm i` for us on Release (its BuildFrontend target).
# pnpm refuses to purge a stale node_modules when it has no TTY
# (ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY), which is exactly the case in a
# non-interactive autostart build, so behave like CI.
export CI=true

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

APP_DLL="DesktopApp/bin/Release/net8.0/media-control-desktop.dll"
SERVER_DLL="Server/bin/Release/net8.0/Server.dll"

build() {
  echo "media-remote: building the desktop wrapper from source"

  # The server csproj builds the frontend itself on Release (its BuildFrontend
  # target runs pnpm in ../Frontend), so this covers Server + Frontend/dist + the
  # Avalonia app.
  dotnet build -c Release DesktopApp/DesktopApp.csproj
  dotnet build -c Release Server/Server.csproj
}

if [ -z "${NIXOS_JNCCD_GUI_STARTER_UNCHANGED:-}" ]; then
  build
fi

# The wrapper normally finds the server next to itself. Here the two are built
# into separate directories instead, so point at the server we just built and
# make it run from the repo's Server directory: the server looks for
# Frontend/dist relative to its content root (RegisterStaticFiles.cs), and only
# under the repo does ../Frontend/dist resolve.
#
# Both are given as .dll paths, which ServerProcess turns into `dotnet <dll>`.
export MEDIA_CONTROL_SERVER="$PWD/$SERVER_DLL"
export MEDIA_CONTROL_SERVER_CWD="$PWD/Server"

# Run through the `dotnet` muxer rather than the apphost the SDK generates next
# to the .dll. That apphost is a native launcher the SDK patches at build time,
# and on NixOS it ends up mixing glibc versions - it resolves librt.so.1 from one
# glibc and libc.so.6 from the SDK's own - so it dies before reaching managed
# code with:
#   version `GLIBC_ABI_DT_X86_64_PLT' not found (required by .../librt.so.1)
# The muxer comes from nixpkgs and is patched properly. The notes and
# music-player apps are started this way too.
exec dotnet "$PWD/$APP_DLL"
