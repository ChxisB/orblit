#!/bin/bash
# Requires a version bump and a changelog line for every package whose library
# code changed on this branch.
#
# The rule is in VERSIONING.md; this is what stops it depending on somebody
# remembering it at five o'clock. It is deliberately narrow: only files under
# a package's lib/ count, so a branch that fixes a test, a comment, a workflow
# or a page of documentation is not asked to invent a release.
#
# A branch whose library changes are genuinely not a feature — a reformat, a
# rename with no behaviour in it — says so with a `Version-exempt: <reason>`
# line in one of its commit messages, and this honours it and prints it.
#
# A C# project under dotnet/ follows the same rule. It counts when its csproj
# has a <Version>, and a changed .cs, .axaml or .csproj file asks for a bump.
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
  # $1 ref, $2 package. Empty when the package did not exist there, which is
  # a new package and needs no bump.
  git show "$1:packages/$2/pubspec.yaml" 2>/dev/null \
    | sed -n 's/^version: *//p' | head -1
}

dotnet_version_at() {
  # $1 ref, $2 project. A project is versioned by a <Version> in its csproj,
  # which is named like its folder. Test and tool projects have none, so
  # they are never asked for a bump.
  git show "$1:dotnet/$2/$2.csproj" 2>/dev/null \
    | sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' | head -1
}

# An escape hatch, because the check cannot tell a reflow from a rewrite.
# `dart format` joining two lines is not a feature, and git cannot see that:
# --ignore-all-space compares within lines and a reflow changes how many there
# are. Rather than guess, the exemption is stated in a commit and printed
# here, so the exception is in the history next to the reason for it.
# The trailer names the packages it covers, and only those:
#
#   Version-exempt: orblit_camera orblit_light - dart format only
#   Version-exempt: all - repository-wide reformat
#
# Naming them matters. A branch-wide exemption exempts everything committed
# after it too, so a reformat early on quietly excuses the feature that lands
# later — which is the failure this check exists to prevent, reintroduced by
# the escape hatch meant to make it usable.
exempt=$(git log --format='%B' "$MERGE_BASE"..HEAD \
  | sed -n 's/^Version-exempt: *//p' \
  | sed 's/[-—:].*//' | tr ' ' '\n' | grep -v '^$' | sort -u)

# Dart and native both. Watching only lib/ was wrong for this repository in
# particular: the renderer is four thousand lines of C++ under darwin/, and a
# change to it is exactly the kind a consumer notices — but it touched no Dart,
# so the rule that a feature is a version did not apply to the largest feature
# there is. Generated material headers and the fetched SDK are excluded; they
# are build output, not source.
changed=$(git diff --name-only "$MERGE_BASE"...HEAD \
    -- 'packages/*/lib/*' 'packages/*/darwin/*' 'packages/*/android/*' \
       'packages/*/linux/*' 'packages/*/windows/*' 'packages/*/src/*' \
  | grep -vE '/(third_party|generated)/' \
  | cut -d/ -f2 | sort -u)

# The C# projects. Only source counts, as only lib/ does above.
changed_dotnet=$(git diff --name-only "$MERGE_BASE"...HEAD \
    -- 'dotnet/*.cs' 'dotnet/*.axaml' 'dotnet/*.csproj' \
  | grep -vE '/(bin|obj)/' | cut -d/ -f2 | sort -u)

if [ -z "$changed$changed_dotnet" ]; then
  echo "no package library code changed, so no version bump needed"
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
for package in $changed; do
  check_version "$package" "$(version_at "$MERGE_BASE" "$package")" \
    "$(version_at HEAD "$package")" "packages/$package/pubspec.yaml" \
    "packages/$package/CHANGELOG.md" || failures=$((failures + 1))
done

for project in $changed_dotnet; do
  now=$(dotnet_version_at HEAD "$project")
  [ -n "$now" ] || continue
  check_version "$project" "$(dotnet_version_at "$MERGE_BASE" "$project")" \
    "$now" "dotnet/$project/$project.csproj" \
    "dotnet/$project/CHANGELOG.md" || failures=$((failures + 1))
done

echo
if [ "$failures" -eq 0 ]; then
  echo "every changed package is versioned"
else
  echo "$failures package(s) need a version bump"
fi
exit "$failures"
