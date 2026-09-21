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

internal sealed partial class YouTubeWindow : Window
{
    private readonly WebView2 _browser = new();
    private readonly TextBox _address = new() { MinWidth = 180, Padding = new(8), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(14, 8, 14, 8), FontSize = 12 };
    private readonly Button _chatButton = new() { Content = "자막 · AI 채팅", IsEnabled = false };
    private readonly VideoChatPanel _chat;
    private readonly Grid _workspace = new();
    private readonly ContentControl _browserFrame = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Border _chatFrame = new() { BorderThickness = new(1, 0, 0, 0) };
    private readonly Button _placementButton = new() { Content = "채팅 배치" };
    private string _chatPlacement = "auto";
    private readonly string? _testProfileDirectory;
    private bool _closed, _ready;
    private string _requestedUrl, _videoId = "";
    private int _revision;
    private WindowState _beforeFullscreen;
    internal Task Initialization { get; private set; } = Task.CompletedTask;
    internal YouTubeWindow(string url, string? testProfileDirectory = null, ApiKeyStore? keys = null)
    {
        _requestedUrl = url;
        _testProfileDirectory = testProfileDirectory;
        Style = (Style)FindResource(typeof(Window));
        Title = "YouTube · 신플레이어";
        Width = 1280; Height = 820; MinWidth = 840; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _chat = new VideoChatPanel(ReadTranscriptAsync, SeekAsync, keys: keys);
        _chat.ResetTranscript("유튜브 영상의 ‘스크립트 표시’를 연 뒤 자막을 불러오세요. 질문에 답하고, 이동을 요청하면 해당 구간으로 이동합니다.");
        _chatFrame.Child = _chat;
        _chatFrame.SetResourceReference(Border.BackgroundProperty, "Panel");
        _chatFrame.SetResourceReference(Border.BorderBrushProperty, "Line");
        _browserFrame.Content = _browser;
        _workspace.ColumnDefinitions.Add(new()); _workspace.ColumnDefinitions.Add(new() { Width = new(390) });
        _workspace.RowDefinitions.Add(new()); _workspace.RowDefinitions.Add(new() { Height = new(0) });
        _workspace.Children.Add(_browserFrame); _workspace.Children.Add(_chatFrame);
        Grid.SetColumn(_chatFrame, 1);
        var root = new DockPanel();
        var toolbar = new DockPanel { Margin = new(10) };
        var back = new Button { Content = "←", ToolTip = "뒤로" };
        var forward = new Button { Content = "→", ToolTip = "앞으로" };
        var reload = new Button { Content = "↻", ToolTip = "새로고침" };
        var go = new Button { Content = "이동" };
        var external = new Button { Content = "외부 열기", ToolTip = "기본 브라우저에서 열기" };
        var gif = new Button { Content = "GIF", ToolTip = "선택 구간의 재생 화면을 GIF로 저장" };
        gif.Click += (_, _) => ShowGif();
        toolbar.Children.Add(back); toolbar.Children.Add(forward); toolbar.Children.Add(reload);
        DockPanel.SetDock(_chatButton, Dock.Right); toolbar.Children.Add(_chatButton);
        DockPanel.SetDock(_placementButton, Dock.Right); toolbar.Children.Add(_placementButton);
        DockPanel.SetDock(external, Dock.Right); toolbar.Children.Add(external);
        DockPanel.SetDock(gif, Dock.Right); toolbar.Children.Add(gif);
        DockPanel.SetDock(go, Dock.Right); toolbar.Children.Add(go); toolbar.Children.Add(_address);
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        root.Children.Add(_workspace); Content = root;
        _address.Text = url;
        _status.Text = "유튜브 페이지를 그대로 재생합니다. 자막 검색은 영상 설명의 ‘스크립트 표시’를 연 뒤 이용하세요.";
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        go.Click += (_, _) => NavigateInput();
        _address.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; NavigateInput(); } };
        PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if ((Keyboard.Modifiers == ModifierKeys.Control && key is Key.L or Key.U) || (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.D))
            { _address.Focus(); _address.SelectAll(); e.Handled = true; }
        };
        back.Click += (_, _) => { if (_ready && _browser.CanGoBack) _browser.GoBack(); };
        forward.Click += (_, _) => { if (_ready && _browser.CanGoForward) _browser.GoForward(); };
        reload.Click += (_, _) => { if (_ready) _browser.Reload(); };
        external.Click += (_, _) => OpenExternal(_browser.Source?.AbsoluteUri ?? _requestedUrl);
        _chatButton.Content = "질문";
        _chatButton.Click += (_, _) => _chat.FocusQuestion();
        _placementButton.Click += (_, _) => ShowPlacementMenu();
        _workspace.SizeChanged += (_, _) => UpdateChatLayout();
        Loaded += (_, _) => Initialization = InitializeAsync();
        Closing += async (_, e) =>
        {
            if (_gifWindow is not { IsBusy: true }) return;
            e.Cancel = true;
            await StopGifAsync();
            if (!_closed) Close();
        };
        Closed += (_, _) => { _gifWindow?.Cancel(); _closed = true; _revision++; _chat.Close(); _browser.Dispose(); };
    }
    private void ShowPlacementMenu()
    {
        var menu = new ContextMenu();
        foreach (var (id, label) in new[] { ("auto", "자동 · 창 크기에 맞추기"), ("right", "오른쪽"), ("bottom", "아래쪽") })
        {
            var item = new MenuItem { Header = label, IsCheckable = true, IsChecked = _chatPlacement == id };
            item.Click += (_, _) => { _chatPlacement = id; UpdateChatLayout(); };
            menu.Items.Add(item);
        }
        menu.PlacementTarget = _placementButton; menu.IsOpen = true;
    }
    private void UpdateChatLayout()
    {
        bool bottom = _chatPlacement == "bottom" || (_chatPlacement == "auto" && _workspace.ActualWidth < 1100);
        _workspace.ColumnDefinitions[1].Width = new GridLength(bottom ? 0 : Math.Clamp(_workspace.ActualWidth * .31, 350, 420));
        _workspace.RowDefinitions[1].Height = new GridLength(bottom ? Math.Clamp(_workspace.ActualHeight * .46, 260, 340) : 0);
        Grid.SetColumn(_chatFrame, bottom ? 0 : 1); Grid.SetRow(_chatFrame, bottom ? 1 : 0);
        _chatFrame.BorderThickness = bottom ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);
        _placementButton.ToolTip = bottom ? "채팅 패널: 아래쪽" : "채팅 패널: 오른쪽";
    }
    private async Task InitializeAsync()
    {
        try
        {
            var folder = _testProfileDirectory ?? Path.Combine(PlayerSettings.DirectoryPath, "youtube-browser");
            if (_testProfileDirectory == null && (((App)Application.Current).IsTest || ((App)Application.Current).IsDiagnosticSession))
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
                _revision++;
                _chat.ResetTranscript("페이지가 바뀌었습니다. 영상의 스크립트를 열고 자막을 다시 불러오세요.");
            };
            _browser.CoreWebView2.SourceChanged += (_, _) =>
            {
                var id = YouTubePage.VideoId(_browser.Source?.AbsoluteUri ?? "");
                if (id != _videoId)
                {
                    _revision++;
                    _chat.ResetTranscript(id.Length > 0 ? "영상이 바뀌었습니다. ‘스크립트 표시’를 연 뒤 자막을 불러오세요." : "유튜브 영상을 먼저 열어주세요.");
                }
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
            _browserFrame.Content = button;
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
        if (!YouTubePage.TryNormalizeAddress(_address.Text, out var url)) { _status.Text = "유튜브 주소를 입력하세요. youtube.com 또는 유튜브 영상 주소를 사용할 수 있습니다."; return; }
        NavigateVideo(url);
    }
    internal void NavigateVideo(string url)
    {
        if (!YouTubePage.TryNormalizeAddress(url, out var normalized)) return;
        _requestedUrl = normalized; _address.Text = normalized;
        if (_ready) _browser.CoreWebView2.Navigate(normalized);
    }
    private async Task<VideoTranscript> ReadTranscriptAsync(CancellationToken cancel)
    {
        if (_closed || !_ready || _videoId.Length == 0) throw new InvalidOperationException("유튜브 영상을 먼저 열어주세요.");
        string id = _videoId; int revision = _revision;
        var raw = await _browser.ExecuteScriptAsync(YouTubePage.ReadTranscriptScript).WaitAsync(cancel);
        cancel.ThrowIfCancellationRequested();
        if (_closed || revision != _revision || id != _videoId) throw new InvalidOperationException("영상이 바뀌었습니다. 현재 영상의 자막을 다시 불러오세요.");
        var transcript = YouTubePage.ParseTranscript(raw, id);
        // The app keeps the loaded transcript; return the web page to its video after reading.
        await _browser.ExecuteScriptAsync("""
            (()=>{
              const panel=[...document.querySelectorAll('ytd-engagement-panel-section-list-renderer')].find(e=>e.getAttribute('target-id')==='engagement-panel-searchable-transcript' && e.getAttribute('visibility')==='ENGAGEMENT_PANEL_VISIBILITY_EXPANDED');
              const close=panel?.querySelector('#visibility-button button, button[aria-label="닫기"], button[aria-label="Close"]');
              if(close)close.click();
              requestAnimationFrame(()=>requestAnimationFrame(()=>document.querySelector('#movie_player')?.scrollIntoView({block:'center'})));
            })()
            """);
        cancel.ThrowIfCancellationRequested();
        if (_closed || revision != _revision || id != _videoId) throw new InvalidOperationException("영상이 바뀌었습니다. 현재 영상의 자막을 다시 불러오세요.");
        return transcript;
    }
    private async Task SeekAsync(VideoTranscript transcript, double seconds)
    {
        string id = YouTubePage.VideoId(transcript.Source);
        if (_closed || !_ready || id.Length == 0 || id != _videoId) throw new InvalidOperationException("영상이 바뀌었습니다. 현재 영상의 자막으로 다시 질문하세요.");
        var result = await _browser.ExecuteScriptAsync(YouTubePage.SeekScript(id, seconds));
        var error = JsonSerializer.Deserialize<string>(result);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        await _browser.ExecuteScriptAsync("document.querySelector('#movie_player')?.scrollIntoView({block:'center'})");
        _status.Text = $"AI가 찾은 {MediaFiles.Time(seconds)} 구간으로 이동했습니다.";
    }
    private void OpenExternal(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception) { _status.Text = "기본 브라우저를 열지 못했습니다."; }
    }
}
