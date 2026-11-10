#!/bin/bash
set -euo pipefail
if [ "$(uname -s)" != Darwin ]; then echo 'Run this repair on your Mac.'; exit 1; fi
if /usr/bin/pgrep -x Unity >/dev/null; then
    echo 'Unity Editor를 먼저 종료한 뒤 다시 실행해 주세요. 저장하지 않은 작업은 자동으로 닫지 않습니다.'
    exit 1
fi
state="$HOME/Library/Application Support/DDES9902AutoSync"
plist="$HOME/Library/LaunchAgents/com.ddes9902.code-autosync.plist"
if [ ! -f "$plist" ] || [ ! -f "$state/sync.sh" ]; then
    echo '기존 자동 동기화 설치를 찾지 못했습니다. Install Auto Sync.command를 실행해 주세요.'
    exit 1
fi
project=$(/usr/bin/plutil -extract ProgramArguments.2 raw -o - "$plist")
remote=$(/usr/bin/plutil -extract ProgramArguments.4 raw -o - "$plist")
if [ ! -d "$project/Assets/Script" ] || [ ! -d "$project/ProjectSettings" ]; then
    echo "등록된 프로젝트 폴더를 찾을 수 없습니다: $project"; exit 1
fi
project=$(cd "$project" && pwd -P)
destination=$project
case "$project" in
    "$HOME/Documents"/*|"$HOME/Desktop"/*|"$HOME/Downloads"/*|"$HOME/Library/Mobile Documents"/*)
        destination="$HOME/UnityProjects/DDES9902_diego"
        if [ -e "$destination" ]; then destination="$destination-$(date +%Y%m%d-%H%M%S)-$$"; fi
        mkdir -p "$destination"
        echo "기존 프로젝트를 보존하고 사본을 만듭니다: $destination"
        # 원본은 지우지 않습니다. Unity가 재생성할 수 있는 캐시만 복사에서 제외합니다.
        /usr/bin/rsync -a --exclude=/Library/ --exclude=/Temp/ --exclude=/Logs/ \
            --exclude=/obj/ --exclude=/Obj/ "$project/" "$destination/"
        ;;
esac
timestamp=$(date +%Y%m%d-%H%M%S)-$$
cp -p "$plist" "$state/agent-before-repair-$timestamp.plist"
/bin/launchctl bootout "gui/$(id -u)" "$plist" 2>/dev/null || true
if [ -d "$state/lock" ]; then mv "$state/lock" "$state/lock-before-repair-$timestamp"; fi

# 이미 설치에 사용한 Git 인증과 저장소에서 최신 동기화 도구만 가져옵니다.
export GIT_TERMINAL_PROMPT=0
cache="$state/cache.git"
/usr/bin/git -C "$cache" fetch --quiet --filter=blob:none --depth=1 origin refs/heads/main
revision=$(/usr/bin/git -C "$cache" rev-parse FETCH_HEAD)
/usr/bin/git -C "$cache" show "$revision:Tools/MacAutoSync/sync.sh" > "$state/sync.sh.next"
/bin/bash -n "$state/sync.sh.next"
mv "$state/sync.sh.next" "$state/sync.sh"
if ! /bin/bash "$state/sync.sh" "$destination" "$state" "$remote" >> "$state/sync.log" 2>&1; then
    tail -n 15 "$state/sync.log"
    echo "동기화 실패로 중단했습니다. 원본 프로젝트는 보존되어 있습니다: $project"
    exit 1
fi
/usr/bin/plutil -replace ProgramArguments.2 -string "$destination" "$plist"
if [ -f "$state/last-success.txt" ]; then mv "$state/last-success.txt" "$state/manual-success-$timestamp.txt"; fi
/bin/launchctl bootstrap "gui/$(id -u)" "$plist"
# 직접 실행 성공과 백그라운드 실행 성공을 구분합니다.
for attempt in {1..20}; do
    if [ -f "$state/last-success.txt" ]; then
        printf '\n백그라운드 자동 동기화 성공. 이후 60초마다 확인합니다.\n'
        echo "Unity Hub에서 앞으로 열 프로젝트: $destination"
        echo '기존 Documents 프로젝트가 아니라 위 사본을 열어 주세요.'
        /usr/bin/open "$destination"
        exit 0
    fi
    sleep 1
done
echo '직접 동기화는 성공했지만 백그라운드 실행 성공은 확인하지 못했습니다.'
tail -n 15 "$state/sync.log"
echo "사본 위치: $destination"
exit 1
