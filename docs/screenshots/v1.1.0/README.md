# v1.1 쇼츠 영역·글꼴·색상

2026-09-23 최종 통합 검사 `artifacts/verification-v1.1.0-release`에서 생성한 실제 WPF 화면입니다.

- `01-shorts-styles.png`: 문구·스타일 탭. 검은고딕 글꼴과 라임 글자색, 18색 팔레트·직접 색상 선택.
- `02-shorts-region.png`: 원본 위에서 사각형 영역을 고르는 창. 안쪽 이동·가장자리와 모서리 크기 조절.
- `03-shorts-framing.png`: 구간·화면 탭. 영역 직접 선택, 화면 맞춤, 확대와 미리보기.
- `04-shorts-saved.png`: 실제 MP4 저장 직후 형광 라임색 완료 상태.

원본은 `scripts/fixtures/shorts-demo.png`를 사용해 `scripts/create-test-media.py`로 생성한 기능 설명용 합성 영상입니다. 화면의 장면 미리보기는 FFmpeg로 읽은 실제 프레임이며 실시간 재생 화면은 아닙니다. UI나 완료 문구를 이미지에 합성하지 않았습니다. 실제 개인 영상·API 키는 사용하지 않았습니다.

릴리스의 `ShinPlayer-shorts-demo.mp4`와 홈페이지의 같은 파일은 이 검사에서 실제로 내보낸 720×1280, 2.1초 H.264 MP4입니다. 새 글꼴과 영역 선택 검증에는 별도로 색상 구분 원본·회전 원본·얇은 영역을 사용했습니다. 모든 글꼴의 렌더링과 네 가지 구도의 미리보기/MP4 비교 결과는 로컬 보고서에 남깁니다. [검증 기록](../../verification-v1.1.0.md).
