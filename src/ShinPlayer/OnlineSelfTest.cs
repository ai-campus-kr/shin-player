using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

internal static class OnlineSelfTest
{
    internal const string TestId = "M7lc1UVf-VE";
    internal const string TestKey = "sk-shinplayer-test-not-a-real-key";
    internal static VideoTranscript Transcript => new("synthetic-demo.mp4", "신플레이어 기능 데모 · 테스트 자막",
        new[] { new SubtitleCue(.2, 1.8, "첫 번째 빨간 장면"), new SubtitleCue(2.2, 3.8, "두 번째 초록 장면"), new SubtitleCue(4.2, 5.8, "세 번째 파란 장면") });

    internal sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> run) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => run(request, cancellationToken);
    }
    internal static HttpResponseMessage Reply(int[] ids, string answer = "초록 장면을 찾았습니다.", bool seek = true) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[] { new { content = new[] { new { type = "output_text", text = JsonSerializer.Serialize(new { answer, cue_ids = ids, seek_requested = seek }) } } } },
            usage = new { input_tokens = 150, output_tokens = 30 }
        }))
    };
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Invalid input was accepted");
    }
    internal static async Task RunAsync(Func<string, Func<Task<object?>>, Task> test, string fixtures, string output, Window owner)
    {
        await test("youtube-address-bar-pages-and-host-validation", () =>
        {
            foreach (var input in new[] { "youtube.com", " www.youtube.com ", "m.youtube.com/results?search_query=music", "youtu.be/" + TestId + "?t=5", "https://www.youtube.com/playlist?list=PLtest" })
                Require(YouTubePage.TryNormalizeAddress(input, out var url) && YouTubePage.IsYouTube(new Uri(url)), "Valid browser address rejected");
            foreach (var input in new[] { "", "just a search", "http://youtube.com", "https://youtube.com:444/", "https://user@youtube.com/", "https://youtube.com.example.org/", "javascript:alert(1)", "file:///C:/video.mp4", "https://example.com/" })
                Require(!YouTubePage.TryNormalizeAddress(input, out _), "Invalid browser address accepted");
            Require(YouTubePage.TryNormalizeAddress("youtube.com/watch?v=" + TestId + "&list=PLtest&t=5", out var preserved) && preserved.EndsWith("&list=PLtest&t=5"), "Browser address lost query parameters");
            return Task.FromResult<object?>(new { pagesAccepted = 5, rejected = 9, queryPreserved = true });
        });
        await test("youtube-links-normalize-and-reject-spoofed-hosts", () =>
        {
            foreach (var link in new[] { $"https://www.youtube.com/watch?v={TestId}&list=abc", $"https://youtu.be/{TestId}?si=tracking", $"https://m.youtube.com/shorts/{TestId}", $"https://youtube.com/live/{TestId}", $"youtu.be/{TestId}" })
                Require(YouTubePage.TryNormalize(link, out var normalized) && normalized == $"https://www.youtube.com/watch?v={TestId}", link);
            Require(YouTubePage.TryNormalize($"https://youtu.be/{TestId}?t=120s", out var time) && time.EndsWith("&t=120s"), "Time not preserved");
            foreach (var link in new[] { $"https://youtube.com.evil.test/watch?v={TestId}", $"https://youtube.com@evil.test/watch?v={TestId}", $"http://youtube.com/watch?v={TestId}", $"https://youtube.com:99/watch?v={TestId}", "file:///C:/movie.mp4", "https://www.youtube.com/watch?v=bad" })
                Require(!YouTubePage.TryNormalize(link, out _), "Unsafe link accepted");
            return Task.FromResult<object?>(new { valid = 6, rejected = 6 });
        });
        await test("youtube-transcript-source-validation-and-cue-times", () =>
        {
            var json = JsonSerializer.Serialize(new { videoId = TestId, title = "Fixture", language = "한국어", cues = new[] { new { start = 4.2, text = "last" }, new { start = .2, text = "first" } } });
            var transcript = YouTubePage.ParseTranscript(json, TestId);
            Require(transcript.Cues[0].Start == .2 && transcript.Cues[0].End == 4.2, "Wrong cue ordering");
            Reject(() => YouTubePage.ParseTranscript(json, "otherVideo1"));
            Reject(() => YouTubePage.ParseTranscript("""{"error":"스크립트가 없습니다."}""", TestId));
            Reject(() => YouTubePage.ParseTranscript(JsonSerializer.Serialize(new { videoId = TestId, title = "", language = "", cues = Array.Empty<object>() }), TestId));
            return Task.FromResult<object?>(new { cues = transcript.Cues.Count, staleSourceRejected = true });
        });
        await test("chat-request-model-privacy-schema-and-grounded-seek-time", async () =>
        {
            using var http = new HttpClient(new Handler(async (request, cancel) =>
            {
                Require(request.RequestUri!.AbsoluteUri == "https://api.openai.com/v1/responses", "Unexpected destination");
                Require(request.Headers.Authorization?.Parameter == TestKey, "Missing authorization");
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancel));
                var root = json.RootElement;
                Require(root.GetProperty("model").GetString() == "gpt-5.4-mini" && !root.GetProperty("store").GetBoolean(), "Wrong model/privacy option");
                Require(root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean(), "Missing strict schema");
                using var input = JsonDocument.Parse(root.GetProperty("input").GetString()!);
                Require(root.GetProperty("input").GetString()!.Contains("초록 장면"), "Korean text is escaped into expensive literal Unicode sequences");
                Require(input.RootElement.GetProperty("cues").GetArrayLength() == 3, "Missing subtitles");
                return Reply(new[] { 1 });
            }));
            var reply = await new VideoChatClient(http).AskAsync(TestKey, Transcript, "초록 장면 찾아줘", "", CancellationToken.None);
            Require(reply.Matches.Single().Start == 2.2 && reply.Matches[0].Quote == Transcript.Cues[1].Text, "Seek not grounded in actual cue");
            Require(reply.InputTokens == 150 && reply.OutputTokens == 30, "Token accounting wrong");
            return new { model = VideoChatClient.Model, seek = reply.Matches[0].Start, liveApiCalled = false };
        });
        await test("chat-invalid-evidence-and-incomplete-response-never-seek", async () =>
        {
            foreach (var ids in new[] { new[] { -1 }, new[] { 99 } })
            {
                using var response = Reply(ids);
                Reject(() => VideoChatClient.ParseReply(response.Content.ReadAsStringAsync().Result, Transcript.Cues, new[] { 0, 1, 2 }, false));
            }
            using var omitted = Reply(new[] { 2 });
            Reject(() => VideoChatClient.ParseReply(omitted.Content.ReadAsStringAsync().Result, Transcript.Cues, new[] { 0 }, true));
            Reject(() => VideoChatClient.ParseReply("""{"status":"incomplete"}""", Transcript.Cues, new[] { 0 }, false));
            Reject(() => VideoChatClient.ParseReply("""{"status":"completed","output":[{"content":[{"type":"refusal"}]}]}""", Transcript.Cues, new[] { 0 }, false));
            using var empty = Reply(Array.Empty<int>(), "근거를 찾지 못했습니다.");
            var noMatch = VideoChatClient.ParseReply(await empty.Content.ReadAsStringAsync(), Transcript.Cues, new[] { 0, 1, 2 }, false);
            Require(noMatch.Matches.Count == 0, "Missing evidence still caused a match");
            return new { invalidResponsesRejected = 5, noMatch = true };
        });
        await test("chat-http-errors-sanitized-and-cancellation", async () =>
        {
            foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
            {
                using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent("PRIVATE_RESPONSE_MARKER") })));
                try { await new VideoChatClient(http).AskAsync(TestKey, Transcript, "질문", "", CancellationToken.None); throw new Exception("HTTP error accepted"); }
                catch (InvalidOperationException ex) { Require(!ex.Message.Contains("PRIVATE_RESPONSE_MARKER") && !ex.Message.Contains(TestKey), "Provider data leaked"); }
            }
            using var slow = new HttpClient(new Handler(async (_, cancel) => { await Task.Delay(10000, cancel); return Reply(new[] { 0 }); }));
            using var cancellation = new CancellationTokenSource(40);
            try { await new VideoChatClient(slow).AskAsync(TestKey, Transcript, "질문", "", cancellation.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
            return new { codes = 4, cancelled = true };
        });
        await test("chat-long-subtitles-have-bounded-cost-and-late-video-coverage", () =>
        {
            var cues = Enumerable.Range(0, 10000).Select(i => new SubtitleCue(i, i + 1, "일반 자막 문장 " + new string('가', 55))).ToArray();
            cues[9980] = cues[9980] with { Text = "초록장면 설명은 영상 끝에 있습니다." };
            var ids = VideoChatClient.SelectCues(cues, "초록장면 찾아줘");
            Require(ids.Contains(9980) && ids.Contains(9979) && ids.Contains(9981), "Late matching cues not included");
            Require(ids.Sum(i => cues[i].Text.Length + 55) <= VideoChatClient.TranscriptBudget, "Budget exceeded");
            return Task.FromResult<object?>(new { total = cues.Length, selected = ids.Count, characterBudget = VideoChatClient.TranscriptBudget });
        });
        await test("api-key-dpapi-roundtrip-isolated-from-real-user-key", () =>
        {
            var directory = Path.Combine(output, "test-key");
            var keys = new ApiKeyStore(directory);
            try
            {
                keys.Save(TestKey);
                Require(keys.Load() == TestKey, "DPAPI round trip failed");
                Require(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "openai-key.dat"))).Contains(TestKey), "Plaintext credential");
                keys.Delete(); Require(keys.Load() == "", "Credential deletion failed");
                Require(new ApiKeyStore().Load() == "", "Tests can access real key");
            }
            finally { keys.Delete(); }
            return Task.FromResult<object?>(new { encrypted = true, isolated = true });
        });
        await test("local-chat-reads-only-selected-embedded-text-track", async () =>
        {
            var tools = await CaptureTools.EnsureAsync(new Progress<string>(), CancellationToken.None);
            var capture = new SubtitleCapture(tools);
            var video = await capture.ProbeAsync(Path.Combine(fixtures, "캡처 '테스트 [한글].mp4"), CancellationToken.None);
            var transcript = await VideoTranscriptLoader.ExtractAsync(tools, video, video.Tracks.Single(t => t.Language == "kor"), .3, CancellationToken.None);
            Require(transcript.Cues.Count == 3 && Math.Abs(transcript.Cues[0].Start - .5) < .001 && transcript.Cues[0].Text.Contains("빨간"), "Wrong embedded transcript");
            var noSubs = await capture.ProbeAsync(Path.Combine(fixtures, "한글 영상 sample.mp4"), CancellationToken.None);
            Require(noSubs.Tracks.Count == 0, "External subtitles became eligible");
            return new { embeddedCues = transcript.Cues.Count, subtitleDelay = .3, externalIgnored = true };
        });
        await test("chat-window-answer-seek-no-match-cancel-and-close", async () =>
            await VideoChatPanel.TestFlowAsync(owner, output));
        await test("youtube-docked-chat-responsive-layout", async () =>
            await YouTubeWindow.TestLayoutAsync(owner, output));
        await test("webview2-runtime-transcript-and-seek-offline-fixture", async () =>
            await TestBrowserAsync(fixtures, output, owner));
    }

    private static async Task<object> TestBrowserAsync(string fixtures, string output, Window owner)
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(output, "webview-test-profile"));
        var browser = new WebView2();
        var window = new Window { Owner = owner, Content = browser, Width = 900, Height = 640, ShowActivated = false, ShowInTaskbar = false, Left = -2000, Top = 0 };
        window.Show();
        try
        {
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.SetVirtualHostNameToFolderMapping("shinplayer.test", fixtures, CoreWebView2HostResourceAccessKind.DenyCors);
            var loaded = new TaskCompletionSource<bool>();
            browser.CoreWebView2.NavigationCompleted += (_, e) => loaded.TrySetResult(e.IsSuccess);
            browser.NavigateToString("""
                <!doctype html><html><body>
                <h1 class="ytd-watch-metadata">Synthetic subtitle fixture</h1>
                <ytd-watch-flexy video-id="M7lc1UVf-VE"></ytd-watch-flexy>
                <div id="movie_player"><video muted preload="auto" src="https://shinplayer.test/한글%20영상%20sample.mp4"></video></div>
                <ytd-engagement-panel-section-list-renderer target-id="engagement-panel-searchable-transcript" visibility="ENGAGEMENT_PANEL_VISIBILITY_EXPANDED">
                  <ytd-transcript-segment-renderer><span class="segment-timestamp">0:02</span><span class="segment-text">테스트 자막 첫 번째</span></ytd-transcript-segment-renderer>
                  <ytd-transcript-segment-renderer><span class="segment-timestamp">0:05</span><span class="segment-text">테스트 자막 두 번째</span></ytd-transcript-segment-renderer>
                  <ytd-transcript-footer-renderer><button>한국어</button></ytd-transcript-footer-renderer>
                </ytd-engagement-panel-section-list-renderer></body></html>
                """);
            Require(await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15)), "Fixture navigation failed");
            var script = YouTubePage.ReadTranscriptScript.Replace("new URL(location.href)", $"new URL('https://www.youtube.com/watch?v={TestId}')");
            var transcript = YouTubePage.ParseTranscript(await browser.ExecuteScriptAsync(script), TestId);
            Require(transcript.Cues.Count == 2 && transcript.Cues[1].Start == 5, "DOM transcript read failed");
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (await browser.ExecuteScriptAsync("Number.isFinite(document.querySelector('video').duration)") == "true") break;
                await Task.Delay(50);
            }
            var seek = YouTubePage.SeekScript(TestId, 2.2).Replace("new URL(location.href)", $"new URL('https://www.youtube.com/watch?v={TestId}')");
            Require(await browser.ExecuteScriptAsync(seek) == "\"\"", "Video seek failed");
            await browser.ExecuteScriptAsync("document.querySelector('video').pause()");
            var position = JsonSerializer.Deserialize<double>(await browser.ExecuteScriptAsync("document.querySelector('video').currentTime"));
            Require(Math.Abs(position - 2.2) < .5, "Wrong browser video position");
            await browser.ExecuteScriptAsync("document.querySelector('#movie_player').classList.add('ad-showing')");
            Require(await browser.ExecuteScriptAsync(seek) != "\"\"", "Seek changed ad playback");
            await browser.ExecuteScriptAsync("document.querySelector('ytd-engagement-panel-section-list-renderer').setAttribute('visibility','ENGAGEMENT_PANEL_VISIBILITY_HIDDEN')");
            var hidden = await browser.ExecuteScriptAsync(script);
            Reject(() => YouTubePage.ParseTranscript(hidden, TestId));
            return new { runtime = environment.BrowserVersionString, transcriptCues = 2, position, adSeekRejected = true, liveYouTubeTest = false };
        }
        finally { browser.Dispose(); window.Close(); }
    }

    internal static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}

