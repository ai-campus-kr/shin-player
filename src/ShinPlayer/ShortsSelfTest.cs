using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinPlayer;

public partial class MainWindow
{
    private async Task RunShortsTestsAsync(Func<string, Func<Task<object?>>, Task> test, string fixtures, string output)
    {
        var tools = await CaptureTools.EnsureAsync(new Progress<string>(), CancellationToken.None);
        var video = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "쇼츠 '테스트 [한글].mp4"), CancellationToken.None);
        var destination = Path.Combine(output, "shorts-output");
        var progress = new ImmediateProgress<string>(_ => { });
        var options = new ShortsOptions(1, 3, 720, PanX: 0, Text: "신플레이어\n이 장면을 쇼츠로", TextColor: "#FFE03B");
        await RunShortsEditingTestsAsync(test, tools, video, fixtures, output);

        await test("shorts-validation-text-limits-and-three-minute-range", () =>
        {
            options.Validate(video.Duration);
            foreach (var invalid in new[] { options with { Start = -1 }, options with { End = double.NaN }, options with { Start = 3 }, options with { End = 182 },
                options with { PanX = double.NaN }, options with { TextY = 2 }, options with { Width = 1920 }, options with { FontSize = 300 },
                options with { Text = new string('가', 161) }, options with { TextColor = "red;movie=file" } })
            {
                try { invalid.Validate(600); throw new Exception("Invalid shorts settings accepted"); } catch (InvalidOperationException) { }
            }
            var longText = options with { Text = "한 줄\n두 줄\n세 줄\n네 줄\n다섯 줄" };
            try { ShortsComposition.RenderLabel(longText); throw new Exception("Clipped caption accepted"); } catch (InvalidOperationException) { }
            ShortsCheck(ShortsComposition.RenderLabel(options with { Text = "한글 100% [괄호] ' ; : \\" }) != null, "Literal punctuation lost");
            var range = new GifRangeSelector("쇼츠") { MaximumLength = 180, Width = 500 };
            range.Measure(new Size(500, 84)); range.Arrange(new Rect(0, 0, 500, 84)); range.Initialize(600, 0, 30);
            range.BeginDrag(GifRangeSelector.Part.End, range.XAt(30)); range.EndDrag(range.XAt(600));
            ShortsCheck(range.End == 180, "Shorts drag exceeded 3 minutes");
            range.AdjustKey(GifRangeSelector.Part.Selection, Key.End, ModifierKeys.None);
            ShortsCheck(range.Start == 420 && range.End == 600, "Shorts range cannot reach the end");
            return Task.FromResult<object?>(new { maxSeconds = 180, textLines = 4, literalText = true });
        });

        await test("shorts-mp4-trim-audio-korean-text-crop-and-no-overwrite", async () =>
        {
            await OpenFilesAsync([video.Path]); await _player!.SetAsync("pause", true); await SeekAsync(4, false);
            await WaitUntilAsync(() => _player.Flag("pause") && Math.Abs(_player.Number("time-pos") - 4) < .15, TimeSpan.FromSeconds(5));
            var path = await ShortsExport.ExportAsync(tools, video, options, destination, progress, CancellationToken.None);
            await VerifyShortsAsync(tools, path, options, true, output);
            string png = await ShortsFrameAsync(tools, path, "shorts-export.png", output);
            var pixels = LoadPixels(png); int bottom = ((pixels.Height * 3 / 4) * pixels.Width + pixels.Width / 2) * 4;
            ShortsCheck(pixels.Pixels[bottom + 2] > 200 && pixels.Pixels[bottom] < 30, "Crop did not select the left red scene");
            var label = ShortsComposition.RenderLabel(options)!; double scale = options.Width / 1080d;
            int yellow = 0, yellowOutside = 0;
            for (int y = 0; y < pixels.Height; y += 2)
            for (int x = 0; x < pixels.Width; x += 2)
            {
                int at = (y * pixels.Width + x) * 4;
                if (pixels.Pixels[at] < 100 && pixels.Pixels[at + 1] > 150 && pixels.Pixels[at + 2] > 180)
                {
                    if (new Rect(label.Bounds.X * scale - 3, label.Bounds.Y * scale - 3, label.Bounds.Width * scale + 6, label.Bounds.Height * scale + 6).Contains(new Point(x, y))) yellow++;
                    else yellowOutside++;
                }
            }
            ShortsCheck(yellow > 250 && yellowOutside < 30, "Caption raster or export position differs from preview");
            string pcm = Path.Combine(output, "shorts-audio.pcm");
            await CaptureTools.RunAsync(tools.Ffmpeg, ["-v", "error", "-i", path, "-vn", "-ac", "1", "-ar", "48000", "-f", "s16le", "-y", pcm], output, CancellationToken.None);
            var audio = await File.ReadAllBytesAsync(pcm); double energy = 0;
            for (int i = 0; i + 1 < audio.Length; i += 2) energy += Math.Pow(BitConverter.ToInt16(audio, i) / 32768d, 2);
            ShortsCheck(audio.Length >= 180000 && Math.Sqrt(energy / (audio.Length / 2)) > .03, "Audio is missing, silent or truncated");
            var repeat = await ShortsExport.ExportAsync(tools, video, options, destination, progress, CancellationToken.None);
            ShortsCheck(repeat != path && File.Exists(path), "Existing shorts was overwritten");
            ShortsCheck(_player.Flag("pause") && Math.Abs(_player.Number("time-pos") - 4) < .15, "Shorts export changed playback");
            File.Copy(path, Path.Combine(output, "ShinPlayer-shorts-demo.mp4"), true);
            return new { path, seconds = options.Length, audio = true, koreanCaptionPixels = yellow, repeat, playbackPreserved = true };
        });

        await test("shorts-fit-entire-video-black-bars-and-mute", async () =>
        {
            var fit = options with { Start = 4, End = 5, Fit = ShortsFit.Contain, Text = "", Audio = false };
            var path = await ShortsExport.ExportAsync(tools, video, fit, destination, progress, CancellationToken.None);
            await VerifyShortsAsync(tools, path, fit, false, output);
            var frame = LoadPixels(await ShortsFrameAsync(tools, path, "shorts-fit.png", output));
            int top = (20 * frame.Width + frame.Width / 2) * 4, left = (frame.Height / 2 * frame.Width + 30) * 4, right = (frame.Height / 2 * frame.Width + frame.Width - 30) * 4;
            ShortsCheck(Enumerable.Range(0, 3).All(c => frame.Pixels[top + c] < 10), "Contain padding is not black");
            ShortsCheck(frame.Pixels[left + 2] > 220 && frame.Pixels[right] > 220, "Contain mode cut off the source edges");
            int marker = (471 * frame.Width + frame.Width / 2) * 4;
            ShortsCheck(frame.Pixels[marker] < 40 && frame.Pixels[marker + 1] > 210 && frame.Pixels[marker + 2] > 210, "Trim used the pre-4-second scene");
            return new { entireFrame = true, blackBars = true, audio = false };
        });

        await test("shorts-right-crop-1080-output-and-silent-source", async () =>
        {
            var right = options with { Start = 0, End = .4, Width = 1080, PanX = 1, Text = "", Audio = false };
            var path = await ShortsExport.ExportAsync(tools, video, right, destination, progress, CancellationToken.None);
            await VerifyShortsAsync(tools, path, right, false, output);
            var frame = LoadPixels(await ShortsFrameAsync(tools, path, "shorts-right.png", output));
            int center = (frame.Height / 2 * frame.Width + frame.Width / 2) * 4;
            ShortsCheck(frame.Pixels[center] > 220 && frame.Pixels[center + 2] < 30, "Right crop did not select blue scene");
            var silent = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "캡처 '테스트 [한글].mp4"), CancellationToken.None);
            var noSound = right with { Width = 720, Audio = true };
            var silentPath = await ShortsExport.ExportAsync(tools, silent, noSound, destination, progress, CancellationToken.None);
            await VerifyShortsAsync(tools, silentPath, noSound, false, output);
            return new { output1080 = true, cropRight = true, sourceWithoutAudio = true };
        });

        await test("shorts-portrait-rotation-preview-and-export-agree", async () =>
        {
            var rotated = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "shorts-rotated-90.mp4"), CancellationToken.None);
            var portrait = options with { Start = 0, End = .4, Text = "", Audio = false, PanX = .5 };
            var preview = await ShortsExport.FrameAsync(tools, rotated, .1, CancellationToken.None);
            ShortsCheck(preview.PixelHeight > preview.PixelWidth, "Rotation ignored by preview");
            string expected = Path.Combine(output, "shorts-rotation-source.png"); ShortsComposition.SavePng(preview, expected);
            var path = await ShortsExport.ExportAsync(tools, rotated, portrait, destination, progress, CancellationToken.None);
            await VerifyShortsAsync(tools, path, portrait, false, output);
            var original = LoadPixels(expected); var actual = LoadPixels(await ShortsFrameAsync(tools, path, "shorts-rotation.png", output));
            foreach (double y in new[] { .15, .5, .85 })
            {
                int a = ((int)(original.Height * y) * original.Width + original.Width / 2) * 4;
                int b = ((int)(actual.Height * y) * actual.Width + actual.Width / 2) * 4;
                ShortsCheck(Enumerable.Range(0, 3).All(c => Math.Abs(original.Pixels[a + c] - actual.Pixels[b + c]) < 25), "Rotated export differs from preview");
            }
            return new { rotation = 90, previewMatchesExport = true };
        });

        await test("shorts-delayed-audio-keeps-original-sync", async () =>
        {
            var delayed = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "shorts-delayed-audio.mp4"), CancellationToken.None);
            var settings = options with { Start = 0, End = 2, Text = "", Fit = ShortsFit.Contain };
            var path = await ShortsExport.ExportAsync(tools, delayed, settings, destination, progress, CancellationToken.None);
            string pcm = Path.Combine(output, "shorts-delay.pcm");
            await CaptureTools.RunAsync(tools.Ffmpeg, ["-v", "error", "-i", path, "-vn", "-ac", "1", "-ar", "48000", "-f", "s16le", "-y", pcm], output, CancellationToken.None);
            var samples = await File.ReadAllBytesAsync(pcm);
            double Rms(int start, int end) => Math.Sqrt(Enumerable.Range(start, end - start).Average(i => Math.Pow(BitConverter.ToInt16(samples, i * 2) / 32768d, 2)));
            ShortsCheck(samples.Length > 180000 && Rms(4800, 24000) < .002 && Rms(60000, 84000) > .03, "Delayed audio moved to the beginning");
            return new { initialSilencePreserved = true, soundAfterOneSecond = true };
        });

        await test("shorts-cancellation-cleans-encoder-and-unfinished-files", async () =>
        {
            var before = Directory.Exists(ShortsExport.TempRoot) ? Directory.GetDirectories(ShortsExport.TempRoot) : [];
            var count = Directory.GetFiles(destination, "*.mp4", SearchOption.AllDirectories).Length;
            using var cancel = new CancellationTokenSource();
            try
            {
                await ShortsExport.ExportAsync(tools, video, options with { Start = 0, End = 8, Width = 1080 }, destination,
                    new ImmediateProgress<string>(_ => cancel.CancelAfter(180)), cancel.Token);
                throw new Exception("Cancel ignored");
            }
            catch (OperationCanceledException) { }
            ShortsCheck(!Directory.GetDirectories(ShortsExport.TempRoot).Any(p => !before.Contains(p)) &&
                Directory.GetFiles(destination, "*.partial", SearchOption.AllDirectories).Length == 0 &&
                Directory.GetFiles(destination, "*.mp4", SearchOption.AllDirectories).Length == count, "Cancelled export left unfinished files");
            return new { cancelled = true, tempCleaned = true };
        });

        await test("shorts-button-preview-drag-text-four-themes-and-close", async () =>
        {
            await OpenFilesAsync([Path.Combine(fixtures, "신플레이어 쇼츠 데모.mp4")]); await _player!.SetAsync("pause", true);
            ShortsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var dialog = _shortsWindow ?? throw new Exception("Shorts button not wired");
            try
            {
                await WaitUntilAsync(() => dialog.IsLoaded, TimeSpan.FromSeconds(5)); await dialog.Initialization;
                ShortsCheck(dialog.Preview.HasFrame, "Initial frame missing");
                dialog.StartTime.Text = "1"; dialog.EndTime.Text = "3"; dialog.Caption.Text = "이 장면을, 쇼츠로.\n신플레이어"; dialog.FontSizeInput.Value = 64;
                await dialog.RefreshPreviewAsync(); dialog.UpdateLayout();
                var label = dialog.Preview.TextBounds;
                var grab = new Point(label.X + label.Width / 2, label.Y + label.Height / 2);
                dialog.Preview.BeginDrag(grab); dialog.Preview.EndDrag(new Point(grab.X + 35, grab.Y + 700));
                var changed = dialog.ReadOptions();
                ShortsCheck(changed.TextY > .5 && dialog.Preview.TextBounds.Bottom < 1841, "Text drag did not update export coordinates");
                dialog.Preview.BeginDrag(new Point(500, 1750)); dialog.Preview.EndDrag(new Point(9000, 1750));
                ShortsCheck(dialog.ReadOptions().PanX == 0, "Crop drag did not clamp at source edge");
                dialog.PreviewTime.Value = 2.8; await dialog.RefreshPreviewAsync(); ShortsCheck(dialog.Preview.HasFrame, "Scrubbing lost frame");
                dialog.StartTime.Text = "invalid"; ShortsCheck(!dialog.SaveButton.IsEnabled, "Invalid range allows saving");
                dialog.Range.AdjustKey(GifRangeSelector.Part.End, Key.Right, ModifierKeys.None);
                ShortsCheck(dialog.SaveButton.IsEnabled, "Range drag did not recover invalid typed input");
                VisualChildren<Button>(dialog).Single(b => Equals(b.Content, "가운데 맞춤")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                dialog.TextTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); dialog.UpdateLayout();
                VisualChildren<Button>(dialog).Single(b => Equals(b.Content, "위")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                dialog.FontInput.SelectedIndex = 1; dialog.ColorInput.HexInput.Text = "#B7FF00";
                dialog.ColorInput.HexInput.Text = "#zzzzzz"; ShortsCheck(!dialog.SaveButton.IsEnabled, "Invalid custom color allows saving");
                dialog.ColorInput.HexInput.Text = "#B7FF00"; ShortsCheck(dialog.SaveButton.IsEnabled, "Valid color does not recover saving");
                dialog.ZoomInput.Value = 1.5;
                dialog.Preview.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120) { RoutedEvent = Mouse.MouseWheelEvent });
                ShortsCheck(Math.Abs(dialog.ReadOptions().Zoom - 1.6) < .001, "Preview wheel and zoom slider disagree");
                dialog.ZoomInput.Value = 1;
                dialog.Caption.Text = "영상을 보다가,\n바로 쇼츠로.";
                foreach (var design in UiDesigns.All)
                {
                    ApplyUiDesign(design.Id); dialog.Width = 1020; dialog.Height = 830;
                    await dialog.RefreshPreviewAsync();
                    await dialog.Dispatcher.InvokeAsync(dialog.UpdateLayout, System.Windows.Threading.DispatcherPriority.Render);
                    await Task.Delay(80);
                    string screenshot = Path.Combine(output, "shorts-" + design.Id + ".png");
                    OnlineSelfTest.Capture(dialog, screenshot);
                    dialog.FrameTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); dialog.UpdateLayout();
                    OnlineSelfTest.Capture(dialog, Path.Combine(output, "shorts-frame-" + design.Id + ".png"));
                    dialog.TextTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); dialog.UpdateLayout();
                    var captured = LoadPixels(screenshot);
                    var previewBounds = dialog.Preview.TransformToAncestor(dialog).TransformBounds(new Rect(dialog.Preview.RenderSize));
                    int visible = 0, sampled = 0;
                    for (int y = (int)previewBounds.Top + 5; y < previewBounds.Bottom - 5; y += 8)
                    for (int x = (int)previewBounds.Left + 5; x < previewBounds.Right - 5; x += 8)
                    {
                        int at = (y * captured.Width + x) * 4; sampled++;
                        if (captured.Pixels[at] + captured.Pixels[at + 1] + captured.Pixels[at + 2] > 45) visible++;
                    }
                    ShortsCheck(visible > sampled * .5, "Preview is blank after resize/theme change: " + design.Id);
                    dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight; dialog.UpdateLayout();
                    var save = dialog.SaveButton.TransformToAncestor(dialog).TransformBounds(new Rect(dialog.SaveButton.RenderSize));
                    ShortsCheck(new Rect(0, 0, dialog.ActualWidth, dialog.ActualHeight).Contains(save), "Save button clipped at minimum window size");
                    ShortsCheck(dialog.Preview.ActualHeight > 0 && dialog.Preview.ActualWidth > 0, "Preview collapsed");
                }
                ApplyUiDesign("minimal"); dialog.Width = 1020; dialog.Height = 830;
                VisualChildren<Button>(dialog).Single(b => Equals(b.Content, "위")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                dialog.WidthInput.SelectedIndex = 1;
                var blocker = Path.Combine(output, "shorts-blocked-destination"); await File.WriteAllTextAsync(blocker, "occupied");
                await dialog.ExportAsync(blocker);
                ShortsCheck(dialog.Feedback.State == ExportState.Failed && dialog.SavedPath == null && !dialog.OpenResult.IsEnabled, "Export failure appears successful");
                var saving = dialog.ExportAsync(destination);
                ShortsCheck(dialog.IsBusy && dialog.Feedback.State == ExportState.Working && dialog.Feedback.IsProgressVisible && !dialog.OpenResult.IsEnabled, "Shorts progress is unclear");
                await saving; ShortsCheck(dialog.SavedPath != null, "Dialog export failed");
                await VerifyExportCompletionAsync(dialog, dialog.Feedback, dialog.OpenResult, dialog.OpenFolder, "shorts", output);
                OnlineSelfTest.Capture(dialog, Path.Combine(output, "shorts-dialog.png"));
                File.Copy(dialog.SavedPath!, Path.Combine(output, "ShinPlayer-shorts-demo.mp4"), true);
                await ShortsFrameAsync(tools, dialog.SavedPath!, "shorts-demo-frame.png", output);
                dialog.FontInput.SelectedIndex = 2;
                ShortsCheck(dialog.Feedback.State == ExportState.Edited, "Font change still appears saved");
                dialog.EndTime.Text = "8";
                ShortsCheck(dialog.Feedback.State == ExportState.Edited && dialog.OpenResult.Content.ToString() == "이전 영상 열기", "Changed shorts settings still appear saved");
                var pending = dialog.ExportAsync(destination); await Task.Delay(80); dialog.Close(); await pending;
                await WaitUntilAsync(() => _shortsWindow == null, TimeSpan.FromSeconds(5));
                ShortsCheck(dialog.Feedback.State == ExportState.Cancelled && !dialog.Feedback.IsProgressVisible, "Cancellation looks like success");
                ShortsCheck(Directory.GetFiles(destination, "*.partial", SearchOption.AllDirectories).Length == 0, "Closing editor left unfinished output");
                ShortsCheck(IsVisible && IsEnabled && AddressBar.IsVisible && AddressInput.ActualHeight >= 28, "Closing shorts editor hid or disabled the player address bar");
                return new { button = true, preview = true, dragCoordinates = true, themes = 4, typedInputRecovery = true, closeCancels = true, neonCompletion = true, fixedFooter = true, editedState = true, failureRetry = true };
            }
            finally { await dialog.StopAsync(); dialog.Close(); }
        });
    }

    private static void ShortsCheck(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task VerifyShortsAsync(CaptureTools tools, string path, ShortsOptions options, bool audio, string output)
    {
        using var json = JsonDocument.Parse(await CaptureTools.RunAsync(tools.Ffprobe, ["-v", "error", "-show_entries", "stream=codec_type,codec_name,width,height,sample_aspect_ratio,pix_fmt:format=duration", "-of", "json", path], output, CancellationToken.None));
        var streams = json.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var video = streams.Single(s => s.GetProperty("codec_type").GetString() == "video");
        ShortsCheck(video.GetProperty("width").GetInt32() == options.Width && video.GetProperty("height").GetInt32() == options.Height && video.GetProperty("codec_name").GetString() == "h264" && video.GetProperty("pix_fmt").GetString() == "yuv420p" && video.GetProperty("sample_aspect_ratio").GetString() == "1:1", "Wrong MP4 dimensions or codec");
        ShortsCheck(streams.Any(s => s.GetProperty("codec_type").GetString() == "audio") == audio, "Wrong audio inclusion");
        double duration = double.Parse(json.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        ShortsCheck(Math.Abs(duration - options.Length) < .08, "Wrong trim duration: " + duration);
    }
    private static async Task<string> ShortsFrameAsync(CaptureTools tools, string path, string name, string output)
    {
        string png = Path.Combine(output, name);
        await CaptureTools.RunAsync(tools.Ffmpeg, ["-v", "error", "-ss", "0.1", "-i", path, "-frames:v", "1", "-update", "1", "-y", png], output, CancellationToken.None);
        return png;
    }
}
