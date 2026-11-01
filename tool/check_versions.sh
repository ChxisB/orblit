#!/bin/bash
# Requires a version bump and a changelog line for every C# project whose
# source changed on this branch.
#
# The rule is in VERSIONING.md; this is what stops it depending on somebody
# remembering it at five o'clock. A project counts when its csproj has a
# <Version>. A changed .cs, .axaml or .csproj file under it asks for a bump,
# and so does a changed native source, since the renderer is four thousand
# lines of C++ and a change to it is exactly the kind a consumer notices.
# Test projects have no <Version>, so a branch that fixes a test, a comment,
# a workflow or a page of documentation is not asked to invent a release.
#
# A branch whose changes are genuinely not a feature, such as a reformat or a
# rename with no behaviour in it, says so with a `Version-exempt: <reason>`
# line in one of its commit messages, and this honours it and prints it.
#
# Usage: check_versions.sh [base ref]   (default: origin/main, else main)
set -uo pipefail
cd "$(dirname "$0")/.."

BASE="${1:-}"
if [ -z "$BASE" ]; then
  if git rev-parse --verify --quiet origin/main > /dev/null; then
    BASE=origin/main
  else
    BASE=main
  fi
fi

if ! git rev-parse --verify --quiet "$BASE" > /dev/null; then
  echo "no such ref: $BASE"
  exit 1
fi

# The merge base, not the tip: comparing against a main that has moved on
# would demand a bump for somebody else's changes.
MERGE_BASE=$(git merge-base "$BASE" HEAD) || exit 1

if [ "$MERGE_BASE" = "$(git rev-parse HEAD)" ]; then
  echo "nothing on this branch that is not already on $BASE"
  exit 0
fi

version_at() {
  # $1 ref, $2 project. A project is versioned by a <Version> in its csproj,
  # which is named like its folder. Empty when the project did not exist
  # there, which is a new project and needs no bump.
  git show "$1:dotnet/$2/$2.csproj" 2>/dev/null \
    | sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' | head -1
}

# The project that packs each native folder.
project_of_native() {
  case "$1" in
    orblit_audio_device) echo Orblit.AudioDevice ;;
    orblit_core) echo Orblit.Core ;;
    orblit_filament) echo Orblit.Filament ;;
    orblit_native) echo Orblit.Native ;;
  esac
}

# An escape hatch, because the check cannot tell a reflow from a rewrite.
# The exemption is stated in a commit and printed here, so the exception is
# in the history next to the reason for it. The trailer names the projects it
# covers, and only those:
#
#   Version-exempt: Orblit.Camera Orblit.Light - formatter only
#
# Naming them matters. A branch-wide exemption exempts everything committed
# after it too, so a reformat early on quietly excuses the feature that lands
# later, which is the failure this check exists to prevent.
exempt=$(git log --format='%B' "$MERGE_BASE"..HEAD \
  | sed -n 's/^Version-exempt: *//p' \
  | sed 's/[-—:].*//' | tr ' ' '\n' | grep -v '^$' | sort -u)

changed=$(git diff --name-only "$MERGE_BASE"...HEAD \
    -- 'dotnet/*.cs' 'dotnet/*.axaml' 'dotnet/*.csproj' \
  | grep -vE '/(bin|obj)/' | cut -d/ -f2 | sort -u)

# Generated material headers and the fetched SDK are build output, not source.
for folder in $(git diff --name-only "$MERGE_BASE"...HEAD \
    -- 'packages/*/include/*' 'packages/*/src/*' 'packages/*/darwin/*' \
       'packages/*/linux/*' 'packages/*/windows/*' \
  | grep -vE '/(third_party|generated)/' | cut -d/ -f2 | sort -u); do
  changed="$changed $(project_of_native "$folder")"
done
changed=$(echo "$changed" | tr ' ' '\n' | grep -v '^$' | sort -u)

if [ -z "$changed" ]; then
  echo "no project source changed, so no version bump needed"
  exit 0
fi

# $1 name, $2 version at the merge base, $3 version now, $4 where the version
# lives, $5 the changelog. Prints one line and returns 1 on a failure.
check_version() {
  local name=$1 was=$2 now=$3 manifest=$4 changelog=$5

  if echo "$exempt" | grep -qx all || echo "$exempt" | grep -qx "$name"; then
    echo "  --    $name exempted"
    return 0
  fi

  if [ -z "$was" ]; then
    echo "  ok    $name is new at $now"
    return 0
  fi

  if [ "$was" = "$now" ]; then
    echo "  FAIL  $name: source changed but version is still $now"
    echo "        Bump it in $manifest and add a line to"
    echo "        $changelog. See VERSIONING.md."
    return 1
  fi

  # A bump nobody wrote down is half the rule. The changelog is the half a
  # consumer actually reads.
  if ! grep -qF "$now" "$changelog" 2>/dev/null; then
    echo "  FAIL  $name: version is $now but $changelog does not mention it"
    return 1
  fi

  echo "  ok    $name $was -> $now"
}

failures=0
for project in $changed; do
  now=$(version_at HEAD "$project")
  [ -n "$now" ] || continue
  check_version "$project" "$(version_at "$MERGE_BASE" "$project")" \
    "$now" "dotnet/$project/$project.csproj" \
    "dotnet/$project/CHANGELOG.md" || failures=$((failures + 1))
done

echo
if [ "$failures" -eq 0 ]; then
  echo "every changed project is versioned"
else
  echo "$failures project(s) need a version bump"
fi
exit "$failures"
