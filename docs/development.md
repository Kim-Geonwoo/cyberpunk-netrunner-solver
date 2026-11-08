# 개발

`cyberpunk-netrunner-solver` 프로젝트의 구조, 빌드 및 검증 스크립트, 실행 옵션, 데이터 보관 위치에 대한 안내입니다.

## 프로젝트 구성

솔루션은 4개의 프로젝트로 구성되어 있습니다.

- **`NetrunnerSolver.Core`**: 퍼즐 풀이 알고리즘, 화면 인식 결과 해석 및 레이아웃 분석, 사용자 설정 모델, 다국어 화면 문구를 담당하는 OS 무관 라이브러리입니다.
- **`NetrunnerSolver.Windows`**: Windows 전용 기능 라이브러리로, Windows 내장 문자 인식 엔진 연동, 화면 캡처(DXGI 및 GDI), 단축키 폴링 감지, 이미지 파일 입출력을 처리합니다.
- **`NetrunnerSolver.App`**: WPF 기반의 오버레이 창, 조작 패널, 시스템 트레이 아이콘을 포함한 메인 실행 애플리케이션입니다.
- **`tools/NetrunnerSolver.OcrDump`**: 이미지 파일의 문자 인식 결과를 시험 픽스처 JSON 파일로 저장하거나, `--capture` 인자를 통해 주 모니터 화면을 캡처하여 저장하는 진단용 도구입니다.

## 검증과 발행 스크립트

- **`./verify.sh`**:
  - 코드 서식 검사(`dotnet format`)를 수행합니다.
  - 컴파일 경고를 오류로 다루며 빌드(`dotnet build -warnaserror`)합니다.
  - 단위 시험(`dotnet test`)을 실행합니다.
  - Windows 환경에서는 전체 솔루션을 검증하며, Windows가 아닌 환경에서는 OS 무관 프로젝트만 묶은 `ci.slnf`를 대상으로 검증합니다.
- **`./publish.sh`**:
  - `publish/framework-dependent` 디렉터리에 .NET 10 데스크톱 런타임이 필요한 단일 exe를 만듭니다.
  - `publish/self-contained` 디렉터리에 런타임이 포함되어 별도 설치 없이 바로 실행 가능한 단일 exe를 만듭니다.

## 실행 인자

`NetrunnerSolver.App` 실행 시 명령줄 인자를 통해 미리보기 모드나 시뮬레이션 모드를 실행할 수 있습니다.

- **`--preview <이미지> <출력.png> [--lang ko|en]`**:
  창을 띄우지 않고 지정한 이미지 파일에서 퍼즐을 판독하여 풀이 결과를 렌더링한 이미지 파일로 저장한 뒤 종료합니다.
- **`--simulate <이미지>`**:
  실제 게임 대신 주어진 이미지 파일을 화면에 띄우고 실제 오버레이와 패널을 호출하여 퍼즐을 풀어 보는 시뮬레이션 환경을 실행합니다.

## 시험과 픽스처

- 게임 화면 원본 이미지는 저장소에 커밋하지 않습니다.
- 로컬 환경에 해당 원본 이미지 픽스처가 없으면 관련된 시험은 자동으로 건너뜁니다.

## 설정·캡처·로그 위치

- **설정 파일**: `%APPDATA%\cyberpunk-netrunner-solver\settings.json` (단축키 조합, 마지막 버퍼 크기, 인터페이스 언어)
- **캡처 폴더**: `%LOCALAPPDATA%\cyberpunk-netrunner-solver\captures\` (저장된 화면 캡처 이미지)
- **로그 폴더**: `%LOCALAPPDATA%\cyberpunk-netrunner-solver\logs\` (실행 기록 로그 파일, 최근 7일치 보관 후 자동 정리)
