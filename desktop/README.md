# MediaControl — Tauri 2 desktop wrapper

This is the Tauri 2 port of the Electron `ServerWrapper`. It starts the MediaControl server,
streams the server's stdout/stderr into a console view, and lives in the system tray
(hide on close / via the titlebar button, show on tray double-click, quit from the tray).

> **Status: scaffold.** It is a complete, self-contained project but has not yet been
> compiled/validated here. Build it on a machine with Rust, the Tauri CLI, and the platform
> WebView deps (see below). The original Electron wrapper is kept in `../ServerWrapper`.

## Layout

```
desktop/
  src/                frontend (React + Vite): console view, listens to server events
  src-tauri/          Rust app: spawns server, tray, minimize-to-tray, kill command
    capabilities/     Tauri permission capabilities
    resources/server/ place the published server binary here before packaging
    icons/            app icons
```

## Dev run

```bash
cd desktop
npm install
# point the wrapper at the Server source dir (defaults to ../../Server):
export MEDIA_SERVER_DIR="$PWD/../Server"     # dev uses `dotnet run -c Debug`
npm install -g @tauri-apps/cli
npm run tauri dev
```

## Produce a release

1. Publish the server self-contained and bundle the frontend (from the repo root):

   ```pwsh
   ./publish.ps1
   ```

   This creates `dist/win-x64/` and `dist/linux-x64/`, each containing the published server
   plus `Frontend/dist` so the server can serve the UI.

2. Copy the correct published server into the Tauri resources dir, named so the Rust code
   finds it (`MediaControlServer` on Linux / `MediaControlServer.exe` on Windows). The
   published binary is named `Server`/`Server.exe`, so rename it:

   ```pwsh
   # Windows build
   Copy-Item dist/win-x64/Server.exe desktop/src-tauri/resources/server/MediaControlServer.exe
   Copy-Item dist/win-x64/Server.dll desktop/src-tauri/resources/server/   # only if published without single-file
   ```

   ```sh
   # Linux build
   cp dist/linux-x64/Server desktop/src-tauri/resources/server/MediaControlServer
   ```

3. Build:

   ```bash
   cd desktop && npm run tauri build
   ```

## Platform deps

- **Windows:** WebView2 (installed with Windows/V8). `tauri build` produces an NSIS/MSI.
- **Linux / NixOS:** needs `webkitgtk-4.1`, `gtk3`, `librsvg`, and (for the tray)
  `libayatana-appindicator`. On NixOS provide these in a devShell or the package derivation.
- **Global input on Linux:** the server uses `ydotool` + `uinput`. Enable the NixOS module at
  `../nixos/modules/media-control.nix` (loads `uinput`, adds users to the `input` group, runs
  `ydotoold`). Without it, media keys/volume won't inject on Wayland.

## Events & commands

| Frontend | Backend |
|---|---|
| `listen('server:stdout', cb)` | emitted per stdout line |
| `listen('server:stderr', cb)` | emitted per stderr line |
| `listen('server:exit', cb)` | emitted when stdout closes |
| `invoke('kill_server')` | kills the server process |
| `invoke('hide_window')` | hides the window to tray |
