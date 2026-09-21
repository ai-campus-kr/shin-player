using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

// Captures only the browser's rendered pixels; never reads media URLs or downloads streams.
internal sealed class BrowserGifSource : GifSource
{
    private readonly WebView2 _browser;
    private readonly Func<bool> _valid;
    private readonly Window _owner;
    private const string Video = "document.querySelector('#movie_player video') || document.querySelector('video')";
    private const string Ad = "document.querySelector('#movie_player.ad-showing, #movie_player.ad-interrupting')";
    private const string StateScript = """
        (()=>{
          const v=document.querySelector('#movie_player video') || document.querySelector('video');
          if(!v || !Number.isFinite(v.duration) || v.duration<=0 || document.querySelector('#movie_player.ytp-live')) return {error:'재생 가능한 일반 영상을 먼저 여세요. 실시간 방송은 지원하지 않습니다.'};
          if(document.querySelector('#movie_player.ad-showing, #movie_player.ad-interrupting')) return {error:'광고가 끝난 뒤 GIF를 만들어 주세요.'};
          if(v.mediaKeys) return {error:'보호된 영상은 화면 캡처를 지원하지 않습니다.'};
          const r=v.getBoundingClientRect();
          return {time:v.currentTime,duration:v.duration,paused:v.paused,rate:v.playbackRate,muted:v.muted,seeking:v.seeking,ready:v.readyState,
            x:r.x,y:r.y,width:r.width,height:r.height,vw:innerWidth,vh:innerHeight,
            title:document.querySelector('h1.ytd-watch-metadata')?.innerText || document.title || 'YouTube'};
        })()
        """;
    private sealed record State(double Time, double Duration, bool Paused, double Rate, bool Muted, bool Seeking, int Ready,
        double X, double Y, double Width, double Height, double Vw, double Vh, string Title, string? Error);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private BrowserGifSource(WebView2 browser, Window owner, Func<bool> valid, State state) : base(state.Title, state.Duration, true)
    { _browser = browser; _owner = owner; _valid = valid; }
    internal static async Task<GifSource> CreateAsync(WebView2 browser, Window owner, Func<bool> valid, CancellationToken cancel)
    {
        if (!valid()) throw new InvalidOperationException("유튜브 영상을 먼저 열어주세요.");
        var state = await ReadAsync(browser, cancel);
        if (!valid()) throw new InvalidOperationException("영상이 바뀌었습니다. GIF 창을 다시 여세요.");
        return new BrowserGifSource(browser, owner, valid, state);
    }
    private static async Task<State> ReadAsync(WebView2 browser, CancellationToken cancel)
    {
        string raw = await browser.ExecuteScriptAsync(StateScript).WaitAsync(TimeSpan.FromSeconds(10), cancel);
        var state = JsonSerializer.Deserialize<State>(raw, Json) ?? throw new InvalidOperationException("영상 정보를 읽지 못했습니다.");
        if (state.Error != null) throw new InvalidOperationException(state.Error);
        return state;
    }
    private void Check()
    {
        if (!_valid()) throw new InvalidOperationException("영상이 바뀌었거나 브라우저가 닫혔습니다. GIF 창을 다시 여세요.");
        if (_owner.WindowState == WindowState.Minimized || !_owner.IsVisible) throw new InvalidOperationException("캡처하려면 유튜브 창을 화면에 띄워 주세요.");
    }
    internal override async Task<double> PositionAsync() { Check(); return (await ReadAsync(_browser, CancellationToken.None)).Time; }
    internal override async Task<string> ExportAsync(CaptureTools tools, GifOptions options, string pictures, IProgress<string> progress, CancellationToken cancel)
    {
        Check(); options.Validate(Duration);
        string token = Guid.NewGuid().ToString("N");
        string temp = GifExport.CreateTemp();
        var frames = new List<(string File, double Time)>();
        (double Width, double Height)? initialSize = null;
        bool started = false;
        try
        {
            var original = await ReadAsync(_browser, cancel); Check(); options.Validate(original.Duration);
            // A page-local session keeps the exact video element and playback state for safe restoration.
            string start = $$"""
                (()=>{
                  const v={{Video}};
                  if(!v || {{Ad}} || v.mediaKeys) return false;
                  const s={token:'{{token}}',v,time:v.currentTime,paused:v.paused,rate:v.playbackRate,muted:v.muted,end:{{GifExport.Number(options.End)}},error:''};
                  s.id=document.querySelector('ytd-watch-flexy')?.getAttribute('video-id');
                  window.__shinGif=s; v.pause(); v.playbackRate=1; v.muted=true;
                  v.scrollIntoView({block:'center',inline:'center',behavior:'instant'});
                  v.currentTime={{GifExport.Number(options.Start)}};
                  s.timer=setInterval(()=>{
                    if({{Ad}} || s.id!==document.querySelector('ytd-watch-flexy')?.getAttribute('video-id') || v!==({{Video}})) {
                      clearInterval(s.timer);s.error='영상이 바뀌거나 광고가 시작되어 캡처를 취소했습니다.';return;
                    }
                    if(v.currentTime>=s.end)v.pause();
                  },20);
                  return true;
                })()
                """;
            started = await _browser.ExecuteScriptAsync(start) == "true";
            if (!started) throw new InvalidOperationException("광고가 끝난 뒤 재생 가능한 영상에서 다시 시도하세요.");
            var watch = Stopwatch.StartNew();
            State state;
            do
            {
                cancel.ThrowIfCancellationRequested(); Check();
                state = await ReadAsync(_browser, cancel);
                if (watch.Elapsed.TotalSeconds > 15) throw new TimeoutException("선택한 구간을 불러오지 못했습니다. 영상이 재생되는지 확인하세요.");
                if (!state.Seeking && state.Ready >= 2 && Math.Abs(state.Time - options.Start) < .15) break;
                await Task.Delay(40, cancel);
            } while (true);
            await Task.Delay(100, cancel);
            await CaptureAsync(state, 0);
            await _browser.ExecuteScriptAsync($"(()=>{{const s=window.__shinGif;if(s?.token==='{token}')s.v.play().catch(()=>s.error='재생을 시작하지 못했습니다. 영상을 한 번 재생한 뒤 다시 시도하세요.');}})()");
            var previous = options.Start;
            var lastMovement = Stopwatch.StartNew();
            var lastCapture = Stopwatch.StartNew();
            watch.Restart();
            while (true)
            {
                cancel.ThrowIfCancellationRequested(); Check();
                var session = await _browser.ExecuteScriptAsync($"(()=>{{const s=window.__shinGif;return !s || s.token!=='{token}' || s.v!==({Video}) ? '영상이 바뀌었습니다.' : s.error;}})()");
                var error = JsonSerializer.Deserialize<string>(session);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                state = await ReadAsync(_browser, cancel); Check();
                if (state.Seeking || state.Time < previous - .15 || state.Time > previous + 1.5 || Math.Abs(state.Rate - 1) > .01)
                    throw new InvalidOperationException("캡처 중 재생 위치나 속도가 변경되었습니다. 다시 만들어 주세요.");
                if (state.Time >= options.End - .02) break;
                if (state.Time > previous + .005) { lastMovement.Restart(); previous = state.Time; }
                if (lastMovement.Elapsed.TotalSeconds > 15 || watch.Elapsed.TotalSeconds > options.Length + 40)
                    throw new TimeoutException("영상 재생이 멈춰 캡처를 취소했습니다. 다시 재생한 뒤 시도하세요.");
                if (state.Paused && state.Time < options.End - .1 && watch.Elapsed.TotalSeconds > 1)
                    throw new InvalidOperationException("재생이 일시정지되어 캡처를 취소했습니다.");
                var offset = Math.Clamp(state.Time - options.Start, 0, options.Length);
                if (offset > frames[^1].Time + .01 && lastCapture.Elapsed.TotalSeconds >= 1.0 / options.Fps)
                {
                    progress.Report($"화면 캡처 · {offset:0.0} / {options.Length:0.0}초 · 창을 열어 두세요");
                    await CaptureAsync(state, offset); lastCapture.Restart();
                }
                await Task.Delay(10, cancel);
            }
            await RestoreAsync(); started = false;
            return await GifExport.EncodeFramesAsync(tools, frames, temp, options, Title, pictures, progress, cancel);
        }
        finally
        {
            if (started) await RestoreAsync();
            CaptureTools.RemovePrivateDirectory(temp, GifExport.TempRoot);
        }

        async Task RestoreAsync()
        {
            try
            {
                await _browser.ExecuteScriptAsync($$"""
                    (()=>{
                      const s=window.__shinGif;if(!s || s.token!=='{{token}}')return;
                      clearInterval(s.timer);delete window.__shinGif;
                      if(!{{(_valid() ? "true" : "false")}} || s.v!==({{Video}}))return;
                      const v=s.v;v.playbackRate=s.rate;v.muted=s.muted;
                      if({{Ad}})return;
                      v.pause();v.currentTime=s.time;
                      if(!s.paused)v.play().catch(()=>{});
                    })()
                    """).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception) { /* A closed or navigated browser must not be resurrected. */ }
        }
        async Task CaptureAsync(State geometry, double offset)
        {
            cancel.ThrowIfCancellationRequested(); Check();
            if (geometry.Width < 16 || geometry.Height < 16 || geometry.X < -1 || geometry.Y < -1 || geometry.X + geometry.Width > geometry.Vw + 1 || geometry.Y + geometry.Height > geometry.Vh + 1)
                throw new InvalidOperationException("영상 화면 전체가 보이도록 유튜브 창을 키운 뒤 다시 시도하세요.");
            if (initialSize is { } size && (Math.Abs(size.Width - geometry.Width) > 2 || Math.Abs(size.Height - geometry.Height) > 2))
                throw new InvalidOperationException("캡처 중 영상 크기가 변경되었습니다. 창 크기를 정한 뒤 다시 시도하세요.");
            initialSize ??= (geometry.Width, geometry.Height);
            using var stream = new MemoryStream();
            await _browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            cancel.ThrowIfCancellationRequested(); Check();
            stream.Position = 0;
            var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            double sx = bitmap.PixelWidth / geometry.Vw, sy = bitmap.PixelHeight / geometry.Vh;
            int x = Math.Max(0, (int)Math.Round(geometry.X * sx)), y = Math.Max(0, (int)Math.Round(geometry.Y * sy));
            int width = Math.Min(bitmap.PixelWidth - x, (int)Math.Round(geometry.Width * sx));
            int height = Math.Min(bitmap.PixelHeight - y, (int)Math.Round(geometry.Height * sy));
            if (width < 1 || height < 1) throw new InvalidOperationException("영상 화면을 캡처하지 못했습니다.");
            BitmapSource frame = new CroppedBitmap(bitmap, new Int32Rect(x, y, width, height));
            double scale = Math.Min(1, (double)options.Width / Math.Max(width, height));
            if (scale < 1) frame = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            frame.Freeze();
            string path = Path.Combine(temp, $"frame{frames.Count:D5}.png");
            await Task.Run(() =>
            {
                cancel.ThrowIfCancellationRequested();
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame));
                using var file = File.Create(path); encoder.Save(file);
            }, cancel);
            frames.Add((path, offset));
        }
    }
}
