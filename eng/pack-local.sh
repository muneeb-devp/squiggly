#!/usr/bin/env bash
# Packs Squiggly into a local folder feed so manual IDE testing runs against the real package,
# not project references. Each run gets a unique version, so NuGet's cache never serves a stale build.
#
#   eng/pack-local.sh                 pack only
#   eng/pack-local.sh ~/ArcadeTest    pack, then create (or update) a console project there that uses it
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
feed="$root/artifacts/local-feed"
version="0.0.0-local.$(date +%Y%m%d%H%M%S)"

dotnet pack "$root/src/Squiggly.Package" --configuration Release --output "$feed" -p:MinVerVersionOverride="$version"
echo "Packed Squiggly $version into $feed"

if [ $# -ge 1 ]; then
  playground="$1"
  if ! ls "$playground"/*.csproj >/dev/null 2>&1; then
    dotnet new console --output "$playground" --name "$(basename "$playground")"
  fi
  dotnet add "$playground" package Squiggly --version "$version" --source "$feed"
  if [ ! -f "$playground/Game.cs" ]; then
    printf '// squiggly\nnamespace Arcade;\n' > "$playground/Game.cs"
  fi
  echo "Ready: open $playground/Game.cs, put the caret on line 1 and press Ctrl+. (Alt+Enter in Rider)."
fi
