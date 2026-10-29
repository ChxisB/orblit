#!/bin/bash
# Packs every library under dotnet/ into a local NuGet folder, so a repo
# outside this one restores the engine packages without a public feed.
#
#   tool/pack_local.sh [feed]    # feed: $ORBLIT_FEED, else ~/.orblit/feed
#
# A repacked package keeps its version number, and NuGet keeps the files it
# first extracted. So the cache entry of each package packed here is removed.
set -euo pipefail
cd "$(dirname "$0")/../dotnet"

feed=${1:-${ORBLIT_FEED:-$HOME/.orblit/feed}}
mkdir -p "$feed"
marker=$(mktemp)
trap 'rm -f "$marker"' EXIT

# Tests are not libraries. The code generator packs as an analyzer.
for project in Orblit.*/Orblit.*.csproj; do
  case $project in
    *.Tests/* | Orblit.Tests.*/*) continue ;;
  esac
  dotnet pack "$project" -c Release -o "$feed" --nologo -v q
done

cache=$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')
find "$feed" -name '*.nupkg' -newer "$marker" | while read -r file; do
  name=$(basename "$file" .nupkg)
  version=$(echo "$name" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+.*$')
  id=$(echo "${name%".$version"}" | tr '[:upper:]' '[:lower:]')
  rm -rf "${cache:?}/${id:?}/${version:?}"
done
echo "  ok    packed to $feed"
