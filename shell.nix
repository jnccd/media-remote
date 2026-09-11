# Dev shells for media-remote.
#
#   nix develop          -> default: server + frontend tooling
#   nix develop .#tauri  -> default + Rust and the GTK/WebKit stack
#
# The flake (flake.nix) builds both shells; this file stays the place where the
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
in
{
  inherit commonPackages;

  # Default shell, as before.
  default = pkgs.mkShell {
    packages = commonPackages;
  };
}
