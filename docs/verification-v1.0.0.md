# v1.0.0 검증·코드 정리 기록

2026-09-22, Windows x64 / .NET 8 WPF. v1.0 소스의 자체 포함 배포 빌드에서 **95개 통합 검사 통과, 실패 0, 프로세스 종료 코드 0, 완료 표식 PASS**를 확인했습니다.

## 완료 안내

- 쇼츠·GIF의 실제 파일 저장이 끝난 뒤 형광 라임색 체크, 파일명·길이·용량, 완성 파일 열기 버튼을 표시합니다.
- 완료 영역은 스크롤 밖에 고정됩니다. 네 가지 테마와 최소 창 크기에서 화면 안에 남는지 확인했습니다.
- 저장 중·취소 중·취소·실패·완료 후 설정 변경을 구분합니다. 실패 후 재시도, 창을 닫을 때 작업 종료·미완성 파일 정리, 늦은 진행 메시지가 완료를 덮지 않는 동작을 검사했습니다.
- [실제 화면과 제작 방식](screenshots/v1.0.0/README.md)

## 코드·배포 점검

| 점검 영역 | 결과 |
|---|---|
| 완료 UI | `ExportFeedback`을 GIF·쇼츠에서 함께 사용해 상태·색·안내 처리의 중복을 줄였습니다. |
| GIF 구조 | 구현이 하나뿐인 `GifSource` 추상 계층을 없애고 로컬 `LocalGifSource`로 연결했습니다. |
| 결과 파일 저장 | 파일명 정리와 덮어쓰기 없는 최종 파일 이동을 `ExportFiles`로 모았습니다. 같은 이름을 쓰는 동시 저장 4건과 기존 파일의 내용 보존을 검사했습니다. |
| 반복 처리 | 사용하지 않는 재생 정보 조회를 제거했습니다. 기존의 mpv 이벤트 대기, UI 갱신 합치기, 180ms 미리보기 요청 지연·취소, 문구 비트맵 재사용을 확인했습니다. |
| 메모리·종료 | GIF는 두 번의 FFmpeg 처리로 전체 구간 프레임을 메모리에 쌓지 않는 구조를 유지합니다. GIF·쇼츠 취소 시 인코더 종료와 작업별 임시 파일 정리를 검사했습니다. |
| 배포 | 실행에 필요 없는 WebView2 XML 문서 3개와 앱 PDB를 제외합니다. 업데이트 시 기존 설치의 해당 파일만 정리합니다. |

이전 `0.6.0-beta.11` 배포본에서 제외 대상 네 파일의 크기는 합계 **941,097바이트**였습니다. 기능과 검사 코드가 추가되므로 이 수치가 ZIP 전체의 감소량이나 실행 속도 개선율을 뜻하지는 않습니다. .NET 런타임·재생 엔진·다국어 런타임 리소스·진단 검사는 유지했습니다. 임의의 DLL 제거, WPF 트리밍, 대규모 구조 변경은 적용하지 않았습니다.

## 실행한 검증

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -SkipNative -OutputDirectory artifacts/v1-candidate/app
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1 -Executable artifacts/v1-candidate/app/ShinPlayer.exe -ReportName verification-v1.0.0
dotnet build src/ShinPlayer/ShinPlayer.csproj -c Release -p:IncludeNativeEngine=false --warnaserror
```

검사 범위는 실제 libmpv 디코딩·탐색·배속·음량 증폭, 재생목록·트레이, 테마 전환·최소 창, 자막 캡처, GIF 길이·배속·반복, 쇼츠 구간·구도·한글 위치·회전·오디오 지연·무음, API 키 저장 상태, 자막 로딩·영상 변경·근거에 따른 이동, WebView2의 두 자막 화면 구조를 포함합니다.

보고서는 로컬 `artifacts/verification-v1.0.0/results.json`과 `complete.txt`에 남깁니다. 원본 보고서에는 로컬 경로가 있으므로 공개 파일에 포함하지 않습니다.

API 검사는 대체 응답과 격리된 테스트 키를 사용했고, 유튜브 회귀 검사는 실제 WebView2 런타임에 격리된 페이지를 열었습니다. 이번 v1.0 검사에서 실제 OpenAI API는 호출하지 않았습니다. 앞선 베타의 실서비스 검증과 구분합니다. 사이트 변경이나 자막 제공 여부에 따라 실제 유튜브 영상 동작은 달라질 수 있습니다.

WPF 이벤트·좌표·레이아웃 검사를 물리 마우스 입력이나 실제 모니터 DPI 변경 검사로 해석하지 않습니다. WPF 렌더 이미지와 libmpv 디코딩 검사는 별개로 확인했으며, 특정 합성 영상의 결과로 모든 코덱이나 다른 플레이어 대비 성능을 보장하지 않습니다.
