using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ShinPlayer;

public partial class MainWindow
{
    private async Task RunGifTestsAsync(Func<string, Func<Task<object?>>, Task> test, string fixtures, string output)
    {
        var tools = await CaptureTools.EnsureAsync(new Progress<string>(), CancellationToken.None);
        var video = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "캡처 '테스트 [한글].mp4"), CancellationToken.None);
        var source = new LocalGifSource(video, () => _player?.Number("time-pos") ?? 0);
        var pictures = Path.Combine(output, "gif-output");
        var options = new GifOptions(1, 5, 480, 12);
        var progress = new ImmediateProgress<string>(_ => { });
        await test("gif-range-validation-and-time-input", () =>
        {
            if (GifOptions.Parse("01:23.500") != 83.5 || GifOptions.Parse("1:02:03.125") != 3723.125 || GifOptions.Parse("2.25") != 2.25) throw new Exception("Time parse failed");
            foreach (string invalid in new[] { "NaN", "Infinity", "-1", "1:99", "1.5:20", "", "1:2:3:4" })
            {
                try { GifOptions.Parse(invalid); throw new Exception("Invalid time accepted: " + invalid); }
                catch (FormatException) { }
            }
            foreach (var bad in new[] { options with { Start = -1 }, options with { End = 101 }, options with { Start = 5 }, options with { End = double.NaN }, options with { Width = 100000 }, options with { Speed = 0 }, options with { Speed = 101 }, options with { Speed = double.NaN }, options with { Speed = 100 } })
            {
                try { bad.Validate(100); throw new Exception("Invalid range accepted"); }
                catch (InvalidOperationException) { }
            }
            foreach (var invalid in new[] { "0", "-1", "NaN", "Infinity", "101", "1,5", "" })
            {
                try { GifOptions.ParseSpeed(invalid); throw new Exception("Invalid speed accepted"); } catch (FormatException) { }
            }
            if (GifOptions.ParseSpeed("2.5") != 2.5) throw new Exception("Decimal speed rejected");
            new GifOptions(0, 600, 480, 12, 10).Validate(600);
            new GifOptions(0, 600, 480, 12, 2).Validate(600);
            foreach (var bad in new[] { new GifOptions(0, 3601, 480, 12, 100), new GifOptions(0, 600, 480, 12, 1) })
            {
                try { bad.Validate(7200); throw new Exception("Oversized GIF accepted"); } catch (InvalidOperationException) { }
            }
            return Task.FromResult<object?>(new { validAndInvalidTimeFormats = true, speedMin = .25, speedMax = 100, maxSourceSeconds = 3600, maxOutputSeconds = 300 });
        });
        await test("gif-local-timing-loop-dimensions-and-playback-preserved", async () =>
        {
            await OpenFilesAsync(new[] { video.Path }); await _player!.SetAsync("pause", true);
            await SeekAsync(2, false); await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - 2) < .1 && _player.Flag("pause"), TimeSpan.FromSeconds(5));
            var path = await source.ExportAsync(tools, options, pictures, progress, CancellationToken.None);
            await VerifyGifAsync(path, options, tools, output);
            if (!_player.Flag("pause") || Math.Abs(_player.Number("time-pos") - 2) > .1) throw new Exception("GIF changed local playback");
            var second = await source.ExportAsync(tools, options, pictures, progress, CancellationToken.None);
            if (path == second || !File.Exists(path)) throw new Exception("GIF overwrote existing output");
            return new { path, repeatOutput = second, playbackPreserved = true };
        });
        await test("gif-cancel-cleans-unfinished-output-and-temp", async () =>
        {
            var before = Directory.GetDirectories(GifExport.TempRoot).ToHashSet();
            int count = Directory.GetFiles(pictures, "*.gif", SearchOption.AllDirectories).Length;
            using var cancellation = new CancellationTokenSource();
            try
            {
                await source.ExportAsync(tools, options, pictures, new ImmediateProgress<string>(value => { if (value.StartsWith("2/2")) cancellation.Cancel(); }), cancellation.Token);
                throw new Exception("Cancel ignored");
            }
            catch (OperationCanceledException) { }
            if (Directory.GetDirectories(GifExport.TempRoot).Any(x => !before.Contains(x)) || Directory.GetFiles(pictures, "*.partial", SearchOption.AllDirectories).Length > 0 || Directory.GetFiles(pictures, "*.gif", SearchOption.AllDirectories).Length != count)
                throw new Exception("Cancellation left unfinished output");
            return new { cancelled = true, cleaned = true };
        });
        await test("gif-speed-shortens-and-slows-full-segment", async () =>
        {
            foreach (double speed in new[] { .5, 2, 2.5, 10 })
            {
                var changed = options with { Speed = speed };
                var path = await source.ExportAsync(tools, changed, pictures, progress, CancellationToken.None);
                await VerifyGifAsync(path, changed, tools, output);
                if (!Path.GetFileName(path).Contains($"_{GifExport.Number(speed)}x")) throw new Exception("GIF filename lost speed");
            }
            return new { speeds = new[] { .5, 2, 2.5, 10 }, sourceSeconds = 4, outputSeconds = new[] { 8, 2, 1.6, .4 } };
        });
        await test("gif-ten-minutes-to-one-minute-and-live-estimate", async () =>
        {
            var longVideo = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "gif-ten-minutes.mp4"), CancellationToken.None);
            var longSource = new LocalGifSource(longVideo, () => 0);
            var dialog = new GifWindow(_ => Task.FromResult<GifSource>(longSource)) { Owner = this };
            try
            {
                dialog.Show(); await WaitUntilAsync(() => dialog.IsLoaded, TimeSpan.FromSeconds(5)); await dialog.Initialization;
                dialog.StartTime.Text = "00:00"; dialog.EndTime.Text = "10:00";
                var preset = VisualChildren<Button>(dialog).Single(b => Equals(b.Content, "10×"));
                preset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (dialog.SpeedInput.Text != "10" || dialog.Estimate.Text != "원본 10분 ÷ 10× → GIF 1분") throw new Exception("Incorrect speed estimate or preset wiring");
                await dialog.ExportAsync(pictures);
                if (dialog.SavedPath == null) throw new Exception("Ten-minute export failed");
                await VerifyGifAsync(dialog.SavedPath, new(0, 600, 480, 12, 10), tools, output, [(5, 2), (30, 1), (55, 0)]);
                OnlineSelfTest.Capture(dialog, Path.Combine(output, "gif-speed-10x.png"));
                return new { sourceSeconds = longVideo.Duration, outputSeconds = 60, speed = 10, path = dialog.SavedPath };
            }
            finally { dialog.Close(); }
        });
        await test("gif-button-dialog-four-designs-and-export", async () =>
        {
            GifButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var dialog = _gifWindow ?? throw new Exception("GIF button not wired");
            try
            {
                await dialog.Initialization;
                foreach (var design in UiDesigns.All)
                {
                    ApplyUiDesign(design.Id); dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight;
                    await Task.Delay(50); dialog.UpdateLayout();
                    foreach (var button in VisualChildren<Button>(dialog).Where(b => b.IsVisible))
                    {
                        var bounds = button.TransformToAncestor(dialog).TransformBounds(new Rect(button.RenderSize));
                        if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > dialog.ActualWidth || bounds.Bottom > dialog.ActualHeight) throw new Exception("Clipped GIF button");
                    }
                    OnlineSelfTest.Capture(dialog, Path.Combine(output, "gif-" + design.Id + ".png"));
                }
                ApplyUiDesign("minimal"); dialog.Width = 660; dialog.Height = 640;
                dialog.StartTime.Text = "5"; dialog.EndTime.Text = "1"; await dialog.ExportAsync(pictures);
                if (dialog.SavedPath != null) throw new Exception("Invalid dialog range exported");
                dialog.StartTime.Text = "1"; dialog.EndTime.Text = "5"; await dialog.ExportAsync(pictures);
                if (dialog.SavedPath == null) throw new Exception("Dialog export failed");
                OnlineSelfTest.Capture(dialog, Path.Combine(output, "gif-dialog.png"));
                File.Copy(dialog.SavedPath, Path.Combine(output, "gif-demo.gif"), true);
                var saved = dialog.SavedPath;
                var pending = dialog.ExportAsync(pictures); await Task.Delay(20); dialog.Close(); await pending;
                await WaitUntilAsync(() => _gifWindow == null, TimeSpan.FromSeconds(5));
                if (Directory.GetFiles(pictures, "*.partial", SearchOption.AllDirectories).Length != 0) throw new Exception("Closing GIF dialog left a partial file");
                return new { buttonWorks = true, themes = 4, output = saved, closeCancelsWork = true };
            }
            finally { dialog.Close(); }
        });
        await test("gif-browser-pixels-crop-timing-and-state-restore", async () => await TestBrowserGifAsync(tools, fixtures, output, false));
        await test("gif-browser-cancel-restores-state-and-removes-temp", async () => await TestBrowserGifAsync(tools, fixtures, output, true));
        await test("gif-browser-resumes-original-playing-state", async () => await TestBrowserGifAsync(tools, fixtures, output, false, true));
        await test("gif-browser-speed-compresses-timeline-once", async () => await TestBrowserGifAsync(tools, fixtures, output, false, false, 2.5));
    }

    private static async Task VerifyGifAsync(string path, GifOptions options, CaptureTools tools, string output, (double At, int Color)[]? checkpoints = null)
    {
        using var json = JsonDocument.Parse(await CaptureTools.RunAsync(tools.Ffprobe, ["-v", "error", "-count_frames", "-show_frames", "-show_entries", "stream=width,height,nb_read_frames:format=duration:frame=pts_time", "-of", "json", path], output, CancellationToken.None));
        var stream = json.RootElement.GetProperty("streams")[0];
        if (stream.GetProperty("width").GetInt32() != 480 || stream.GetProperty("height").GetInt32() != 270 || int.Parse(stream.GetProperty("nb_read_frames").GetString()!) < Math.Max(1, Math.Floor(options.OutputLength * options.Fps) - 2))
            throw new Exception("Wrong GIF dimensions/frame count: " + json.RootElement);
        double duration = double.Parse(json.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        if (Math.Abs(duration - options.OutputLength) > .2 || !Encoding.ASCII.GetString(await File.ReadAllBytesAsync(path)).Contains("NETSCAPE2.0")) throw new Exception("GIF duration/loop incorrect");
        foreach (var (at, color) in checkpoints ?? new[] { (.2 / options.Speed, 2), (1.6 / options.Speed, 1), (3.5 / options.Speed, 0) })
        {
            string png = Path.Combine(output, "gif-check.png");
            // Check the frame actually displayed at this time, not the next frame returned by -ss.
            int index = json.RootElement.GetProperty("frames").EnumerateArray().Select((frame, i) => new { Index = i, Time = double.Parse(frame.GetProperty("pts_time").GetString()!, System.Globalization.CultureInfo.InvariantCulture) }).Last(frame => frame.Time <= at + .000001).Index;
            await CaptureTools.RunAsync(tools.Ffmpeg, ["-v", "error", "-i", path, "-vf", $"select='eq(n,{index})'", "-frames:v", "1", "-update", "1", "-y", png], output, CancellationToken.None);
            var frame = LoadPixels(png); int center = (frame.Height / 2 * frame.Width + frame.Width / 2) * 4;
            // WebView2 applies the display color profile; verify the dominant scene color, not exact RGB.
            if (frame.Pixels[center + color] < 90 || Enumerable.Range(0, 3).Any(c => c != color && frame.Pixels[center + c] > 65))
                throw new Exception("GIF frame timing or browser crop is wrong at " + at);
        }
    }
    private async Task<object> TestBrowserGifAsync(CaptureTools tools, string fixtures, string output, bool cancelTest, bool playing = false, double speed = 1)
    {
        var browser = new WebView2();
        var window = new Window { Owner = this, Content = browser, Width = 900, Height = 660, ShowActivated = false, ShowInTaskbar = false };
        bool valid = true;
        string tempRoot = Path.Combine(output, cancelTest ? "browser-gif-cancel" : playing ? "browser-gif-resume" : "browser-gif");
        var tempBefore = Directory.GetDirectories(GifExport.TempRoot).ToHashSet();
        window.Show();
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(output, "gif-browser-profile"));
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.SetVirtualHostNameToFolderMapping("shinplayer.test", fixtures, CoreWebView2HostResourceAccessKind.DenyCors);
            var loaded = new TaskCompletionSource<bool>();
            browser.CoreWebView2.NavigationCompleted += (_, e) => loaded.TrySetResult(e.IsSuccess);
            browser.NavigateToString("""
                <!doctype html><html><head><title>GIF browser fixture</title><style>body{margin:0;background:#fff}#movie_player{margin:50px 80px}video{width:640px;height:360px}</style></head><body>
                <ytd-watch-flexy video-id="M7lc1UVf-VE"></ytd-watch-flexy><div id="movie_player"><video preload="auto" src="https://shinplayer.test/캡처%20'테스트%20[한글].mp4"></video></div>
                <p>THIS PAGE TEXT MUST NEVER APPEAR IN THE GIF</p></body></html>
                """);
            if (!await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15))) throw new Exception("GIF fixture navigation failed");
            await WaitUntilAsync(() => browser.CoreWebView2 != null, TimeSpan.FromSeconds(3));
            for (int i = 0; i < 100; i++)
            {
                if (await browser.ExecuteScriptAsync("document.querySelector('video').readyState>=2") == "true") break;
                await Task.Delay(50);
            }
            await browser.ExecuteScriptAsync("(()=>{const v=document.querySelector('video');v.currentTime=5.2;v.pause();v.playbackRate=1.5;v.muted=false;})()");
            await Task.Delay(150);
            var source = await BrowserGifSource.CreateAsync(browser, window, () => valid, CancellationToken.None);
            if (playing) await browser.ExecuteScriptAsync("(()=>{const v=document.querySelector('video');v.currentTime=1;v.muted=true;v.play();})()");
            var options = new GifOptions(1, 5, 480, 12, speed);
            using var cancel = new CancellationTokenSource();
            string? path = null;
            try
            {
                path = await source.ExportAsync(tools, options, tempRoot, new ImmediateProgress<string>(message => { if (cancelTest && message.StartsWith("화면 캡처")) cancel.Cancel(); }), cancel.Token);
                if (cancelTest) throw new Exception("Browser capture ignored cancellation");
            }
            catch (OperationCanceledException) when (cancelTest) { }
            await Task.Delay(100);
            using var state = JsonDocument.Parse(await browser.ExecuteScriptAsync("(()=>{const v=document.querySelector('video');return {time:v.currentTime,paused:v.paused,muted:v.muted,rate:v.playbackRate,session:!!window.__shinGif}})()"));
            var s = state.RootElement;
            var restoredTime = s.GetProperty("time").GetDouble();
            bool correctTime = playing ? restoredTime is > 1 and < 4 : Math.Abs(restoredTime - 5.2) < .1;
            if (!correctTime || s.GetProperty("paused").GetBoolean() == playing || s.GetProperty("muted").GetBoolean() != playing || s.GetProperty("rate").GetDouble() != 1.5 || s.GetProperty("session").GetBoolean()) throw new Exception("Browser playback not restored: " + s);
            if (Directory.GetDirectories(GifExport.TempRoot).Any(x => !tempBefore.Contains(x))) throw new Exception("Browser frames leaked");
            if (path != null) await VerifyGifAsync(path, options, tools, output);
            await browser.ExecuteScriptAsync("document.querySelector('#movie_player').classList.add('ad-showing')");
            try { await source.PositionAsync(); throw new Exception("Ad accepted"); } catch (InvalidOperationException) { }
            await browser.ExecuteScriptAsync("document.querySelector('#movie_player').className='ytp-live'");
            try { await source.PositionAsync(); throw new Exception("Live stream accepted"); } catch (InvalidOperationException) { }
            valid = false;
            try { await source.PositionAsync(); throw new Exception("Changed video accepted"); } catch (InvalidOperationException) { }
            return new { path, cancelTest, stateRestored = true, adRejected = true, navigationRejected = true, renderedPixelsOnly = true };
        }
        finally { valid = false; browser.Dispose(); window.Close(); }
    }
}
