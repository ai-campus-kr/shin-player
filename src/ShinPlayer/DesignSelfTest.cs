using System;
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
    private async Task RunDesignTestsAsync(Func<string, Func<Task<object?>>, Task> test, string fixtures, string output)
    {
        await test("address-bar-typing-submit-and-escape", async () =>
        {
            await OpenFilesAsync(new[] { Path.Combine(fixtures, "한글 영상 sample.mp4") });
            await _player!.SetAsync("pause", true);
            await WaitUntilAsync(() => _player.Flag("pause"), TimeSpan.FromSeconds(3));
            Activate(); UpdateLayout();
            AddressInput.Text = "youtu.be/" + OnlineSelfTest.TestId;
            HandleAction("open-link");
            if (!AddressInput.IsKeyboardFocusWithin || AddressInput.SelectedText != AddressInput.Text)
                throw new Exception("Address shortcut did not focus and select the URL");
            long requests = _player.SeekRequestCount;
            foreach (var key in new[] { Key.Space, Key.Left, Key.Right, Key.Up, Key.Down, Key.Home, Key.End, Key.Back, Key.F, Key.M, Key.A, Key.S, Key.N, Key.OemPeriod })
            {
                var args = KeyArgs(key, PreviewKeyDownEvent);
                Window_KeyDown(this, args);
                if (args.Handled) throw new Exception("Player intercepted address editing: " + key);
            }
            if (_player.SeekRequestCount != requests || !_player.Flag("pause") || _fullScreen)
                throw new Exception("Address editing changed playback");
            AddressInput.Text = "https://youtube.com.example.org/watch?v=" + OnlineSelfTest.TestId;
            AddressGoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (_youtubeWindow != null || !AddressInput.IsKeyboardFocusWithin)
                throw new Exception("Invalid address opened a browser or lost focus");
            try
            {
                AddressInput.Text = " youtube.com ";
                AddressInput.RaiseEvent(KeyArgs(Key.Enter, KeyDownEvent));
                await WaitUntilAsync(() => _youtubeWindow != null, TimeSpan.FromSeconds(3));
                await _youtubeWindow!.Initialization;
                if (AddressInput.Text != "https://youtube.com/") throw new Exception("Enter did not normalize the address");
                _youtubeWindow.Close();
                Activate(); FocusAddress();
                AddressInput.Text = "unfinished edit";
                AddressInput.RaiseEvent(KeyArgs(Key.Escape, KeyDownEvent));
                if (AddressInput.Text != "https://youtube.com/" || AddressInput.IsKeyboardFocusWithin)
                    throw new Exception("Escape did not restore the address and return focus");
            }
            finally
            {
                _youtubeWindow?.Close();
                AddressInput.Text = _lastYouTubeAddress = "";
                PlayButton.Focus();
            }
            return new { keyboardEditingPreserved = true, enterOpens = true, invalidAddressRejected = true, escapeRestores = true, liveNetworkUsed = false };

            KeyEventArgs KeyArgs(Key key, RoutedEvent routedEvent) => new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), Environment.TickCount, key) { RoutedEvent = routedEvent };
        });
        await test("ui-design-settings-roundtrip-and-migration", async () =>
        {
            foreach (var design in UiDesigns.All)
            {
                var path = Path.Combine(output, "settings-" + design.Id + ".json");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new PlayerSettings { UiDesign = design.Id, Volume = 42 }));
                var restored = JsonSerializer.Deserialize<PlayerSettings>(await File.ReadAllTextAsync(path))!;
                restored.Normalize();
                if (restored.UiDesign != design.Id || restored.Volume != 42) throw new Exception("UI choice did not survive serialization");
            }
            foreach (var json in new[] { "{\"Volume\":42}", "{\"UiDesign\":\"unknown\"}", "{\"UiDesign\":null}" })
            {
                var settings = JsonSerializer.Deserialize<PlayerSettings>(json)!; settings.Normalize();
                if (settings.UiDesign != UiDesigns.DefaultId) throw new Exception("Old/invalid settings did not fall back to the default UI");
            }
            return null;
        });
        await test("ui-design-four-styles-empty-and-minimum-layout", async () =>
        {
            await StopPlaybackAsync();
            foreach (var design in UiDesigns.All)
            {
                Width = 1180; Height = 760; PlaylistPanel.Visibility = Visibility.Collapsed;
                ApplyUiDesign(design.Id); await Task.Delay(80);
                VerifyButtonLayout();
                CaptureUi(Path.Combine(output, "ui-" + design.Id + ".png"));
                Width = 820; Height = 560; PlaylistPanel.Visibility = Visibility.Visible;
                await Task.Delay(80); VerifyButtonLayout();
                if (EmptyArtwork.IsVisible) throw new Exception("Narrow window shows overlapping decoration");
                var stageBounds = Stage.TransformToAncestor(Root).TransformBounds(new Rect(Stage.RenderSize));
                var queueBounds = PlaylistPanel.TransformToAncestor(Root).TransformBounds(new Rect(PlaylistPanel.RenderSize));
                if (Rect.Intersect(stageBounds, queueBounds).Width > 1) throw new Exception("Playlist overlaps the video surface");
                CaptureUi(Path.Combine(output, "ui-" + design.Id + "-compact.png"));
            }
            Width = 1180; Height = 760; PlaylistPanel.Visibility = Visibility.Collapsed;
            ApplyUiDesign(UiDesigns.DefaultId);
            return new { styles = UiDesigns.All.Select(x => x.Id), windowWidths = new[] { 820, 1180 } };
        });
        await test("ui-design-live-switch-preserves-playback-and-seek", async () =>
        {
            await OpenFilesAsync(new[] { Path.Combine(fixtures, "한글 영상 sample.mp4") });
            await _player!.SetAsync("pause", true); await SetSpeedAsync(1.75);
            await WaitUntilAsync(() => _player.Flag("pause"), TimeSpan.FromSeconds(3));
            var handle = Video.WindowHandle;
            foreach (var design in UiDesigns.All)
            {
                await SeekAsync(4, false);
                await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - 4) < .15, TimeSpan.FromSeconds(3));
                var requests = _player.SeekRequestCount;
                ApplyUiDesign(design.Id); UpdateLayout(); await Task.Delay(80);
                if (Video.WindowHandle != handle || !_loaded || !_player.Flag("pause") || Math.Abs(_player.Number("time-pos") - 4) > .15 || Math.Abs(_requestedSpeed - 1.75) > .001 || _player.SeekRequestCount != requests)
                    throw new Exception("UI switching changed playback or recreated its video host");
                if (((SolidColorBrush)Background).Color != UiDesigns.Brush(design.Bg).Color || ((SolidColorBrush)PlayButton.Background).Color != UiDesigns.Brush(design.Accent).Color)
                    throw new Exception("Existing controls did not receive the palette");
                if (((SolidColorBrush)AbButton.Foreground).Color != UiDesigns.Brush(design.Ink).Color)
                    throw new Exception("Inactive loop button kept an unreadable foreground");
                foreach (var fraction in new[] { .15, .5, .85 })
                {
                    var point = SeekPoint(fraction); BeginSeek(point); EndSeek(point);
                    await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - _player.Number("duration") * fraction) < .15, TimeSpan.FromSeconds(3));
                }
                ToggleFullscreen(); ApplyUiDesign(design.Id); UpdateLayout();
                if (TitleRow.ActualHeight != 0) throw new Exception("Theme restored the hidden fullscreen title bar");
                if (!AddressBar.IsVisible || AddressInput.ActualWidth < 200) throw new Exception("Fullscreen lost the persistent address bar");
                ToggleFullscreen(); UpdateLayout(); VerifyButtonLayout();
                if (Math.Abs(TitleRow.ActualHeight - design.HeaderHeight) > 1) throw new Exception("Fullscreen restored the wrong title height");
            }
            await _player.SetAsync("pause", false);
            var start = _player.Number("time-pos");
            ApplyUiDesign(UiDesigns.DefaultId);
            await WaitUntilAsync(() => _player.Number("time-pos") > start + .1, TimeSpan.FromSeconds(3));
            if (_player.Flag("pause") || Math.Abs(_player.Number("speed") - 1.75) > .001) throw new Exception("Switch interrupted active playback");
            return new { sameVideoHost = true, positionPreserved = true, speed = 1.75, coordinateSeeks = 12 };
        });
        await test("ui-design-picker-buttons-and-open-dialog-recolor", async () =>
        {
            await OpenFilesAsync(new[] { Path.Combine(fixtures, "캡처 '테스트 [한글].mp4") });
            await _player!.SetAsync("pause", true);
            await WaitUntilAsync(() => _player.Number("video-params/w") > 0, TimeSpan.FromSeconds(3));
            ShowSubtitleCapture();
            var capture = _captureWindow!;
            await WaitUntilAsync(() => capture.IsLoaded, TimeSpan.FromSeconds(3));
            await capture.Initialization;
            ShowDesignPicker();
            var picker = _designPicker!;
            await WaitUntilAsync(() => picker.IsLoaded, TimeSpan.FromSeconds(3));
            try
            {
                foreach (var design in UiDesigns.All)
                {
                    var button = VisualChildren<Button>(picker).Single(x => Equals(x.Tag, design.Id));
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    capture.UpdateLayout(); picker.UpdateLayout();
                    if (Settings.UiDesign != design.Id || picker.SelectedId != design.Id) throw new Exception("Picker button did not apply selection");
                    if (((SolidColorBrush)capture.Background).Color != UiDesigns.Brush(design.Bg).Color) throw new Exception("Open capture window kept the old background");
                    var selector = VisualChildren<ComboBox>(capture).Single();
                    if (((SolidColorBrush)selector.Foreground).Color != UiDesigns.Brush(design.Ink).Color) throw new Exception("Open subtitle selector kept the old foreground");
                    if (design.Id == "light") CaptureDesignWindow(capture, Path.Combine(output, "ui-light-capture.png"));
                }
                picker.SelectDesign(UiDesigns.DefaultId); picker.UpdateLayout();
                CaptureDesignWindow(picker, Path.Combine(output, "ui-picker.png"));
            }
            finally { picker.Close(); capture.Close(); ApplyUiDesign(UiDesigns.DefaultId); }
            return null;
        });
    }

    private static void CaptureDesignWindow(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
