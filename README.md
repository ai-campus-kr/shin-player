# 신플레이어

빠른 조작과 세밀한 배속 재생을 위한 Windows 10/11 x64 영상 플레이어입니다. C# / .NET 8 WPF로 만든 네이티브 앱에 mpv를 내장했습니다. 브라우저나 개발 서버 없이 실행됩니다.

[최신 버전 다운로드](https://github.com/ai-campus-kr/shin-player/releases/latest) · [한국AI교육진흥원](https://github.com/ai-campus-kr) · [변경 기록](CHANGELOG.md)

## 실행 및 기본 앱 지정

- GitHub 배포 ZIP의 압축을 모두 풀고 **Install.cmd**를 실행합니다. 관리자 권한과 별도 .NET 설치는 필요하지 않습니다. 첫 설치 시 인터넷으로 고정된 mpv 엔진을 내려받고 SHA256을 확인합니다.
- 설치 위치: `%LOCALAPPDATA%\Programs\ShinPlayer\ShinPlayer.exe`
- 시작 메뉴에서 **신플레이어**를 검색하거나 프로젝트 폴더의 **신플레이어.lnk**를 엽니다.
- 기본 앱: 앱의 `⋯ → Windows 기본 앱으로 설정`에서 신플레이어를 선택합니다. Windows 설정에서 원하는 확장자의 기본 앱을 신플레이어로 지정하면 영상을 더블클릭해 열 수 있습니다.
- 설치 과정은 신플레이어를 기본 앱 후보와 '연결 프로그램'에 등록합니다. 기존 기본 앱 선택(UserChoice)은 변경하지 않습니다.
- Windows 로그인 시 트레이에서 대기합니다. 창의 X를 누르면 재생을 멈추고 트레이로 돌아갑니다. 트레이 아이콘을 더블클릭하면 다시 열립니다.
- 완전 종료: 트레이 우클릭 → **완전히 종료**, 또는 `Ctrl+Q`.
- 자동 시작 및 트레이 동작은 `⋯` 메뉴에서 변경할 수 있습니다.
- 제거: Windows 설정 → 앱 → 설치된 앱 → 신플레이어 → 제거. 최근 재생 기록과 설정은 재설치를 위해 보존됩니다.

## 재생 기능

- MP4, MKV, AVI, MOV, WMV, WebM, TS, MTS, M2TS, FLV 등 30개 영상 확장자를 Windows에 등록합니다. 오디오 파일도 직접 열 수 있습니다.
- H.264, HEVC, AV1, VP9 등은 mpv/FFmpeg로 디코딩하며, 지원 GPU에서는 D3D11 하드웨어 디코딩을 사용합니다. 하드웨어 미지원 코덱은 소프트웨어로 재생합니다.
- **0.25~8배속**, 프리셋, **0.05배속 미세 조절**, 배속 숫자를 클릭해 직접 입력, 음높이 유지.
- 정확한 시간 탐색, 프레임 이동, A–B 구간 반복, 현재 영상 반복.
- 재생목록, 다음 영상 자동 재생, 한글 경로, 최근 영상, 마지막 위치 이어보기.
- 외부 자막, 자동 자막 탐색, 자막 및 오디오 트랙 전환, 싱크 조절, 화면 캡처.
- 내장 텍스트 자막의 구간마다 PNG 한 장을 저장하는 **자막별 일괄 캡처**.
- 파일/폴더 드래그 앤 드롭. 폴더에서는 바로 아래의 미디어 파일을 이름순으로 추가합니다.
- 여러 번 실행해도 기존 프로세스에 파일을 전달하는 단일 인스턴스 방식입니다.

DRM으로 보호된 영상, 손상된 파일, 특수한 독점 형식까지 모두 재생된다고 보장하지는 않습니다. 고배속의 실제 처리량은 해상도·코덱·디스크·GPU 성능에 따라 달라집니다. 일반 로컬 영상 재생을 대상으로 만든 첫 버전이며, HDR 색상 정확도와 장시간 4K/8K 재생은 별도 검증이 필요합니다.

## 자막별 일괄 캡처

영상을 열고 **`⋯ → 자막별 일괄 캡처…`** 또는 **`Ctrl+Shift+S`**를 누릅니다. 자막 메뉴에서도 열 수 있습니다. 캡처할 내장 자막 트랙을 고른 뒤 **전체 캡처**를 누르면 한 번에 저장합니다.

- 자막 구간마다 **중간 시점의 PNG 한 장**을 저장합니다. 여러 줄로 된 한 자막은 한 장이며, 여러 언어를 섞지 않고 선택한 트랙만 처리합니다.
- Windows의 실제 **사진 폴더** 안에 `2026-09-20_영상이름_사진` 형태의 폴더를 만듭니다. 사진 폴더를 OneDrive 등으로 옮겼어도 해당 위치를 사용합니다.
- 사진 이름은 `00001_00-01-23-450.png`처럼 **순번_시-분-초-밀리초**입니다. 같은 이름의 폴더가 있으면 `(2)`, `(3)`을 붙여 이전 사진을 보존합니다.
- **사진에 자막 포함**이 기본값입니다. 해제하면 영상만 저장합니다. 자막은 공통 글꼴로 표시하며, 창을 열 때의 자막 싱크 보정을 반영합니다. ASS의 원래 꾸밈·배치가 완전히 보존되는 기능은 아닙니다.
- 진행 수와 저장 위치를 표시하며 **취소**할 수 있습니다. 이미 저장한 PNG는 남겨 두고, `캡처목록.json`에 각 사진의 자막·시간과 완료/취소 상태를 기록합니다. 완료 후 **저장 폴더 열기**로 확인합니다.
- MP4의 내장 텍스트 자막, MKV의 SRT/ASS 등 **텍스트 자막**을 지원합니다. 외부 자막, 이미지 자막, 화면에 새겨진 글자만 있는 영상은 지원하지 않습니다. 자막이 없으면 안내하고 캡처하지 않습니다.
- 재생과 독립된 작업으로 진행하며 플레이어의 위치·배속을 바꾸지 않습니다. 긴 영상이나 4K 영상에서는 처리 시간과 저장 공간이 많이 필요할 수 있습니다.
- **GPT·API 키·토큰 비용이 필요 없습니다.** 로컬 FFmpeg/FFprobe를 사용합니다. 도구가 없을 때만 첫 실행에 고정 버전의 도구를 원본 배포처에서 내려받아 SHA256을 확인합니다. 영상과 자막은 업로드하지 않습니다.

## AI 기능 개발 조건 (아직 미구현)

현재 배포 버전에는 AI 분석이나 OpenAI API 연동이 없습니다. 향후 AI 검색·요약은 다음 조건으로 구현합니다.

- OpenAI API 모델은 **`gpt-5.4-mini`**를 사용합니다.
- 영상 파일 자체에 포함된 **내장 텍스트 자막**을 추출할 수 있을 때만 AI 기능을 활성화합니다. 자막 표시를 꺼 두어도 내장 텍스트 자막이 있으면 지원 대상입니다.
- 외부 SRT 등 별도 자막 파일, 자동으로 불러온 외부 자막, 이미지 형식의 내장 자막, 화면에 새겨진 자막은 AI 분석 대상에서 제외합니다.
- 지원하는 내장 텍스트 자막이 없거나 추출에 실패하면 AI 기능을 비활성화하고 이유를 안내합니다. 이 경우 OpenAI API를 호출하지 않으며 음성 받아쓰기나 OCR로 대체하지 않습니다.
- 이 조건은 AI 기능에만 적용합니다. 일반 영상 재생과 기존 자막 표시 기능은 그대로 사용할 수 있습니다.

## 단축키

| 키 | 기능 |
|---|---|
| Space | 재생 / 일시정지 |
| ← / → | 5초 이동 |
| Shift+← / → | 30초 이동 |
| ↑ / ↓, 영상 위 마우스 휠 | 음량 |
| [ / ] | 0.25배속 조절 |
| Shift+[ / ] | 0.05배속 조절 |
| R 또는 Backspace | 1배속 복귀 |
| F / F11, 영상 더블클릭 | 전체화면 |
| Esc | 전체화면 해제 |
| A | 반복 시작 → 끝 → 해제 |
| . / , | 다음 / 이전 프레임 |
| M / S | 음소거 / 자막 표시 |
| N / Shift+N | 다음 / 이전 영상 |
| Ctrl+O / Ctrl+L | 파일 열기 / 재생목록 |
| Ctrl+S | 사진 폴더의 `신플레이어`에 PNG 저장 |
| Ctrl+Shift+S | 내장 자막별 일괄 캡처 |
| Ctrl+Q | 완전히 종료 |

## 소스 빌드 및 설치

.NET 8 SDK, Windows x64, PowerShell, Windows 기본 `tar.exe`가 필요합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

`build.ps1`은 날짜와 SHA256을 고정한 mpv 아카이브를 검증하고, .NET 런타임을 포함하는 설치 없는 실행 폴더 `dist\ShinPlayer`를 만듭니다. 설치 스크립트는 현재 사용자 계정에만 설치하며 관리자 권한이 필요하지 않습니다. `-NoStartup`, `-NoLaunch` 옵션을 지원합니다.

`scripts\install.ps1`을 다시 실행하면 기존 신플레이어를 종료하고 업데이트합니다. 재생 설정, 기록, 기존 자동 시작 선택은 유지됩니다. 프로젝트 소스는 설치 폴더의 `ShinPlayer-source.zip`에도 포함됩니다.

## GitHub 배포 파일 만들기

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
```

`dist/release`에 설치용 ZIP, 소스 ZIP, `SHA256SUMS.txt`가 생성됩니다. 설치용 ZIP은 신플레이어와 .NET 런타임을 포함하고 mpv 바이너리는 포함하지 않습니다. 설치 스크립트가 원본 엔진 릴리스에서 직접 다운로드합니다. 개인 기록, 로그, 영상, 개발용 경로, 바로가기는 패키지에 포함하지 않습니다.

공개 릴리스에는 `dist/release`의 세 파일을 사용하세요. 개발용 `dist/ShinPlayer`는 엔진을 포함하므로 그대로 업로드하지 않습니다. 자세한 출처와 배포 구분은 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)에 있습니다. `.github/workflows/build.yml`은 Windows에서 컴파일과 패키징을 수행하며, 자동 공개 게시나 GPU 통합 검사는 수행하지 않습니다.

## 검증

Python 및 FFmpeg가 설치된 환경에서:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

합성 영상을 만들고 실제 WPF 창과 libmpv에서 재생합니다. 한글 파일 경로, 8개 영상 컨테이너/코덱 조합, FLAC 오디오, 배속 범위, 음높이 보정, seek, pause, 음량, A–B 반복, 한글 SRT, 프레임 이동, 전체화면, 재생목록, 오류 복구, 자동 다음 영상, 이어보기, 트레이 정지와 재열기를 검사합니다. 결과는 `artifacts\verification\results.json`, 복호화한 프레임은 `decoded-*.png`, UI 렌더는 `01-home.png` 등에 저장됩니다. WPF UI 렌더는 별도 HWND로 그리는 영상 영역을 포함하지 않습니다.

측정은 합성 640×360 영상과 이 PC를 기준으로 합니다. `loadMs`는 파일 열기 요청에서 mpv의 file-loaded까지이며 화면 첫 프레임이 보이는 전체 지연과 다릅니다. seek 측정은 playback-restart 이벤트까지 기다립니다. 이 결과만으로 Windows 기본 플레이어보다 몇 배 빠르다고 주장하지 않습니다.

자막 캡처 검사는 별도의 합성 MP4/MKV로 수행합니다. 내장 자막 트랙 선택, 자막 구간 중간 시점의 실제 프레임 색상, PNG의 한글 자막, 자막 제외 옵션, 중복 저장, 취소 시 부분 결과 보존, 실행 중인 작업 프로세스 종료, 재생 위치 유지와 캡처 창을 포함해 총 45개 통합 검사를 실행합니다. 검사용 이미지는 `artifacts` 아래에만 저장합니다.

## 구현 및 데이터

- `MpvPlayer.cs`: UTF-8 P/Invoke, 비동기 명령, 이벤트 전용 스레드, idle 시 이벤트 대기.
- `MainWindow.xaml(.cs)`: 영상은 native HWND로 직접 출력. WPF 컨트롤은 영상 바깥에서 그립니다. 슬라이더 탐색은 드래그 끝에 실행합니다.
- `Program.cs` / `App.xaml.cs`: WPF를 생성하기 전에 파일 요청을 기존 앱에 전달합니다. 사용자별 뮤텍스와 현재 사용자만 접근 가능한 named pipe를 사용합니다. 로그인 시 화면에 나타나지 않게 창과 재생 엔진을 미리 준비합니다.
- `WindowsIntegration.cs`: HKCU 앱 등록, 30개 영상 확장자, 자동 시작, Windows 기본 앱 설정 링크.
- 설정/최근 40개 파일/이어보기: `%LOCALAPPDATA%\ShinPlayer\settings.json`. `⋯ → 최근 기록 지우기`로 삭제할 수 있습니다.
- 오류 로그: 같은 폴더의 `player.log`, 2MB 단위 순환. 파일은 외부로 업로드하지 않습니다.
- `SubtitleCapture.cs` / `SubtitleCaptureWindow.cs` / `CaptureTools.cs`: 내장 자막 추출, 독립된 프레임 캡처, 진행·취소 UI. 도구 캐시는 `%LOCALAPPDATA%\ShinPlayer\capture-tools`에 저장됩니다.

## 근거 및 라이선스

- [mpv 공식 안내](https://mpv.io/) / [mpv 매뉴얼](https://mpv.io/manual/master/)
- [mpv 공식 다운로드 안내에 연결된 Windows 빌드](https://mpv.io/installation/)
- [Microsoft 기본 앱 등록 및 설정](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/default-apps-platform)
- [앱별 기본 앱 설정 URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-apps-settings)

신플레이어 자체 소스는 [MIT 라이선스](LICENSE)로 공개합니다. Copyright (c) 2026 한국AI교육진흥원. mpv·FFmpeg·.NET 등 외부 구성요소에는 각자의 라이선스가 적용됩니다. 엔진과 런타임의 출처 및 배포 구분은 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)를 참조하세요.
