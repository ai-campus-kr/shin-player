using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ShinPlayer;

internal sealed class GifWindow : Window
{
    private readonly Func<CancellationToken, Task<LocalGifSource>> _load;
    internal readonly TextBox StartTime = new(), EndTime = new();
    internal readonly GifRangeSelector Range = new();
    internal readonly TextBox SpeedInput = new() { Text = "1", Width = 72, VerticalContentAlignment = VerticalAlignment.Center };
    internal readonly TextBlock Estimate = new() { TextWrapping = TextWrapping.Wrap, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new(0, 4, 0, 12) };
    private readonly ComboBox _width = new() { ItemsSource = new[] { 360, 480, 720 }, SelectedItem = 480, MinWidth = 100 };
    private readonly ComboBox _fps = new() { ItemsSource = new[] { 10, 12, 15, 20 }, SelectedItem = 12, MinWidth = 100 };
    private readonly StackPanel _fields = new();
    private readonly TextBlock _title = new() { TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 6, 0, 20) };
    internal readonly ExportFeedback Feedback = new("GIF");
    private readonly Button _start = new() { Content = "GIF 만들기", IsEnabled = false };
    private readonly Button _cancel = new() { Content = "취소", Margin = new(8, 0, 0, 0) };
    internal readonly Button OpenResult = new() { Content = "GIF 열기", IsEnabled = false, Margin = new(0, 0, 8, 0) };
    internal readonly Button OpenFolder = new() { Content = "저장 폴더", IsEnabled = false, Margin = new(0, 0, 8, 0) };
    private CancellationTokenSource _cancelSource = new();
    private LocalGifSource? _source;
    private CaptureTools? _tools;
    private bool _busy = true, _closeWhenStopped, _closed, _syncingRange;
    private GifOptions? _savedOptions;
    internal string? SavedPath { get; private set; }
    internal Task Initialization { get; private set; } = Task.CompletedTask;
    private Task _operation = Task.CompletedTask;
    internal bool IsBusy => _busy;

    internal GifWindow(Func<CancellationToken, Task<LocalGifSource>> load)
    {
        _load = load;
        Style = (Style)FindResource(typeof(Window));
        Title = "구간 GIF 만들기 · 신플레이어";
        Width = 720; Height = 720; MinWidth = 600; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new(24) };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel();
        var eyebrow = new TextBlock { Text = "SAVE THE MOMENT", FontSize = 11, Margin = new(0, 0, 0, 10) };
        eyebrow.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); heading.Children.Add(eyebrow);
        heading.Children.Add(new TextBlock { Text = "이 구간을, GIF로.", FontSize = 26, FontWeight = FontWeights.SemiBold });
        _title.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); heading.Children.Add(_title); root.Children.Add(heading);
        var body = new StackPanel();
        body.Children.Add(_fields);
        var rangeHeading = new DockPanel { Margin = new(0, 0, 0, 4) };
        var rangeActions = new StackPanel { Orientation = Orientation.Horizontal };
        var zoom = new Button { Content = "선택 확대", FontSize = 11, Padding = new(10, 6, 10, 6), ToolTip = "선택 구간 주변을 확대해 더 정밀하게 조절" };
        var all = new Button { Content = "전체 보기", FontSize = 11, Padding = new(10, 6, 10, 6), Margin = new(6, 0, 0, 0) };
        zoom.Click += (_, _) => Range.ZoomToSelection(); all.Click += (_, _) => Range.ShowAll();
        rangeActions.Children.Add(zoom); rangeActions.Children.Add(all);
        DockPanel.SetDock(rangeActions, Dock.Right); rangeHeading.Children.Add(rangeActions);
        rangeHeading.Children.Add(new TextBlock { Text = "드래그로 구간 선택", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        _fields.Children.Add(rangeHeading); _fields.Children.Add(Range);
        var rangeHelp = new TextBlock { Text = "양 끝은 시작·끝 조절 · 가운데는 구간 이동", FontSize = 12, Margin = new(0, 0, 0, 14) };
        rangeHelp.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _fields.Children.Add(rangeHelp);
        var times = new Grid { Margin = new(0, 0, 0, 16) };
        times.ColumnDefinitions.Add(new()); times.ColumnDefinitions.Add(new() { Width = new GridLength(16) }); times.ColumnDefinitions.Add(new());
        times.Children.Add(TimeField("시작", StartTime));
        var endField = TimeField("끝", EndTime); Grid.SetColumn(endField, 2); times.Children.Add(endField); _fields.Children.Add(times);
        var speedRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 10) };
        speedRow.Children.Add(new TextBlock { Text = "배속", Width = 42 });
        speedRow.Children.Add(SpeedInput);
        speedRow.Children.Add(new TextBlock { Text = "×", Margin = new(8, 0, 12, 0) });
        foreach (double speed in new[] { .5, 1, 2, 5, 10, 20 })
        {
            var preset = new Button { Content = $"{speed:0.#}×", Padding = new(8, 6, 8, 6), FontSize = 11, Margin = new(0, 0, 3, 0), ToolTip = $"GIF {speed:0.#}배속" };
            preset.Click += (_, _) => SpeedInput.Text = GifExport.Number(speed);
            speedRow.Children.Add(preset);
        }
        _fields.Children.Add(speedRow);
        Estimate.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        _fields.Children.Add(Estimate);
        var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 10) };
        options.Children.Add(new TextBlock { Text = "최대 크기", Margin = new(0, 0, 10, 0) }); options.Children.Add(_width);
        options.Children.Add(new TextBlock { Text = "px     부드러움", Margin = new(8, 0, 10, 0) }); options.Children.Add(_fps);
        options.Children.Add(new TextBlock { Text = "fps", Margin = new(8, 0, 0, 0) }); _fields.Children.Add(options);
        var help = new TextBlock { Text = "배속 0.25~100× · 원본 최대 1시간 · 완성 GIF 0.2초~5분\n시간 직접 입력도 가능합니다. 손잡이에서 ← → 0.1초 · Shift 1초 · Ctrl 10초.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        help.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); body.Children.Add(help);
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        var footer = new StackPanel { Margin = new(0, 12, 0, 0) }; footer.Children.Add(Feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 10, 0, 0) };
        _start.Style = (Style)FindResource("Primary");
        buttons.Children.Add(OpenFolder); buttons.Children.Add(OpenResult); buttons.Children.Add(_start); buttons.Children.Add(_cancel);
        footer.Children.Add(buttons); Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
        Loaded += (_, _) => Initialization = InitializeAsync();
        _start.Click += async (_, _) => await ExportAsync();
        _cancel.Click += (_, _) => { if (_busy) Cancel(); else Close(); };
        OpenResult.Click += (_, _) => Open(false); OpenFolder.Click += (_, _) => Open(true);
        Closing += OnClosing;
        Closed += (_, _) => { _closed = true; _cancelSource.Cancel(); _cancelSource.Dispose(); };
        Range.RangeChanged += () =>
        {
            _syncingRange = true;
            try { StartTime.Text = GifOptions.Time(Range.Start); EndTime.Text = GifOptions.Time(Range.End); }
            finally { _syncingRange = false; }
            RefreshEstimate();
        };
        StartTime.TextChanged += (_, _) => SyncTypedRange();
        EndTime.TextChanged += (_, _) => SyncTypedRange();
        SpeedInput.TextChanged += (_, _) => RefreshEstimate();
        _width.SelectionChanged += (_, _) => RefreshEstimate(); _fps.SelectionChanged += (_, _) => RefreshEstimate();
    }
    private FrameworkElement TimeField(string label, TextBox input)
    {
        var row = new StackPanel();
        var heading = new DockPanel { Margin = new(0, 0, 0, 6) };
        var now = new Button { Content = "현재 위치", Padding = new(8, 3, 8, 3), FontSize = 11 };
        DockPanel.SetDock(now, Dock.Right); heading.Children.Add(now);
        heading.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(heading);
        input.FontFamily = new System.Windows.Media.FontFamily("Consolas"); input.FontSize = 15;
        System.Windows.Automation.AutomationProperties.SetName(input, "GIF " + label + " 시간 직접 입력");
        row.Children.Add(input);
        now.Click += async (_, _) =>
        {
            try { if (_source != null) input.Text = GifOptions.Time(await _source.PositionAsync()); }
            catch (Exception ex) { Feedback.Show(ExportState.Failed, "재생 위치를 읽지 못했습니다", ex.Message); UpdateResultActions(); }
        };
        return row;
    }
    private void SyncTypedRange()
    {
        if (_syncingRange) return;
        try { Range.SetRange(GifOptions.Parse(StartTime.Text), GifOptions.Parse(EndTime.Text)); }
        catch (FormatException) { }
        RefreshEstimate();
    }
    private async Task InitializeAsync()
    {
        try
        {
            _fields.IsEnabled = false; Feedback.Show(ExportState.Preparing, "GIF 준비 중", "영상을 확인하고 있습니다.");
            _source = await _load(_cancelSource.Token);
            var position = Math.Clamp(await _source.PositionAsync(), 0, Math.Max(0, _source.Duration - .2));
            Range.Initialize(_source.Duration, position, Math.Min(_source.Duration, position + 5));
            StartTime.Text = GifOptions.Time(position); EndTime.Text = GifOptions.Time(Math.Min(_source.Duration, position + 5));
            _title.Text = _source.Title;
            _tools = await CaptureTools.EnsureAsync(new Progress<string>(value => { if (!_closed) Feedback.UpdateProgress(value); }), _cancelSource.Token);
            Feedback.Show(ExportState.Ready, "GIF 저장 준비", "구간과 배속을 정한 뒤 GIF 만들기를 누르세요.", "사진 → 신플레이어 GIF");
        }
        catch (OperationCanceledException) { Feedback.Show(ExportState.Cancelled, "준비 취소", "영상 준비를 취소했습니다."); }
        catch (Exception ex) { Feedback.Show(ExportState.Failed, "영상을 준비하지 못했습니다", ex.Message); }
        finally { Finish(); }
    }
    internal Task ExportAsync(string? pictures = null)
    {
        if (_busy) return _operation;
        return _operation = ExportCoreAsync(pictures);
    }
    private async Task ExportCoreAsync(string? pictures)
    {
        if (_busy || _source == null || _tools == null) return;
        try
        {
            var options = ReadOptions();
            options.Validate(_source.Duration);
            _cancelSource.Dispose(); _cancelSource = new(); _busy = true;
            _fields.IsEnabled = _start.IsEnabled = OpenResult.IsEnabled = OpenFolder.IsEnabled = false;
            _cancel.Content = "취소"; SavedPath = null; _savedOptions = null;
            Feedback.Show(ExportState.Working, "GIF 저장 중…", "완료되면 형광색으로 알려드립니다. 잠시만 기다려 주세요."); UpdateResultActions();
            SavedPath = await _source.ExportAsync(_tools, options, pictures ?? SubtitleCapture.PicturesDirectory,
                new Progress<string>(value => { if (_busy && !_closed) Feedback.UpdateProgress(value); }), _cancelSource.Token);
            long bytes = new FileInfo(SavedPath).Length;
            string size = bytes < 1048576 ? $"{Math.Max(1, bytes / 1024.0):0.#} KB" : $"{bytes / 1048576.0:0.##} MB";
            _savedOptions = options;
            Feedback.Complete($"원본 {GifOptions.DurationText(options.Length)} → GIF {GifOptions.DurationText(options.OutputLength)} · {options.Speed:0.###}× · {size}", Path.GetFileName(SavedPath), SavedPath);
        }
        catch (OperationCanceledException) { Feedback.Show(ExportState.Cancelled, "GIF 저장 취소", "미완성 GIF는 저장하지 않았습니다. 구간을 확인하고 다시 만드세요."); }
        catch (Exception ex) { Feedback.Show(ExportState.Failed, "GIF 저장 실패", ex.Message); App.Log("GIF: " + ex.Message); }
        finally { Finish(); }
    }
    internal void Cancel() { if (_busy && !_closed) { _cancelSource.Cancel(); Feedback.Show(ExportState.Cancelling, "취소 중…", "미완성 파일을 정리하고 있습니다."); _cancel.IsEnabled = false; } }
    private GifOptions ReadOptions() => new(GifOptions.Parse(StartTime.Text), GifOptions.Parse(EndTime.Text), (int)_width.SelectedItem, (int)_fps.SelectedItem, GifOptions.ParseSpeed(SpeedInput.Text));
    private void RefreshEstimate()
    {
        try
        {
            var options = ReadOptions();
            options.Validate(_source?.Duration ?? Math.Max(options.End, .2));
            if (!_busy && SavedPath != null && options != _savedOptions) Feedback.MarkEdited();
            Estimate.Text = $"원본 {GifOptions.DurationText(options.Length)} ÷ {options.Speed:0.###}× → GIF {GifOptions.DurationText(options.OutputLength)}";
            _start.IsEnabled = !_busy && _source != null && _tools != null;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            Estimate.Text = ex.Message;
            _start.IsEnabled = false;
            if (!_busy && SavedPath != null) Feedback.MarkEdited();
        }
        UpdateResultActions();
    }
    private void UpdateResultActions()
    {
        bool saved = SavedPath != null && !_busy;
        OpenResult.IsEnabled = OpenFolder.IsEnabled = saved;
        OpenResult.Content = saved ? Feedback.State == ExportState.Succeeded ? "완성된 GIF 열기" : "이전 GIF 열기" : "GIF 열기";
        _start.Content = _busy && _source != null && _tools != null ? "저장 중…" : SavedPath != null ? "다시 만들기" : "GIF 만들기";
        Feedback.StyleOpenButton(OpenResult);
    }
    internal async Task StopAsync() { Cancel(); await Initialization; await _operation; }
    private void Finish()
    {
        _busy = false; _fields.IsEnabled = true;
        RefreshEstimate(); _cancel.Content = "닫기"; _cancel.IsEnabled = true;
        if (_closeWhenStopped && !_closed) Close();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_busy) return;
        e.Cancel = true; _closeWhenStopped = true; Cancel();
    }
    private void Open(bool folder)
    {
        try { if (SavedPath != null) Process.Start(new ProcessStartInfo(folder ? Path.GetDirectoryName(SavedPath)! : SavedPath) { UseShellExecute = true }); }
        catch (Exception ex) { Feedback.Show(ExportState.Failed, "파일을 열지 못했습니다", "저장된 파일은 유지됩니다. " + ex.Message, Path.GetFileName(SavedPath), SavedPath); UpdateResultActions(); }
    }
}
