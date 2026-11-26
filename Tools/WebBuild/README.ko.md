# Mac Web 빌드 자동화

이 도구는 **이미 Unity와 Web Build Support가 설치된 Mac**에서 실행합니다. Unity Editor를 설치하거나 다운로드하지 않습니다. Linux 소스 작업공간에서는 실행할 수 없습니다.

최종 업로드 순서와 한 번에 실행하는 명령은 [FINAL-UPLOAD.ko.md](FINAL-UPLOAD.ko.md)에 있습니다.

Unity에서 작업을 저장한 뒤 `Build Web on Mac.command`를 실행합니다. 열려 있는 Unity 프로젝트가 하나면 자동 선택하고, 그렇지 않으면 프로젝트 폴더를 한 번 선택합니다. 터미널 인자로 프로젝트 절대 경로를 넘길 수도 있습니다.

```bash
/bin/bash '/path/to/Build Web on Mac.command' '/path/to/DDES9902_diego'
```

## 수행 작업

- 원본 Assets / Packages / ProjectSettings를 별도 빌드 사본으로 복사합니다. 원본의 씬, 프리팹, .meta, 코드, 패키지와 Library는 수정하거나 삭제하지 않습니다.
- 알려진 이전 코드 버전만 빌드 사본에서 최신 main 코드로 갱신합니다. `A2_WEB_SOURCE_REVISION`에 main 이력의 전체 커밋을 지정하면 해당 게임 버전으로 고정합니다. 별도 로컬 코드 편집을 발견하면 덮어쓰지 않고 중단합니다.
- 기존 Library, TypeDB, Bee, PackageCache를 복사하지 않습니다. 빌드 전용 Bee 경로와 `CleanBuildCache` 옵션으로 타입 캐시 충돌을 배제합니다. NUnit 또는 테스트 패키지를 제거하지 않습니다.
- Web 모듈은 Hub의 `Unity.app` 옆 경로와 앱 내부 경로를 모두 확인합니다. 폴더 탐색만으로 미설치라고 단정하지 않고 설치된 Unity의 API/빌드 로그로 최종 확인합니다.
- `How to be a bartender` 씬 하나를 Web 릴리즈로 빌드합니다. 테스트 어셈블리 포함 옵션은 사용하지 않습니다. 압축은 이 빌드 사본에서 Disabled로, 웹 제목은 `How to be a Bartender`로 설정합니다.
- Unity 종료 코드, BuildReport, 오류 수, index.html / loader / wasm / data / framework 파일을 확인합니다.
- 성공한 경우에만 바탕화면에 `Bartender_Web_Fixed-날짜.zip`을 만들고 ZIP 무결성과 루트의 index.html을 검사합니다.
- 새 빌드 ZIP의 `Bartender-release.txt`에 게임 소스 버전을 기록하고 ZIP 옆에 `.sha256` 체크섬을 만듭니다. 원본 Mac 프로젝트 경로는 ZIP에 넣지 않습니다.

## 실패 시

`~/Library/Application Support/DDES9902WebBuild/실행날짜/`에 실제 `Unity-WebBuild.log`, 소스 버전, NUnit DLL 목록과 플레이어 어셈블리 목록이 남습니다. 실패하거나 비어 있는 폴더는 업로드용 ZIP으로 만들지 않습니다. 사본과 로그는 진단을 위해 보존합니다.

이 도구의 작성/정적 검사와 실제 Mac Unity 빌드 성공은 별개입니다. `WEB BUILD SUCCEEDED`와 ZIP 경로가 나온 경우에만 빌드 완료입니다. 브라우저 플레이 검증은 생성된 빌드로 별도 확인해야 합니다.

## 빌드는 성공했지만 ZIP 생성만 멈춘 경우

`Unity-WebBuild.log`에 `A2_WEB_BUILD_SUCCEEDED`가 있고 필수 결과 파일이 남아 있다면 재빌드할 필요가 없습니다.

```bash
/bin/bash '/path/to/Build Web on Mac.command' --package-existing "$HOME/Library/Application Support/DDES9902WebBuild/실행날짜"
```

이 모드는 Unity 실행/캐시 사본 생성/코드 동기화를 건너뛰고, 기존 성공 로그·결과 요약·필수 Web 파일을 검증한 뒤 ZIP만 만듭니다. 기존 ZIP을 덮어쓰거나 병합하지 않도록 매번 새 이름을 사용합니다.

## Unity 없이 수행하는 검사

```bash
bash -n 'Tools/WebBuild/Build Web on Mac.command'
python3 Tools/Validation/WebBuild/check_release.py
python3 Tools/Validation/WebBuild/check_builder.py --dotnet /path/to/existing/dotnet
```

첫 검증기는 임시 Git 이력/파일로 버전 선택·실패 거부·ZIP 구성·체크섬을 확인합니다. 두 번째는 기존 .NET SDK와 Unity API 대역으로 성공/실패/예외 시 빌드 설정 복구를 확인합니다. 검사용 Web 파일은 임시 폴더에서 삭제되며 게임 결과물이 아닙니다. 실제 Unity 컴파일·브라우저 실행과 구분해야 합니다.
