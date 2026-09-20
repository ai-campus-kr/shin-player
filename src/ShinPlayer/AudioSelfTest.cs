using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShinPlayer;

public partial class MainWindow
{
    private async Task RunAudioTestsAsync(Func<string, Func<Task<object?>>, Task> test, string fixtures, string output)
    {
        await test("audio-boost-settings-roundtrip-and-old-settings", () =>
        {
            foreach (var preset in AudioBoost.Presets)
            {
                var saved = JsonSerializer.Serialize(new PlayerSettings { AudioBoostDb = preset.Db, Volume = 42, Muted = true });
                var restored = JsonSerializer.Deserialize<PlayerSettings>(saved)!; restored.Normalize();
                if (restored.AudioBoostDb != preset.Db || restored.Volume != 42 || !restored.Muted) throw new Exception("Boost settings changed on restore");
            }
            var legacy = JsonSerializer.Deserialize<PlayerSettings>("{\"Volume\":42}")!; legacy.Normalize();
            if (legacy.AudioBoostDb != 0) throw new Exception("Legacy settings unexpectedly enabled boost");
            foreach (var (input, expected) in new[] { (-1d, 0d), (999d, 12d), (double.NaN, 0d), (double.PositiveInfinity, 0d) })
            {
                var settings = new PlayerSettings { AudioBoostDb = input }; settings.Normalize();
                if (settings.AudioBoostDb != expected) throw new Exception("Invalid gain was not normalized");
            }
            return Task.FromResult<object?>(null);
        });
        await test("audio-boost-measured-gain-all-presets", async () =>
        {
            await SetSpeedAsync(1);
            await _player!.SetAsync("mute", true);
            await OpenFilesAsync(new[] { Path.Combine(fixtures, "boost-quiet.wav") });
            var measurements = new List<object>();
            foreach (var preset in AudioBoost.Presets)
            {
                await SetAudioBoostAsync(preset.Db);
                var reading = await MeasureAudioAsync();
                var expectedPeak = 20 * Math.Log10(.025) + preset.Db;
                if (Math.Abs(reading.Peak - expectedPeak) > .15 || Math.Abs(reading.Rms - (expectedPeak - 3.0103)) > .25)
                    throw new Exception($"Gain {preset.Db}: expected peak {expectedPeak}, measured {reading}");
                measurements.Add(new { boostDb = preset.Db, peakDbfs = reading.Peak, rmsDbfs = reading.Rms });
            }
            if (!_player.Flag("mute")) throw new Exception("Audio measurements must remain muted");
            return measurements;
        });
        await test("audio-boost-limiter-prevents-fullscale-clipping", async () =>
        {
            await SetAudioBoostAsync(12);
            await OpenFilesAsync(new[] { Path.Combine(fixtures, "boost-loud.wav") });
            var reading = await MeasureAudioAsync();
            var expected = 20 * Math.Log10(.95);
            if (Math.Abs(reading.Peak - expected) > .08 || reading.Peak > -.4)
                throw new Exception($"Limiter exceeded its ceiling: {reading.Peak} dBFS");
            await SetAudioBoostAsync(0);
            var original = await MeasureAudioAsync();
            if (Math.Abs(original.Peak - 20 * Math.Log10(.8)) > .08) throw new Exception("Disabling boost did not restore the original amplitude");
            return new { boostedPeakDbfs = reading.Peak, ceilingDbfs = expected, disabledPeakDbfs = original.Peak };
        });
        await test("audio-boost-rapid-changes-preserve-playback-and-other-filters", async () =>
        {
            await _player!.SetAsync("pause", true);
            await SetSpeedAsync(1.75);
            await WaitUntilAsync(() => _player.Flag("pause") && Math.Abs(_player.Number("speed") - 1.75) < .01, TimeSpan.FromSeconds(3));
            await _player.CommandAsync("af", "add", "@shin-preserve:lavfi=[anull]");
            try
            {
                // Observed properties are delivered asynchronously. Speed/filter
                // reconfiguration can leave the cached clock one event behind.
                var position = double.Parse(_player.ReadProperty("time-pos"), CultureInfo.InvariantCulture);
                var volume = _player.Number("volume");
                await Task.WhenAll(new[] { 3d, 12, 0, 9, 6, 0 }.Select(SetAudioBoostAsync));
                var positionAfter = double.Parse(_player.ReadProperty("time-pos"), CultureInfo.InvariantCulture);
                var filters = _player.ReadProperty("af");
                if (!filters.Contains("shin-preserve") || filters.Contains(AudioBoost.Label)) throw new Exception("Boost replaced unrelated filters or remained enabled");
                if (Settings.AudioBoostDb != 0 || _player.AudioBoostDb != 0 || !_player.Flag("pause") || !_player.Flag("mute") ||
                    Math.Abs(positionAfter - position) > .15 || Math.Abs(_player.Number("volume") - volume) > .01 ||
                    Math.Abs(_player.Number("speed") - 1.75) > .01 || !_player.Flag("audio-pitch-correction"))
                    throw new Exception("Changing gain altered playback state: " + JsonSerializer.Serialize(new {
                        positionBefore = position, positionAfter,
                        volumeBefore = volume, volumeAfter = _player.Number("volume"), speed = _player.Number("speed"),
                        paused = _player.Flag("pause"), muted = _player.Flag("mute"), pitchCorrection = _player.Flag("audio-pitch-correction"),
                        savedBoost = Settings.AudioBoostDb, engineBoost = _player.AudioBoostDb
                    }));
                await SetAudioBoostAsync(6);
                await OpenFilesAsync(new[] { Path.Combine(fixtures, "캡처 '테스트 [한글].mp4") });
                await _player.SetAsync("pause", true);
                // A video with no audio must still allow disabling and enabling the feature.
                await SetAudioBoostAsync(0); await SetAudioBoostAsync(9);
                await OpenFilesAsync(new[] { Path.Combine(fixtures, "한글 영상 sample.mp4") });
                await _player.SetAsync("pause", true);
                if (Settings.AudioBoostDb != 9 || !_player.ReadProperty("af").Contains(AudioBoost.Label)) throw new Exception("File changes lost the boost setting");
                return new { positionBefore = position, positionAfter, unchangedVolume = volume, preservedOtherFilter = true, silentVideoSupported = true };
            }
            finally
            {
                await _player.CommandAsync("af", "remove", "@shin-preserve");
                await SetAudioBoostAsync(0); await SetSpeedAsync(1);
            }
        });
        await test("audio-boost-menu-selection-and-four-theme-layouts", async () =>
        {
            try
            {
                foreach (var design in UiDesigns.All)
                {
                    ApplyUiDesign(design.Id); Width = 820; Height = 560; PlaylistPanel.Visibility = Visibility.Visible;
                    await Task.Delay(80); VerifyButtonLayout();
                    var menu = CreateBoostMenu();
                    var choice = (MenuItem)menu.Items[4];
                    choice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    await WaitUntilAsync(() => Settings.AudioBoostDb == 12, TimeSpan.FromSeconds(3));
                    if (!Equals(BoostButton.Content, "+12 dB") || ((SolidColorBrush)BoostButton.Foreground).Color != UiDesigns.Brush(design.Accent).Color)
                        throw new Exception("Boost button did not show the selected gain in this palette");
                    var selected = CreateBoostMenu().Items.Cast<MenuItem>().Where(x => x.IsChecked).ToArray();
                    if (selected.Length != 1 || !Equals(selected[0].Header, AudioBoost.Presets[4].Name)) throw new Exception("Gain menu checkmark is incorrect");
                    CaptureUi(Path.Combine(output, "audio-boost-" + design.Id + ".png"));
                    var off = (MenuItem)CreateBoostMenu().Items[0]; off.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    await WaitUntilAsync(() => Settings.AudioBoostDb == 0, TimeSpan.FromSeconds(3));
                }
                return null;
            }
            finally
            {
                await SetAudioBoostAsync(0); ApplyUiDesign(UiDesigns.DefaultId);
                Width = 1180; Height = 760; PlaylistPanel.Visibility = Visibility.Collapsed;
            }
        });
    }

    private async Task<(double Peak, double Rms)> MeasureAudioAsync()
    {
        const string label = "shin-meter";
        await _player!.CommandAsync("af", "add", $"@{label}:lavfi=[astats=metadata=1:reset=1]");
        try
        {
            await SeekAsync(1, false);
            await _player.SetAsync("pause", false);
            double peak = double.NaN, rms = double.NaN;
            bool Read(string key, out double value) => double.TryParse(_player.ReadProperty($"af-metadata/{label}/by-key/lavfi.astats.Overall.{key}"), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
            await WaitUntilAsync(() => Read("Peak_level", out peak) && Read("RMS_level", out rms), TimeSpan.FromSeconds(5));
            await Task.Delay(120);
            if (!Read("Peak_level", out peak) || !Read("RMS_level", out rms)) throw new Exception("Audio meter returned no finite measurement");
            return (peak, rms);
        }
        finally { await _player.CommandAsync("af", "remove", "@" + label); }
    }
}
