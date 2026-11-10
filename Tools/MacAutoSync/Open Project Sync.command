#!/bin/bash
set -euo pipefail
if [ "$(uname -s)" != Darwin ]; then echo 'Run this on your Mac.'; exit 1; fi

# Unity가 실제로 연 프로젝트를 찾습니다. 예전 설치 경로는 사용하지 않습니다.
candidates=$(/bin/ps -ww -axo args= | /usr/bin/awk '
    index($0, "/Unity.app/Contents/MacOS/Unity") {
        path=""
        for (i=1; i<=NF; i++) {
            if (tolower($i)=="-projectpath") {
                for (j=i+1; j<=NF && $j !~ /^-/; j++) path=path (path=="" ? "" : " ") $j
                sub(/^"/, "", path); sub(/"$/, "", path)
                if (path!="") print path
                break
            }
        }
    }' | /usr/bin/sort -u)
count=$(printf '%s\n' "$candidates" | /usr/bin/awk 'NF {n++} END {print n+0}')
if [ "$count" = 1 ] && [ -d "$candidates/ProjectSettings" ]; then
    project=$candidates
else
    project=$(/usr/bin/osascript -e 'POSIX path of (choose folder with prompt "지금 Unity에서 열고 있는 프로젝트 폴더를 선택하세요")')
fi
project=$(cd "$project" && pwd -P)
files=("Bar intro.cs" "BarPlayerMovement.cs" "BeerGlassPour.cs" "EntranceGuide.cs" "UniformInteraction.cs")
for file in "${files[@]}"; do
    [ -f "$project/Assets/Script/$file" ] || { echo "프로젝트가 다릅니다. 파일 없음: $file"; exit 1; }
done
[ -f "$project/Assets/How to be a bartender.unity" ] || { echo '바 씬이 없는 프로젝트입니다.'; exit 1; }
echo "현재 Unity 프로젝트: $project"
echo '이 창을 열어두면 같은 프로젝트를 60초마다 동기화합니다. 종료는 Control+C입니다.'

base="$HOME/Library/Application Support/DDES9902AutoSync"
key=$(printf '%s' "$project" | /usr/bin/shasum -a 256 | /usr/bin/awk '{print $1}')
state="$base/terminal-$key"
mkdir -p "$state"
if ! mkdir "$state/session.lock" 2>/dev/null; then
    pid=$(cat "$state/session.lock/pid" 2>/dev/null || true)
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
        echo '이 프로젝트의 동기화 터미널이 이미 실행 중입니다.'; exit 1
    fi
    mv "$state/session.lock" "$state/stale-session-$(date +%s)-$$"
    mkdir "$state/session.lock"
fi
echo "$$" > "$state/session.lock/pid"
trap 'rm -rf "$state/session.lock"' EXIT
trap 'exit 0' INT TERM
if [ -d "$state/lock" ]; then mv "$state/lock" "$state/stale-worker-$(date +%s)-$$"; fi

# Documents에 접근할 수 없는 이전 백그라운드 작업은 해제합니다.
plist="$HOME/Library/LaunchAgents/com.ddes9902.code-autosync.plist"
/bin/launchctl bootout "gui/$(id -u)" "$plist" 2>/dev/null || true
if [ -f "$plist" ]; then
    cp -p "$plist" "$base/agent-before-terminal-$(date +%s)-$$.plist"
    rm "$plist"
fi
export GIT_TERMINAL_PROMPT=0
export GIT_SSH_COMMAND="${GIT_SSH_COMMAND:-ssh -oBatchMode=yes -oConnectTimeout=15}"
remote=https://github.com/DiegoBidam/DDES9902_diego.git
cache="$state/cache.git"
if [ ! -d "$cache" ]; then
    /usr/bin/git init --bare --quiet "$cache"
    /usr/bin/git -C "$cache" remote add origin "$remote"
    /usr/bin/git -C "$cache" config remote.origin.promisor true
    /usr/bin/git -C "$cache" config remote.origin.partialclonefilter blob:none
fi
/usr/bin/git -C "$cache" fetch --quiet --filter=blob:none --depth=32 origin refs/heads/main
revision=$(/usr/bin/git -C "$cache" rev-parse FETCH_HEAD)
/usr/bin/git -C "$cache" show "$revision:Tools/MacAutoSync/sync.sh" > "$state/sync.sh"
/usr/bin/git -C "$cache" show "$revision:Tools/MacAutoSync/original-hashes.txt" > "$state/original-hashes.txt"
hash() { /usr/bin/shasum -a 256 "$1" | /usr/bin/awk '{print $1}'; }
backup="$state/before-apply-$(date +%Y%m%d-%H%M%S)-$$"
mkdir -p "$backup"

# 이미 제공했던 수정본은 인식하고, 별도 사용자 편집은 보존합니다.
for index in "${!files[@]}"; do
    file=${files[$index]}
    current=$(hash "$project/Assets/Script/$file")
    cp -p "$project/Assets/Script/$file" "$backup/$file"
    known=0
    while IFS= read -r commit; do
        /usr/bin/git -C "$cache" show "$commit:Assets/Script/$file" > "$state/candidate.cs"
        if [ "$(hash "$state/candidate.cs")" = "$current" ]; then known=1; break; fi
    done < <(/usr/bin/git -C "$cache" log "$revision" --format=%H -- "Assets/Script/$file")
    if [ "$known" != 1 ]; then
        echo "별도 로컬 편집을 발견해 덮어쓰지 않았습니다: $file"
        echo "기존 파일 백업: $backup"
        exit 2
    fi
    printf '%s\n' "$current" > "$state/$index.applied"
done
rm -f "$state/candidate.cs"

while true; do
    if /bin/bash "$state/sync.sh" "$project" "$state" "$remote"; then
        revision=$(cat "$state/last-revision.txt")
        # 실제 대상 파일 5개가 GitHub 내용과 일치하는지 재검증합니다.
        for file in "${files[@]}"; do
            /usr/bin/git -C "$cache" show "$revision:Assets/Script/$file" > "$state/verify.cs"
            if ! cmp -s "$project/Assets/Script/$file" "$state/verify.cs"; then
                echo "반영 검증 실패: $file"; exit 1
            fi
        done
        rm -f "$state/verify.cs"
        echo "$(date '+%H:%M:%S') 적용 확인 완료: $project (main ${revision:0:7})"
        if /usr/bin/grep -q 'private sealed class CocktailPractice' "$project/Assets/Script/BeerGlassPour.cs" \
            && /usr/bin/grep -q 'Start to make' "$project/Assets/Script/BeerGlassPour.cs"; then
            echo '연습 스테이지 코드 확인: Start to make / 45 seconds'
        fi
        echo 'Unity의 컴파일이 끝난 뒤 Play를 다시 시작하세요. 이 터미널은 열어 두세요.'
    else
        result=$?
        if [ "$result" = 2 ]; then
            echo '로컬 편집을 보존하기 위해 멈췄습니다. 위 파일은 덮어쓰지 않았습니다.'
            exit 2
        fi
        echo "이번 갱신은 실패했습니다 (code $result). 적용 완료가 아닙니다."
        echo '터미널을 유지하고 60초 뒤 자동 재시도합니다.'
    fi
    sleep 60
done
