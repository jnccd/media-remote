# nix develop . --experimental-features 'nix-command flakes'
# nix develop --experimental-features 'nix-command flakes' --command bash -c "bash update_and_start.sh"
# export NIXPKGS_ALLOW_INSECURE=1 && nix develop --impure --experimental-features 'nix-command flakes' --command bash -c "bash update_and_start.sh"
{
  description = "Nix Shell Wrapper";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-26.05";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs { inherit system; };
        packages = import ./nix/packages.nix { inherit pkgs; lib = nixpkgs.lib; };

        # Tauri v2 Linux prerequisites (see https://wiki.nixos.org/wiki/Tauri):
        # webkitgtk 4.1 + libsoup 3 for the webview, libayatana-appindicator for
        # the tray icon (Cargo.toml enables tauri's "tray-icon"), librsvg for
        # icons, xdotool/libxdo for input, plus GTK/X11/Wayland build inputs.
        # Note: in nixpkgs 25.05 there is no `libxdo` attribute - `xdotool`
        # supplies libxdo.so and its headers.
        tauriDeps = with pkgs; [
          cargo
          rustc
          rustfmt
          clippy

          pkg-config
          gcc
          gnumake
          file
          curl
          wget
          openssl

          glib
          gtk3
          webkitgtk_4_1
          libsoup_3
          librsvg
          libayatana-appindicator
          xdotool

          cairo
          pango
          gdk-pixbuf
          atk
          harfbuzz
          libGL
          libGLU

          # X11
          xorg.libX11
          xorg.libXcursor
          xorg.libXrandr
          xorg.libXi
          xorg.libXext
          xorg.libXrender
          xorg.libXtst
          xorg.libxcb

          # Wayland (libxkbcommon is top-level, not under xorg)
          wayland
          wayland-protocols
          libxkbcommon
        ];
      in
      {
        # `nix build` / `nix build .#` -> the desktop wrapper (Avalonia now; the
        # Tauri one is still buildable as `.#desktop` but nothing builds it).
        packages = {
          default = packages.desktop-app;
          inherit (packages) frontend server server-with-ui desktop desktop-app;
        };

        # Unchanged default: the server + frontend tooling from shell.nix.
        devShells.default = (import ./shell.nix { inherit pkgs; }).default;

        # The Avalonia wrapper's shell: the default plus the native libraries
        # SkiaSharp dlopen()s. `nix develop .#desktop` is what the GUI autostart
        # enters so the client can build the app from source, the same way the
        # notes and music-player apps do.
        devShells.desktop = (import ./shell.nix { inherit pkgs; }).desktop;

        # Everything the desktop wrapper needs on top of that. Use this for
        # `npm --prefix desktop run tauri dev/build`.
        devShells.tauri = pkgs.mkShell {
          packages =
            (import ./shell.nix { inherit pkgs; }).commonPackages ++ tauriDeps;

          shellHook = ''
            # The Rust side spawns the server with `dotnet run -c Debug` in
            # ../../Server during debug builds, unless MEDIA_SERVER_DIR points
            # somewhere else (see server_command() in src-tauri/src/lib.rs).
            export MEDIA_SERVER_DIR="''${MEDIA_SERVER_DIR:-$PWD/Server}"

            # libappindicator-sys dlopen()s the tray library at RUNTIME rather
            # than linking it against the binary, so it has to be on
            # LD_LIBRARY_PATH. Without this the app compiles fine and then dies
            # at startup with "Failed to load ayatana-appindicator3 dynamic
            # library". A successful cargo build is not sufficient.
            export LD_LIBRARY_PATH="${pkgs.libayatana-appindicator}/lib:${pkgs.libayatana-indicator}/lib''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

            echo "media-remote (tauri) shell"
            echo "  server:  dotnet run -c Release --project Server"
            echo "  desktop: npm --prefix desktop run tauri dev"
          '';
        };
      });
}
