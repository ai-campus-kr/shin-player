# 0.6 베타 화면 출처

- `01-chat-ready.png`: 신플레이어의 실제 `VideoChatWindow`를 WPF `RenderTargetBitmap`으로 렌더했습니다.
- 앱 통합 검사에서 합성 자막 3개를 읽고 API 설정을 펼친 상태입니다.
- 저장 상태 검증에는 테스트 전용 키를 사용했습니다. 사용자 키를 읽거나 공개하지 않았으며 비밀번호 입력칸은 비어 있습니다.
- OpenAI 실제 요청이나 답변을 촬영한 이미지가 아닙니다. AI 응답과 자동 이동 검사는 HTTP 대체 응답을 사용하는 별도 검사로 수행합니다.
- 창의 비클라이언트 테두리와 실제 YouTube 영상은 포함하지 않습니다. 그림을 생성하거나 UI를 재구성한 목업이 아닙니다.
- 생성 경로: `OnlineSelfTest.cs`의 `VideoChatWindow.TestFlowAsync` → `artifacts/v0.6.0-beta.1-published/chat-ready.png`.
