using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

internal sealed partial class YouTubeWindow
{
    internal static async Task<int> RunLiveTestAsync(Window owner, string envPath, string url, string output, bool captureOnly = false)
    {
        Directory.CreateDirectory(output);
        var keys = new ApiKeyStore(Path.Combine(output, "isolated-key"));
        YouTubeWindow? window = null;
        object report;
        int exitCode = 1;
        try
        {
            if (!YouTubePage.TryNormalizeAddress(url, out var normalized)) throw new InvalidOperationException("Invalid YouTube URL.");
            keys.Save(LiveChatTest.ReadKey(envPath));
            window = new YouTubeWindow(normalized, Path.Combine(output, "browser-profile"), keys) { Owner = owner, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -16000, Top = 0 };
            window.Show(); await window.Initialization.WaitAsync(TimeSpan.FromSeconds(30));
            if (!window._ready) throw new InvalidOperationException("Embedded browser did not initialize.");
            if (captureOnly)
            {
                await Task.Delay(5000); window.UpdateLayout();
                if (Math.Abs(window._browser.ActualWidth - window._browserFrame.ActualWidth) > 1 || Math.Abs(window._browser.ActualHeight - window._browserFrame.ActualHeight) > 1)
                    throw new InvalidOperationException("WebView2 does not fill its browser frame.");
                await window.CaptureLiveAsync(Path.Combine(output, "youtube-layout-live.png"));
                await File.WriteAllTextAsync(Path.Combine(output, "live-youtube.json"), JsonSerializer.Serialize(new { passed = true, mode = "layout-only", apiCalled = false, browserWidth = window._browser.ActualWidth, browserHeight = window._browser.ActualHeight }));
                return 0;
            }
            // YouTube can replace the initial page with a theme/consent navigation.
            // Follow the active automatic load, not a task from the outgoing page.
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var loading = window._automaticTranscriptTask;
                await loading.WaitAsync(TimeSpan.FromSeconds(25));
                if (ReferenceEquals(loading, window._automaticTranscriptTask) && window._chat.TranscriptForTest != null) break;
                await Task.Delay(500);
            }
            var transcript = window._chat.TranscriptForTest ?? throw new InvalidOperationException("Automatic transcript load failed: " + window._chat.StatusForTest);
            await File.WriteAllTextAsync(Path.Combine(output, "progress.json"), JsonSerializer.Serialize(new { phase = "waiting-for-video", subtitlesLoadedBeforeQuestion = true, cues = transcript.Cues.Count }));
            await WaitForContentAsync(window);
            // Pause so an ordinary answer can be proven not to move the playback cursor.
            await window._browser.ExecuteScriptAsync("document.querySelector('video')?.pause()");
            double before = await Position();
            await File.WriteAllTextAsync(Path.Combine(output, "progress.json"), JsonSerializer.Serialize(new { phase = "asking-api", cues = transcript.Cues.Count }));
            // Navigation must load subtitles before any question; no test-only opening steps.
            var answer = await window._chat.AskForLiveTestAsync("이 영상에서 다루는 핵심 내용을 설명해 주세요. 이동하지 말고 설명만 해주세요.");
            double afterAnswer = await Position();
            if (answer.SeekRequested || Math.Abs(afterAnswer - before) > .8) throw new InvalidOperationException("An ordinary question moved playback.");
            var seek = await window._chat.AskForLiveTestAsync("방금 설명한 부분으로 이동해줘");
            if (!seek.SeekRequested || seek.Matches.Count == 0) throw new InvalidOperationException("No supported seek destination returned.");
            await Task.Delay(500);
            await WaitForContentAsync(window);
            await window._browser.ExecuteScriptAsync("document.querySelector('video')?.pause()");
            double afterSeek = await Position();
            await File.WriteAllTextAsync(Path.Combine(output, "seek-diagnostic.json"), JsonSerializer.Serialize(new { before, afterAnswer, expected = seek.Matches[0].Start, afterSeek }));
            if (Math.Abs(afterSeek - seek.Matches[0].Start) > 2) throw new InvalidOperationException("Browser cursor did not reach the requested subtitle.");
            await window.CaptureLiveAsync(Path.Combine(output, "youtube-chat-live.png"));
            report = new { passed = true, model = VideoChatClient.Model, video = normalized, subtitlesLoadedBeforeQuestion = true, cues = transcript.Cues.Count, ordinaryQuestionKeptPosition = true, before, afterAnswer, requestedPosition = seek.Matches[0].Start, actualPosition = afterSeek, inputTokens = answer.InputTokens + seek.InputTokens, outputTokens = answer.OutputTokens + seek.OutputTokens };
            exitCode = 0;
            async Task<double> Position() => JsonSerializer.Deserialize<double>(await window._browser.ExecuteScriptAsync("document.querySelector('video')?.currentTime ?? -1"));
        }
        catch (Exception ex)
        {
            if (window?._ready == true)
            {
                try
                {
                    await window.CaptureLiveAsync(Path.Combine(output, "youtube-live-failure.png"));
                    var state = await window._browser.ExecuteScriptAsync("JSON.stringify({title:document.title,videoCount:document.querySelectorAll('video').length,videos:[...document.querySelectorAll('video')].map(v=>({time:v.currentTime,duration:v.duration,paused:v.paused,readyState:v.readyState,seeking:v.seeking})),ad:!!document.querySelector('.ad-showing,.ad-interrupting')})");
                    await File.WriteAllTextAsync(Path.Combine(output, "browser-state.json"), state);
                }
                catch { }
            }
            string error = ex is InvalidOperationException ? ex.Message : "Live YouTube test failed: " + ex.GetType().Name;
            report = new { passed = false, error, realYouTube = true };
        }
        finally { window?.Close(); keys.Delete(); }
        await File.WriteAllTextAsync(Path.Combine(output, "live-youtube.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return exitCode;
    }

    private static async Task WaitForContentAsync(YouTubeWindow window, int attempts = 600)
    {
        for (int i = 0; i < attempts; i++)
        {
            var state = await window._browser.ExecuteScriptAsync("""
                (()=>{
                  const visible=e=>e && e.getBoundingClientRect().height>0;
                  const dismiss=[...document.querySelectorAll('button')].find(e=>visible(e) && /^(닫기|close|no thanks|나중에)$/i.test((e.innerText||e.getAttribute('aria-label')||'').trim()));
                  if(dismiss)dismiss.click();
                  const player=document.querySelector('#movie_player'), video=player?.querySelector('video');
                  if(!video)return 'waiting';
                  video.muted=true;
                  const ad=player.classList.contains('ad-showing')||player.classList.contains('ad-interrupting');
                  if(!ad && Number.isFinite(video.duration) && video.duration>0 && video.readyState>=2)return 'content';
                  video.play().catch(()=>{});
                  const skip=[...player.querySelectorAll('button, [role="button"], .ytpSkipAdButton, .ytp-skip-ad-button, .ytp-ad-skip-button, .ytp-ad-skip-button-modern')].find(e=>visible(e) && (/건너뛰기|skip ad|skip$/i.test((e.innerText||e.getAttribute('aria-label')||'').trim()) || /ytp-(ad-)?skip-ad?-?button|ytpSkipAdButton/.test(e.className)));
                  if(skip)skip.click();
                  return ad?'ad':'waiting';
                })()
                """);
            if (state == "\"content\"") return;
            await Task.Delay(500);
        }
        throw new InvalidOperationException("YouTube content did not become ready after waiting for ads.");
    }

    private async Task CaptureLiveAsync(string path)
    {
        UpdateLayout();
        using var stream = new MemoryStream();
        await _browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
        stream.Position = 0;
        var page = new BitmapImage(); page.BeginInit(); page.CacheOption = BitmapCacheOption.OnLoad; page.StreamSource = stream; page.EndInit(); page.Freeze();
        var chrome = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); chrome.Render(this);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(chrome, new Rect(0, 0, ActualWidth, ActualHeight));
            drawing.DrawImage(page, _browser.TransformToAncestor(this).TransformBounds(new Rect(_browser.RenderSize)));
        }
        var composed = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); composed.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(composed));
        using var file = File.Create(path); encoder.Save(file);
    }
}

internal sealed partial class VideoChatPanel
{
    internal VideoTranscript? TranscriptForTest => _transcript;
    internal string StatusForTest => _status.Text;
    internal async Task<VideoChatReply> AskForLiveTestAsync(string question)
    {
        _question.Text = question; _lastReplyForTest = null;
        await AskAsync();
        return _lastReplyForTest ?? throw new InvalidOperationException(_status.Text);
    }
    private VideoChatReply? _lastReplyForTest;
}
