#!/bin/bash
set -euo pipefail

project=$1
state=$2
remote=$3
mkdir -p "$state"
if ! mkdir "$state/lock" 2>/dev/null; then exit 0; fi
trap 'rm -rf "$state/lock"' EXIT
export GIT_TERMINAL_PROMPT=0
export GIT_SSH_COMMAND="${GIT_SSH_COMMAND:-ssh -oBatchMode=yes -oConnectTimeout=15}"
files=("Bar intro.cs" "BarPlayerMovement.cs" "BeerGlassPour.cs" "EntranceGuide.cs" "UniformInteraction.cs")
hash() {
    local output
    if ! output=$(/usr/bin/shasum -a 256 "$1"); then
        echo "Cannot read project file: $1. If macOS denies Documents access, run Repair Auto Sync.command." >&2
        return 1
    fi
    printf '%s\n' "${output%% *}"
}
cache="$state/cache.git"
if [ ! -d "$cache" ]; then
    /usr/bin/git init --bare --quiet "$cache"
    /usr/bin/git -C "$cache" remote add origin "$remote"
    /usr/bin/git -C "$cache" config remote.origin.promisor true
    /usr/bin/git -C "$cache" config remote.origin.partialclonefilter blob:none
fi
# Fetch code on demand, without downloading Unity's large asset files.
/usr/bin/git -C "$cache" fetch --quiet --filter=blob:none --depth=1 origin refs/heads/main
revision=$(/usr/bin/git -C "$cache" rev-parse FETCH_HEAD)
staging="$state/lock/staging"
mkdir -p "$staging"
for index in "${!files[@]}"; do
    file=${files[$index]}
    target="$project/Assets/Script/$file"
    [ -f "$target" ] || { echo "Missing script: $target"; exit 1; }
    /usr/bin/git -C "$cache" show "$revision:Assets/Script/$file" > "$staging/$index.cs"
    hash "$target" > "$staging/$index.before"
    current=$(cat "$staging/$index.before")
    original=$(/usr/bin/awk -v i="$index" '$1 == i {print $2}' "$state/original-hashes.txt")
    incoming=$(hash "$staging/$index.cs")
    previous=""
    if [ -f "$state/$index.applied" ]; then previous=$(cat "$state/$index.applied"); fi
    if [ "$current" != "$original" ] && [ "$current" != "$incoming" ] && [ "$current" != "$previous" ]; then
        echo "$(date): Local edits detected; no files updated: $file"
        exit 2
    fi
done

# Validate all five files before changing any. Keep original copies outside Assets.
mkdir -p "$state/backups"
for index in "${!files[@]}"; do
    target="$project/Assets/Script/${files[$index]}"
    [ "$(hash "$target")" = "$(cat "$staging/$index.before")" ] || { echo "File changed during sync; cancelled."; exit 2; }
done
changed=0
for index in "${!files[@]}"; do
    target="$project/Assets/Script/${files[$index]}"
    if ! cmp -s "$target" "$staging/$index.cs"; then
        backup="$state/backups/$revision"
        mkdir -p "$backup"
        cp -p "$target" "$backup/${files[$index]}"
        temporary=$(mktemp "$project/Assets/Script/.a2-sync.XXXXXX")
        cp -p "$target" "$temporary"
        cat "$staging/$index.cs" > "$temporary"
        mv -f "$temporary" "$target"
        changed=1
    fi
    hash "$staging/$index.cs" > "$state/$index.applied"
done
if [ "$changed" = 1 ]; then echo "$(date): Synced five scripts from main ($revision)."; fi
printf '%s\n' "$revision" > "$state/last-revision.txt"
date +%s > "$state/last-success.txt"
echo "$(date): Checked main ($revision). Project: $project"
