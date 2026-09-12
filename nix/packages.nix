{
  # Packages for media-remote: the web remote (React/Vite), the ASP.NET Core
  # server, and the Tauri desktop wrapper that bundles both.
  #
  # Build strategy, deliberately: every derivation here is fixed-output
  # (`outputHash`). Nix therefore pins the exact result bytes and can cache and
  # substitute them, but the builds need network access (npm/NuGet/crates). The
  # stricter alternative is `buildNpmPackage` + `buildDotnetModule` + vendored
  # crates, which needs a lock-regeneration step whenever a dependency changes.
  # Start here, tighten later if a fully offline evaluation is wanted.
  pkgs,
  lib,
}:

let
  version = "0.1.0";

  # --- Web remote (React + Vite SPA) ---------------------------------------
  # Served by the server from Frontend/dist; also embedded into the desktop app
  # as its webview content.
  frontend = pkgs.stdenv.mkDerivation (finalAttrs: {
    pname = "media-remote-frontend";
    inherit version;
    src = ../Frontend;

    nativeBuildInputs = with pkgs; [
      nodejs_22
      pnpm
    ];

    buildPhase = ''
      runHook preBuild
      # CI=true keeps pnpm non-interactive: without it `pnpm install` blocks on
      # "modules directory will be removed, proceed?" and the build hangs.
      export CI=true
      export HOME=$TMPDIR
      # Inside the build sandbox there is no ambient CA store, and pnpm/node
      # reject the registry with UNABLE_TO_GET_ISSUER_CERT_LOCALLY without it.
      export SSL_CERT_FILE=${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt
      export NIX_SSL_CERT_FILE=${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt
      export NODE_EXTRA_CA_CERTS=${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt
      # pnpm 10 refuses to run dependency build scripts unless they are
      # approved, reporting ERR_PNPM_IGNORED_BUILDS. esbuild needs its
      # postinstall (it lays down the native binary vite uses). In a
      # fixed-output derivation the sources are pinned by the lockfile anyway,
      # so allowing the scripts here is acceptable.
      printf 'dangerouslyAllowAllBuilds: true\n' > pnpm-workspace.yaml
      pnpm install --frozen-lockfile
      pnpm run build
      runHook postBuild
    '';

    installPhase = ''
      runHook preInstall
      mkdir -p $out
      cp -R dist/. $out/
      runHook postInstall
    '';

    outputHashMode = "recursive";
    outputHashAlgo = "sha256";
    outputHash = "sha256-LdutMIJczzutCFhElfcS11fhD9sMLbMX2ut22r2c75Y=";

    meta = {
      description = "MediaControl web remote (React/Vite SPA)";
      license = lib.licenses.mit;
      platforms = lib.platforms.linux;
    };
  });

  # --- Server (self-contained ASP.NET Core) --------------------------------
  # Self-contained on purpose: the app targets net8.0 and a host may only ship a
  # different major runtime, in which case a framework-dependent build refuses
  # to start ("You must install .NET 8.0"). Bundling removes that coupling.
  # NOT a fixed-output derivation: a FOD may not depend on any store path, and
  # this needs the .NET SDK. The Nix way is buildDotnetModule with `nugetDeps`
  # pointing at a generated nuget-deps.json, which makes restore fully offline.
  # Leaving nugetDeps = null makes the first build fetch and hash the closure
  # and then fail with the hash to paste in (see the comment on the attribute).
  server = pkgs.buildDotnetModule {
    pname = "media-control-server";
    inherit version;
    src = ../Server;

    projectFile = "Server.csproj";
    # Must match <TargetFramework>net8.0</TargetFramework> in Server.csproj.
    dotnet-sdk = pkgs.dotnetCorePackages.sdk_8_0;
    dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_8_0;

    selfContainedBuild = true;
    runtimeId = "linux-x64";

    # The csproj has a BuildFrontend target that shells out to pnpm; the UI is
    # built separately, so disable it.
    dotnetBuildFlags = [ "-p:BuildFrontend=false" ];
    dotnetPublishFlags = [ "-p:BuildFrontend=false" ];

    # Regenerate after changing package references:
    #   nix-build -A server.fetch-deps && ./result nuget-deps.nix
    # then set: nugetDeps = ./nuget-deps.nix;
    nugetDeps = ../nuget-deps.json;

    # Self-contained output is under $out/lib/media-control-server, with the
    # binary named Server; expose the friendly name too.
    postInstall = ''
      mkdir -p $out/bin
      ln -s $out/lib/media-control-server/Server $out/bin/media-control-server
    '';

    meta = {
      description = "MediaControl server (input injection + media control over HTTP)";
      license = lib.licenses.mit;
      platforms = lib.platforms.linux;
      mainProgram = "media-control-server";
    };
  };

  # Server + web UI in the layout the server actually expects at runtime
  # (Frontend/dist next to the binary). This is a normal derivation, so it can
  # reference both store paths.
  server-with-ui = pkgs.runCommand "media-control-server-with-ui" { } ''
    mkdir -p $out/lib/media-control-server
    cp -R ${server}/lib/media-control-server/. $out/lib/media-control-server/
    mkdir -p $out/lib/media-control-server/Frontend/dist
    cp -R ${frontend}/. $out/lib/media-control-server/Frontend/dist/
    mkdir -p $out/bin
    ln -s $out/lib/media-control-server/Server $out/bin/media-control-server
  '';

  # --- Cargo dependency cache ----------------------------------------------
  # Fixed-output so it may use the network: it downloads the crate tree pinned
  # by Cargo.lock into a CARGO_HOME that the (offline) desktop build reuses.
  # Regenerate `cargoHash` whenever Cargo.lock changes: build, then copy the
  # "got:" hash from the mismatch error.
  cargo-deps = pkgs.stdenv.mkDerivation {
    pname = "media-control-desktop-cargo-deps";
    inherit version;

    # Only the crate manifest/lock; the full source is not needed to fetch.
    src = ../desktop/src-tauri;
    nativeBuildInputs = with pkgs; [ cargo rustc ];
    dontConfigure = true;
    dontFixup = true;

    buildPhase = ''
      runHook preBuild
      # Fetch into a temp CARGO_HOME; $out is only materialised in installPhase.
      export CARGO_HOME="$PWD/.cargo-home"
      mkdir -p "$CARGO_HOME"
      # No ambient CA store in the sandbox; cargo/openssl needs this.
      export SSL_CERT_FILE=${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt
      export NIX_SSL_CERT_FILE=${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt
      # Fetch exactly what Cargo.lock pins.
      cargo fetch --locked
      runHook postBuild
    '';

    installPhase = ''
      runHook preInstall
      mkdir -p $out
      cp -R "$PWD/.cargo-home" $out/cargo-home
      runHook postInstall
    '';

    outputHashMode = "recursive";
    outputHashAlgo = "sha256";
    outputHash = "sha256-+wO4q2aMUoKRV8MVRO6DimdTKBz4bxpTtlSSbgWxZ4s=";

    meta.description = "Prefetched Rust crate tree for the MediaControl desktop wrapper";
  };

  # --- Desktop wrapper (Tauri v2) ------------------------------------------
  # Tauri embeds the webview assets and a resource manifest into the binary, and
  # its build script VALIDATES `bundle.resources` while building - so the path
  # has to resolve to something real here, not only when bundling installers.
  # Rather than rewriting tauri.conf.json (and depending on how Tauri treats
  # absolute paths in its glob), the Nix-built frontend and server are staged
  # into the exact layout tauri.conf.json already declares:
  #   ../dist                      (build.frontendDist)
  #   src-tauri/resources/server   (bundle.resources)
  desktop = pkgs.stdenv.mkDerivation (finalAttrs: {
    pname = "media-control-desktop";
    inherit version;
    src = ../desktop;

    nativeBuildInputs = with pkgs; [
      cargo
      rustc
      pkg-config
      # Sets the runtime library search paths for the GTK/WebKit stack, which
      # matters because Tauri dlopen()s libayatana-appindicator at startup
      # rather than linking it - without this the app builds and then panics
      # with "Failed to load ayatana-appindicator3 dynamic library".
      wrapGAppsHook3
    ];

    buildInputs = with pkgs; [
      glib
      gtk3
      webkitgtk_4_1
      libsoup_3
      librsvg
      libayatana-appindicator
      libayatana-indicator
      openssl
      cairo
      pango
      gdk-pixbuf
      harfbuzz
      libGL
      xorg.libX11
      xorg.libXcursor
      xorg.libXrandr
      xorg.libXi
      xorg.libXext
      xorg.libXtst
      xorg.libxcb
      wayland
      libxkbcommon
    ];

    buildPhase = ''
      runHook preBuild

      # Stage the Nix-built UI where build.frontendDist points.
      mkdir -p dist
      cp -R ${frontend}/. dist/

      # Stage the Nix-built server where bundle.resources points, under the
      # name the Rust code probes for (MediaControlServer, not Server).
      mkdir -p src-tauri/resources/server
      cp -R ${server-with-ui}/lib/media-control-server/. src-tauri/resources/server/
      [ -e src-tauri/resources/server/Server ] &&
        mv src-tauri/resources/server/Server src-tauri/resources/server/MediaControlServer

      # Crates must already be in CARGO_HOME: the build sandbox has no network.
      export CARGO_HOME=${cargo-deps}/cargo-home
      export CARGO_TARGET_DIR=$TMPDIR/target

      cd src-tauri
      cargo build --release

      runHook postBuild
    '';

    installPhase = ''
      runHook preInstall
      mkdir -p $out/bin
      install -m755 "$TMPDIR/target/release/media-control-desktop" \
        "$out/bin/media-control-desktop"

      # Put the server where src-tauri/src/lib.rs looks for it: next to the
      # executable. Symlink to the server package so its own sibling libs and
      # Frontend/dist stay resolvable in the store.
      ln -s ${server-with-ui}/lib/media-control-server/Server \
        "$out/bin/MediaControlServer"
      runHook postInstall
    '';


    # NOTE: no outputHash here on purpose. A fixed-output derivation may not
    # reference any store path, and this one references the frontend, the server
    # and the GTK/WebKit stack. Determinism for the Rust part comes from
    # cargo-deps (pinned by Cargo.lock + its own hash) instead.
    # libappindicator-sys dlopen()s the tray library at RUNTIME, so it must be
    # on LD_LIBRARY_PATH. wrapGAppsHook3 only handles the GTK/WebKit stack, so
    # wrap the program again with the appindicator paths added.
    postFixup = ''
      wrapProgram "$out/bin/media-control-desktop" \
        --prefix LD_LIBRARY_PATH : "${lib.makeLibraryPath [
          pkgs.libayatana-appindicator
          pkgs.libayatana-indicator
        ]}"
    '';

    meta = {
      description = "MediaControl desktop wrapper (bundled server + tray + console)";
      license = lib.licenses.mit;
      platforms = lib.platforms.linux;
      mainProgram = "media-control-desktop";
    };
  });

  # --- Desktop wrapper (Avalonia UI) ---------------------------------------
  # The current wrapper and the flake default. Same job as `desktop` above -
  # spawn the bundled server, live in the tray, show its log - but as an
  # ordinary .NET app: no webview, no Rust toolchain, and a build measured in
  # seconds rather than minutes. That matters here because the autostart
  # rebuilds from source on every new commit.
  desktop-app = pkgs.buildDotnetModule {
    pname = "media-control-desktop";
    inherit version;

    src = lib.cleanSourceWith {
      src = ../DesktopApp;
      # A local build tree must never leak into the derivation: DesktopApp/bin is
      # ~127M and would otherwise make the output depend on whatever happened to
      # be built last in the working copy.
      filter = path: _: !(builtins.elem (baseNameOf path) [ "bin" "obj" ]);
    };

    projectFile = "DesktopApp.csproj";
    # Must match <TargetFramework>net8.0</TargetFramework> in DesktopApp.csproj.
    dotnet-sdk = pkgs.dotnetCorePackages.sdk_8_0;
    dotnet-runtime = pkgs.dotnetCorePackages.runtime_8_0;

    selfContainedBuild = true;
    runtimeId = "linux-x64";

    # Regenerate after changing package references (the fetch script dumps
    # whatever is in the NuGet cache, so publish first to pull the runtime
    # packs that a self-contained build needs):
    #   nix-build -E 'import ./nix/fetch-nuget-deps.nix {
    #     src = ./DesktopApp; projectFile = "DesktopApp.csproj";
    #     name = "media-control-desktop-nuget-deps"; }'
    nugetDeps = ../nuget-deps-desktop.json;

    # Avalonia renders through Skia, which dlopen()s libSkiaSharp.so at runtime;
    # that in turn needs fontconfig/freetype and the X11/GL stack. Without these
    # on LD_LIBRARY_PATH the app starts and then dies with "Unable to load shared
    # library 'libSkiaSharp' ... libfontconfig.so.1: cannot open shared object".
    runtimeDeps = with pkgs; [
      fontconfig
      freetype
      libGL
      harfbuzz
      icu
      zlib
      libx11
      libice
      libsm
      libxcb
      libxrandr
      libxi
      libxcursor
      libxext
      libxrender
      libxkbcommon
    ];

    # buildDotnetModule installs the published app under $out/lib/<pname>. This
    # puts the executable in $out/bin and, more importantly, wraps it with the
    # runtimeDeps LD_LIBRARY_PATH - which is what makes libSkiaSharp's
    # fontconfig/freetype/X11 dependencies resolvable.
    executables = [ "media-control-desktop" ];

    postInstall = ''
      mkdir -p $out/bin
      # The app probes for MediaControlServer next to its own executable (see
      # ServerProcess.LocateServer). $out/bin/media-control-desktop is a
      # makeWrapper script that execs the real binary in $out/lib/<pname>, so
      # Environment.ProcessPath - and therefore the directory that gets probed -
      # is the lib one, not bin. Link the server there, and in bin as well so it
      # is also on PATH. A symlink into the server package keeps that server's
      # own Frontend/dist and native libraries resolvable.
      ln -s ${server-with-ui}/lib/media-control-server/Server $out/lib/media-control-desktop/MediaControlServer
      ln -s ${server-with-ui}/lib/media-control-server/Server $out/bin/MediaControlServer
    '';

    meta = {
      description = "MediaControl desktop wrapper (Avalonia tray app + bundled server)";
      license = lib.licenses.mit;
      platforms = lib.platforms.linux;
      mainProgram = "media-control-desktop";
    };
  };
in
{
  inherit frontend server server-with-ui desktop desktop-app;

  # The Avalonia wrapper is the thing users install. The Tauri one stays
  # buildable as `nix build .#desktop` until the new one has proven itself, but
  # nothing builds it any more - which is the point, since its Rust build was
  # the slow part.
  default = desktop-app;
}
