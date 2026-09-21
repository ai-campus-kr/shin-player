using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

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
                dialog.UpdateLayout();
                dialog.Range.BeginDrag(GifRangeSelector.Part.End, dialog.Range.XAt(dialog.Range.End) + 8);
                dialog.Range.DragTo(dialog.Range.XAt(600) + 8); dialog.Range.EndDrag();
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
        await test("gif-range-drag-geometry-resize-and-scaling", async () =>
        {
            var longVideo = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "gif-ten-minutes.mp4"), CancellationToken.None);
            var dialog = new GifWindow(_ => Task.FromResult<GifSource>(new LocalGifSource(longVideo, () => 60))) { Owner = this };
            try
            {
                dialog.Show(); await dialog.Initialization; var range = dialog.Range;
                foreach (double width in new[] { 600d, 920d })
                foreach (double scale in new[] { 1d, 1.25, 1.5, 2 })
                {
                    dialog.Width = width; range.LayoutTransform = new ScaleTransform(scale, scale); dialog.UpdateLayout();
                    range.SetRange(60, 180);
                    // Grabbing off-center must retain its offset across repeated layout passes.
                    double grab = range.XAt(60) - 13, destination = range.XAt(90) - 13;
                    var rootPoint = range.TranslatePoint(new Point(destination, 30), dialog);
                    range.BeginDrag(GifRangeSelector.Part.Start, grab);
                    for (int i = 0; i < 10; i++) { range.DragTo(dialog.TranslatePoint(rootPoint, range).X); dialog.UpdateLayout(); }
                    range.EndDrag();
                    if (Math.Abs(range.Start - 90) > .001 || range.End != 180 || GifOptions.Parse(dialog.StartTime.Text) != 90) throw new Exception("Drag start drifted or text did not synchronize");
                    range.BeginDrag(GifRangeSelector.Part.End, range.XAt(180) + 7);
                    range.EndDrag(range.XAt(300) + 7); // Mouse-up must use its coordinate even without a move event.
                    if (range.End != 300) throw new Exception("End handle jumped");
                    range.BeginDrag(GifRangeSelector.Part.Selection, range.XAt(150));
                    range.DragTo(range.XAt(200)); range.EndDrag();
                    if (range.Start != 140 || range.End != 350) throw new Exception("Moving selection changed its length");
                    range.BeginDrag(GifRangeSelector.Part.Start, range.XAt(140));
                    range.DragTo(range.ActualWidth + 500); range.EndDrag();
                    if (Math.Abs(range.End - range.Start - .2) > .000001) throw new Exception("Handles crossed");
                    range.BeginDrag(GifRangeSelector.Part.Selection, range.XAt(range.Start));
                    range.DragTo(-2000); range.EndDrag();
                    if (range.Start != 0 || Math.Abs(range.End - .2) > .000001) throw new Exception("Outside drag did not clamp");
                }
                range.LayoutTransform = Transform.Identity;
                return new { widths = new[] { 600, 920 }, layoutScales = new[] { 1, 1.25, 1.5, 2 }, grabOffsetPreserved = true, crossingPrevented = true };
            }
            finally { dialog.Close(); }
        });
        await test("gif-range-zoom-keyboard-typed-input-and-recovery", async () =>
        {
            var dialog = new GifWindow(_ => Task.FromResult<GifSource>(source)) { Owner = this };
            try
            {
                dialog.Show(); await dialog.Initialization; dialog.UpdateLayout(); var range = dialog.Range;
                dialog.StartTime.Text = "1"; dialog.EndTime.Text = "5";
                if (range.Start != 1 || range.End != 5) throw new Exception("Typed range did not move handles");
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog), Environment.TickCount, Key.Right) { RoutedEvent = PreviewKeyDownEvent };
                range.StartHandle.RaiseEvent(key);
                if (!key.Handled || Math.Abs(range.Start - 1.1) > .000001) throw new Exception("Handle keyboard event was not handled");
                range.AdjustKey(GifRangeSelector.Part.Start, Key.Right, ModifierKeys.Shift);
                range.AdjustKey(GifRangeSelector.Part.End, Key.Left, ModifierKeys.Shift);
                if (range.Start != 2.1 || range.End != 4) throw new Exception("Fine adjustment failed");
                VisualChildren<Button>(dialog).Single(b => Equals(b.Content, "선택 확대")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (range.ViewStart >= range.Start || range.ViewEnd <= range.End || range.ViewEnd - range.ViewStart >= source.Duration) throw new Exception("Selection zoom failed");
                range.AdjustKey(GifRangeSelector.Part.Selection, Key.Home, ModifierKeys.None);
                if (Math.Abs(range.Start - range.ViewStart) > .000001 || Math.Abs(range.End - range.Start - 1.9) > .000001) throw new Exception("Zoomed selection moved outside view");
                VisualChildren<Button>(dialog).Single(b => Equals(b.Content, "전체 보기")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (range.ViewStart != 0 || range.ViewEnd != source.Duration) throw new Exception("Full timeline not restored");
                dialog.StartTime.Text = "invalid";
                if (VisualChildren<Button>(dialog).Single(b => Equals(b.Content, "GIF 만들기")).IsEnabled) throw new Exception("Invalid typed time enables export");
                range.AdjustKey(GifRangeSelector.Part.End, Key.Right, ModifierKeys.None);
                if (!VisualChildren<Button>(dialog).Single(b => Equals(b.Content, "GIF 만들기")).IsEnabled || GifOptions.Parse(dialog.StartTime.Text) != range.Start) throw new Exception("Drag/keyboard did not recover from invalid typing");
                range.StartHandle.RaiseEvent(new DragStartedEventArgs(0, 0));
                range.StartHandle.RaiseEvent(new DragDeltaEventArgs(0, 0));
                range.StartHandle.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
                double before = range.Start; range.DragTo(2000);
                if (before != range.Start) throw new Exception("Completed gesture remained active");
                OnlineSelfTest.Capture(dialog, Path.Combine(output, "gif-range-zoom.png"));
                return new { keyboardEvent = true, zoom = true, typedTimeSync = true, invalidInputRecovery = true };
            }
            finally { dialog.Close(); }
        });
        await test("gif-range-short-video-and-one-hour-limit", () =>
        {
            var range = new GifRangeSelector { Width = 600 }; range.Measure(new Size(600, 84)); range.Arrange(new Rect(0, 0, 600, 84));
            range.Initialize(.2, 0, .2);
            range.AdjustKey(GifRangeSelector.Part.Start, Key.End, ModifierKeys.Control);
            range.AdjustKey(GifRangeSelector.Part.End, Key.Home, ModifierKeys.Control);
            if (range.Start != 0 || range.End != .2) throw new Exception("Shortest valid source changed");
            range.Initialize(.3, .1, .3); // .1 + .2 is slightly greater than .3 in floating-point arithmetic.
            range.AdjustKey(GifRangeSelector.Part.End, Key.Home, ModifierKeys.None);
            if (Math.Abs(range.End - range.Start - .2) > .000001) throw new Exception("Fractional boundary failed");
            range.Initialize(7200, 0, 5);
            range.BeginDrag(GifRangeSelector.Part.End, range.XAt(5)); range.DragTo(range.XAt(7200)); range.EndDrag();
            if (range.End != 3600) throw new Exception("One-hour drag limit ignored");
            range.AdjustKey(GifRangeSelector.Part.Selection, Key.End, ModifierKeys.None);
            if (range.Start != 3600 || range.End != 7200) throw new Exception("Selection cannot reach video end");
            if (range.SetRange(double.NaN, 10) || range.SetRange(4, 1)) throw new Exception("Invalid times accepted");
            return Task.FromResult<object?>(new { shortestSourceSeconds = .2, maximumSelectionSeconds = 3600 });
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
            // Verify the dominant scene color after GIF palette quantization.
            if (frame.Pixels[center + color] < 90 || Enumerable.Range(0, 3).Any(c => c != color && frame.Pixels[center + c] > 65))
                throw new Exception("GIF frame timing is wrong at " + at);
        }
    }
}
