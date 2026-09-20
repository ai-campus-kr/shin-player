using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

public partial class MainWindow
{
    private sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }

    private async Task RunCaptureTestsAsync(Func<string, Func<Task<object?>>, Task> test, string fixtures, string output)
    {
        var source = Path.Combine(fixtures, "캡처 '테스트 [한글].mp4");
        CaptureTools? tools = null;
        SubtitleCapture? service = null;
        SubtitleVideo? video = null;
        string? captured = null;
        var target = Path.Combine(output, "사진");
        await test("capture-tools-and-embedded-track-probe", async () =>
        {
            var environmentPath = Environment.GetEnvironmentVariable("PATH");
            try
            {
                if (Environment.GetEnvironmentVariable("SHIN_TEST_CAPTURE_DOWNLOAD") == "1") Environment.SetEnvironmentVariable("PATH", "");
                tools = await CaptureTools.EnsureAsync(new ImmediateProgress<string>(_ => { }), CancellationToken.None);
            }
            finally { Environment.SetEnvironmentVariable("PATH", environmentPath); }
            service = new(tools);
            video = await service.ProbeAsync(source, CancellationToken.None);
            if (video.Tracks.Count != 2 || video.Tracks[0].Language != "kor" || video.VideoIndex < 0) throw new Exception("Embedded track probe failed");
            var plain = await service.ProbeAsync(Path.Combine(fixtures, "한글 영상 sample.mp4"), CancellationToken.None);
            if (plain.Tracks.Count != 0) throw new Exception("External subtitles were treated as embedded");
            return new { tracks = video.Tracks.Select(x => x.Label), video.Duration, ffmpeg = tools.Ffmpeg };
        });
        await test("subtitle-cues-overlap-empty-invalid-and-delay", () =>
        {
            var cues = SubtitleCapture.ParseSrt("\uFEFF1\r\n00:00:01,000 --> 00:00:03,000\r\nfirst\r\nline\r\n\r\n2\r\n00:00:02,000 --> 00:00:04,000\r\noverlap\r\n\r\n3\r\n00:00:05,000 --> 00:00:04,000\r\ninvalid\r\n\r\n4\r\n00:00:04,000 --> 00:00:05,000\r\n<b></b>\r\n\r\n5\r\n00:00:09,000 --> 00:00:20,000\r\nend", 10, .5);
            if (cues.Count != 3 || cues[0].Midpoint != 2.5 || !cues[0].Text.Contains('\n') || cues[1].Start != 2.5 || cues[2].End != 10) throw new Exception("Cue handling regression");
            return Task.FromResult<object?>(new { count = cues.Count });
        });
        await test("subtitle-capture-png-timing-text-and-playback-preserved", async () =>
        {
            await OpenFilesAsync(new[] { source });
            await _player!.SetAsync("pause", true);
            var restart = _player.RestartCount;
            await SeekAsync(2, false);
            await WaitUntilAsync(() => _player.Flag("pause") && _player.RestartCount > restart && Math.Abs(_player.Number("time-pos") - 2) < .1, TimeSpan.FromSeconds(5));
            var original = _player.Number("time-pos");
            var result = await service!.ExportAsync(video!, video!.Tracks[0], target, true, 0, new ImmediateProgress<CaptureProgress>(_ => { }), CancellationToken.None);
            captured = result.Directory;
            var pictures = Directory.GetFiles(captured, "*.png").OrderBy(x => x).ToArray();
            if (result.Count != 3 || pictures.Length != 3) throw new Exception("Expected one PNG per subtitle cue");
            for (int i = 0; i < pictures.Length; i++)
            {
                var image = LoadPixels(pictures[i]);
                if (image.Width != 640 || image.Height != 360) throw new Exception("Wrong image dimensions");
                int r = image.Pixels[2], g = image.Pixels[1], b = image.Pixels[0];
                if (i == 0 && !(r > 200 && g < 20 && b < 20) || i == 1 && !(g > 90 && r < 20 && b < 20) || i == 2 && !(b > 200 && r < 20 && g < 20)) throw new Exception("Frame does not match subtitle timestamp");
                int whites = 0;
                for (int y = image.Height / 2; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                    {
                        int at = (y * image.Width + x) * 4;
                        if (image.Pixels[at] > 200 && image.Pixels[at + 1] > 200 && image.Pixels[at + 2] > 200) whites++;
                    }
                if (whites < 50) throw new Exception("Subtitle text missing from exported frame");
            }
            if (!_player.Flag("pause") || Math.Abs(_player.Number("time-pos") - original) > .1) throw new Exception($"Export changed playback state from {original}: {JsonSerializer.Serialize(GetStatus())}");
            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(captured, "캡처목록.json")));
            if (manifest.RootElement.GetProperty("state").GetString() != "complete" || manifest.RootElement.GetProperty("captures")[1].GetProperty("capture").GetDouble() != 3) throw new Exception("Capture manifest mismatch");
            return new { result.Count, result.Directory, playerPosition = original };
        });
        await test("subtitle-capture-second-track-no-overlay-no-overwrite", async () =>
        {
            var result = await service!.ExportAsync(video!, video!.Tracks[1], target, false, 0, new ImmediateProgress<CaptureProgress>(_ => { }), CancellationToken.None);
            if (result.Count != 2 || result.Directory == captured || Directory.GetFiles(captured!, "*.png").Length != 3) throw new Exception("Repeated capture overwrote output or mixed tracks");
            var first = LoadPixels(Directory.GetFiles(result.Directory, "*.png").OrderBy(x => x).First());
            for (int i = 0; i < first.Pixels.Length; i += 4)
                if (first.Pixels[i] > 20 || first.Pixels[i + 1] > 20 || first.Pixels[i + 2] < 200) throw new Exception("Unexpected text on plain video capture");
            return result;
        });
        await test("subtitle-capture-cancel-keeps-completed-only", async () =>
        {
            using var cancel = new CancellationTokenSource();
            string? directory = null;
            var progress = new ImmediateProgress<CaptureProgress>(value => { directory = value.Directory ?? directory; if (value.Completed == 1) cancel.Cancel(); });
            try { await service!.ExportAsync(video!, video!.Tracks[0], target, true, 0, progress, cancel.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
            if (directory == null || Directory.GetFiles(directory, "*.png").Length != 1) throw new Exception("Partial output was lost or cancellation was ignored");
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "캡처목록.json")));
            if (json.RootElement.GetProperty("state").GetString() != "cancelled") throw new Exception("Missing partial status");
            return new { directory };
        });
        await test("subtitle-capture-ass-mkv", async () =>
        {
            var mkv = await service!.ProbeAsync(Path.Combine(fixtures, "capture-ass.mkv"), CancellationToken.None);
            var result = await service.ExportAsync(mkv, mkv.Tracks.Single(), target, true, 0, new ImmediateProgress<CaptureProgress>(_ => { }), CancellationToken.None);
            if (result.Count != 3) throw new Exception("ASS capture failed");
            return result;
        });
        await test("capture-cancel-terminates-running-worker", async () =>
        {
            using var cancel = new CancellationTokenSource(300);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await CaptureTools.RunAsync(tools!.Ffmpeg, new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-re", "-f", "lavfi", "-i", "color=black:size=64x64:rate=1", "-t", "30", "-f", "null", "-" }, AppContext.BaseDirectory, cancel.Token);
                throw new Exception("Running worker ignored cancellation");
            }
            catch (OperationCanceledException) { }
            if (clock.Elapsed.TotalSeconds > 5) throw new Exception("Worker shutdown was too slow");
            return new { elapsedMs = clock.ElapsedMilliseconds };
        });
        await test("subtitle-capture-window-keybinding-and-export", async () =>
        {
            await OpenFilesAsync(new[] { source });
            await _player!.SetAsync("pause", true);
            await WaitUntilAsync(() => _player.Flag("pause") && _player.Number("video-params/w") > 0, TimeSpan.FromSeconds(5));
            await _player!.CommandAsync("keypress", "Ctrl+Shift+s");
            await WaitUntilAsync(() => _captureWindow is { IsLoaded: true }, TimeSpan.FromSeconds(5));
            var window = _captureWindow!;
            await window.Initialization;
            if (window.Background is not SolidColorBrush brush || brush.Color.R > 100) throw new Exception("Capture window lost dark theme");
            window.UpdateLayout();
            var selector = VisualChildren<ComboBox>(window).Single();
            var selected = (SubtitleTrack)selector.SelectedItem;
            if (!VisualChildren<TextBlock>(selector).Any(x => x.Text == selected.Label)) throw new Exception("Subtitle selector displays internal data instead of its language label");
            selector.IsDropDownOpen = true;
            await Task.Delay(80);
            var popup = (Popup)selector.Template.FindName("PART_Popup", selector);
            if (popup.Child == null || VisualChildren<ComboBoxItem>(popup.Child).Count() != 2) throw new Exception("Subtitle choices did not render");
            selector.SelectedIndex = 1;
            selector.IsDropDownOpen = false;
            window.UpdateLayout();
            if (!VisualChildren<TextBlock>(selector).Any(x => x.Text == ((SubtitleTrack)selector.SelectedItem).Label)) throw new Exception("Subtitle selection did not update its displayed label");
            selector.SelectedItem = selected;
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(output, "04-subtitle-capture.png"))) encoder.Save(file);
            var dialogOutput = Path.Combine(output, "dialog-export", Guid.NewGuid().ToString("N"));
            await window.StartCaptureAsync(dialogOutput);
            if (Directory.GetFiles(dialogOutput, "*.png", SearchOption.AllDirectories).Length != 3) throw new Exception("Capture dialog did not export");
            window.Close();
            if (_captureWindow != null) throw new Exception("Capture window did not close cleanly");
            return null;
        });
    }
    private static (byte[] Pixels, int Width, int Height) LoadPixels(string path)
    {
        using var file = File.OpenRead(path);
        var bitmap = new FormatConvertedBitmap(BitmapFrame.Create(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return (pixels, bitmap.PixelWidth, bitmap.PixelHeight);
    }
}
