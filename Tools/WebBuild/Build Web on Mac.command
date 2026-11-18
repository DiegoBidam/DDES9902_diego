#!/bin/bash
set -euo pipefail
if [ "$(uname -s)" != Darwin ]; then
    echo '이 도구는 Unity가 이미 설치된 Mac에서 실행합니다. Unity 설치/다운로드는 하지 않습니다.'
    exit 1
fi

package_web_build() {
    local run_directory=$1 desktop=${2:-"$HOME/Desktop"} built log manifest suffix destination zipfile stamp
    local -a matches
    run_directory=$(cd "$run_directory" && pwd -P)
    built="$run_directory/Bartender_Web"
    log="$run_directory/Unity-WebBuild.log"
    manifest="$built/A2-build-result.txt"
    if [ ! -s "$log" ] || ! /usr/bin/grep -Fxq "A2_WEB_BUILD_SUCCEEDED: $built" "$log" \
        || [ ! -s "$manifest" ] || ! /usr/bin/grep -Fxq 'Result: Succeeded' "$manifest" \
        || ! /usr/bin/grep -Fxq 'Errors: 0' "$manifest" || [ ! -s "$built/index.html" ]; then
        echo '검증된 성공 빌드를 찾지 못해 압축하지 않았습니다. Unity-WebBuild.log를 확인하세요.'
        return 1
    fi
    for suffix in .loader.js .wasm .data .framework.js; do
        shopt -s nullglob
        matches=("$built"/Build/*"$suffix")
        shopt -u nullglob
        if [ "${#matches[@]}" = 0 ] || [ ! -s "${matches[0]}" ]; then
            echo "필수 Web 빌드 파일이 없어 압축하지 않았습니다: $suffix"
            return 1
        fi
    done
    mkdir -p "$desktop"
    stamp=$(basename "$run_directory")
    destination=$(mktemp -d "$desktop/Bartender_Web_Fixed-$stamp.XXXXXX")
    zipfile="$destination.zip"
    /usr/bin/ditto "$built" "$destination"
    (cd "$destination" && /usr/bin/zip -qr "$zipfile" . -x '*.DS_Store' '__MACOSX/*')
    /usr/bin/unzip -tq "$zipfile"
    /usr/bin/unzip -Z1 "$zipfile" | /usr/bin/grep -Fx 'index.html' >/dev/null
    echo 'WEB BUILD SUCCEEDED'
    echo "업로드할 ZIP: $zipfile"
    echo "실제 Unity 빌드 로그: $log"
    /usr/bin/open -R "$zipfile"
}

if [ "${1:-}" = --package-existing ]; then
    [ "$#" = 2 ] || { echo '사용법: --package-existing 성공한-빌드-실행폴더'; exit 1; }
    package_web_build "$2"
    exit 0
fi

project=${1:-}
if [ -z "$project" ]; then
    candidates=$(/bin/ps -ww -axo args= | /usr/bin/awk '
        index($0, "/Unity.app/Contents/MacOS/Unity") {
            for (i=1; i<=NF; i++) if (tolower($i)=="-projectpath") {
                path=""
                for (j=i+1; j<=NF && $j !~ /^-/; j++) path=path (path=="" ? "" : " ") $j
                sub(/^"/, "", path); sub(/"$/, "", path)
                if (path!="") print path
                break
            }
        }' | /usr/bin/sort -u)
    count=$(printf '%s\n' "$candidates" | /usr/bin/awk 'NF {n++} END {print n+0}')
    if [ "$count" = 1 ]; then project=$candidates; fi
fi
if [ -z "$project" ]; then
    project=$(/usr/bin/osascript -e 'POSIX path of (choose folder with prompt "바텐더 Unity 프로젝트 폴더를 선택하세요 (Assets / Packages / ProjectSettings가 있는 폴더)")')
fi
project=$(cd "$project" && pwd -P)
for directory in Assets Packages ProjectSettings; do
    [ -d "$project/$directory" ] || { echo "프로젝트 폴더가 아닙니다: $project"; exit 1; }
done
[ -f "$project/Assets/How to be a bartender.unity" ] || { echo '바텐더 게임 씬이 없습니다.'; exit 1; }
version=$(/usr/bin/awk '/^m_EditorVersion:/ {print $2; exit}' "$project/ProjectSettings/ProjectVersion.txt")
unity="/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
if [ ! -x "$unity" ]; then
    echo "설치된 Unity $version 실행 파일을 찾지 못했습니다: $unity"
    echo '설치하거나 버전을 자동으로 바꾸지 않았습니다.'
    exit 1
fi
find_webgl_support() {
    local executable=$1 contents_root editor_root candidate
    contents_root=$(dirname "$(dirname "$executable")")
    editor_root=$(dirname "$(dirname "$contents_root")")
    # Hub는 Unity.app 옆에 모듈을 설치합니다. 앱 내부 배치도 함께 지원합니다.
    for candidate in "$editor_root/PlaybackEngines/WebGLSupport" "$contents_root/PlaybackEngines/WebGLSupport"; do
        if [ -d "$candidate" ]; then printf '%s\n' "$candidate"; return 0; fi
    done
    return 1
}
webgl_support=$(find_webgl_support "$unity" || true)
if [ -n "$webgl_support" ]; then
    echo "Web Build Support 경로: $webgl_support"
else
    # 폴더 배치만으로 미설치라고 단정하지 않습니다. 최종 판단은 설치된 Unity가 수행합니다.
    echo '표준 경로에서 Web 모듈 폴더를 찾지 못했습니다. 기존 Unity의 실제 Web 지원 여부를 확인하며 진행합니다.'
fi

base="$HOME/Library/Application Support/DDES9902WebBuild"
stamp=$(date +%Y%m%d-%H%M%S)-$$
run="$base/$stamp"
staging="$run/Project"
output="$run/Bartender_Web"
log="$run/Unity-WebBuild.log"
mkdir -p "$staging"
# Assets/Packages/ProjectSettings만 복사합니다. Library/TypeDB/Bee/PackageCache는 새로 생성합니다.
echo "원본 프로젝트를 보존한 채 캐시 없는 빌드 사본을 만듭니다: $staging"
for directory in Assets Packages ProjectSettings; do
    /usr/bin/ditto "$project/$directory" "$staging/$directory"
done

echo '최신 코드와 별도 로컬 편집 여부를 확인합니다.'
export GIT_TERMINAL_PROMPT=0
export GIT_SSH_COMMAND="${GIT_SSH_COMMAND:-ssh -oBatchMode=yes -oConnectTimeout=15}"
cache="$base/source.git"
if [ ! -d "$cache" ]; then
    /usr/bin/git init --bare --quiet "$cache"
    /usr/bin/git -C "$cache" remote add origin https://github.com/DiegoBidam/DDES9902_diego.git
    /usr/bin/git -C "$cache" config remote.origin.promisor true
    /usr/bin/git -C "$cache" config remote.origin.partialclonefilter blob:none
fi
/usr/bin/git -C "$cache" fetch --quiet --filter=blob:none --depth=64 origin refs/heads/main
revision=$(/usr/bin/git -C "$cache" rev-parse FETCH_HEAD)
/usr/bin/git -C "$cache" show "$revision:Tools/MacAutoSync/original-hashes.txt" > "$run/original-hashes.txt"
files=("Bar intro.cs" "BarPlayerMovement.cs" "BeerGlassPour.cs" "EntranceGuide.cs" "UniformInteraction.cs")
hash() { /usr/bin/shasum -a 256 "$1" | /usr/bin/awk '{print $1}'; }
for index in "${!files[@]}"; do
    file=${files[$index]}
    current=$(hash "$staging/Assets/Script/$file")
    original=$(/usr/bin/awk -v i="$index" '$1==i {print $2}' "$run/original-hashes.txt")
    known=0
    if [ "$current" = "$original" ]; then known=1; fi
    if [ "$known" = 0 ]; then
        while IFS= read -r commit; do
            /usr/bin/git -C "$cache" show "$commit:Assets/Script/$file" > "$run/candidate.cs"
            if [ "$(hash "$run/candidate.cs")" = "$current" ]; then known=1; break; fi
        done < <(/usr/bin/git -C "$cache" log "$revision" --format=%H -- "Assets/Script/$file")
    fi
    if [ "$known" != 1 ]; then
        echo "별도 로컬 편집을 발견해 빌드를 중단했습니다: $file"
        echo '원본이나 사본의 해당 코드를 덮어쓰지 않았습니다.'
        exit 2
    fi
    # 갱신은 빌드 사본에만 적용합니다. 원본의 .cs/.meta/씬에는 쓰지 않습니다.
    /usr/bin/git -C "$cache" show "$revision:Assets/Script/$file" > "$staging/Assets/Script/$file"
done
mkdir -p "$staging/Assets/Editor"
/usr/bin/git -C "$cache" show "$revision:Tools/WebBuild/BartenderWebBuild.cs" > "$staging/Assets/Editor/A2WebBuild.cs"
printf 'Source revision: %s\nOriginal project: %s\nUnity: %s\n' "$revision" "$project" "$version" > "$run/build-info.txt"
/usr/bin/find "$staging/Assets" "$staging/Packages" -type f -iname '*nunit*.dll' -print > "$run/nunit-asset-inventory.txt"
# 전역 Bee 캐시도 이 빌드 전용 경로로 분리합니다.
export BEE_CACHE_DIRECTORY="$run/BeeCache"
echo "Unity $version 로 Web 빌드를 시작합니다. 완료될 때까지 이 터미널을 열어 두세요."
echo "로그: $log"
set +e
"$unity" -batchmode -nographics -quit -projectPath "$staging" -buildTarget WebGL \
    -executeMethod BartenderWebBuild.Build -a2WebOutput "$output" -logFile "$log"
result=$?
set -e
if [ "$result" != 0 ] || ! /usr/bin/grep -q 'A2_WEB_BUILD_SUCCEEDED:' "$log" \
    || [ ! -s "$output/index.html" ] || [ ! -s "$output/A2-build-result.txt" ]; then
    echo 'Web 빌드가 완료되지 않았습니다. 빈 폴더를 업로드하지 마세요.'
    if [ -f "$log" ]; then /usr/bin/tail -n 100 "$log"; fi
    echo "진단 파일 모음: $run"
    /usr/bin/open "$run"
    exit 1
fi

# 성공 검증/압축은 재개 모드와 같은 경로를 사용합니다.
package_web_build "$run"
