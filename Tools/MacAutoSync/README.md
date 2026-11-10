# Mac 코드 자동 동기화

## Documents 접근 거부 복구

`Operation not permitted`가 Documents 아래 프로젝트를 가리키면 Unity Editor를 종료하고 `Repair Auto Sync.command`를 실행합니다. 원본은 유지하고 `~/UnityProjects`에 캐시를 제외한 사본을 만들며, 동기화 경로를 사본으로 변경합니다. 이후 Unity Hub에서 출력된 새 경로를 열어야 합니다. 원본 Documents 프로젝트는 더 이상 자동화 대상이 아닙니다. 복구 도구는 백그라운드 작업의 성공 기록까지 확인한 뒤에만 성공을 표시합니다. 전체 디스크 접근 권한을 설정하지 않습니다.

신규 설치 시에도 `~/UnityProjects` 아래 프로젝트를 사용합니다. Documents, Desktop, Downloads, iCloud Documents는 백그라운드 접근 제한 때문에 거부합니다. macOS 보호 폴더의 원본을 읽기 위해 Terminal이 폴더 접근 허용을 요청할 수 있습니다.

`Install Auto Sync.command`를 Mac에서 한 번 실행하고, 실제로 Unity에서 사용하는 프로젝트 폴더를 선택합니다. 다운로드한 ZIP 안의 새 프로젝트를 선택할 필요는 없습니다. 기존 프로젝트를 선택하면 됩니다. 설치 중 최신 main의 스크립트 5개를 적용하고, 이후 로그인 시 및 60초마다 확인합니다. GitHub main은 GitHub에서 즉시 보이지만 Mac 동기화는 이 최초 설치 후에만 작동합니다.

대상은 Assets/Script의 `Bar intro.cs`, `BarPlayerMovement.cs`, `BeerGlassPour.cs`, `EntranceGuide.cs`, `UniformInteraction.cs`입니다. 씬, 프리팹, .meta, 패키지와 그 외 파일은 변경하지 않습니다. .git이 없는 ZIP 프로젝트도 지원합니다. 대상 프로젝트의 Git 브랜치나 커밋 기록은 변경하지 않습니다.

GitHub 인증은 Mac의 기존 Git/SSH 자격 증명을 사용합니다. 비공개 저장소에 대한 인증이 없으면 로그를 남기고 설치를 중단합니다. 자격 증명이나 프로그램을 설치하지 않습니다. macOS에서 다운로드한 실행 파일의 보안 확인을 요구할 수 있습니다.

로컬 파일은 원본 버전, 현재 원격 버전 또는 마지막으로 적용한 버전과 일치할 때만 갱신합니다. 별도 편집이 있으면 5개 전체를 갱신하지 않고 로그를 남깁니다. 기존 변경 파일은 `~/Library/Application Support/DDES9902AutoSync/backups`에 보존합니다. 동기화는 Unity의 저장하지 않은 메모리 상태를 검사하지 못하므로 씬 저장을 자동 수행하지 않습니다.

중지는 `Uninstall Auto Sync.command`를 실행합니다. 백업과 프로젝트 파일은 삭제하지 않습니다. 로그는 `~/Library/Application Support/DDES9902AutoSync/sync.log`입니다.

Linux에서 셸 구문과 가짜 Git 저장소를 이용한 동기화/로컬 편집 보호 검사를 수행합니다. macOS launchd 설치, Unity 컴파일·실행 검증은 수행하지 않았습니다.
