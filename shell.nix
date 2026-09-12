# Dev shells for media-remote.
#
#   nix develop            -> default: server + frontend tooling
#   nix develop .#desktop  -> default + the Avalonia wrapper's native libraries
#   nix develop .#tauri    -> default + Rust and the GTK/WebKit stack (legacy wrapper)
#
# The flake (flake.nix) builds the shells; this file stays the place where the
# package list lives so nothing is duplicated.
{ pkgs ? import <nixpkgs> { } }:
let
  # What the server and the two frontends need. .NET 8 must match
  # <TargetFramework>net8.0</TargetFramework> in Server.csproj: a host with only
  # a different major runtime (e.g. .NET 10) cannot run a framework-dependent
  # net8.0 build.
  commonPackages = with pkgs; [
    icu

    dotnet-sdk_8
    dotnet-ef

    pnpm
    nodejs_22
  ];

  # Avalonia renders through Skia. SkiaSharp dlopen()s libSkiaSharp.so at
  # runtime and that in turn wants fontconfig/freetype plus the X11/GL stack, so
  # these have to be both present and on LD_LIBRARY_PATH. Without them `dotnet
  # run` in DesktopApp dies with:
  #   Unable to load shared library 'libSkiaSharp' or one of its dependencies
  #   libfontconfig.so.1: cannot open shared object file
  avaloniaNativeLibs = with pkgs; [
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
in
{
  inherit commonPackages avaloniaNativeLibs;

  # Default shell, as before.
  default = pkgs.mkShell {
    packages = commonPackages;
  };

  # What the client-side build of the Avalonia wrapper runs in: the GUI
  # autostart enters this shell and runs ../start_desktop_app.sh inside it.
  desktop = pkgs.mkShell {
    packages = commonPackages ++ avaloniaNativeLibs;

    shellHook = ''
      export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath avaloniaNativeLibs}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

      # `dotnet build` produces framework-dependent apphosts (no RID, nothing
      # self-contained), and start_desktop_app.sh starts the apphost directly -
      # as does the wrapper for the server's apphost. Such an apphost has to be
      # able to locate the shared runtime, and without this it fails with
      # "Failed to resolve libhostfxr.so [not found]". nixpkgs' dotnet wrapper
      # sets this for the `dotnet` command itself, but that does not help a
      # directly executed apphost.
      export DOTNET_ROOT="${pkgs.dotnetCorePackages.sdk_8_0}/share/dotnet"
    '';
  };
}
