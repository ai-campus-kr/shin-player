# 쇼츠·GIF 저장 완료 안내

신플레이어 1.0.0가 실제 파일을 저장한 직후의 WPF 화면입니다.

- `01-shorts-completed.png`: 형광 라임색 쇼츠 저장 완료 패널과 완성된 영상 열기 버튼.
- `02-gif-completed.png`: 스크롤 밖에 고정된 GIF 저장 완료 패널과 완성된 GIF 열기 버튼.
- `03-shorts-light.png`: 최소 크기의 라이트 테마에서도 보이는 완료 패널.
- `04-home.png`: 주소창·AI·GIF·쇼츠 버튼과 KAI·네이버 배지가 있는 시작 화면.

쇼츠는 `scripts/fixtures/shorts-demo.png` 기반 합성 영상, GIF는 색상과 자막을 가진 합성 테스트 영상을 사용했습니다. 실제 사용자 영상·키·개인정보는 포함하지 않습니다. `ShortsSelfTest.cs`, `GifSelfTest.cs`, `ExportFeedbackSelfTest.cs`에서 저장 상태와 화면을 검증합니다.

화면은 `artifacts/verification-v1.0.0`의 WPF 렌더이며, 쇼츠 미리보기에는 FFmpeg로 읽은 실제 프레임이 포함됩니다. 시작 화면에는 재생 중인 영상이 없습니다. 릴리스의 `ShinPlayer-shorts-demo.mp4`는 첫 화면의 완료 상태에서 저장된 실제 결과입니다. 이미지에 UI나 완료 문구를 합성하지 않았습니다.
