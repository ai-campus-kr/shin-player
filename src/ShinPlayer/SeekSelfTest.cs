using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ShinPlayer;

public partial class MainWindow
{
    private Track SeekTrack => (Track)SeekBar.Template.FindName("PART_Track", SeekBar);
    private Point SeekPoint(double fraction) => SeekTrack.TranslatePoint(new Point(SeekTrack.Thumb.ActualWidth / 2 + (SeekTrack.ActualWidth - SeekTrack.Thumb.ActualWidth) * fraction, SeekTrack.ActualHeight / 2), SeekBar);

    private async Task RunSeekCoordinateTestsAsync(Func<string, Func<Task<object?>>, Task> test, string fixtures)
    {
        var file = Path.Combine(fixtures, "한글 영상 sample.mp4");
        await OpenFilesAsync(new[] { file });
        await _player!.SetAsync("pause", true);
        await SetSpeedAsync(1);
        await WaitUntilAsync(() => _player.Flag("pause"), TimeSpan.FromSeconds(3));
        RefreshState(); UpdateLayout();
        await test("seek-coordinate-stable-before-layout", () =>
        {
            CancelSeek();
            SeekBar.Value = 1; UpdateLayout();
            var point = SeekPoint(.4);
            var expected = SeekBar.Maximum * .4;
            var nativePoint = SeekBar.TranslatePoint(point, SeekTrack);
            var oldFirst = SeekTrack.ValueFromPoint(nativePoint);
            // Reproduce the previous sequence: Slider changes Value on down, then
            // capture produces another move before WPF arranges the new thumb.
            SeekBar.Value = oldFirst;
            var oldSecond = SeekTrack.ValueFromPoint(nativePoint);
            var fixedSecond = SeekTarget(point) ?? -1;
            if (Math.Abs(fixedSecond - expected) > .001) throw new Exception($"Same coordinate drifted to {fixedSecond}");
            BeginSeek(point);
            for (int i = 0; i < 20; i++) UpdateSeek(point);
            if (Math.Abs(SeekBar.Value - expected) > .001 || SeekBar.IsMoveToPointEnabled) throw new Exception("Competing pointer handling remains");
            CancelSeek(); RefreshState();
            return Task.FromResult<object?>(new { expected, oldFirst, oldSecond, fixedSecond });
        });
        await test("seek-coordinate-layout-matrix", async () =>
        {
            var results = new List<object>();
            foreach (var width in new[] { 820d, 1180d })
            foreach (var playlist in new[] { false, true })
            {
                Width = width; Height = width == 820 ? 560 : 760;
                PlaylistPanel.Visibility = playlist ? Visibility.Visible : Visibility.Collapsed;
                UpdateLayout();
                foreach (var fraction in new[] { 0d, .1, .25, .5, .75, .9, 1 })
                {
                    var point = SeekPoint(fraction);
                    var expected = _player.Number("duration") * fraction;
                    var before = _player.SeekRequestCount;
                    BeginSeek(point);
                    RefreshState(); // Native playback updates must not overwrite the gesture.
                    if (Math.Abs(SeekBar.Value - expected) > .001) throw new Exception("Playback refresh overwrote click coordinate");
                    EndSeek(point); CommitSeek();
                    await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - expected) < .15, TimeSpan.FromSeconds(5));
                    if (_player.SeekRequestCount != before + 1 || !_player.Flag("pause")) throw new Exception("Duplicate or unpaused coordinate seek");
                    results.Add(new { width, playlist, fraction, expected, actual = _player.Number("time-pos") });
                }
            }
            Width = 1180; Height = 760; PlaylistPanel.Visibility = Visibility.Collapsed; UpdateLayout();
            return results;
        });
        await test("seek-release-position-without-mousemove", async () =>
        {
            var start = SeekPoint(.2);
            var end = SeekPoint(.7);
            BeginSeek(start);
            EndSeek(end); // No intermediate move event is guaranteed by Windows.
            var expected = _player.Number("duration") * .7;
            await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - expected) < .15, TimeSpan.FromSeconds(5));
            return new { expected, actual = _player.Number("time-pos") };
        });
        await test("seek-drag-reverse-outside-and-capture-loss", async () =>
        {
            BeginSeek(SeekPoint(.8));
            UpdateSeek(SeekPoint(.15));
            SeekBar.ReleaseMouseCapture();
            var expected = _player.Number("duration") * .15;
            await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - expected) < .15, TimeSpan.FromSeconds(5));
            if (_scrubbing) throw new Exception("Drag remained active after capture loss");
            BeginSeek(SeekPoint(.5));
            EndSeek(new Point(-50, -50));
            await WaitUntilAsync(() => _player.Number("time-pos") < .15, TimeSpan.FromSeconds(5));
            BeginSeek(SeekPoint(.5));
            EndSeek(new Point(SeekBar.ActualWidth + 50, 100));
            await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - _player.Number("duration")) < .15, TimeSpan.FromSeconds(5));
            return null;
        });
        await test("seek-coordinate-layout-scaling-and-fullscreen", async () =>
        {
            try
            {
                foreach (var scale in new[] { 1d, 1.25, 1.5, 2 })
                {
                    SeekBar.LayoutTransform = new ScaleTransform(scale, scale); UpdateLayout();
                    var expected = SeekBar.Maximum * .65;
                    var rootPoint = SeekBar.TranslatePoint(SeekPoint(.65), Root);
                    var inputPoint = Root.TranslatePoint(rootPoint, SeekBar);
                    BeginSeek(inputPoint); UpdateSeek(inputPoint); EndSeek(inputPoint);
                    await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - expected) < .15, TimeSpan.FromSeconds(5));
                }
            }
            finally { SeekBar.LayoutTransform = Transform.Identity; UpdateLayout(); }
            ToggleFullscreen(); UpdateLayout();
            try
            {
                BeginSeek(SeekPoint(.3)); EndSeek(SeekPoint(.3));
                await WaitUntilAsync(() => Math.Abs(_player.Number("time-pos") - _player.Number("duration") * .3) < .15, TimeSpan.FromSeconds(5));
            }
            finally { ToggleFullscreen(); UpdateLayout(); }
            return new { layoutScales = new[] { 1, 1.25, 1.5, 2 }, fullscreen = true, note = "WPF layout transform coverage; not physical monitor DPI automation" };
        });
        await test("seek-file-change-cancels-gesture", async () =>
        {
            BeginSeek(SeekPoint(.6));
            var before = _player.SeekRequestCount;
            await OpenFilesAsync(new[] { Path.Combine(fixtures, "hevc.mkv") });
            EndSeek(SeekPoint(.8));
            if (_scrubbing || SeekBar.IsMouseCaptured || _player.SeekRequestCount != before) throw new Exception("Old gesture sought in the newly opened video");
            await OpenFilesAsync(new[] { file });
            await _player.SetAsync("pause", true);
            return null;
        });
    }
}
