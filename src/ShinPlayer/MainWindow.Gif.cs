using System;
using System.Threading.Tasks;
using System.Windows;

namespace ShinPlayer;

public partial class MainWindow
{
    private GifWindow? _gifWindow;
    internal async Task StopGifAsync()
    {
        if (_gifWindow != null) await _gifWindow.StopAsync();
        if (_youtubeWindow != null) await _youtubeWindow.StopGifAsync();
    }
    private void Gif_Click(object sender, RoutedEventArgs e) => ShowGif();
    private void ShowGif()
    {
        if (_gifWindow != null) { _gifWindow.Activate(); return; }
        if (!_loaded || _currentPath == null || _player == null) { StatusText.Text = "영상을 연 뒤 GIF를 누르세요. 유튜브는 유튜브 창의 GIF 버튼을 사용하세요."; return; }
        string path = _currentPath;
        int generation = _openGeneration;
        _gifWindow = new GifWindow(async cancel =>
        {
            var tools = await CaptureTools.EnsureAsync(new Progress<string>(), cancel);
            var video = await new SubtitleCapture(tools).ProbeAsync(path, cancel);
            if (video.VideoIndex < 0 || video.Duration < .2) throw new InvalidOperationException("GIF로 만들 수 있는 영상 트랙이 없습니다.");
            return new LocalGifSource(video, () =>
            {
                if (_closed || path != _currentPath || generation != _openGeneration) throw new InvalidOperationException("재생 중인 영상이 바뀌었습니다. GIF 창을 다시 여세요.");
                return _player?.Number("time-pos") ?? 0;
            });
        }) { Owner = this };
        _gifWindow.Closed += (_, _) => _gifWindow = null;
        _gifWindow.Show();
    }
}

internal sealed partial class YouTubeWindow
{
    private GifWindow? _gifWindow;
    internal async Task StopGifAsync() { if (_gifWindow != null) await _gifWindow.StopAsync(); }
    private void ShowGif()
    {
        if (_gifWindow != null) { _gifWindow.Activate(); return; }
        if (!_ready || _closed || _videoId.Length == 0) { _status.Text = "유튜브 영상을 먼저 열고 GIF를 누르세요."; return; }
        int revision = _revision; string id = _videoId;
        _gifWindow = new GifWindow(cancel => BrowserGifSource.CreateAsync(_browser, this,
            () => !_closed && _ready && revision == _revision && id == _videoId, cancel)) { Owner = this };
        _gifWindow.Closed += (_, _) => _gifWindow = null;
        _gifWindow.Show();
    }
}
