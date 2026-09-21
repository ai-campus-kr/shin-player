using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

public partial class MainWindow
{
    // Isolated opt-in integration runner: the real window, decoder, commands and state.
    // It does not register file associations or read/write the user's playback history.
    public async Task RunSelfTestAsync(string fixtureDirectory, string outputDirectory, bool shortsOnly = false)
    {
        Directory.CreateDirectory(outputDirectory);
        var results = new List<object>();
        int failures = 0;
        async Task Test(string name, Func<Task<object?>> run)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var detail = await run();
                results.Add(new { name, passed = true, elapsedMs = timer.Elapsed.TotalMilliseconds, detail });
            }
            catch (Exception ex)
            {
                failures++;
                results.Add(new { name, passed = false, elapsedMs = timer.Elapsed.TotalMilliseconds, error = ex.ToString() });
            }
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "results.json"), JsonSerializer.Serialize(new { failures, tests = results }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (shortsOnly)
        {
            await RunShortsTestsAsync(Test, fixtureDirectory, outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "complete.txt"), failures == 0 ? "PASS" : $"FAIL {failures}");
            Environment.ExitCode = failures == 0 ? 0 : 1; return;
        }
        await Task.Delay(150);
        await Test("empty-window-layout", async () =>
        {
            CaptureUi(Path.Combine(outputDirectory, "01-home.png"));
            var elapsed = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
            VerifyButtonLayout();
            foreach (var width in new[] { 820d, 1180d })
            {
                Width = width; Height = width == 820 ? 560 : 760;
                PlaylistPanel.Visibility = Visibility.Visible;
                await Task.Delay(80);
                VerifyButtonLayout();
                if (EmptyArtwork.Visibility == Visibility.Visible) throw new Exception("Empty artwork overlaps the narrow stage");
                CaptureUi(Path.Combine(outputDirectory, $"home-playlist-{width:0}.png"));
            }
            PlaylistPanel.Visibility = Visibility.Collapsed; UpdateLayout();
            return new { readyMs = elapsed, windowWidth = ActualWidth, windowHeight = ActualHeight };
        });
        var fixture = Path.Combine(fixtureDirectory, "한글 영상 sample.mp4");
        await Test("standby-prepares-gpu-renderer", async () =>
        {
            await WarmupAsync();
            return new { engineReady = _player!.Flag("vo-configured") };
        });
        await Test("audio-boost-restored-at-engine-startup", async () =>
        {
            if (Settings.AudioBoostDb != 6 || _player!.AudioBoostDb != 6 || !_player.ReadProperty("af").Contains(AudioBoost.Label))
                throw new Exception("Saved boost was not restored when the engine started");
            await SetAudioBoostAsync(0);
            if (_player.ReadProperty("af").Contains(AudioBoost.Label)) throw new Exception("Disabling idle boost left its filter attached");
            return new { restoredDb = 6, disabledWhileIdle = true };
        });
        await Test("h264-aac-unicode-path-real-decoding", async () =>
        {
            await OpenFilesAsync(new[] { fixture });
            await WaitUntilAsync(() => _loaded && _player!.Number("time-pos") > .15, TimeSpan.FromSeconds(20));
            await _player!.SetAsync("pause", true);
            await WaitUntilAsync(() => _player.Flag("pause"), TimeSpan.FromSeconds(2));
            var decoded = Path.Combine(outputDirectory, "decoded-h264.png");
            await _player.CommandAsync("screenshot-to-file", decoded, "video");
            if (!File.Exists(decoded) || new FileInfo(decoded).Length < 1024) throw new Exception("No decoded video frame");
            return new { loadMs = LastLoadMilliseconds, duration = _player.Number("duration"), width = _player.Number("video-params/w"), height = _player.Number("video-params/h"), decoder = _player.Text("hwdec-current"), engine = _player.Version };
        });
        await Test("pause-resume", async () =>
        {
            if (_player == null) throw new Exception("No player");
            double before = _player.Number("time-pos");
            await Task.Delay(250);
            if (Math.Abs(_player.Number("time-pos") - before) > .15) throw new Exception("Position moved while paused");
            await TogglePlayAsync();
            await WaitUntilAsync(() => _player.Number("time-pos") > before + .2, TimeSpan.FromSeconds(3));
            await _player.SetAsync("pause", true);
            return null;
        });
        await Test("speed-range-fine-control-pitch-correction", async () =>
        {
            foreach (var speed in new[] { .25, .75, 1.25, 1.5, 2, 3, 8, 1.05, 1 })
            {
                await SetSpeedAsync(speed);
                await WaitUntilAsync(() => Math.Abs(_player!.Number("speed") - speed) < .001, TimeSpan.FromSeconds(3));
            }
            if (!_player!.Flag("audio-pitch-correction")) throw new Exception("Pitch correction is disabled");
            return new { testedSpeeds = new[] { .25, .75, 1.25, 1.5, 2, 3, 8, 1.05, 1 } };
        });
        await Test("exact-seek-paused", async () =>
        {
            long restart = _player!.RestartCount;
            var timer = Stopwatch.StartNew();
            await SeekAsync(4, false);
            await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - 4) < .15 && _player.RestartCount > restart, TimeSpan.FromSeconds(5));
            return new { seekMs = timer.Elapsed.TotalMilliseconds, actualPosition = _player!.Number("time-pos") };
        });
        await Test("double-speed-actual-playback-clock", async () =>
        {
            await SetSpeedAsync(2);
            await _player!.SetAsync("pause", false);
            await Task.Delay(150);
            var before = _player.Number("time-pos");
            var watch = Stopwatch.StartNew();
            await Task.Delay(600);
            var elapsed = watch.Elapsed.TotalSeconds;
            var mediaElapsed = _player.Number("time-pos") - before;
            await _player.SetAsync("pause", true);
            await SetSpeedAsync(1);
            if (mediaElapsed / elapsed < 1.5 || mediaElapsed / elapsed > 2.5) throw new Exception("Unexpected playback rate: " + mediaElapsed / elapsed);
            return new { measuredRate = mediaElapsed / elapsed };
        });
        await Test("seekbar-commit-one-command", async () =>
        {
            long before = _player!.SeekRequestCount;
            _scrubbing = true;
            SeekBar.Value = 2;
            CommitSeek();
            CommitSeek(); // Mouse-up and lost capture can both arrive for one gesture.
            await WaitUntilAsync(() => Math.Abs(_player!.Number("time-pos") - 2) < .15, TimeSpan.FromSeconds(5));
            if (_player.SeekRequestCount != before + 1) throw new Exception("Duplicate seek for one gesture");
            return null;
        });
        await Test("seekbar-handled-pointer-events", async () =>
        {
            // Verify handled-event routing and the committed native seek here.
            // The physical cursor can move between synthetic down/up events;
            // independent coordinate expectations belong to SeekSelfTest.
            CancelSeek();
            UpdateLayout();
            var requests = _player!.SeekRequestCount;
            var restart = _player.RestartCount;
            SeekBar.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = SeekBar, Handled = true });
            if (!_scrubbing) throw new Exception("Slider consumed mouse-down before the seek handler received it.");
            SeekBar.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = SeekBar, Handled = true });
            var expected = SeekBar.Value;
            if (_scrubbing) throw new Exception("Timeline interaction did not finish on pointer release.");
            await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - expected) < .15 && _player.RestartCount > restart, TimeSpan.FromSeconds(5));
            if (_player.SeekRequestCount != requests + 1) throw new Exception("Handled pointer events did not issue exactly one seek");
            return new { committedPosition = expected, actualPosition = _player.Number("time-pos") };
        });
        await Test("volume-mute", async () =>
        {
            await _player!.SetAsync("volume", 42);
            await _player.SetAsync("mute", true);
            await WaitUntilAsync(() => Math.Abs(_player.Number("volume") - 42) < .1 && _player.Flag("mute"), TimeSpan.FromSeconds(3));
            return null;
        });
        await Test("seekbar-lost-capture-and-paused-seek", async () =>
        {
            await _player!.SetAsync("pause", true);
            _scrubbing = true;
            SeekBar.Value = 4;
            SeekBar.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
            { RoutedEvent = LostMouseCaptureEvent, Source = SeekBar, Handled = true });
            await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - 4) < .15, TimeSpan.FromSeconds(5));
            if (_scrubbing || !_player.Flag("pause")) throw new Exception("Lost capture left a stuck or unpaused scrub");
            return null;
        });
        await Test("rapid-mute-toggles", async () =>
        {
            await _player!.SetAsync("mute", true);
            await WaitUntilAsync(() => _player.Flag("mute"), TimeSpan.FromSeconds(2));
            await Task.WhenAll(ToggleMuteAsync(), ToggleMuteAsync());
            await Task.Delay(100);
            if (!_player.Flag("mute")) throw new Exception("Two toggles must restore mute");
            return null;
        });
        await Test("ab-loop-actual-repetition", async () =>
        {
            await ClearLoopAsync();
            await SeekAsync(1, false);
            await WaitUntilAsync(() => Math.Abs(_player!.Number("time-pos") - 1) < .15, TimeSpan.FromSeconds(5));
            await CycleLoopAsync();
            await SeekAsync(2, false);
            await WaitUntilAsync(() => Math.Abs(_player!.Number("time-pos") - 2) < .15, TimeSpan.FromSeconds(5));
            await CycleLoopAsync();
            await _player!.SetAsync("pause", false);
            await Task.Delay(1700);
            var position = _player.Number("time-pos");
            if (position < .85 || position > 2.2) throw new Exception("Playback escaped AB interval: " + position);
            await _player.SetAsync("pause", true);
            await ClearLoopAsync();
            return new { positionAfterLoop = position };
        });
        await Test("external-korean-subtitles", async () =>
        {
            await _player!.CommandAsync("sub-add", Path.Combine(fixtureDirectory, "테스트 자막.srt"), "select");
            await WaitUntilAsync(() => _player.Text("sid") is not "" and not "no", TimeSpan.FromSeconds(5));
            await SeekAsync(1, false);
            await Task.Delay(200);
            await _player.CommandAsync("screenshot-to-file", Path.Combine(outputDirectory, "decoded-subtitles.png"), "subtitles");
            return new { sid = _player.Text("sid") };
        });
        await Test("keyboard-actions-frame-step-fullscreen", async () =>
        {
            await _player!.CommandAsync("keypress", "}");
            await WaitUntilAsync(() => Math.Abs(_player!.Number("speed") - 1.05) < .001, TimeSpan.FromSeconds(3));
            await _player.CommandAsync("keypress", "r");
            await WaitUntilAsync(() => Math.Abs(_player!.Number("speed") - 1) < .001, TimeSpan.FromSeconds(3));
            var before = _player!.Number("time-pos");
            await _player.CommandAsync("frame-step");
            await WaitUntilAsync(() => _player.Number("time-pos") > before, TimeSpan.FromSeconds(3));
            ToggleFullscreen(); if (!_fullScreen) throw new Exception("No fullscreen");
            ToggleFullscreen(); if (_fullScreen) throw new Exception("Fullscreen stuck");
            return null;
        });
        await Test("playlist-add-switch-remove", async () =>
        {
            await OpenFilesAsync(new[] { fixture, Path.Combine(fixtureDirectory, "hevc.mkv") });
            await WaitUntilAsync(() => _loaded, TimeSpan.FromSeconds(10));
            if (_queue.Count != 2) throw new Exception("Queue count mismatch");
            await PlayIndexAsync(1);
            await WaitUntilAsync(() => _loaded && _player!.Text("path").EndsWith("hevc.mkv"), TimeSpan.FromSeconds(10));
            QueueList.SelectedIndex = 0; await RemoveSelectedAsync();
            if (_queue.Count != 1 || _currentIndex != 0) throw new Exception("Queue index mismatch after remove");
            return null;
        });
        await Test("playlist-remove-current-and-last", async () =>
        {
            var next = Path.Combine(fixtureDirectory, "vp9.webm");
            await OpenFilesAsync(new[] { fixture, next });
            QueueList.SelectedIndex = 0;
            await RemoveSelectedAsync();
            if (_queue.Count != 1 || _currentIndex != 0 || _currentPath != next || !_loaded)
                throw new Exception("Removing current item did not play the successor");
            QueueList.SelectedIndex = 0;
            await RemoveSelectedAsync();
            await WaitUntilAsync(() => _player!.Flag("idle-active"), TimeSpan.FromSeconds(5));
            if (_queue.Count != 0 || _loaded || _currentPath != null) throw new Exception("Removing last item did not stop");
            return null;
        });
        await Test("rapid-open-replace-and-append", async () =>
        {
            var last = Path.Combine(fixtureDirectory, "vp9.webm");
            await Task.WhenAll(OpenFilesAsync(new[] { fixture }), OpenFilesAsync(new[] { Path.Combine(fixtureDirectory, "hevc.mkv") }), OpenFilesAsync(new[] { last }));
            if (_queue.Count != 1 || _currentIndex != 0 || _currentPath != last || _player!.Text("path") != last || !_loaded)
                throw new Exception("Rapid replacement mixed file state");
            await Task.WhenAll(OpenFilesAsync(new[] { fixture }, true), OpenFilesAsync(new[] { Path.Combine(fixtureDirectory, "hevc.mkv") }, true));
            if (_queue.Count != 3 || _currentPath != last) throw new Exception("Append interrupted playback or lost items");
            return null;
        });
        await Test("playlist-keyboard-navigation-keeps-volume", () =>
        {
            PlaylistPanel.Visibility = Visibility.Visible;
            Activate(); UpdateLayout();
            QueueList.Focus();
            if (!QueueList.IsKeyboardFocusWithin) throw new Exception("Could not focus playlist");
            double volume = VolumeBar.Value;
            var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), Environment.TickCount, Key.Down)
            { RoutedEvent = PreviewKeyDownEvent };
            Window_KeyDown(this, key);
            if (key.Handled || VolumeBar.Value != volume) throw new Exception("Window intercepted playlist navigation");
            return Task.FromResult<object?>(null);
        });
        await Test("video-and-subtitle-open-together", async () =>
        {
            await OpenFilesAsync(new[] { fixture, Path.Combine(fixtureDirectory, "테스트 자막.srt") });
            await WaitUntilAsync(() => _player!.Text("sid") is not "" and not "no", TimeSpan.FromSeconds(5));
            if (!_loaded || _currentPath != fixture) throw new Exception("Subtitle was attached before its video loaded");
            return null;
        });
        foreach (var file in new[] { "hevc.mkv", "vp9.webm", "mpeg4.avi", "h264.mov", "h264.ts", "wmv.wmv", "av1.mkv", "audio.flac" })
        {
            await Test("format-" + file, async () =>
            {
                await OpenFilesAsync(new[] { Path.Combine(fixtureDirectory, file) });
                await WaitUntilAsync(() => _loaded && _player!.Number("time-pos") > .12, TimeSpan.FromSeconds(15));
                var detail = new { loadMs = LastLoadMilliseconds, codec = _player!.Text("video-codec"), audioCodec = _player.Text("audio-codec-name"), width = _player.Number("video-params/w"), decoder = _player.Text("hwdec-current") };
                if (!file.EndsWith(".flac")) await _player.CommandAsync("screenshot-to-file", Path.Combine(outputDirectory, "decoded-" + file + ".png"), "video");
                await _player.SetAsync("pause", true);
                return detail;
            });
        }
        await Test("corrupt-file-error-and-recovery", async () =>
        {
            await OpenFilesAsync(new[] { Path.Combine(fixtureDirectory, "broken.mp4") });
            await WaitUntilAsync(() => EmptyTitle.Text == "파일을 재생할 수 없습니다", TimeSpan.FromSeconds(10));
            await OpenFilesAsync(new[] { fixture });
            await WaitUntilAsync(() => _loaded, TimeSpan.FromSeconds(10));
            return null;
        });
        await Test("end-of-file-next-in-playlist", async () =>
        {
            await OpenFilesAsync(new[] { fixture, Path.Combine(fixtureDirectory, "vp9.webm") });
            await WaitUntilAsync(() => _loaded && _player!.Number("duration") > 10, TimeSpan.FromSeconds(10));
            await SeekAsync(_player!.Number("duration") - .4, false);
            await _player.SetAsync("pause", false);
            await WaitUntilAsync(() => _currentIndex == 1 && _loaded && _player.Text("path").EndsWith("vp9.webm"), TimeSpan.FromSeconds(10));
            return null;
        });
        await Test("resume-position-history", async () =>
        {
            Settings.Resume = true;
            await OpenFilesAsync(new[] { fixture });
            await WaitUntilAsync(() => _loaded, TimeSpan.FromSeconds(10));
            await _player!.SetAsync("pause", true);
            await SeekAsync(6, false);
            await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - 6) < .15, TimeSpan.FromSeconds(5));
            RememberCurrent();
            var remembered = Settings.Recent.First(x => x.Path == fixture).Position;
            if (Math.Abs(remembered - 6) > .2) throw new Exception("Wrong remembered position " + remembered);
            await OpenFilesAsync(new[] { Path.Combine(fixtureDirectory, "vp9.webm") });
            await WaitUntilAsync(() => _loaded, TimeSpan.FromSeconds(10));
            await OpenFilesAsync(new[] { fixture });
            await WaitUntilAsync(() => _loaded && _player.Number("time-pos") >= 5.9, TimeSpan.FromSeconds(10));
            Settings.Resume = false;
            return new { resumedPosition = _player.Number("time-pos") };
        });
        await Test("clear-history-stays-cleared-until-next-file", async () =>
        {
            RememberCurrent();
            if (Settings.Recent.Count == 0) throw new Exception("History precondition missing");
            ClearHistory();
            RememberCurrent();
            if (Settings.Recent.Count != 0) throw new Exception("Autosave recreated cleared history");
            await OpenFilesAsync(new[] { fixture });
            RememberCurrent();
            if (Settings.Recent.Count != 1) throw new Exception("New playback did not resume normal history recording");
            return null;
        });
        await Test("settings-invalid-values-and-duplicates", () =>
        {
            var settings = new PlayerSettings { Volume = double.NaN, Speed = -5, Width = double.PositiveInfinity };
            settings.Recent.Add(new("C:\\test.mp4", double.NaN, DateTimeOffset.UtcNow));
            settings.Recent.Add(new("c:\\TEST.mp4", 10, DateTimeOffset.UtcNow));
            settings.Recent.Add(new("C:\\other.mp4", -40, DateTimeOffset.UtcNow));
            settings.Normalize();
            if (settings.Volume != 70 || settings.Speed != .25 || settings.Width != 1180 || settings.Recent.Count != 2 || settings.Recent.Any(x => x.Position != 0))
                throw new Exception("Invalid saved settings were not normalized");
            _ = MediaFiles.Time(double.MaxValue);
            _ = MediaFiles.Time(double.NaN);
            return Task.FromResult<object?>(null);
        });
        await Test("replay-from-end-starts-playing", async () =>
        {
            await OpenFilesAsync(new[] { fixture });
            await SeekAsync(_player!.Number("duration"), false);
            await WaitUntilAsync(() => _player.Flag("eof-reached"), TimeSpan.FromSeconds(5));
            await TogglePlayAsync();
            await WaitUntilAsync(() => !_player.Flag("pause") && _player.Number("time-pos") > .1 && _player.Number("time-pos") < 2, TimeSpan.FromSeconds(5));
            return null;
        });
        await Test("close-cancels-pending-open-requests", async () =>
        {
            var opening = OpenFilesAsync(new[] { fixture });
            var queued = OpenFilesAsync(new[] { Path.Combine(fixtureDirectory, "hevc.mkv") });
            var youtube = OpenYouTubeAsync("https://www.youtube.com/watch?v=M7lc1UVf-VE");
            await HideToTrayAsync();
            await Task.WhenAll(opening, queued, youtube);
            await WaitUntilAsync(() => _player!.Flag("idle-active"), TimeSpan.FromSeconds(5));
            if (_loaded || IsVisible || _currentPath != null || _youtubeWindow != null) throw new Exception("Queued local/YouTube open survived close-to-tray");
            Show();
            await OpenFilesAsync(new[] { fixture });
            return null;
        });
        await Test("native-window-layout-playing-minimum-size", async () =>
        {
            await _player!.SetAsync("pause", true);
            await SetSpeedAsync(1.5);
            CaptureUi(Path.Combine(outputDirectory, "02-player.png"));
            PlaylistPanel.Visibility = Visibility.Collapsed;
            Width = 820; Height = 560;
            await Task.Delay(150);
            VerifyButtonLayout();
            CaptureUi(Path.Combine(outputDirectory, "03-compact.png"));
            Width = 1180; Height = 760;
            return null;
        });
        await Test("close-to-tray-stops-decoding-and-reopen", async () =>
        {
            await HideToTrayAsync();
            await WaitUntilAsync(() => _player!.Flag("idle-active"), TimeSpan.FromSeconds(5));
            if (IsVisible) throw new Exception("Window did not hide");
            await Task.Delay(1000);
            var process = Process.GetCurrentProcess();
            var cpu = process.TotalProcessorTime;
            await Task.Delay(1500);
            var idleCpu = (process.TotalProcessorTime - cpu).TotalMilliseconds;
            Show();
            await OpenFilesAsync(new[] { fixture });
            await WaitUntilAsync(() => _loaded, TimeSpan.FromSeconds(10));
            return new { idleCpuMsOver1500ms = idleCpu, reopenMs = LastLoadMilliseconds, workingSetMB = Process.GetCurrentProcess().WorkingSet64 / 1048576d };
        });
        await RunSeekCoordinateTestsAsync(Test, fixtureDirectory);
        await RunCaptureTestsAsync(Test, fixtureDirectory, outputDirectory);
        await RunGifTestsAsync(Test, fixtureDirectory, outputDirectory);
        await RunShortsTestsAsync(Test, fixtureDirectory, outputDirectory);
        await RunDesignTestsAsync(Test, fixtureDirectory, outputDirectory);
        await RunAudioTestsAsync(Test, fixtureDirectory, outputDirectory);
        await OnlineSelfTest.RunAsync(Test, fixtureDirectory, outputDirectory, this);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "complete.txt"), failures == 0 ? "PASS" : $"FAIL {failures}");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    private void CaptureUi(string path)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private static IEnumerable<T> VisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in VisualChildren<T>(child)) yield return descendant;
        }
    }
    private void VerifyButtonLayout()
    {
        UpdateLayout();
        if (!AddressBar.IsVisible || AddressInput.ActualWidth < 200 || AddressInput.ActualHeight < 28)
            throw new Exception("Persistent address bar is hidden or clipped");
        var addressBounds = AddressBar.TransformToAncestor(Root).TransformBounds(new Rect(AddressBar.RenderSize));
        var stageBounds = StageLayout.TransformToAncestor(Root).TransformBounds(new Rect(StageLayout.RenderSize));
        if (addressBounds.Bottom > stageBounds.Top + 1) throw new Exception("Address bar overlaps the video stage");
        if (EmptyState.IsVisible && !new Rect(Stage.RenderSize).Contains(EmptyActions.TransformToAncestor(Stage).TransformBounds(new Rect(EmptyActions.RenderSize))))
            throw new Exception("Empty-state buttons are clipped by the video stage");
        var bounds = VisualChildren<Button>(Root).Where(x => x.IsVisible)
            .Select(x => (Button: x, Rect: x.TransformToAncestor(Root).TransformBounds(new Rect(x.RenderSize))))
            .ToArray();
        var viewport = new Rect(-1, -1, Root.ActualWidth + 2, Root.ActualHeight + 2);
        for (int i = 0; i < bounds.Length; i++)
        {
            if (!viewport.Contains(bounds[i].Rect)) throw new Exception($"Button outside window: {bounds[i].Button.Content}");
            for (int j = i + 1; j < bounds.Length; j++)
            {
                var intersection = Rect.Intersect(bounds[i].Rect, bounds[j].Rect);
                if (!intersection.IsEmpty && intersection.Width > 1 && intersection.Height > 1)
                    throw new Exception($"Overlapping buttons: {bounds[i].Button.Content} / {bounds[j].Button.Content}");
            }
        }
    }
}
