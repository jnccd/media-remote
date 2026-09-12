{
  pkgs ? import <nixpkgs> { },
  lib ? pkgs.lib,
  # Which project to resolve. Defaults to the server; pass the desktop app with
  #   nix-build -E 'import ./nix/fetch-nuget-deps.nix {
  #     src = ./DesktopApp; projectFile = "DesktopApp.csproj";
  #     name = "media-control-desktop-nuget-deps"; }'
  src ? ../Server,
  projectFile ? "Server.csproj",
  name ? "media-control-nuget-deps",
}:

# Generates nix/deps.json for nix/packages.nix (buildDotnetModule's `nugetDeps`).
#
#   nix-build nix/fetch-nuget-deps.nix
#   cp result nuget-deps.json     # or point nugetDeps at ./nuget-deps.json
#
# Explicitly uses the user's populated ~/.nuget/packages as the package source
# and nuget-to-json (nuget-to-nix was removed in nixpkgs 26.05).
pkgs.stdenv.mkDerivation {
  inherit name;

  nativeBuildInputs = with pkgs; [
    dotnetCorePackages.sdk_8_0
    nuget-to-json
    cacert
  ];

  inherit src;

  buildCommand = ''
    export HOME=$TMPDIR
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export DOTNET_NOLOGO=1
    export SSL_CERT_FILE=${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt
    export NIX_SSL_CERT_FILE=${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt

    # Restore writes obj/, so work on a writable copy (the store is read-only).
    workdir=$TMPDIR/proj
    mkdir -p "$workdir"
    cp -R "$src/." "$workdir/"
    chmod -R u+w "$workdir"
    cd "$workdir"

    # Point restore at the already-populated cache so this needs no network.
    export NUGET_PACKAGES="$TMPDIR/nuget-packages"
    mkdir -p "$NUGET_PACKAGES"
    if [ -d /home/sandbox/.nuget/packages ]; then
      cp -R /home/sandbox/.nuget/packages/. "$NUGET_PACKAGES/"
      chmod -R u+w "$NUGET_PACKAGES"
    fi

    # Belt and braces: also allow nuget.org (a Nix build sandbox blocks
    # network, so this only matters if that cache is incomplete).
    export keepNugetConfig=1
    cat > nuget.config <<'NUGET'
    <?xml version="1.0" encoding="utf-8"?>
    <configuration>
      <packageSources>
        <clear />
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
      </packageSources>
    </configuration>
    NUGET

    dotnet restore ${projectFile} \
      -p:BuildFrontend=false \
      -p:RestoreSources="https://api.nuget.org/v3/index.json" \
      --ignore-failed-sources

    nuget-to-json "$NUGET_PACKAGES" > "$out"
  '';
}
