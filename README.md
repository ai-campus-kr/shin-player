<div align="center">

**한국어** · [English](README.en.md) · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)

# 신플레이어

**듣기 편하게 키우고, 자막마다 한 장씩 남기세요.**

세밀한 배속, 음량 증폭, 자막별 일괄 캡처를 갖춘 Windows 영상 플레이어.

**v1.0 정식 출시:** 브라우저형 주소창 · 유튜브 옆 AI 채팅 · 요청하면 해당 구간으로 이동.

**Windows 10/11 x64 · 무료 · 앱 소스 MIT 공개**

</div>

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v1.0.0/docs/screenshots/v1.0.0/04-home.png" width="1000" alt="하단 오른쪽에 선명한 보라색 KAI·초록색 네이버 배지를 고정한 신플레이어">
</p>

| **0.25–8×** | **최대 +12 dB** | **자막 → PNG** | **4가지 UI** |
|:---:|:---:|:---:|:---:|
| 0.05배속 단위로 조절 | 작게 녹음된 소리 증폭 | 자막 구간마다 한 장씩 | 취향에 맞춰 즉시 전환 |

[빠른 시작](#빠른-시작) · [음량 증폭](#작은-소리는-더-크게) · [자막 캡처](#자막마다-사진-한-장) · [UI 선택](#취향대로-고르는-네-가지-ui) · [단축키](#자주-쓰는-단축키) · [개발 및 검증](#개발-및-검증)

## 원하는 구간을 세로 쇼츠로

로컬 영상을 열고 상단 **쇼츠**를 누르세요. **구간 선택 → 세로 구도 → 문구 추가 → 쇼츠 저장**까지 플레이어 안에서 끝납니다.

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v1.0.0/docs/screenshots/v1.0.0/01-shorts-completed.png" width="980" alt="실제 쇼츠 편집창: 세로 장면 미리보기, 드래그 구간 선택, 한글 문구와 화면 맞춤"></p>

1. 타임라인 양 끝을 드래그해 시작·끝을 정합니다. 가운데를 끌면 구간 전체가 이동합니다. 왼쪽 **장면 미리보기** 슬라이더로 선택한 구간의 화면을 확인하세요.
2. **세로로 꽉 채우기**에서는 영상을 드래그해 구도를 조정합니다. **원본 전체 보이기**는 화면을 자르지 않고 검은 여백을 넣습니다.
3. **올릴 문구**에 글자를 입력합니다. 최대 **160자·4줄**, 크기·흰색/노랑/민트·어두운 배경을 선택하고, 미리보기에서 문구를 드래그해 위치를 잡습니다.
4. **쇼츠 저장**을 누릅니다. **9:16**, **1080×1920 / 720×1280**, **30fps H.264 MP4**로 저장하며 **원본 소리 포함**을 끄면 무음으로 만듭니다.

**저장이 끝나면 하단에 형광 라임색 ✓ 쇼츠 저장 완료 표시가 남습니다.** 파일명·길이·용량을 확인하고 **완성된 영상 열기** 또는 **저장 폴더**를 누르세요. 구간·문구·구도를 바꾸면 **설정 변경 · 다시 저장 필요**로 전환되어 이전 파일과 구분됩니다.

- 구간 길이는 **0.2초~3분**. Windows **동영상 → 신플레이어 쇼츠**에 저장합니다. 원본과 기존 결과 파일을 덮어쓰지 않습니다.
- **로컬 영상 전용**이며 자막이나 API 키가 필요하지 않습니다. GPT 토큰을 사용하지 않습니다. 재생 중인 원본의 위치·배속은 그대로 유지합니다.
- 네 가지 UI를 지원하고, 작업 취소나 창 닫기 시 변환을 중단하고 미완성 파일을 정리합니다.

**[이 화면에서 실제로 저장한 MP4 보기](https://github.com/ai-campus-kr/shin-player/releases/download/v1.0.0/ShinPlayer-shorts-demo.mp4)** · 기능 설명용 합성 영상으로 검증한 실제 앱 화면과 결과입니다.

## 마음에 드는 구간을 GIF로

로컬 영상 파일을 열고 **GIF** 버튼으로 움직이는 짤을 만드세요. **타임라인의 양 끝 손잡이를 드래그**해 구간을 고르고 **GIF 만들기**를 누르면 됩니다. 가운데 선택 영역을 끌면 길이를 유지하며 구간을 옮깁니다.

**형광 라임색 ✓ GIF 저장 완료**와 **완성된 GIF 열기** 버튼으로 완료를 바로 확인합니다. 완료 영역은 스크롤 밖에 고정되며 저절로 사라지지 않습니다. 저장 중·취소·실패·설정 변경도 각각 표시합니다.

- **선택 확대 / 전체 보기**로 짧은 구간도 정밀하게 조절합니다. 손잡이에서 방향키는 0.1초, Shift는 1초, Ctrl은 10초씩 이동합니다.
- 시작·끝 시간 직접 입력과 **현재 위치** 버튼도 사용할 수 있으며 타임라인과 서로 연동됩니다.

- **0.25~100배속** 직접 입력과 빠른 선택. **원본 10분 ÷ 10배속 → 1분 GIF**처럼 예상 길이가 바로 표시됩니다.
- 원본 구간 최대 **1시간**, 완성 GIF **0.2초~5분**. 최대 크기 **360 / 480 / 720px**, **10 / 12 / 15 / 20fps**. 무음 반복 재생입니다.
- Windows **사진 → 신플레이어 GIF**에 날짜·영상 이름·구간을 담아 저장합니다. 같은 이름은 덮어쓰지 않습니다.
- **로컬:** 원본 영상 구간을 변환합니다. 재생 위치는 그대로 유지됩니다.
- 자막과 API 키가 없어도 됩니다. **GPT 토큰을 사용하지 않습니다.** FFmpeg는 첫 사용에 준비합니다.

**GIF 만들기는 로컬 영상 파일만 지원합니다. 유튜브 GIF 기능은 제공하지 않습니다.**

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v1.0.0/docs/screenshots/v1.0.0/02-gif-completed.png" width="620" alt="신플레이어 GIF 구간 선택: 드래그 손잡이, 선택 영역 이동, 확대와 시간 입력 연동"></p>

*합성 테스트 영상으로 촬영한 실제 앱 화면입니다.*

## 빠른 시작

1. [최신 릴리스](https://github.com/ai-campus-kr/shin-player/releases/tag/v1.0.0)에서 `ShinPlayer-버전-win-x64.zip`을 받고 압축을 모두 풉니다.
2. `Install.cmd`를 실행합니다. 관리자 권한과 별도 .NET 설치는 필요하지 않습니다.
3. 시작 메뉴의 **신플레이어**를 열고 영상 파일을 끌어놓거나 **영상 열기**를 누릅니다.

첫 설치에는 인터넷으로 재생 엔진을 내려받고 SHA256을 확인합니다. 자막 캡처 도구도 처음 필요할 때 준비합니다. 업데이트는 같은 설치 과정을 거치며 기존 설정과 재생 기록을 보존합니다.

영상 파일을 더블클릭해 열고 싶다면 `⋯ → Windows 기본 앱으로 설정`에서 원하는 확장자의 기본 앱을 신플레이어로 선택하세요. 창을 닫으면 재생을 멈추고 트레이에서 대기합니다.

위의 언어 링크는 README 번역입니다. 현재 앱 화면은 한국어로 제공됩니다.

<details>
<summary><strong>설치 위치 · 기본 앱 · 자동 시작 · 제거 안내</strong></summary>

- GitHub 배포 ZIP의 압축을 모두 풀고 **Install.cmd**를 실행합니다. 관리자 권한과 별도 .NET 설치는 필요하지 않습니다. 첫 설치 시 인터넷으로 고정된 mpv 엔진을 내려받고 SHA256을 확인합니다.
- 설치 위치: `%LOCALAPPDATA%\Programs\ShinPlayer\ShinPlayer.exe`
- 시작 메뉴에서 **신플레이어**를 검색하거나 프로젝트 폴더의 **신플레이어.lnk**를 엽니다.
- 기본 앱: 앱의 `⋯ → Windows 기본 앱으로 설정`에서 신플레이어를 선택합니다. Windows 설정에서 원하는 확장자의 기본 앱을 신플레이어로 지정하면 영상을 더블클릭해 열 수 있습니다.
- 설치 과정은 신플레이어를 기본 앱 후보와 '연결 프로그램'에 등록합니다. 기존 기본 앱 선택(UserChoice)은 변경하지 않습니다.
- Windows 로그인 시 트레이에서 대기합니다. 창의 X를 누르면 재생을 멈추고 트레이로 돌아갑니다. 트레이 아이콘을 더블클릭하면 다시 열립니다.
- 완전 종료: 트레이 우클릭 → **완전히 종료**, 또는 `Ctrl+Q`.
- 자동 시작 및 트레이 동작은 `⋯` 메뉴에서 변경할 수 있습니다.
- 제거: Windows 설정 → 앱 → 설치된 앱 → 신플레이어 → 제거. 최근 재생 기록과 설정은 재설치를 위해 보존됩니다.

</details>

## 작은 소리는 더 크게

음량을 높여도 잘 들리지 않는 영상에는 **증폭**을 켜 보세요. 아래쪽 음량 슬라이더 옆 버튼에서 단계를 고르면, 선택한 값이 바로 표시되고 다음 실행에도 유지됩니다.

`⋯ → 음량 증폭…`에서도 열 수 있습니다.

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/02-boost-menu.png" width="310" alt="음량 증폭 메뉴 — 끄기, +3, +6, +9, +12 dB. 최대 단계 선택 상태">
</p>

**끄기 · 약하게 +3 dB · 보통 +6 dB · 강하게 +9 dB · 최대 +12 dB**

기본 0~100 음량과 별개로 조절하며, 큰 소리는 **리미터**로 제한합니다. 음소거·배속·음높이 유지와 함께 사용할 수 있고, **끄기**를 선택하면 원래 음량으로 돌아갑니다. 기본값은 끄기입니다.

목소리와 배경음이 함께 커지는 **전체 오디오 증폭**입니다. 원본에 이미 들어 있는 잡음이나 찌그러짐을 복구하는 기능은 아닙니다.

## 자막마다 사진 한 장

강의나 학습 영상의 장면을 모아두고 싶을 때, **자막 캡처 → 전체 캡처**를 누르세요. 영상에 들어 있는 텍스트 자막을 기준으로 **각 구간의 중간 장면을 PNG로 저장**합니다. 사진에 자막을 넣을 수도 있습니다.

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/04-capture-completed.png" width="760" alt="자막별 일괄 캡처 실제 완료 화면 — 데모 영상에서 PNG 3장 저장">
</p>

**아래 세 장은 데모 영상에서 실제로 저장한 PNG입니다.** 한국어 자막 세 구간의 1·3·5초 장면을 각각 캡처했습니다.

| 1초 · 첫 번째 자막 | 3초 · 두 번째 자막 | 5초 · 세 번째 자막 |
|:---:|:---:|:---:|
| ![첫 번째 자막의 실제 PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-1.png) | ![두 번째 자막의 실제 PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-2.png) | ![세 번째 자막의 실제 PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-3.png) |

**[6초 데모 영상으로 바로 체험하기](https://github.com/ai-campus-kr/shin-player/releases/download/v0.5.0/ShinPlayer-subtitle-demo.mp4)**

데모 MP4 열기 → **자막 캡처** 또는 `Ctrl+Shift+S` → 한국어 트랙 선택 → **전체 캡처**. Windows **사진** 폴더에 `날짜_영상이름_사진` 폴더와 PNG 세 장이 생깁니다.

재생 위치와 배속을 유지하며 별도 작업으로 처리하고, 진행 중 취소할 수도 있습니다. **내장 텍스트 자막이 있는 영상에서 작동**합니다. 외부 SRT·이미지 자막·영상에 새겨진 글자는 일괄 캡처 대상이 아닙니다.

<details>
<summary><strong>저장 폴더 · 사진 이름 · 자막 표시 · 취소 동작 자세히 보기</strong></summary>

영상을 열고 상단의 **자막 캡처** 또는 `Ctrl+Shift+S`를 누릅니다. `⋯ → 자막별 일괄 캡처…`와 자막 메뉴에서도 열 수 있습니다. 캡처할 내장 자막 트랙을 고른 뒤 **전체 캡처**를 누르면 한 번에 저장합니다.

- 자막 구간마다 **중간 시점의 PNG 한 장**을 저장합니다. 여러 줄로 된 한 자막은 한 장이며, 여러 언어를 섞지 않고 선택한 트랙만 처리합니다.
- Windows의 실제 **사진 폴더** 안에 `2026-09-20_영상이름_사진` 형태의 폴더를 만듭니다. 사진 폴더를 OneDrive 등으로 옮겼어도 해당 위치를 사용합니다.
- 사진 이름은 `00001_00-01-23-450.png`처럼 **순번_시-분-초-밀리초**입니다. 같은 이름의 폴더가 있으면 `(2)`, `(3)`을 붙여 이전 사진을 보존합니다.
- **사진에 자막 포함**이 기본값입니다. 해제하면 영상만 저장합니다. 자막은 공통 글꼴로 표시하며, 창을 열 때의 자막 싱크 보정을 반영합니다. ASS의 원래 꾸밈·배치가 완전히 보존되는 기능은 아닙니다.
- 진행 수와 저장 위치를 표시하며 **취소**할 수 있습니다. 이미 저장한 PNG는 남겨 두고, `캡처목록.json`에 각 사진의 자막·시간과 완료/취소 상태를 기록합니다. 완료 후 **저장 폴더 열기**로 확인합니다.
- MP4의 내장 텍스트 자막, MKV의 SRT/ASS 등 **텍스트 자막**을 지원합니다. 외부 자막, 이미지 자막, 화면에 새겨진 글자만 있는 영상은 지원하지 않습니다. 자막이 없으면 안내하고 캡처하지 않습니다.
- 재생과 독립된 작업으로 진행하며 플레이어의 위치·배속을 바꾸지 않습니다. 긴 영상이나 4K 영상에서는 처리 시간과 저장 공간이 많이 필요할 수 있습니다.
- **GPT·API 키·토큰 비용이 필요 없습니다.** 로컬 FFmpeg/FFprobe를 사용합니다. 도구가 없을 때만 첫 실행에 고정 버전의 도구를 원본 배포처에서 내려받아 SHA256을 확인합니다. 영상과 자막은 업로드하지 않습니다.

</details>

## 취향대로 고르는 네 가지 UI

상단 **UI 선택**에서 원하는 화면을 고르세요. 색감부터 컨트롤 배치, 재생목록 위치까지 달라집니다. 기본 UI는 미니멀입니다.

`⋯ → UI 선택…`에서도 열 수 있습니다.

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/03-ui-picker.png" width="760" alt="신플레이어 UI 선택 창 — 미니멀, 스튜디오, 라이트, 라임">
</p>

| UI | 이런 화면을 좋아한다면 |
|---|---|
| **미니멀** | 모노톤, 넓은 영상 영역, 아래쪽 타임라인 |
| **스튜디오** | 왼쪽 재생목록, 큰 타임코드, 촘촘한 컨트롤 |
| **라이트** | 밝은 표면, 파란 포인트, 여유 있는 배치 |
| **라임** | 짙은 배경, 라임 포인트, 카드형 컨트롤 |

재생 중 전환해도 **영상 위치·배속·일시정지 상태를 유지**합니다. 선택은 다음 실행에도 적용되고, 열려 있는 자막 캡처 창의 색상도 함께 바뀝니다. UI를 바꾸는 순간 진행 중이던 재생 바 드래그는 취소합니다.

<sub>화면은 0.5.0의 실제 UI 렌더이며, PNG 예시는 데모 영상에서 저장한 원본입니다. <a href="https://github.com/ai-campus-kr/shin-player/blob/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/README.md">이미지 출처와 제작 방식</a></sub>

## 재생에 필요한 기능도 함께

- **0.25~8배속**, **0.05배속 미세 조절**, 배속 직접 입력, 음높이 유지
- 클릭·드래그로 원하는 시간 이동, 프레임 이동, **A–B 구간 반복**, 현재 영상 반복
- 재생목록, 자동 다음 영상, 최근 영상, 마지막 위치 이어보기
- 외부 자막, 자막·오디오 트랙 전환, 싱크 조절, 한 장씩 화면 저장
- 파일·폴더 드래그 앤 드롭, 한글 경로, 단일 인스턴스, 트레이 대기
- MP4·MKV·AVI·MOV·WMV·WebM 등 일반 로컬 영상 및 오디오 재생

<details>
<summary><strong>지원 형식과 재생 범위 자세히 보기</strong></summary>

- 선택 가능한 네 가지 UI, 넓은 영상 영역, 한눈에 조절하는 배속 프리셋. 창 크기와 재생목록 상태에 맞춰 배치를 조정합니다.
- MP4, MKV, AVI, MOV, WMV, WebM, TS, MTS, M2TS, FLV 등 30개 영상 확장자를 Windows에 등록합니다. 오디오 파일도 직접 열 수 있습니다.
- H.264, HEVC, AV1, VP9 등은 mpv/FFmpeg로 디코딩하며, 지원 GPU에서는 D3D11 하드웨어 디코딩을 사용합니다. 하드웨어 미지원 코덱은 소프트웨어로 재생합니다.
- **0.25~8배속**, 프리셋, **0.05배속 미세 조절**, 배속 숫자를 클릭해 직접 입력, 음높이 유지.
- 작게 녹음된 영상의 소리를 키우는 **음량 증폭**, 최대 **+12 dB**, 큰 소리를 제한하는 리미터.
- 재생 바의 클릭·드래그 위치에 맞춘 정확한 시간 탐색, 프레임 이동, A–B 구간 반복, 현재 영상 반복.
- 재생목록, 다음 영상 자동 재생, 한글 경로, 최근 영상, 마지막 위치 이어보기.
- 외부 자막, 자동 자막 탐색, 자막 및 오디오 트랙 전환, 싱크 조절, 화면 캡처.
- 내장 텍스트 자막의 구간마다 PNG 한 장을 저장하는 **자막별 일괄 캡처**.
- 파일/폴더 드래그 앤 드롭. 폴더에서는 바로 아래의 미디어 파일을 이름순으로 추가합니다.
- 여러 번 실행해도 기존 프로세스에 파일을 전달하는 단일 인스턴스 방식입니다.

DRM으로 보호된 영상, 손상된 파일, 특수한 독점 형식까지 모두 재생된다고 보장하지는 않습니다. 고배속의 실제 처리량은 해상도·코덱·디스크·GPU 성능에 따라 달라집니다. 일반 로컬 영상 재생을 대상으로 만든 첫 버전이며, HDR 색상 정확도와 장시간 4K/8K 재생은 별도 검증이 필요합니다.

</details>

## 자주 쓰는 단축키

| 키 | 기능 |
|---|---|
| `Space` | 재생 / 일시정지 |
| `←` / `→` | 5초 이동 |
| `[` / `]` | 0.25배속 조절 |
| `Shift+[` / `Shift+]` | 0.05배속 미세 조절 |
| `F` / `F11` | 전체화면 |
| `A` | A–B 반복 시작 → 끝 → 해제 |
| `Alt+D` / `Ctrl+U` | 유튜브 주소창 선택 |
| `Ctrl+J` | 로컬 영상 AI 채팅 |
| `Ctrl+S` | 현재 화면 PNG 저장 |
| `Ctrl+Shift+S` | 내장 자막별 일괄 캡처 |

<details>
<summary><strong>전체 단축키 보기</strong></summary>

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

</details>

## 유튜브와 영상 AI 채팅

**“이 내용 설명해줘” → 스크립트로 답변. “그 부분으로 이동해줘” → 재생 위치 이동.** [v1.0 다운로드](https://github.com/ai-campus-kr/shin-player/releases/tag/v1.0.0)

1. 항상 표시되는 상단 **주소창**에 유튜브 주소를 입력하고 **Enter** 또는 **→**를 누릅니다. **Alt+D / Ctrl+U**로 주소를 선택할 수 있으며, `youtube.com`처럼 `https://`를 생략해도 됩니다. 앱 내부의 **Edge WebView2 브라우저**로 일반 유튜브 페이지를 엽니다.
2. **영상을 열면 자막을 자동으로 불러옵니다.** 자막이 없으면 <strong>“자막이 없는 영상입니다”</strong>라고 표시합니다. 채팅은 넓은 창에서 오른쪽, 좁은 창에서 아래쪽에 표시되며 **채팅 배치**에서 위치를 선택할 수 있습니다. 기존 스크립트 창과 새 **동영상 정보 → 스크립트** 화면을 모두 지원합니다.
3. **API 설정** 창에서 본인의 OpenAI 키를 저장하세요. 초록색 **✓ API 키 저장됨**으로 저장을 확인하고, **연결 확인**으로 실제 API 연결을 시험할 수 있습니다. 성공하면 **✓ API 연결 확인됨**이 표시됩니다. 일반 질문은 재생 위치를 유지하며, **“그 부분으로 이동해줘”** 또는 근거 시간 버튼으로 이동합니다.

로컬 영상은 **AI / Ctrl+J**를 열면 내장 텍스트 자막을 자동으로 읽습니다. 여러 트랙이 있으면 한국어 → 기본 트랙 → 첫 트랙 순서로 선택합니다. 외부 SRT·이미지 자막·OCR·음성 받아쓰기는 사용하지 않습니다.

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.9/docs/screenshots/v0.6.0-beta.9/01-api-settings.png" width="430" alt="API key saved status"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.9/docs/screenshots/v0.6.0-beta.9/02-chat-ready.png" width="430" alt="Automatic subtitles and chat"></p>

<sub>실제 앱에서 격리된 테스트 키와 합성 자막으로 캡처한 설정·채팅 화면입니다. 실제 API 키는 표시하지 않습니다.</sub>

- 모델은 `gpt-5.4-mini`입니다. 키는 Windows 사용자 계정으로 암호화해 `%LOCALAPPDATA%\ShinPlayer\openai-key.dat`에 저장하며 앱에서 삭제할 수 있습니다.
- 자막 자동 확인에는 API 키나 토큰이 필요하지 않습니다. **연결 확인**은 영상·자막 없이 짧은 요청만 보내며 소량의 토큰을 사용합니다.
- 질문할 때 **질문·직전 질문과 답변·영상 이름·선택한 자막 텍스트**를 OpenAI로 전송합니다. 영상·음성 파일은 보내지 않습니다. API 비용과 한도는 본인 OpenAI 계정에 적용됩니다. 요청마다 입력/출력 토큰 수를 표시합니다.
- 긴 자막은 로컬 검색으로 후보를 고르며 행별 부가 정보를 포함해 최대 60,000자 예산 안에서 선택합니다. **부분 검색** 안내가 표시되며 일부 문맥을 놓칠 수 있습니다.
- 근거 없음·잘못된 응답·취소·창 닫기·영상 변경 후 응답은 재생 위치를 바꾸지 않습니다. 광고 재생 중에는 이동하지 않습니다.
- 자막은 채팅 창의 메모리에 보관하며 별도 대화 기록 파일을 만들지 않습니다. 웹 로그인과 쿠키는 `%LOCALAPPDATA%\ShinPlayer\youtube-browser`의 앱 전용 프로필에 저장합니다. Chrome·Edge·Whale의 기존 로그인과 확장 프로그램은 가져오지 않습니다.

WebView2 Runtime이 없으면 Microsoft 공식 설치 안내를 표시합니다. 유튜브 로그인·지역·영상 제한은 적용되며 **외부 열기**로 외부 브라우저를 사용할 수 있습니다. 외부 창에는 AI 이동이 연결되지 않습니다. 웹 영상은 유튜브 자체 컨트롤을 쓰며 로컬 mpv의 배속·음량 증폭·일괄 캡처 설정을 적용하지 않습니다. 영상 스트림 추출이나 다운로드 도구는 사용하지 않습니다.

**검증 범위:** 95개 통합 검사로 키 저장·연결 상태, 자막 자동 로딩·없음·영상 변경, 채팅 배치와 로컬 GIF를 확인했습니다. 실제 YouTube 자동 자막·GPT-5.4-mini 답변·구간 이동은 앞선 베타 개발에서 별도로 검증했습니다. v1.0 회귀 검사에는 대체 API 응답과 격리된 WebView2 페이지를 사용했습니다. 자막의 제공 여부와 사이트 변경에 따라 일부 영상은 지원되지 않을 수 있습니다.

음량 증폭과 로컬 자막 캡처는 계속 **API 키·토큰 비용 없이 PC에서 처리**합니다.

## 개발 및 검증

C# / .NET 8 WPF와 mpv로 만든 Windows 네이티브 앱입니다. 로컬 재생은 mpv, 선택적 유튜브 기능은 Edge WebView2를 사용하며 개발 서버 없이 실행됩니다.

v1.0은 WPF/libmpv와 격리된 WebView2를 사용하는 **95개 통합 검사**로 검증합니다. API 검사는 대체 응답을 사용하며 실제 서비스 검증 한계는 위에 명시했습니다. [변경 기록](CHANGELOG.md)

<details>
<summary><strong>소스 빌드 및 설치</strong></summary>

.NET 8 SDK, Windows x64, PowerShell, Windows 기본 `tar.exe`가 필요합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

`build.ps1`은 날짜와 SHA256을 고정한 mpv 아카이브를 검증하고, .NET 런타임을 포함하는 설치 없는 실행 폴더 `dist\ShinPlayer`를 만듭니다. 설치 스크립트는 현재 사용자 계정에만 설치하며 관리자 권한이 필요하지 않습니다. `-NoStartup`, `-NoLaunch` 옵션을 지원합니다.

`scripts\install.ps1`을 다시 실행하면 기존 신플레이어를 종료하고 업데이트합니다. 재생 설정, 기록, 기존 자동 시작 선택은 유지됩니다. 프로젝트 소스는 설치 폴더의 `ShinPlayer-source.zip`에도 포함됩니다.

</details>

<details>
<summary><strong>GitHub 배포 파일 만들기</strong></summary>

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
```

`dist/release`에 설치용 ZIP, 소스 ZIP, `SHA256SUMS.txt`가 생성됩니다. 설치용 ZIP은 신플레이어와 .NET 런타임을 포함하고 mpv 바이너리는 포함하지 않습니다. 설치 스크립트가 원본 엔진 릴리스에서 직접 다운로드합니다. 개인 기록, 로그, 영상, 개발용 경로, 바로가기는 패키지에 포함하지 않습니다.

공개 릴리스에는 `dist/release`의 세 파일을 사용하세요. 개발용 `dist/ShinPlayer`는 엔진을 포함하므로 그대로 업로드하지 않습니다. 자세한 출처와 배포 구분은 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)에 있습니다. `.github/workflows/build.yml`은 Windows에서 컴파일과 패키징을 수행하며, 자동 공개 게시나 GPU 통합 검사는 수행하지 않습니다.

</details>

<details>
<summary><strong>검증</strong></summary>

Python 및 FFmpeg가 설치된 환경에서:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

합성 영상을 만들고 실제 WPF 창과 libmpv에서 재생합니다. 한글 파일 경로, 8개 영상 컨테이너/코덱 조합, FLAC 오디오, 배속 범위, 음높이 보정, seek, pause, 음량, A–B 반복, 한글 SRT, 프레임 이동, 전체화면, 재생목록, 오류 복구, 자동 다음 영상, 이어보기, 트레이 정지와 재열기를 검사합니다. 결과는 `artifacts\verification\results.json`, 복호화한 프레임은 `decoded-*.png`, UI 렌더는 `01-home.png` 등에 저장됩니다. WPF UI 렌더는 별도 HWND로 그리는 영상 영역을 포함하지 않습니다.

측정은 합성 640×360 영상과 이 PC를 기준으로 합니다. `loadMs`는 파일 열기 요청에서 mpv의 file-loaded까지이며 화면 첫 프레임이 보이는 전체 지연과 다릅니다. seek 측정은 playback-restart 이벤트까지 기다립니다. 이 결과만으로 Windows 기본 플레이어보다 몇 배 빠르다고 주장하지 않습니다.

자막 캡처 검사는 별도의 합성 MP4/MKV로 수행합니다. 내장 자막 트랙 선택, 자막 구간 중간 시점의 실제 프레임 색상, PNG의 한글 자막, 자막 제외 옵션, 중복 저장, 취소 시 부분 결과 보존, 실행 중인 작업 프로세스 종료, 재생 위치 유지와 캡처 창을 확인합니다. 검사용 이미지는 `artifacts` 아래에만 저장합니다.

총 95개 통합 검사를 실행합니다. 탐색 좌표는 두 창 크기와 재생목록 상태별 28개 지점, 레이아웃 갱신 전 반복 입력, 놓기 위치, 캡처 해제, 파일 전환과 전체화면을 포함합니다. 1~2배 WPF 레이아웃 변환도 검사하지만 물리적인 마우스 입력이나 실제 모니터 DPI 변경을 자동화한 검사는 아닙니다. UI는 네 가지 스타일의 시작 화면·최소 창·재생목록·캡처 창을 렌더하고 버튼 겹침과 자막 선택 표시를 확인합니다. 설정 직렬화와 이전 설정 호환, 선택 버튼, 재생 중 스타일 전환과 위치·배속 유지도 검사합니다.

음량 증폭 검사는 합성 PCM 음원을 음소거 상태로 재생하고 mpv 필터 출력의 피크·RMS 레벨을 측정합니다. 모든 증폭 단계의 실제 게인, 최대 증폭 시 리미터 한도, 끄기 시 원래 진폭 복원, 초기 설정 복원, 빠른 연속 조절과 다른 필터 보존을 확인합니다.

</details>

<details>
<summary><strong>구현 및 데이터</strong></summary>

- `MpvPlayer.cs`: UTF-8 P/Invoke, 비동기 명령, 이벤트 전용 스레드, idle 시 이벤트 대기.
- `MainWindow.xaml(.cs)`: 영상은 native HWND로 직접 출력. WPF 컨트롤은 영상 바깥에서 그립니다. 슬라이더 탐색은 드래그 끝에 실행합니다.
- `Program.cs` / `App.xaml.cs`: WPF를 생성하기 전에 파일 요청을 기존 앱에 전달합니다. 사용자별 뮤텍스와 현재 사용자만 접근 가능한 named pipe를 사용합니다. 로그인 시 화면에 나타나지 않게 창과 재생 엔진을 미리 준비합니다.
- `WindowsIntegration.cs`: HKCU 앱 등록, 30개 영상 확장자, 자동 시작, Windows 기본 앱 설정 링크.
- 설정/최근 40개 파일/이어보기: `%LOCALAPPDATA%\ShinPlayer\settings.json`. `⋯ → 최근 기록 지우기`로 삭제할 수 있습니다.
- 오류 로그: 같은 폴더의 `player.log`, 2MB 단위 순환. 파일은 외부로 업로드하지 않습니다.
- `SubtitleCapture.cs` / `SubtitleCaptureWindow.cs` / `CaptureTools.cs`: 내장 자막 추출, 독립된 프레임 캡처, 진행·취소 UI. 도구 캐시는 `%LOCALAPPDATA%\ShinPlayer\capture-tools`에 저장됩니다.

</details>

## 라이선스와 출처

- [mpv 공식 안내](https://mpv.io/) / [mpv 매뉴얼](https://mpv.io/manual/master/)
- [mpv 공식 다운로드 안내에 연결된 Windows 빌드](https://mpv.io/installation/)
- [Microsoft 기본 앱 등록 및 설정](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/default-apps-platform)
- [앱별 기본 앱 설정 URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-apps-settings)

신플레이어 자체 소스는 [MIT 라이선스](LICENSE)로 공개합니다. Copyright (c) 2026 한국AI교육진흥원. mpv·FFmpeg·.NET 등 외부 구성요소에는 각자의 라이선스가 적용됩니다. 엔진과 런타임의 출처 및 배포 구분은 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)를 참조하세요.

---

[한국AI교육진흥원](https://github.com/ai-campus-kr) · [최신 릴리스](https://github.com/ai-campus-kr/shin-player/releases/tag/v1.0.0) · [오류 제보와 기능 제안](https://github.com/ai-campus-kr/shin-player/issues)
