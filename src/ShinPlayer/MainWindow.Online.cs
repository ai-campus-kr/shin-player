using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShinPlayer;

public partial class MainWindow
{
    private YouTubeWindow? _youtubeWindow;
    private VideoChatWindow? _videoChat;
    private string _lastYouTubeAddress = "";
    private void Chat_Click(object sender, RoutedEventArgs e) => ShowVideoChat();
    private void FocusAddress()
    {
        AddressInput.Focus();
        AddressInput.SelectAll();
    }
    private void Address_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => AddressInput.SelectAll();
    private void Address_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (AddressInput.IsKeyboardFocusWithin) return;
        e.Handled = true;
        FocusAddress();
    }
    private void AddressGo_Click(object sender, RoutedEventArgs e) => NavigateAddress();
    private void Address_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (!e.IsRepeat) NavigateAddress();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            AddressInput.Text = _lastYouTubeAddress;
            PlayButton.Focus();
        }
    }
    private void NavigateAddress()
    {
        if (!YouTubePage.TryNormalizeAddress(AddressInput.Text, out var url))
        {
            StatusText.Text = "유튜브 주소를 확인하세요. youtube.com 또는 유튜브 영상 주소를 입력할 수 있습니다.";
            FocusAddress();
            return;
        }
        Run(() => OpenYouTubeAsync(url));
    }
    private async Task OpenYouTubeAsync(string url)
    {
        if (_closed || !YouTubePage.TryNormalizeAddress(url, out var normalized)) return;
        int generation = _openGeneration;
        await _operations.WaitAsync();
        try
        {
            if (_closed || generation != _openGeneration) return;
            if (_player != null) await _player.SetAsync("pause", true);
            if (_closed || generation != _openGeneration) return;
            AddressInput.Text = _lastYouTubeAddress = normalized;
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
        if (!_loaded || _currentPath == null) { StatusText.Text = "로컬 영상을 먼저 열거나, 유튜브 창의 채팅 패널을 사용하세요."; return; }
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
