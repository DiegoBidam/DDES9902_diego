# Mac Web 빌드 자동화

이 도구는 **이미 Unity와 Web Build Support가 설치된 Mac**에서 실행합니다. Unity Editor를 설치하거나 다운로드하지 않습니다. Linux 소스 작업공간에서는 실행할 수 없습니다.

Unity에서 작업을 저장한 뒤 `Build Web on Mac.command`를 실행합니다. 열려 있는 Unity 프로젝트가 하나면 자동 선택하고, 그렇지 않으면 프로젝트 폴더를 한 번 선택합니다. 터미널 인자로 프로젝트 절대 경로를 넘길 수도 있습니다.

```bash
/bin/bash '/path/to/Build Web on Mac.command' '/path/to/DDES9902_diego'
```

## 수행 작업

- 원본 Assets / Packages / ProjectSettings를 별도 빌드 사본으로 복사합니다. 원본의 씬, 프리팹, .meta, 코드, 패키지와 Library는 수정하거나 삭제하지 않습니다.
- 알려진 이전 코드 버전만 빌드 사본에서 최신 main 코드로 갱신합니다. 별도 로컬 코드 편집을 발견하면 덮어쓰지 않고 중단합니다.
- 기존 Library, TypeDB, Bee, PackageCache를 복사하지 않습니다. 빌드 전용 Bee 경로와 `CleanBuildCache` 옵션으로 타입 캐시 충돌을 배제합니다. NUnit 또는 테스트 패키지를 제거하지 않습니다.
- Web 모듈은 Hub의 `Unity.app` 옆 경로와 앱 내부 경로를 모두 확인합니다. 폴더 탐색만으로 미설치라고 단정하지 않고 설치된 Unity의 API/빌드 로그로 최종 확인합니다.
- `How to be a bartender` 씬 하나를 Web 릴리즈로 빌드합니다. 테스트 어셈블리 포함 옵션은 사용하지 않습니다. 압축은 이 빌드 사본에서 Disabled로 설정합니다.
- Unity 종료 코드, BuildReport, 오류 수, index.html / loader / wasm / data / framework 파일을 확인합니다.
- 성공한 경우에만 바탕화면에 `Bartender_Web_Fixed-날짜.zip`을 만들고 ZIP 무결성과 루트의 index.html을 검사합니다.

## 실패 시

`~/Library/Application Support/DDES9902WebBuild/실행날짜/`에 실제 `Unity-WebBuild.log`, 소스 버전, NUnit DLL 목록과 플레이어 어셈블리 목록이 남습니다. 실패하거나 비어 있는 폴더는 업로드용 ZIP으로 만들지 않습니다. 사본과 로그는 진단을 위해 보존합니다.

이 도구의 작성/정적 검사와 실제 Mac Unity 빌드 성공은 별개입니다. `WEB BUILD SUCCEEDED`와 ZIP 경로가 나온 경우에만 빌드 완료입니다. 브라우저 플레이 검증은 생성된 빌드로 별도 확인해야 합니다.