internal sealed partial class VideoChatPanel
{
    internal static async Task<object> TestFlowAsync(Window owner, string output)
    {
        var keys = new ApiKeyStore(Path.Combine(output, "test-chat-key"));
        keys.Save(OnlineSelfTest.TestKey);
        int calls = 0, seeks = 0;
        var pending = new TaskCompletionSource<HttpResponseMessage>();
        using var http = new HttpClient(new OnlineSelfTest.Handler((_, _) =>
        {
            calls++;
            return calls < 3 ? Task.FromResult(OnlineSelfTest.Reply(calls == 1 ? new[] { 1 } : Array.Empty<int>(), seek: false)) : pending.Task;
        }));
        var panel = new VideoChatPanel(_ => Task.FromResult(OnlineSelfTest.Transcript), (_, time) =>
        {
            if (time != 2.2) throw new Exception("Wrong seek position");
            seeks++; return Task.CompletedTask;
        }, new VideoChatClient(http), keys);
        var window = new Window { Owner = owner, ShowActivated = false, Style = (Style)owner.FindResource(typeof(Window)), Width = 600, Height = 760, Content = panel };
        window.Closed += (_, _) => panel.Close();
        try
        {
            window.Show(); await panel.LoadAsync();
            if (!panel._send.IsEnabled) throw new Exception("Chat not ready");
            panel._apiSettings.IsExpanded = true;
            OnlineSelfTest.Capture(window, Path.Combine(output, "chat-ready.png"));
            panel._question.Text = "초록 장면 설명해줘"; await panel.AskAsync();
            if (seeks != 0 || panel._messages.Children.Count < 4) throw new Exception("Ordinary answer moved playback or lost evidence");
            panel._question.Text = "그 부분으로 이동해줘"; await panel.AskAsync();
            if (seeks != 1 || panel._messages.Children.Count < 4) throw new Exception("Answer did not seek");
            if (calls != 1) throw new Exception("Direct follow-up unnecessarily called the API");
            panel._question.Text = "없는 내용"; await panel.AskAsync();
            if (seeks != 1) throw new Exception("No-match answer sought");
            panel._question.Text = "취소할 질문"; var asking = panel.AskAsync();
            panel._work.Cancel(); pending.TrySetResult(OnlineSelfTest.Reply(new[] { 1 })); await asking;
            if (seeks != 1 || panel._busy) throw new Exception("Cancelled answer sought");
            pending = new TaskCompletionSource<HttpResponseMessage>();
            panel._question.Text = "다른 영상으로 바꾸기 전 질문"; asking = panel.AskAsync();
            panel.ResetTranscript("새 영상");
            pending.TrySetResult(OnlineSelfTest.Reply(new[] { 1 })); await asking;
            if (seeks != 1 || panel._send.IsEnabled || panel._status.Text != "새 영상" || panel._messages.Children.Count != 0)
                throw new Exception("Old response changed the new video's chat");
            await panel.LoadAsync();
            pending = new TaskCompletionSource<HttpResponseMessage>();
            panel._question.Text = "닫은 후 도착하는 답변"; asking = panel.AskAsync(); window.Close();
            pending.TrySetResult(OnlineSelfTest.Reply(new[] { 1 })); await asking;
            if (seeks != 1) throw new Exception("Closed chat answer sought");
            return new { explicitSeeks = seeks, ordinaryAnswerKeptPosition = true, noMatchKeptPosition = true, changedVideoResponseIgnored = true, cancelledAndClosedResponsesIgnored = true, liveApiCalled = false };
        }
        finally { if (!panel._closed) window.Close(); keys.Delete(); }
    }
}

