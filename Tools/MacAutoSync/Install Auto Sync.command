#!/bin/bash
set -euo pipefail
finish() { printf '\n%s\n' 'Press Return to close.'; read -r ignored || true; }
trap finish EXIT
if [ "$(uname -s)" != Darwin ]; then echo 'This installer runs on your Mac only.'; exit 1; fi
source_dir=$(cd "$(dirname "$0")" && pwd)
project=$(/usr/bin/osascript -e 'POSIX path of (choose folder with prompt "Unity 프로젝트 폴더를 선택하세요 (Assets, Packages, ProjectSettings가 있는 폴더)")')
project=${project%/}
project=$(cd "$project" && pwd -P)
if [ ! -d "$project/Assets/Script" ] || [ ! -d "$project/ProjectSettings" ]; then
    echo 'The selected folder is not the Unity project.'; exit 1
fi
case "$project" in
    "$HOME/Documents"/*|"$HOME/Desktop"/*|"$HOME/Downloads"/*|"$HOME/Library/Mobile Documents"/*)
        echo '이 위치는 macOS가 백그라운드 접근을 제한합니다. 프로젝트를 ~/UnityProjects 아래에 복사한 뒤 그 사본을 선택해 주세요.'
        echo '이미 설치했다면 Repair Auto Sync.command가 원본 보존, 사본 생성, 경로 변경을 처리합니다.'
        exit 1
        ;;
esac
state="$HOME/Library/Application Support/DDES9902AutoSync"
label=com.ddes9902.code-autosync
plist="$HOME/Library/LaunchAgents/$label.plist"
remote=https://github.com/DiegoBidam/DDES9902_diego.git
if /usr/bin/git -C "$project" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    remote=$(/usr/bin/git -C "$project" remote get-url origin)
fi
mkdir -p "$state" "$HOME/Library/LaunchAgents"
/bin/launchctl bootout "gui/$(id -u)" "$plist" 2>/dev/null || true
cp "$source_dir/sync.sh" "$source_dir/original-hashes.txt" "$state/"
echo 'Applying the latest main scripts (local edits will be preserved)...'
if ! /bin/bash "$state/sync.sh" "$project" "$state" "$remote" >> "$state/sync.log" 2>&1; then
    tail -n 12 "$state/sync.log"
    echo 'Installation stopped. No background sync was enabled. See the log above.'
    exit 1
fi
rm -f "$plist"
/usr/bin/plutil -create xml1 "$plist"
/usr/bin/plutil -insert Label -string "$label" "$plist"
/usr/bin/plutil -insert ProgramArguments -xml '<array/>' "$plist"
args=(/bin/bash "$state/sync.sh" "$project" "$state" "$remote")
for index in "${!args[@]}"; do
    /usr/bin/plutil -insert "ProgramArguments.$index" -string "${args[$index]}" "$plist"
done
/usr/bin/plutil -insert RunAtLoad -bool YES "$plist"
/usr/bin/plutil -insert StartInterval -integer 60 "$plist"
/usr/bin/plutil -insert StandardOutPath -string "$state/sync.log" "$plist"
/usr/bin/plutil -insert StandardErrorPath -string "$state/sync.log" "$plist"
if [ -f "$state/last-success.txt" ]; then mv "$state/last-success.txt" "$state/manual-success-$(date +%s).txt"; fi
/bin/launchctl bootstrap "gui/$(id -u)" "$plist"
for attempt in {1..20}; do
    if [ -f "$state/last-success.txt" ]; then
        echo "Installed. Background sync verified; checking main every 60 seconds."
        echo "Project: $project"
        echo "Log and backups: $state"
        exit 0
    fi
    sleep 1
done
echo 'Background sync did not confirm success. Installation is not verified.'
tail -n 12 "$state/sync.log"
exit 1
