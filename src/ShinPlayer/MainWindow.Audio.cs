using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShinPlayer;

public partial class MainWindow
{
    private readonly SemaphoreSlim _boostChanges = new(1, 1);

    private void Boost_Click(object sender, RoutedEventArgs e) => ShowBoostMenu();
    private ContextMenu CreateBoostMenu()
    {
        var menu = new ContextMenu { PlacementTarget = BoostButton };
        foreach (var preset in AudioBoost.Presets)
            AddToggle(menu, preset.Name, Math.Abs(Settings.AudioBoostDb - preset.Db) < .01,
                () => Run(() => SetAudioBoostAsync(preset.Db)));
        return menu;
    }
    private void ShowBoostMenu() => CreateBoostMenu().IsOpen = true;

    private async Task SetAudioBoostAsync(double db)
    {
        await _boostChanges.WaitAsync();
        try
        {
            if (_closed) return;
            await EnsurePlayerAsync();
            if (_closed || _player == null) return;
            await _player.SetAudioBoostAsync(db);
            Settings.AudioBoostDb = _player.AudioBoostDb;
            PaintAudioBoost();
            _app.SaveSettings();
            StatusText.Text = Settings.AudioBoostDb > 0
                ? $"음량 증폭 +{Settings.AudioBoostDb:0.#} dB · 큰 소리 제한 적용"
                : "음량 증폭 꺼짐 · 원래 음량";
        }
        finally { _boostChanges.Release(); }
    }
    private void PaintAudioBoost()
    {
        bool enabled = Settings.AudioBoostDb > 0;
        BoostButton.Content = enabled ? $"+{Settings.AudioBoostDb:0.#} dB" : "증폭";
        BoostButton.SetResourceReference(ForegroundProperty, enabled ? "Accent" : "Muted");
        if (enabled) BoostButton.SetResourceReference(BackgroundProperty, "Selected");
        else BoostButton.Background = Brushes.Transparent;
        BoostButton.ToolTip = enabled
            ? $"음량 증폭 +{Settings.AudioBoostDb:0.#} dB · 클릭해서 조절/끄기\n큰 소리를 제한하며, 배경음도 함께 커집니다."
            : "작게 녹음된 소리를 더 크게 · 클릭해서 증폭량 선택";
    }
}