internal sealed partial class YouTubeWindow
{
    internal static async Task<object> TestLayoutAsync(Window owner, string output)
    {
        var window = new YouTubeWindow("https://www.youtube.com/watch?v=" + OnlineSelfTest.TestId) { Owner = owner, ShowActivated = false };
        try
        {
            window.Show(); await window.Initialization;
            window.UpdateLayout();
            var toolbar = (System.Windows.Controls.DockPanel)((System.Windows.Controls.DockPanel)window.Content).Children[0];
            if (toolbar.Children.OfType<System.Windows.Controls.Button>().Any(button => button.Content?.ToString()?.Contains("GIF", StringComparison.OrdinalIgnoreCase) == true))
                throw new Exception("YouTube toolbar still offers GIF export");
            if (Math.Abs(window._browser.ActualWidth - window._browserFrame.ActualWidth) > 1 || Math.Abs(window._browser.ActualHeight - window._browserFrame.ActualHeight) > 1)
                throw new Exception("WebView2 does not fill its browser frame");
            // WebView2 owns a native surface; this screenshot labels the synthetic layout preview explicitly.
            window._browserFrame.Content = new System.Windows.Controls.TextBlock { Text = "YouTube 브라우저 영역\n레이아웃 검증 화면 · 실제 재생 화면 아님", TextAlignment = TextAlignment.Center, FontSize = 22, VerticalAlignment = VerticalAlignment.Center };
            foreach (var (width, height, placement, bottom) in new[] { (1280d, 820d, "auto", false), (840d, 620d, "auto", true), (1280d, 820d, "bottom", true), (840d, 620d, "right", false) })
            {
                window.Width = width; window.Height = height; window._chatPlacement = placement;
                window.UpdateLayout(); window.UpdateChatLayout(); window.UpdateLayout(); await Task.Delay(60);
                var chat = window._chatFrame.TransformToAncestor(window._workspace).TransformBounds(new Rect(window._chatFrame.RenderSize));
                var browser = window._browserFrame.TransformToAncestor(window._workspace).TransformBounds(new Rect(window._browserFrame.RenderSize));
                if (!window._chat.IsVisible || chat.Width < 330 || chat.Height < 240 || browser.Width < 400 || browser.Height < 180)
                    throw new Exception("Docked chat or browser is clipped");
                var intersection = Rect.Intersect(chat, browser);
                if (!intersection.IsEmpty && intersection.Width > 1 && intersection.Height > 1) throw new Exception("Chat covers the browser");
                if (System.Windows.Controls.Grid.GetRow(window._chatFrame) != (bottom ? 1 : 0)) throw new Exception("Wrong chat placement");
                window._chat.VerifyLayout();
                OnlineSelfTest.Capture(window, Path.Combine(output, $"youtube-chat-{width:0}-{placement}.png"));
            }
            return new { layouts = 4, embeddedPanel = true, overlaps = false, gifExportOffered = false, realYouTube = false };
        }
        finally { window.Close(); }
    }
}

internal sealed partial class VideoChatPanel
{
    internal void VerifyLayout()
    {
        UpdateLayout();
        var viewport = new Rect(-1, -1, ActualWidth + 2, ActualHeight + 2);
        foreach (FrameworkElement control in new FrameworkElement[] { _question, _send, _scroll })
        {
            var bounds = control.TransformToAncestor(this).TransformBounds(new Rect(control.RenderSize));
            if (!viewport.Contains(bounds) || bounds.Width < 150 || bounds.Height < 20) throw new Exception("Chat controls are clipped");
        }
    }
}
