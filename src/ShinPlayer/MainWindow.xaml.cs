using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ShinPlayer;

public partial class MainWindow : Window
{
    private readonly App _app = (App)Application.Current;
    private PlayerSettings Settings => _app.Settings;
    private MpvPlayer? _player;
    private SubtitleCaptureWindow? _captureWindow;
    private Task? _initializing;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly ObservableCollection<MediaItem> _queue = new();
    private string? _currentPath;
    private int _currentIndex = -1;
    private bool _updating;
    private bool _scrubbing;
    private bool _loaded;
    private bool _eofHandled;
    private bool _fullScreen;
    private WindowState _previousWindowState;
    private Rect _previousBounds;
    private bool _closed;
    private bool _waitingFirstFrame;
    private bool _suppressCurrentHistory;
    private int _openGeneration;
    private TaskCompletionSource<bool>? _pendingLoad;
    private int _queuedRefresh;
    private double? _loopA;
    private double? _loopB;
    private double _requestedSpeed;
    private readonly DispatcherTimer _clickTimer;
    private readonly Stopwatch _saveClock = Stopwatch.StartNew();
    private readonly Stopwatch _openClock = new();
    public double LastLoadMilliseconds { get; private set; }
    public double LastPlaybackReadyMilliseconds { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => BrandTitle.Visibility = ActualWidth < 940 ? Visibility.Collapsed : Visibility.Visible;
        // Own the complete gesture instead of combining Slider/Thumb defaults
        // with a second coordinate conversion during mouse capture.
        SeekBar.IsMoveToPointEnabled = false;
        SeekBar.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Seek_Down), true);
        SeekBar.AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Seek_Up), true);
        SeekBar.AddHandler(LostMouseCaptureEvent, new MouseEventHandler(Seek_LostCapture), true);
        SeekBar.AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(Seek_Move), true);
        Width = Math.Min(Settings.Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Settings.Height, SystemParameters.WorkArea.Height);
        QueueList.ItemsSource = _queue;
        _queue.CollectionChanged += (_, _) => QueueCount.Text = $"재생목록 · {_queue.Count}";
        _requestedSpeed = Settings.RememberSpeed ? Settings.Speed : 1;
        _updating = true; VolumeBar.Value = Settings.Volume; _updating = false;
        VolumeText.Text = $"{Settings.Volume:0}";
        MuteButton.Content = Settings.Muted ? "\uE74F" : "\uE767";
        PaintSpeed(_requestedSpeed);
        PaintAudioBoost();
        _clickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
        _clickTimer.Tick += (_, _) => { _clickTimer.Stop(); Run(TogglePlayAsync); };
        StateChanged += (_, _) => MaxButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        Stage.SizeChanged += (_, _) => UpdateDesignEmptyLayout();
        ApplyDesignLayout();
        Loaded += (_, _) => StatusText.Text = "준비됨 · 파일을 놓거나 Ctrl+O로 열기";
    }

    private async Task EnsurePlayerAsync()
    {
        if (_closed) return;
        try { await (_initializing ??= InitializePlayerAsync()); }
        catch { _initializing = null; throw; }
    }
    public async Task WarmupAsync()
    {
        await EnsurePlayerAsync();
        await WaitUntilAsync(() => _player!.Flag("vo-configured"), TimeSpan.FromSeconds(15));
    }
    public object GetStatus() => new { running = true, visible = IsVisible, engineReady = _player?.Flag("vo-configured") ?? false, loaded = _loaded, path = _currentPath, position = _player?.Number("time-pos") ?? 0, speed = _requestedSpeed, paused = _player?.Flag("pause") ?? true, idle = _player?.Flag("idle-active") ?? true, loadMs = LastLoadMilliseconds, playbackReadyMs = LastPlaybackReadyMilliseconds, queueCount = _queue.Count, uiDesign = Settings.UiDesign, audioBoostDb = Settings.AudioBoostDb };
    private async Task InitializePlayerAsync()
    {
        Video.Visibility = Visibility.Visible;
        UpdateLayout();
        var hwnd = Video.WindowHandle;
        if (hwnd == IntPtr.Zero) { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded); UpdateLayout(); hwnd = Video.WindowHandle; }
        try
        {
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "libmpv-2.dll")))
                throw new FileNotFoundException("재생 엔진이 없습니다. 배포 압축파일의 Install.cmd를 실행해 설치해 주세요.");
            var player = await Task.Run(() => new MpvPlayer(hwnd, Settings));
            if (_closed) { player.Dispose(); return; }
            _player = player;
            _player.Changed += QueueRefresh;
            _player.FileLoaded += () => Dispatcher.BeginInvoke(OnFileLoaded);
            _player.PlaybackRestarted += () => Dispatcher.BeginInvoke(() =>
            {
                if (_loaded && _waitingFirstFrame) { LastPlaybackReadyMilliseconds = _openClock.Elapsed.TotalMilliseconds; _waitingFirstFrame = false; }
            });
            _player.Error += message => Dispatcher.BeginInvoke(() => OnPlaybackError(message));
            _player.Input += action => Dispatcher.BeginInvoke(() => HandleAction(action));
            _player.Log += App.Log;
            _player.FilesDropped += (files, append) => Dispatcher.BeginInvoke(() => Run(() => OpenFilesAsync(files, append)));
            Video.Visibility = Visibility.Hidden;
            RefreshState();
        }
        catch (Exception ex)
        {
            _initializing = null;
            Video.Visibility = Visibility.Hidden;
            EmptyState.Visibility = Visibility.Visible;
            EmptyTitle.Text = "재생 엔진을 준비할 수 없습니다";
            EmptyDescription.Text = ex.Message;
            throw;
        }
    }
    private void QueueRefresh()
    {
        if (_closed || Interlocked.Exchange(ref _queuedRefresh, 1) != 0) return;
        Dispatcher.BeginInvoke(() => { Interlocked.Exchange(ref _queuedRefresh, 0); if (!_closed) RefreshState(); }, DispatcherPriority.Background);
    }
    private void RefreshState()
    {
        if (_player == null) return;
        _updating = true;
        double duration = _player.Number("duration"), position = _player.Number("time-pos");
        SeekBar.IsEnabled = _loaded && duration > 0;
        if (!_scrubbing) SeekBar.Maximum = Math.Max(1, duration);
        if (!_scrubbing) { SeekBar.Value = Math.Min(position, SeekBar.Maximum); PositionText.Text = MediaFiles.Time(position); }
        DurationText.Text = MediaFiles.Time(duration);
        var speed = _player.Number("speed", _requestedSpeed);
        PaintSpeed(speed);
        VolumeBar.Value = _player.Number("volume", Settings.Volume);
        VolumeText.Text = $"{VolumeBar.Value:0}";
        MuteButton.Content = _player.Flag("mute") ? "\uE74F" : "\uE767";
        PlayButton.Content = _player.Flag("pause") || !_loaded ? "\uE768" : "\uE769";
        var w = _player.Number("video-params/w");
        var h = _player.Number("video-params/h");
        var decoder = _player.Text("hwdec-current", "no");
        var codec = _player.Text("video-codec");
        EngineText.Text = _loaded ? $"{(w > 0 ? $"{w:0}×{h:0}  ·  " : "오디오  ·  ")}{(decoder is "no" or "" ? "CPU" : decoder.ToUpperInvariant())}  ·  {(Settings.PitchCorrection ? "음높이 유지" : "음높이 변경")}" : "음높이 유지  ·  GPU 자동";
        _updating = false;
        if (_loaded && _player.Flag("eof-reached") && !_eofHandled)
        {
            _eofHandled = true;
            if (_currentIndex + 1 < _queue.Count) Run(() => PlayIndexAsync(_currentIndex + 1));
            else StatusText.Text = "재생 완료 · Space로 다시 재생";
        }
        if (!_player.Flag("eof-reached")) _eofHandled = false;
        if (_loaded && _saveClock.Elapsed.TotalSeconds >= 15) { RememberCurrent(); _app.SaveSettings(); _saveClock.Restart(); }
    }
    private void OnFileLoaded()
    {
        if (_closed || _pendingLoad == null || _pendingLoad.Task.IsCompleted) return;
        _loaded = true;
        _pendingLoad.TrySetResult(true);
        _eofHandled = false;
        EmptyState.Visibility = Visibility.Collapsed;
        Video.Visibility = Visibility.Visible;
        LastLoadMilliseconds = _openClock.Elapsed.TotalMilliseconds;
        StatusText.Text = "재생 중 · ← → 5초 이동 / Shift+← → 30초 이동";
        RefreshState();
    }
    private void OnPlaybackError(string message)
    {
        if (_closed) return;
        _loaded = false;
        _waitingFirstFrame = false;
        _pendingLoad?.TrySetResult(false);
        Video.Visibility = Visibility.Hidden;
        EmptyState.Visibility = Visibility.Visible;
        EmptyTitle.Text = "파일을 재생할 수 없습니다";
        EmptyDescription.Text = "파일이 손상되었거나, 지원하지 않는 형식일 수 있습니다. 다른 파일을 열어보세요.";
        StatusText.Text = message;
        App.Log(message);
    }
    public async Task OpenFilesAsync(string[] inputs, bool append = false)
    {
        if (inputs.Length == 1 && YouTubePage.TryNormalize(inputs[0], out var youtubeUrl)) { await OpenYouTubeAsync(youtubeUrl); return; }
        int generation = _openGeneration;
        await _operations.WaitAsync();
        try
        {
        if (_closed || generation != _openGeneration) return;
        var paths = await Task.Run(() => inputs.SelectMany(input =>
        {
            if (Directory.Exists(input))
            {
                try { return Directory.EnumerateFiles(input).Where(MediaFiles.IsMedia).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
            }
            return new[] { input };
        }).Where(File.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        if (_closed || generation != _openGeneration) return;
        if (paths.Length == 0) { StatusText.Text = "열 수 있는 파일이 없습니다. 파일 경로를 확인해 주세요."; return; }
        if (paths.All(MediaFiles.IsSubtitle))
        {
            if (!_loaded) { StatusText.Text = "영상을 먼저 열고 자막을 추가하세요."; return; }
            foreach (var subtitle in paths) await _player!.CommandAsync("sub-add", subtitle, "select");
            StatusText.Text = "자막을 추가했습니다."; return;
        }
        var videos = paths.Where(x => !MediaFiles.IsSubtitle(x)).ToArray();
        if (!append) { RememberCurrent(); _queue.Clear(); _currentIndex = -1; }
        int firstNew = _queue.Count;
        foreach (var path in videos) _queue.Add(new MediaItem(path));
        if (_queue.Count > 1) PlaylistPanel.Visibility = Visibility.Visible;
        if (!append || !_loaded) await PlayIndexCoreAsync(firstNew, generation);
        foreach (var subtitle in paths.Where(MediaFiles.IsSubtitle))
        {
            // sub-add must follow file-loaded, rather than only the loadfile command reply.
            if (_loaded && generation == _openGeneration) await _player!.CommandAsync("sub-add", subtitle, "select");
        }
        }
        finally { _operations.Release(); }
    }
    private async Task PlayIndexAsync(int index)
    {
        int generation = _openGeneration;
        // Preserve the selected item even if an earlier queued operation edits the list.
        var item = index >= 0 && index < _queue.Count ? _queue[index] : null;
        await _operations.WaitAsync();
        try { if (item != null) await PlayIndexCoreAsync(_queue.IndexOf(item), generation); }
        finally { _operations.Release(); }
    }
    private async Task PlayIndexCoreAsync(int index, int generation)
    {
            if (_closed || generation != _openGeneration || index < 0 || index >= _queue.Count) return;
            RememberCurrent();
            var path = _queue[index].Path;
            var resume = Settings.Resume ? Settings.Recent.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase))?.Position ?? 0 : 0;
            if (!File.Exists(path)) { StatusText.Text = "파일이 이동되었거나 삭제되었습니다: " + Path.GetFileName(path); return; }
            _videoChat?.Close();
            _youtubeWindow?.Close();
            _openClock.Restart();
            await EnsurePlayerAsync();
            if (_closed || generation != _openGeneration) return;
            _loaded = false;
            CancelSeek();
            _suppressCurrentHistory = false;
            _waitingFirstFrame = true;
            LastPlaybackReadyMilliseconds = 0;
            _eofHandled = false;
            _currentIndex = index;
            _currentPath = path;
            QueueList.SelectedIndex = index;
            QueueList.ScrollIntoView(_queue[index]);
            TitleFile.Text = Path.GetFileName(path);
            Title = Path.GetFileName(path) + " — 신플레이어";
            EmptyTitle.Text = "영상을 여는 중";
            EmptyDescription.Text = Path.GetFileName(path);
            Video.Visibility = Visibility.Hidden;
            EmptyState.Visibility = Visibility.Visible;
            StatusText.Text = "여는 중 · " + Path.GetFileName(path);
            await ClearLoopAsync();
            await _player!.SetAsync("pause", false);
            await _player.SetAsync("speed", _requestedSpeed);
            if (_closed || generation != _openGeneration) return;
            _pendingLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                await _player.CommandAsync("loadfile", path, "replace", "-1", "start=" + resume.ToString("0.###", CultureInfo.InvariantCulture));
                await _pendingLoad.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                await _player.CommandAsync("stop");
                OnPlaybackError("파일 열기 시간이 초과되었습니다. 다른 파일을 열어보세요.");
            }
            catch (Exception ex) when (!_closed) { OnPlaybackError(ex.Message); }
            finally { _pendingLoad = null; }
    }
    private void RememberCurrent()
    {
        if (!_loaded || _player == null || _currentPath == null) return;
        if (!_suppressCurrentHistory) Settings.Remember(_currentPath, _player.Number("time-pos"), _player.Number("duration"));
        Settings.Volume = _player.Number("volume", Settings.Volume);
        Settings.Muted = _player.Flag("mute");
        Settings.Speed = _requestedSpeed;
    }
    private async Task TogglePlayAsync()
    {
        if (!_loaded) { if (_queue.Count > 0) await PlayIndexAsync(Math.Max(_currentIndex, 0)); else OpenDialog(); return; }
        if (_player!.Flag("eof-reached"))
        {
            await _player.CommandAsync("seek", "0", "absolute+exact");
            await _player.SetAsync("pause", false);
            return;
        }
        await _player.CommandAsync("cycle", "pause");
    }
    private async Task SeekAsync(double value, bool relative = true)
    {
        if (!_loaded || !double.IsFinite(value)) return;
        if (!relative) value = Math.Clamp(value, 0, Math.Max(0, _player!.Number("duration")));
        await _player!.CommandAsync("seek", value.ToString("0.###", CultureInfo.InvariantCulture), relative ? "relative+exact" : "absolute+exact");
        StatusText.Text = relative ? $"{(value >= 0 ? "+" : "")}{value:0}초 이동" : $"{MediaFiles.Time(value)}로 이동";
    }
    private async Task SetSpeedAsync(double value)
    {
        _requestedSpeed = Math.Round(Math.Clamp(value, .25, 8), 2);
        Settings.Speed = _requestedSpeed;
        PaintSpeed(_requestedSpeed);
        if (_player != null) await _player.SetAsync("speed", _requestedSpeed);
        StatusText.Text = $"재생 속도 {_requestedSpeed:0.00}배 · {(Settings.PitchCorrection ? "목소리 높이를 유지합니다" : "음높이 보정 꺼짐")}";
    }
    private void PaintSpeed(double value)
    {
        SpeedValue.Content = $"{value:0.00}×";
        foreach (Button button in SpeedPresets.Children)
        {
            var selected = Math.Abs(double.Parse((string)button.Tag, CultureInfo.InvariantCulture) - value) < .005;
            button.Background = selected ? (Brush)FindResource("Accent") : Brushes.Transparent;
            button.Foreground = (Brush)FindResource(selected ? "AccentInk" : "Muted");
        }
    }
    private async Task ChangeVolumeAsync(double amount)
    {
        _updating = true;
        VolumeBar.Value = Math.Clamp(VolumeBar.Value + amount, 0, 100);
        _updating = false;
        Settings.Volume = VolumeBar.Value;
        VolumeText.Text = $"{VolumeBar.Value:0}";
        if (_player != null) await _player.SetAsync("volume", VolumeBar.Value);
    }
    private async Task ToggleMuteAsync()
    {
        Settings.Muted = !Settings.Muted;
        if (_player != null) await _player.CommandAsync("cycle", "mute");
        MuteButton.Content = Settings.Muted ? "\uE74F" : "\uE767";
    }
    private async Task CycleLoopAsync()
    {
        if (!_loaded) return;
        double now = _player!.Number("time-pos");
        if (!_loopA.HasValue) { _loopA = now; await _player.SetAsync("ab-loop-a", now); AbButton.Content = "A · ·"; StatusText.Text = $"반복 시작 {MediaFiles.Time(now)} · A를 다시 누르면 끝 지점"; }
        else if (!_loopB.HasValue)
        {
            if (now <= _loopA.Value + .2) { StatusText.Text = "반복 끝은 시작보다 뒤에 지정해 주세요."; return; }
            _loopB = now; await _player.SetAsync("ab-loop-b", now); AbButton.Content = "A↔B"; AbButton.SetResourceReference(ForegroundProperty, "Accent"); StatusText.Text = $"구간 반복 {MediaFiles.Time(_loopA.Value)} – {MediaFiles.Time(now)} · A로 해제";
        }
        else { await ClearLoopAsync(); StatusText.Text = "구간 반복 해제"; }
    }
    private async Task ClearLoopAsync()
    {
        _loopA = _loopB = null;
        AbButton.Content = "A–B"; AbButton.SetResourceReference(ForegroundProperty, "Ink");
        if (_player != null) { await _player.SetAsync("ab-loop-a", "no"); await _player.SetAsync("ab-loop-b", "no"); }
    }
    private async Task TakeScreenshotAsync()
    {
        if (!_loaded || _player!.Number("video-params/w") == 0) return;
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "신플레이어");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"신플레이어_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        await _player.CommandAsync("screenshot-to-file", path, "subtitles");
        StatusText.Text = "화면 저장: " + path;
    }
    private void ShowSubtitleCapture()
    {
        if (_captureWindow != null) { _captureWindow.Activate(); return; }
        if (!_loaded || _currentPath == null || _player!.Number("video-params/w") == 0) { StatusText.Text = "캡처할 영상을 먼저 열어주세요."; return; }
        _captureWindow = new SubtitleCaptureWindow(_currentPath, _player.Number("sub-delay")) { Owner = this };
        _captureWindow.Closed += (_, _) => _captureWindow = null;
        _captureWindow.Show();
    }
    public void OpenDialog(bool append = false)
    {
        var dialog = new OpenFileDialog { Title = append ? "재생목록에 추가" : "신플레이어 · 영상 열기", Multiselect = true, Filter = "미디어 파일|" + string.Join(";", MediaFiles.VideoExtensions.Concat(MediaFiles.AudioExtensions).Select(x => "*" + x)) + "|모든 파일|*.*" };
        if (dialog.ShowDialog(this) == true) Run(() => OpenFilesAsync(dialog.FileNames, append));
    }
    private void OpenSubtitle()
    {
        if (!_loaded) { StatusText.Text = "영상을 먼저 열어주세요."; return; }
        var dialog = new OpenFileDialog { Title = "자막 추가", Filter = "자막 파일|*.srt;*.ass;*.ssa;*.smi;*.vtt;*.sub;*.sup|모든 파일|*.*" };
        if (dialog.ShowDialog(this) == true) Run(() => _player!.CommandAsync("sub-add", dialog.FileName, "select"));
    }
    private void ToggleFullscreen()
    {
        if (!_fullScreen)
        {
            _previousWindowState = WindowState;
            _previousBounds = new Rect(Left, Top, Width, Height);
            WindowState = WindowState.Normal;
            _fullScreen = true;
            TitleRow.Height = new GridLength(0);
            TitleBar.Visibility = Visibility.Collapsed;
            ResizeMode = ResizeMode.NoResize;
            // WPF's borderless maximized window fills the current monitor.
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            _fullScreen = false;
            TitleRow.Height = new GridLength(CurrentDesign.HeaderHeight);
            TitleBar.Visibility = Visibility.Visible;
            ResizeMode = ResizeMode.CanResize;
            Left = _previousBounds.Left; Top = _previousBounds.Top; Width = _previousBounds.Width; Height = _previousBounds.Height;
            WindowState = _previousWindowState;
        }
        System.Windows.Shell.WindowChrome.GetWindowChrome(this).CaptionHeight = _fullScreen ? 0 : CurrentDesign.HeaderHeight;
    }
    private void ShowMoreMenu()
    {
        var menu = new ContextMenu();
        AddMenu(menu, "영상 열기…", () => OpenDialog(), "Ctrl+O");
        AddMenu(menu, "유튜브 주소창", FocusAddress, "Alt+D / Ctrl+U");
        AddMenu(menu, "내장 자막 AI 채팅…", ShowVideoChat, "Ctrl+J");
        AddMenu(menu, "최근 영상", ShowRecentMenu);
        AddMenu(menu, "화면 저장", () => Run(TakeScreenshotAsync), "Ctrl+S");
        AddMenu(menu, "자막별 일괄 캡처…", ShowSubtitleCapture, "Ctrl+Shift+S");
        AddMenu(menu, "구간 GIF 만들기…", ShowGif);
        AddMenu(menu, "쇼츠 만들기…", ShowShorts);
        AddMenu(menu, "UI 선택…", ShowDesignPicker);
        AddMenu(menu, "음량 증폭…", ShowBoostMenu);
        menu.Items.Add(new Separator());
        AddMenu(menu, "다음 영상 자동 재생 · 목록 순서", () => { PlaylistPanel.Visibility = Visibility.Visible; });
        AddToggle(menu, "현재 영상 반복", _player?.Text("loop-file") == "inf", () => { if (_player != null) Run(() => _player.SetAsync("loop-file", _player.Text("loop-file") == "inf" ? "no" : "inf")); });
        AddToggle(menu, "항상 위에", Topmost, () => Topmost = !Topmost);
        AddToggle(menu, "이어보기", Settings.Resume, () => Settings.Resume = !Settings.Resume);
        AddToggle(menu, "마지막 배속 기억", Settings.RememberSpeed, () => Settings.RememberSpeed = !Settings.RememberSpeed);
        AddToggle(menu, "배속 재생 시 음높이 유지", Settings.PitchCorrection, () => { Settings.PitchCorrection = !Settings.PitchCorrection; if (_player != null) Run(() => _player.SetAsync("audio-pitch-correction", Settings.PitchCorrection)); });
        AddToggle(menu, "하드웨어 디코딩", Settings.HardwareDecode, () => { Settings.HardwareDecode = !Settings.HardwareDecode; if (_player != null) Run(() => _player.SetAsync("hwdec", Settings.HardwareDecode ? "auto" : "no")); });
        menu.Items.Add(new Separator());
        AddToggle(menu, "닫으면 트레이에서 대기", Settings.CloseToTray, () => Settings.CloseToTray = !Settings.CloseToTray);
        AddToggle(menu, "Windows 시작 시 트레이 대기", WindowsIntegration.StartupEnabled, () => WindowsIntegration.SetStartup(!WindowsIntegration.StartupEnabled));
        AddMenu(menu, "Windows 기본 앱으로 설정", WindowsIntegration.OpenDefaults);
        AddMenu(menu, "최근 기록 지우기", ClearHistory);
        menu.Items.Add(new Separator());
        AddMenu(menu, "단축키 / 정보", ShowHelp, "F1");
        AddMenu(menu, "완전히 종료", _app.Quit, "Ctrl+Q");
        menu.Closed += (_, _) => _app.SaveSettings();
        menu.PlacementTarget = MoreButton; menu.IsOpen = true;
    }
    private void ShowSubtitleMenu()
    {
        var menu = new ContextMenu();
        AddMenu(menu, "자막 파일 추가…", OpenSubtitle);
        AddToggle(menu, "자막 표시", _player?.Flag("sub-visibility") != false, () => { if (_player != null) Run(() => _player.CommandAsync("cycle", "sub-visibility")); });
        AddMenu(menu, "다음 자막 트랙", () => { if (_loaded) Run(() => _player!.CommandAsync("cycle", "sid")); });
        AddMenu(menu, "자막별 일괄 캡처…", ShowSubtitleCapture, "Ctrl+Shift+S");
        AddMenu(menu, "다음 오디오 트랙", () => { if (_loaded) Run(() => _player!.CommandAsync("cycle", "aid")); });
        menu.Items.Add(new Separator());
        AddMenu(menu, "자막 0.1초 빠르게", () => { if (_loaded) Run(() => _player!.CommandAsync("add", "sub-delay", "-0.1")); });
        AddMenu(menu, "자막 0.1초 늦게", () => { if (_loaded) Run(() => _player!.CommandAsync("add", "sub-delay", "0.1")); });
        AddMenu(menu, "자막 시간 초기화", () => { if (_loaded) Run(() => _player!.SetAsync("sub-delay", 0)); });
        AddMenu(menu, "오디오 0.1초 빠르게", () => { if (_loaded) Run(() => _player!.CommandAsync("add", "audio-delay", "-0.1")); });
        AddMenu(menu, "오디오 0.1초 늦게", () => { if (_loaded) Run(() => _player!.CommandAsync("add", "audio-delay", "0.1")); });
        menu.PlacementTarget = AbButton; menu.IsOpen = true;
    }
    private void ShowRecentMenu()
    {
        var menu = new ContextMenu();
        foreach (var entry in Settings.Recent.Take(15))
        {
            var copy = entry;
            AddMenu(menu, Path.GetFileName(copy.Path) + (copy.Position > 0 ? "  ·  " + MediaFiles.Time(copy.Position) : ""), () => Run(() => OpenFilesAsync(new[] { copy.Path })));
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = "최근에 연 영상이 없습니다", IsEnabled = false });
        menu.PlacementTarget = RecentButton.IsVisible ? RecentButton : MoreButton;
        menu.IsOpen = true;
    }
    private void ClearHistory()
    {
        Settings.Recent.Clear();
        _suppressCurrentHistory = true;
        _app.SaveSettings();
        StatusText.Text = "최근 영상과 이어보기 기록을 지웠습니다.";
    }
    private static void AddMenu(ContextMenu menu, string title, Action action, string shortcut = "")
    {
        var item = new MenuItem { Header = title, InputGestureText = shortcut };
        item.Click += (_, _) => action(); menu.Items.Add(item);
    }
    private static void AddToggle(ContextMenu menu, string title, bool check, Action action)
    {
        var item = new MenuItem { Header = title, IsCheckable = true, IsChecked = check };
        item.Click += (_, _) => action(); menu.Items.Add(item);
    }
    private void ShowHelp()
    {
        MessageBox.Show(this,
            $"신플레이어 {typeof(MainWindow).Assembly.GetName().Version?.ToString(3)}\n\nAlt+D / Ctrl+U    유튜브 주소창 선택\nCtrl+J    내장 자막 AI 채팅\nCtrl+Shift+S    자막별 일괄 캡처\n" +
            "Space     재생 / 일시정지\n← / →     5초 이동 (Shift: 30초)\n↑ / ↓      음량 조절\n[ / ]        0.25배속 조절 (Shift: 0.05배)\nR / Backspace    1배속으로 복귀\nF / F11    전체화면 (Esc: 해제)\nM            음소거\nA             구간 반복 시작 → 끝 → 해제\n. / ,         다음 / 이전 프레임\nS             자막 표시 / 숨기기\nN / Shift+N    다음 / 이전 영상\nCtrl+O     영상 열기\nCtrl+L      재생목록\nCtrl+S      화면 저장\nCtrl+Q     완전히 종료\n\n" +
            "창을 닫으면 재생을 멈추고 트레이에서 대기합니다.\n트레이 아이콘을 더블클릭하면 다시 열립니다.\n\n" + (_player?.Version ?? "mpv") + " · .NET 8 / Windows x64\n신플레이어 소스: MIT · 한국AI교육진흥원\n외부 구성요소에는 별도 라이선스가 적용됩니다.\n소스와 라이선스는 설치 폴더에 포함되어 있습니다.",
            "신플레이어 · 단축키", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void HandleAction(string action)
    {
        if (_closed) return;
        switch (action)
        {
            case "click": _clickTimer.Stop(); _clickTimer.Start(); break;
            case "play": Run(TogglePlayAsync); break;
            case "fullscreen": _clickTimer.Stop(); ToggleFullscreen(); break;
            case "escape": if (_fullScreen) ToggleFullscreen(); break;
            case "forward": Run(() => SeekAsync(5)); break;
            case "backward": Run(() => SeekAsync(-5)); break;
            case "forward-long": Run(() => SeekAsync(30)); break;
            case "backward-long": Run(() => SeekAsync(-30)); break;
            case "volume-up": Run(() => ChangeVolumeAsync(5)); break;
            case "volume-down": Run(() => ChangeVolumeAsync(-5)); break;
            case "faster": Run(() => SetSpeedAsync(_requestedSpeed + .25)); break;
            case "slower": Run(() => SetSpeedAsync(_requestedSpeed - .25)); break;
            case "faster-fine": Run(() => SetSpeedAsync(_requestedSpeed + .05)); break;
            case "slower-fine": Run(() => SetSpeedAsync(_requestedSpeed - .05)); break;
            case "normal-speed": Run(() => SetSpeedAsync(1)); break;
            case "mute": Run(ToggleMuteAsync); break;
            case "subtitles": if (_loaded) Run(() => _player!.CommandAsync("cycle", "sub-visibility")); break;
            case "ab": Run(CycleLoopAsync); break;
            case "frame-next": if (_loaded) Run(() => _player!.CommandAsync("frame-step")); break;
            case "frame-back": if (_loaded) Run(() => _player!.CommandAsync("frame-back-step")); break;
            case "start": Run(() => SeekAsync(0, false)); break;
            case "open": OpenDialog(); break;
            case "open-link": FocusAddress(); break;
            case "chat": ShowVideoChat(); break;
            case "playlist": TogglePlaylist(); break;
            case "menu": ShowMoreMenu(); break;
            case "next": Run(() => PlayIndexAsync(_currentIndex + 1)); break;
            case "previous": Run(() => PlayIndexAsync(_currentIndex - 1)); break;
            case "screenshot": Run(TakeScreenshotAsync); break;
            case "subtitle-capture": ShowSubtitleCapture(); break;
            case "help": ShowHelp(); break;
            case "quit": _app.Quit(); break;
        }
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if ((Keyboard.Modifiers == ModifierKeys.Alt && key == Key.D) || (Keyboard.Modifiers == ModifierKeys.Control && key == Key.U))
        {
            FocusAddress(); e.Handled = true; return;
        }
        // Leave text editing (including Space, arrows and Ctrl+A/C/V) to the address box.
        if (AddressInput.IsKeyboardFocusWithin || e.OriginalSource is TextBox) return;
        if (QueueList.IsKeyboardFocusWithin && !ctrl && key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Enter or Key.Delete) return;
        string? action = ctrl ? key switch { Key.O => "open", Key.U => "open-link", Key.J => "chat", Key.L => "playlist", Key.Q => "quit", Key.S => shift ? "subtitle-capture" : "screenshot", _ => null } : key switch
        {
            Key.Space or Key.P => "play", Key.Right => shift ? "forward-long" : "forward", Key.Left => shift ? "backward-long" : "backward",
            Key.Up => "volume-up", Key.Down => "volume-down", Key.OemCloseBrackets => shift ? "faster-fine" : "faster", Key.OemOpenBrackets => shift ? "slower-fine" : "slower",
            Key.R or Key.Back => "normal-speed", Key.F or Key.F11 => "fullscreen", Key.Escape => "escape", Key.M => "mute", Key.S => "subtitles", Key.A => "ab",
            Key.OemPeriod => "frame-next", Key.OemComma => "frame-back", Key.Home => "start", Key.N => shift ? "previous" : "next", Key.F1 => "help", _ => null
        };
        if (action != null) { HandleAction(action); e.Handled = true; }
    }
    private async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { StatusText.Text = "처리하지 못했습니다: " + ex.Message; App.Log(ex.ToString()); }
    }
    private void TogglePlaylist() => PlaylistPanel.Visibility = PlaylistPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void Window_DragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private void Window_Drop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) Run(() => OpenFilesAsync(files, Keyboard.Modifiers.HasFlag(ModifierKeys.Control))); }
    private void Open_Click(object sender, RoutedEventArgs e) => OpenDialog();
    private void Add_Click(object sender, RoutedEventArgs e) => OpenDialog(true);
    private void Recent_Click(object sender, RoutedEventArgs e) => ShowRecentMenu();
    private void More_Click(object sender, RoutedEventArgs e) => ShowMoreMenu();
    private void Capture_Click(object sender, RoutedEventArgs e) => ShowSubtitleCapture();
    private void Play_Click(object sender, RoutedEventArgs e) => Run(TogglePlayAsync);
    private void Backward_Click(object sender, RoutedEventArgs e) => Run(() => SeekAsync(-5));
    private void Forward_Click(object sender, RoutedEventArgs e) => Run(() => SeekAsync(5));
    private void Previous_Click(object sender, RoutedEventArgs e) => Run(() => PlayIndexAsync(_currentIndex - 1));
    private void Next_Click(object sender, RoutedEventArgs e) => Run(() => PlayIndexAsync(_currentIndex + 1));
    private void Mute_Click(object sender, RoutedEventArgs e) => Run(ToggleMuteAsync);
    private void Ab_Click(object sender, RoutedEventArgs e) => Run(CycleLoopAsync);
    private void Subtitle_Click(object sender, RoutedEventArgs e) => ShowSubtitleMenu();
    private void Playlist_Click(object sender, RoutedEventArgs e) => TogglePlaylist();
    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();
    private void Slower_Click(object sender, RoutedEventArgs e) => Run(() => SetSpeedAsync(_requestedSpeed - .05));
    private void Faster_Click(object sender, RoutedEventArgs e) => Run(() => SetSpeedAsync(_requestedSpeed + .05));
    private void Speed_Click(object sender, RoutedEventArgs e) => Run(() => SetSpeedAsync(double.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture)));
    private void CustomSpeed_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox { Text = _requestedSpeed.ToString("0.00", CultureInfo.InvariantCulture), FontSize = 24, Padding = new Thickness(10), Margin = new Thickness(0,16,0,16) };
        var apply = new Button { Content = "적용", IsDefault = true, Style = (Style)FindResource("Primary") };
        var cancel = new Button { Content = "취소", IsCancel = true, Margin = new Thickness(0,6,0,0) };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "재생 속도 · 0.25 ~ 8.00배", FontSize = 15 }); panel.Children.Add(input); panel.Children.Add(apply); panel.Children.Add(cancel);
        var dialog = new Window { Title = "배속 직접 입력", Owner = this, Width = 310, Height = 280, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        apply.Click += (_, _) => { if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value >= .25 && value <= 8) { Run(() => SetSpeedAsync(value)); dialog.Close(); } else { input.BorderBrush = Brushes.OrangeRed; input.ToolTip = "0.25~8 사이의 숫자를 입력하세요."; } };
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        dialog.ShowDialog();
    }
    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || VolumeText == null) return;
        Settings.Volume = e.NewValue; VolumeText.Text = $"{e.NewValue:0}";
        if (_player != null) Run(() => _player.SetAsync("volume", e.NewValue));
    }
    private void Seek_Down(object sender, MouseButtonEventArgs e)
    {
        if (!_loaded || _player!.Number("duration") <= 0) return;
        e.Handled = true;
        BeginSeek(e.GetPosition(SeekBar));
    }
    private void Seek_Move(object sender, MouseEventArgs e)
    {
        if (!_scrubbing || !SeekBar.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
        e.Handled = true;
        UpdateSeek(e.GetPosition(SeekBar));
    }
    private void Seek_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_scrubbing) PositionText.Text = MediaFiles.Time(e.NewValue); }
    private void Seek_Up(object sender, MouseButtonEventArgs e)
    {
        if (!_scrubbing) return;
        e.Handled = true;
        EndSeek(e.GetPosition(SeekBar));
    }
    private double? SeekTarget(Point position)
    {
        if (SeekBar.Template.FindName("PART_Track", SeekBar) is not Track track) return null;
        // Coordinates are WPF DIPs, including DPI and layout transforms.
        // Track.ValueFromPoint uses an old arranged thumb offset when Value has
        // just changed. Fixed geometry avoids applying the click delta twice.
        var x = SeekBar.TranslatePoint(position, track).X;
        var thumbWidth = track.Thumb?.ActualWidth ?? 0;
        var travel = track.ActualWidth - thumbWidth;
        if (!double.IsFinite(x) || !double.IsFinite(travel) || travel <= 0) return null;
        var fraction = Math.Clamp((x - thumbWidth / 2) / travel, 0, 1);
        if (track.IsDirectionReversed) fraction = 1 - fraction;
        return SeekBar.Minimum + fraction * (SeekBar.Maximum - SeekBar.Minimum);
    }
    private void BeginSeek(Point position)
    {
        if (!_loaded || _player!.Number("duration") <= 0 || SeekTarget(position) is not double target) return;
        _scrubbing = true;
        SeekBar.CaptureMouse();
        SeekBar.Value = target;
        PositionText.Text = MediaFiles.Time(target);
    }
    private void UpdateSeek(Point position)
    {
        if (_scrubbing && SeekTarget(position) is double target) SeekBar.Value = target;
    }
    private void EndSeek(Point position)
    {
        if (!_scrubbing) return;
        // Mouse moves can be coalesced, so use the release coordinate as well.
        UpdateSeek(position);
        CommitSeek();
    }
    private void CancelSeek()
    {
        _scrubbing = false;
        if (SeekBar.IsMouseCaptured) SeekBar.ReleaseMouseCapture();
    }
    private void Seek_LostCapture(object sender, MouseEventArgs e) { if (!SeekBar.IsMouseCaptureWithin) CommitSeek(); }
    private void CommitSeek()
    {
        if (!_scrubbing) return;
        var target = SeekBar.Value;
        CancelSeek();
        Run(() => SeekAsync(target, false));
    }
    private void Queue_DoubleClick(object sender, MouseButtonEventArgs e) { if (QueueList.SelectedIndex >= 0) Run(() => PlayIndexAsync(QueueList.SelectedIndex)); }
    private void Queue_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Delete) { Run(RemoveSelectedAsync); e.Handled = true; } else if (e.Key == Key.Enter && QueueList.SelectedIndex >= 0) { Run(() => PlayIndexAsync(QueueList.SelectedIndex)); e.Handled = true; } }
    private void Remove_Click(object sender, RoutedEventArgs e) => Run(RemoveSelectedAsync);
    private async Task RemoveSelectedAsync()
    {
        var item = QueueList.SelectedItem as MediaItem;
        int generation = _openGeneration;
        await _operations.WaitAsync();
        try
        {
        int index = item == null ? -1 : _queue.IndexOf(item);
        if (index < 0 || _closed || generation != _openGeneration) return;
        bool current = index == _currentIndex;
        _queue.RemoveAt(index);
        if (index < _currentIndex) _currentIndex--;
        QueueList.SelectedIndex = Math.Min(index, _queue.Count - 1);
        if (current)
        {
            if (_queue.Count > 0) await PlayIndexCoreAsync(Math.Min(index, _queue.Count - 1), generation);
            else await StopPlaybackAsync();
        }
        }
        finally { _operations.Release(); }
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_app.IsExiting) return;
        e.Cancel = true;
        if (Settings.CloseToTray) Run(HideToTrayAsync); else _app.Quit();
    }
    public async Task HideToTrayAsync()
    {
        _gifWindow?.Close();
        await StopShortsAsync();
        _shortsWindow?.Close();
        _youtubeWindow?.Close();
        _videoChat?.Close();
        RememberCurrent(); _app.SaveSettings();
        _openGeneration++;
        _pendingLoad?.TrySetResult(false);
        CancelSeek();
        _loaded = false;
        Hide();
        _clickTimer.Stop();
        await _operations.WaitAsync();
        try
        {
            await StopPlaybackAsync();
        }
        finally { _operations.Release(); }
    }
    private async Task StopPlaybackAsync()
    {
            RememberCurrent();
            _loaded = false;
            _waitingFirstFrame = false;
            CancelSeek();
            if (_player != null) await _player.CommandAsync("stop");
            _currentPath = null;
            _currentIndex = -1;
            Video.Visibility = Visibility.Hidden;
            EmptyState.Visibility = Visibility.Visible;
            SetIdleHeading();
            TitleFile.Text = ""; Title = "신플레이어";
            StatusText.Text = "준비됨 · 파일을 놓거나 Ctrl+O로 열기";
            RefreshState();
    }
    public void PrepareExit()
    {
        _gifWindow?.Cancel();
        _shortsWindow?.Cancel();
        _youtubeWindow?.Close();
        _videoChat?.Close();
        _captureWindow?.Cancel();
        RememberCurrent();
        if (!_fullScreen && WindowState == WindowState.Normal) { Settings.Width = Width; Settings.Height = Height; }
        _closed = true;
        CancelSeek();
        _openGeneration++;
        _pendingLoad?.TrySetResult(false);
        _clickTimer.Stop();
        _player?.Dispose();
        _player = null;
    }
    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed > timeout) throw new TimeoutException("응답 대기 시간이 초과되었습니다."); await Task.Delay(25); }
    }
}
