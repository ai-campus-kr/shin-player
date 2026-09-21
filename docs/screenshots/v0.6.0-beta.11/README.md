# 쇼츠 만들기 화면

신플레이어 0.6.0-beta.11의 실제 WPF 편집창과 FFmpeg로 저장한 MP4에서 캡처했습니다.

- `01-shorts-editor.png`: 구간·세로 화면·문구를 선택하고 실제 저장을 완료한 미니멀 UI.
- `02-shorts-light.png`: 같은 편집창의 라이트 UI.
- `03-shorts-output.png`: 실제 저장된 720×1280 MP4의 프레임.

영상 소스는 직접 만든 `scripts/fixtures/shorts-demo.png`를 바탕으로 생성한 합성 데모입니다. 사용자 영상·개인정보·API 키는 포함하지 않습니다. 생성 과정은 `scripts/create-test-media.py`, 편집·저장 검증은 `ShortsSelfTest.cs`에 있습니다.
