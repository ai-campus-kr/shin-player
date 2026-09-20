using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ShinPlayer;

public partial class MainWindow
{
    private YouTubeWindow? _youtubeWindow;
    private VideoChatWindow? _videoChat;
    private void Link_Click(object sender, RoutedEventArgs e) => ShowLinkDialog();
    private void Chat_Click(object sender, RoutedEventArgs e) => ShowVideoChat();
    private void ShowLinkDialog()
    {
        var input = new TextBox { Padding = new(10), MinWidth = 370, Margin = new(0, 14, 0, 14) };
        var message = new TextBlock { Text = "유튜브 링크를 넣으면 앱 안의 브라우저에서 재생합니다.", TextWrapping = TextWrapping.Wrap };
        var open = new Button { Content = "유튜브 열기", IsDefault = true, Style = (Style)FindResource("Primary") };
        var panel = new StackPanel { Margin = new(24) };
        panel.Children.Add(new TextBlock { Text = "유튜브 링크 열기", FontSize = 22, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(input); panel.Children.Add(message); panel.Children.Add(open);
        var dialog = new Window { Title = "링크 열기 · 신플레이어", Style = (Style)FindResource(typeof(Window)), Owner = this, Width = 520, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        open.Margin = new(0, 16, 0, 0);
        open.Click += (_, _) =>
        {
            if (!YouTubePage.TryNormalize(input.Text, out var url)) { message.Text = "올바른 유튜브 영상 링크를 입력하세요."; return; }
            dialog.Close(); Run(() => OpenYouTubeAsync(url));
        };
        dialog.Loaded += (_, _) => input.Focus();
        dialog.ShowDialog();
    }
    private async Task OpenYouTubeAsync(string url)
    {
        if (_closed || !YouTubePage.TryNormalize(url, out var normalized)) return;
        int generation = _openGeneration;
        await _operations.WaitAsync();
        try
        {
            if (_closed || generation != _openGeneration) return;
            if (_player != null) await _player.SetAsync("pause", true);
            if (_closed || generation != _openGeneration) return;
            _videoChat?.Close();
            if (_youtubeWindow == null)
            {
                _youtubeWindow = new YouTubeWindow(normalized) { Owner = this };
                _youtubeWindow.Closed += (_, _) => _youtubeWindow = null;
                _youtubeWindow.Show();
            }
            else { _youtubeWindow.NavigateVideo(normalized); _youtubeWindow.Activate(); }
        }
        finally { _operations.Release(); }
    }
    private void ShowVideoChat()
    {
        if (_videoChat != null) { _videoChat.Activate(); return; }
        if (!_loaded || _currentPath == null) { StatusText.Text = "로컬 영상을 먼저 열거나, 유튜브 창의 ‘자막 · AI 채팅’을 사용하세요."; return; }
        var path = _currentPath;
        var generation = _openGeneration;
        _videoChat = new VideoChatWindow(async cancel =>
        {
            var tools = await CaptureTools.EnsureAsync(new Progress<string>(message => StatusText.Text = message), cancel);
            var capture = new SubtitleCapture(tools);
            var video = await capture.ProbeAsync(path, cancel);
            if (video.Tracks.Count == 0) throw new InvalidOperationException("내장 텍스트 자막이 없습니다. 외부 자막·음성 받아쓰기·OCR은 AI 분석에 사용하지 않습니다.");
            cancel.ThrowIfCancellationRequested();
            if (_currentPath != path || _closed) throw new InvalidOperationException("영상이 바뀌었습니다.");
            var track = video.Tracks.FirstOrDefault(t => t.Language is "ko" or "kor") ?? video.Tracks.FirstOrDefault(t => t.Default) ?? video.Tracks[0];
            if (video.Tracks.Count > 1)
            {
                var choose = new ComboBox { ItemsSource = video.Tracks, DisplayMemberPath = nameof(SubtitleTrack.Label), SelectedItem = track, MinHeight = 36, Margin = new(0, 12, 0, 16) };
                var apply = new Button { Content = "이 자막으로 질문하기", IsDefault = true, Style = (Style)FindResource("Primary") };
                var panel = new StackPanel { Margin = new(22) };
                panel.Children.Add(new TextBlock { Text = "분석할 내장 자막", FontSize = 20 }); panel.Children.Add(choose); panel.Children.Add(apply);
                var dialog = new Window { Title = "내장 자막 선택", Style = (Style)FindResource(typeof(Window)), Owner = _videoChat, Width = 480, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
                apply.Click += (_, _) => dialog.DialogResult = true;
                if (dialog.ShowDialog() != true) throw new OperationCanceledException();
                track = (SubtitleTrack)choose.SelectedItem;
            }
            return await VideoTranscriptLoader.ExtractAsync(tools, video, track, _player?.Number("sub-delay") ?? 0, cancel);
        }, async (transcript, time) =>
        {
            if (_closed || !_loaded || _currentPath != transcript.Source || generation != _openGeneration) throw new InvalidOperationException("영상이 바뀌었습니다. 현재 영상으로 다시 질문하세요.");
            await SeekAsync(time, false);
            await _player!.SetAsync("pause", false);
        }) { Owner = this };
        _videoChat.Closed += (_, _) => _videoChat = null;
        _videoChat.Show();
    }
}
