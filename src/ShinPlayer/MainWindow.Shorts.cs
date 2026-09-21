using System;
using System.Threading.Tasks;
using System.Windows;

namespace ShinPlayer;

public partial class MainWindow
{
    private ShortsWindow? _shortsWindow;
    internal async Task StopShortsAsync()
    {
        if (_shortsWindow != null) await _shortsWindow.StopAsync();
    }
    private void Shorts_Click(object sender, RoutedEventArgs e) => ShowShorts();
    internal void ShowShorts()
    {
        if (_shortsWindow != null) { _shortsWindow.Activate(); return; }
        if (!_loaded || _currentPath == null || _player == null) { StatusText.Text = "로컬 영상 파일을 연 뒤 쇼츠를 누르세요."; return; }
        string path = _currentPath;
        _shortsWindow = new ShortsWindow(async cancel =>
        {
            var tools = await CaptureTools.EnsureAsync(new Progress<string>(), cancel);
            return await new SubtitleCapture(tools).ProbeAsync(path, cancel);
        }, _player.Number("time-pos")) { Owner = this };
        _shortsWindow.Closed += (_, _) => _shortsWindow = null;
        _shortsWindow.Show();
    }
}
