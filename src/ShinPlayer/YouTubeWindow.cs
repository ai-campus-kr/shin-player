using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShinPlayer;

internal sealed class YouTubeWindow : Window
{
    private readonly WebView2 _browser = new();
    private readonly TextBox _address = new() { MinWidth = 180, Padding = new(8), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(14, 8, 14, 8), FontSize = 12 };
    private readonly Button _chatButton = new() { Content = "자막 · AI 채팅", IsEnabled = false };
    private VideoChatWindow? _chat;
    private bool _closed, _ready;
    private string _requestedUrl, _videoId = "";
    private int _revision;
    private WindowState _beforeFullscreen;
    internal Task Initialization { get; private set; } = Task.CompletedTask;
    internal YouTubeWindow(string url)
    {
        _requestedUrl = url;
        Style = (Style)FindResource(typeof(Window));
        Title = "YouTube · 신플레이어";
        Width = 1280; Height = 820; MinWidth = 840; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel();
        var toolbar = new DockPanel { Margin = new(10) };
        var back = new Button { Content = "←", ToolTip = "뒤로" };
        var forward = new Button { Content = "→", ToolTip = "앞으로" };
        var reload = new Button { Content = "↻", ToolTip = "새로고침" };
        var go = new Button { Content = "열기" };
        var external = new Button { Content = "브라우저에서 열기" };
        toolbar.Children.Add(back); toolbar.Children.Add(forward); toolbar.Children.Add(reload);
        DockPanel.SetDock(_chatButton, Dock.Right); toolbar.Children.Add(_chatButton);
        DockPanel.SetDock(external, Dock.Right); toolbar.Children.Add(external);
        DockPanel.SetDock(go, Dock.Right); toolbar.Children.Add(go); toolbar.Children.Add(_address);
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        root.Children.Add(_browser); Content = root;
        _address.Text = url;
        _status.Text = "유튜브 페이지를 그대로 재생합니다. 자막 검색은 영상 설명의 ‘스크립트 표시’를 연 뒤 이용하세요.";
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        go.Click += (_, _) => NavigateInput();
        _address.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; NavigateInput(); } };
        back.Click += (_, _) => { if (_ready && _browser.CanGoBack) _browser.GoBack(); };
        forward.Click += (_, _) => { if (_ready && _browser.CanGoForward) _browser.GoForward(); };
        reload.Click += (_, _) => { if (_ready) _browser.Reload(); };
        external.Click += (_, _) => OpenExternal(_browser.Source?.AbsoluteUri ?? _requestedUrl);
        _chatButton.Click += (_, _) => ShowChat();
        Loaded += (_, _) => Initialization = InitializeAsync();
        Closed += (_, _) => { _closed = true; _revision++; _chat?.Close(); _browser.Dispose(); };
    }
    private async Task InitializeAsync()
    {
        try
        {
            var folder = Path.Combine(PlayerSettings.DirectoryPath, "youtube-browser");
            if (((App)Application.Current).IsTest || ((App)Application.Current).IsDiagnosticSession)
                throw new InvalidOperationException("Use the isolated WebView2 fixture runner in diagnostic mode.");
            var environment = await CoreWebView2Environment.CreateAsync(null, folder);
            if (_closed) return;
            await _browser.EnsureCoreWebView2Async(environment);
            if (_closed) return;
            _browser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            _browser.CoreWebView2.Settings.IsWebMessageEnabled = false;
            _browser.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!IsAllowedPage(e.Uri))
                {
                    e.Cancel = true;
                    if (e.IsUserInitiated) OpenExternal(e.Uri);
                    return;
                }
                _revision++; _chat?.Close(); _chat = null;
            };
            _browser.CoreWebView2.SourceChanged += (_, _) =>
            {
                var id = YouTubePage.VideoId(_browser.Source?.AbsoluteUri ?? "");
                if (id != _videoId) { _revision++; _chat?.Close(); _chat = null; }
                _videoId = id; _address.Text = _browser.Source?.AbsoluteUri ?? "";
                _chatButton.IsEnabled = id.Length > 0;
            };
            _browser.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess) _status.Text = "페이지를 열지 못했습니다. 인터넷 연결을 확인하거나 ‘브라우저에서 열기’를 사용하세요.";
            };
            _browser.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; if (e.IsUserInitiated) OpenExternal(e.Uri); };
            _browser.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            _browser.CoreWebView2.DownloadStarting += (_, e) => { e.Cancel = true; _status.Text = "파일 다운로드는 기본 브라우저에서 이용하세요."; };
            _browser.CoreWebView2.ProcessFailed += (_, _) => _status.Text = "브라우저 프로세스가 종료되었습니다. 이 창을 닫고 유튜브 링크를 다시 열어주세요.";
            _browser.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
            {
                bool fullscreen = _browser.CoreWebView2.ContainsFullScreenElement;
                toolbarVisibility(fullscreen);
            };
            _ready = true; _browser.CoreWebView2.Navigate(_requestedUrl);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            _status.Text = "Microsoft Edge WebView2 Runtime이 필요합니다. 아래 공식 주소에서 Evergreen Runtime을 설치한 후 다시 열어주세요.";
            var button = new Button { Content = "WebView2 설치 안내 열기", Margin = new(24) };
            button.Click += (_, _) => OpenExternal("https://developer.microsoft.com/microsoft-edge/webview2/");
            ((DockPanel)Content).Children.Remove(_browser); ((DockPanel)Content).Children.Add(button);
        }
        catch (Exception) { if (!_closed) _status.Text = "브라우저를 시작하지 못했습니다. 창을 다시 열거나 기본 브라우저를 이용하세요."; }

        void toolbarVisibility(bool fullscreen)
        {
            if (Content is DockPanel panel)
            {
                panel.Children[0].Visibility = _status.Visibility = fullscreen ? Visibility.Collapsed : Visibility.Visible;
                if (fullscreen) _beforeFullscreen = WindowState;
                WindowStyle = fullscreen ? WindowStyle.None : WindowStyle.SingleBorderWindow;
                WindowState = fullscreen ? WindowState.Maximized : _beforeFullscreen;
            }
        }
    }
    private static bool IsAllowedPage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        return YouTubePage.IsYouTube(uri) || uri.Host is "accounts.google.com" or "consent.youtube.com" or "consent.google.com";
    }
    private void NavigateInput()
    {
        if (!YouTubePage.TryNormalize(_address.Text, out var url)) { _status.Text = "유튜브 영상 링크를 입력하세요. watch, youtu.be, Shorts, live 링크를 지원합니다."; return; }
        NavigateVideo(url);
    }
    internal void NavigateVideo(string url)
    {
        if (!YouTubePage.TryNormalize(url, out var normalized)) return;
        _requestedUrl = normalized; _address.Text = normalized;
        if (_ready) _browser.CoreWebView2.Navigate(normalized);
    }
    private void ShowChat()
    {
        if (!_ready || _videoId.Length == 0) return;
        if (_chat != null) { _chat.Activate(); return; }
        _chat = new VideoChatWindow(ReadTranscriptAsync, SeekAsync) { Owner = this };
        _chat.Closed += (_, _) => _chat = null;
        _chat.Show();
    }
    private async Task<VideoTranscript> ReadTranscriptAsync(CancellationToken cancel)
    {
        if (_closed || !_ready || _videoId.Length == 0) throw new InvalidOperationException("유튜브 영상을 먼저 열어주세요.");
        string id = _videoId; int revision = _revision;
        var raw = await _browser.ExecuteScriptAsync(YouTubePage.ReadTranscriptScript).WaitAsync(cancel);
        cancel.ThrowIfCancellationRequested();
        if (_closed || revision != _revision || id != _videoId) throw new InvalidOperationException("영상이 바뀌었습니다. 현재 영상의 자막을 다시 불러오세요.");
        return YouTubePage.ParseTranscript(raw, id);
    }
    private async Task SeekAsync(VideoTranscript transcript, double seconds)
    {
        string id = YouTubePage.VideoId(transcript.Source);
        if (_closed || !_ready || id.Length == 0 || id != _videoId) throw new InvalidOperationException("영상이 바뀌었습니다. 현재 영상의 자막으로 다시 질문하세요.");
        var result = await _browser.ExecuteScriptAsync(YouTubePage.SeekScript(id, seconds));
        var error = JsonSerializer.Deserialize<string>(result);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        _status.Text = $"AI가 찾은 {MediaFiles.Time(seconds)} 구간으로 이동했습니다.";
    }
    private void OpenExternal(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception) { _status.Text = "기본 브라우저를 열지 못했습니다."; }
    }
}
