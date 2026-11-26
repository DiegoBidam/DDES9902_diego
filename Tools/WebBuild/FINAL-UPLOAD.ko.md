# 최종 Web 업로드

게임 소스 버전: `857203cfc8c67a89d733bdce0fcc78d6c907e734`.
최신 공간 UI 수정까지 포함한 버전으로 고정합니다. 빌드 도구 변경과 게임 소스 버전은 따로 기록됩니다.

## Mac에서 새 ZIP 만들기

1. 업로드할 프로젝트를 Mac Unity에서 열고 저장합니다. Play 모드는 종료하고 Editor 창은 열어 둡니다.
2. **Mac의 Terminal 앱에서 ⌘N으로 새 창**을 엽니다. 기존 자동 동기화 터미널은 그대로 둡니다.
3. 아래 전체를 한 번 붙여넣고 Return을 누릅니다. 어느 폴더에서 실행해도 됩니다.

```bash
bartender_web_build=$(mktemp -t bartenderweb) &&
curl -fL 'https://raw.githubusercontent.com/DiegoBidam/DDES9902_diego/main/Tools/WebBuild/Build%20Web%20on%20Mac.command' -o "$bartender_web_build" &&
A2_WEB_SOURCE_REVISION=857203cfc8c67a89d733bdce0fcc78d6c907e734 /bin/bash "$bartender_web_build"
```

열려 있는 프로젝트를 자동으로 찾습니다. 폴더 선택 창이 나오면 `Assets`, `Packages`, `ProjectSettings`가 함께 있는 **프로젝트 최상위 폴더**를 선택합니다. `Assets` 폴더 자체나 이전 Web 출력 폴더를 선택하지 않습니다.

원본 프로젝트를 보존하고 별도 사본에서 설치된 Unity로 빌드합니다. 복사·임포트·Web 컴파일 동안 터미널을 닫지 마세요. 기존 코드가 오래돼도 알려진 버전이면 사본에서 갱신합니다. 별도 로컬 편집이 있으면 보존하고 중단합니다.

**`WEB BUILD SUCCEEDED`와 `업로드할 ZIP:` 경로가 나온 뒤 Finder에서 선택된 새 ZIP이 최종 업로드 파일입니다.** 바탕화면에 `Bartender_Web_Fixed-날짜.고유번호.zip` 형태로 생성됩니다. `.zip.sha256`은 확인용 파일이며 업로드하지 않습니다. 이 문구 전에 생기는 빌드 폴더는 완료 결과물이 아닙니다.

최신 코드 적용은 새 빌드가 필요합니다. `--package-existing`은 이전 성공 빌드를 다시 압축할 뿐이므로 이번 버전 갱신에는 사용하지 않습니다.

## Unity Play에 교체 업로드

1. [기존 게임](https://play.unity.com/ko/games/7315e0b5-935e-4938-badd-be152ff2b173/how-to-be-a-bartender)의 편집/업로드 화면을 엽니다.
2. **게임패드 아이콘이 있는 Game 업로드 칸**에 방금 Finder가 선택한 `.zip`을 넣습니다. 썸네일 칸에는 이미지를 넣거나 기존 이미지를 유지합니다.
3. 업로드 처리 후 제공되는 미리보기에서 게임을 확인하고 저장/게시합니다. 프로젝트 전체, `.command`, 빈 폴더는 업로드 대상이 아닙니다.

## 게시 전 확인

- 게임 영역을 클릭해 마우스 입력을 활성화하고 Enter로 시작합니다. 시작 보드, 음성/BGM, 마우스 시점과 WASD 이동을 확인합니다.
- 셔츠 → 앞치마 → 출근 → 바 이동 → 스쿠너/파인트 따르기·서빙 → 칵테일 안내/카드 흐름을 확인합니다.
- 칵테일 연습 1/2/3회의 제한시간 90/60/45초, 재료 선택 피드백, 스터/쉐이킹, 성공/실패를 확인합니다.
- 오픈 뒤 시점 이동, 안내 카드 간 겹침/가림, 손님 선택·서빙을 확인합니다. Easy 10명 완료 후 평가표까지 확인합니다.
- Mac Unity의 성공 로그와 Web 브라우저에서 실제 게임 실행은 별도 확인입니다. 회색 화면이면 브라우저 Console의 첫 게임 오류를 보관합니다.

실패 시 터미널에 나온 `진단 파일 모음:` 폴더의 `Unity-WebBuild.log`가 실제 로그입니다. 빈 결과를 업로드하지 않습니다. 원본 씬·프리팹·.meta·패키지·ProjectSettings는 이 빌드 도구가 수정하지 않습니다.
