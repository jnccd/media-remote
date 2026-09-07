#!/usr/bin/env bash
# One-shot Linux/NixOS release: publish the server for linux-x64, bundle it into the
# Tauri resources, then build the Tauri app. Requires dotnet, pnpm, rust, and the Tauri CLI.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
rid="${RID:-linux-x64}"
dist="$root/dist/$rid"
res_server="$root/desktop/src-tauri/resources/server"

# Always re-publish so the bundled server binary and frontend reflect the latest source.
echo "==> Publishing server ($rid)..."
(cd "$root" && pwsh -NoProfile -File ./publish.ps1 -Rids @("$rid"))

echo "==> Bundling $dist into $res_server"
rm -rf "$res_server"
mkdir -p "$res_server"
cp -R "$dist/." "$res_server"

# Rename to the name the Rust code expects.
if [ -e "$res_server/Server" ]; then
  mv "$res_server/Server" "$res_server/MediaControlServer"
fi

echo "==> Building Tauri app"
cd "$root/desktop"
[ -d node_modules ] || npm install
npm run tauri build

echo "==> Done. App is in desktop/src-tauri/target/release/."
