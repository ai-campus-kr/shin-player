using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ShinPlayer;

internal sealed class ShortsWindow : Window
{
    private readonly Func<CancellationToken, Task<SubtitleVideo>> _load;
    private readonly double _initialPosition;
    internal readonly ShortsPreview Preview = new();
    internal readonly GifRangeSelector Range = new("쇼츠") { MaximumLength = 180 };
    internal readonly TextBox StartTime = new(), EndTime = new();
    internal readonly TextBox Caption = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 74, MaxLength = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    internal readonly Slider PreviewTime = new() { Minimum = 0, Maximum = 1, Focusable = true };
    internal readonly Slider FontSizeInput = new() { Minimum = 32, Maximum = 100, Value = 64, TickFrequency = 2, IsSnapToTickEnabled = true, Focusable = true };
    internal readonly Slider ZoomInput = new() { Minimum = 1, Maximum = 4, Value = 1, TickFrequency = .05, IsSnapToTickEnabled = true, Focusable = true };
    internal readonly ComboBox FontInput = new();
    internal readonly ShortsColorPicker ColorInput = new();
    internal readonly Button CropButton = new() { Content = "영역 직접 선택…", MinHeight = 34 };
    internal readonly ComboBox FitInput = new() { ItemsSource = new[] { "세로로 꽉 채우기", "원본 전체 보이기" }, SelectedIndex = 0 };
    internal readonly ComboBox WidthInput = new() { ItemsSource = new[] { "1080 × 1920", "720 × 1280" }, SelectedIndex = 0, MinWidth = 145 };
    internal readonly CheckBox AudioInput = new() { Content = "원본 소리 포함", IsChecked = true };
    internal readonly CheckBox TextBoxInput = new() { Content = "글자 뒤에 어두운 배경", IsChecked = true };
    internal readonly Button SaveButton = new() { Content = "쇼츠 저장", IsEnabled = false };
    private readonly StackPanel _fields = new();
    private readonly TextBlock _title = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 5, 0, 0) };
    private readonly TextBlock _estimate = new() { FontSize = 12, Margin = new(0, 6, 0, 12), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _validation = new() { FontSize = 12, Foreground = UiDesigns.Brush("#EF626F"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new(0, 10, 0, 0) };
    private readonly TextBlock _frameTime = new() { FontFamily = new("Consolas"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _frameStatus = new() { Text = "장면을 불러오는 중…", FontSize = 12, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _compositionHelp = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _cropStatus = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new(0, 5, 0, 8) };
    private readonly StackPanel _frameFields = new(), _textFields = new() { Visibility = Visibility.Collapsed };
    internal readonly Button FrameTab = new() { Content = "구간 · 화면", MinWidth = 110 };
    internal readonly Button TextTab = new() { Content = "문구 · 스타일", MinWidth = 110, Margin = new(6, 0, 0, 0) };
    internal readonly ExportFeedback Feedback = new("쇼츠");
    private readonly Button _cancel = new() { Content = "닫기", Margin = new(8, 0, 0, 0) };
    internal readonly Button OpenResult = new() { Content = "영상 열기", IsEnabled = false };
    internal readonly Button OpenFolder = new() { Content = "저장 폴더", IsEnabled = false };
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _workCancel, _previewCancel;
    private readonly List<Task> _previewJobs = new();
    private CaptureTools? _tools;
    private SubtitleVideo? _video;
    private Task _operation = Task.CompletedTask;
    private bool _busy = true, _initializing = true, _closed, _closeRequested, _syncing;
    private double _panX = .5, _panY = .5, _textX = .5, _textY = .18, _lastStart, _lastEnd;
    private ShortsCrop _crop = ShortsCrop.Full;
    private long _previewGeneration;
    private ShortsOptions? _savedOptions;
    internal string? SavedPath { get; private set; }
    internal Task Initialization { get; private set; } = Task.CompletedTask;
    internal bool IsBusy => _busy;

    internal ShortsWindow(Func<CancellationToken, Task<SubtitleVideo>> load, double initialPosition)
    {
        _load = load; _initialPosition = initialPosition;
        Style = (Style)FindResource(typeof(Window)); Title = "쇼츠 만들기 · 신플레이어";
        Width = Math.Min(1100, SystemParameters.WorkArea.Width - 30); Height = Math.Min(870, SystemParameters.WorkArea.Height - 30);
        MinWidth = 820; MinHeight = 580; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new(22, 18, 22, 18) };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new DockPanel { Margin = new(0, 0, 0, 16) };
        var badge = new Border { CornerRadius = new(5), Padding = new(12, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "9:16  /  MP4", FontFamily = new("Consolas"), FontSize = 12 } };
        badge.SetResourceReference(Border.BackgroundProperty, "Raised"); DockPanel.SetDock(badge, Dock.Right); heading.Children.Add(badge);
        var names = new StackPanel(); names.Children.Add(new TextBlock { Text = "이 장면을 쇼츠로", FontSize = 25, FontWeight = FontWeights.SemiBold });
        _title.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); names.Children.Add(_title); heading.Children.Add(names); root.Children.Add(heading);

        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(.9, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new() { Width = new GridLength(24) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(1.12, GridUnitType.Star) });
        Grid.SetRow(body, 1); root.Children.Add(body);
        var previewDeck = new Grid(); previewDeck.RowDefinitions.Add(new()); previewDeck.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var previewBorder = new Border { Background = UiDesigns.Brush("#08090B"), BorderBrush = UiDesigns.Brush("#323237"), BorderThickness = new(1), CornerRadius = new(8), Padding = new(12) };
        var viewbox = new Viewbox { Child = Preview, Stretch = Stretch.Uniform }; previewBorder.Child = viewbox; previewDeck.Children.Add(previewBorder);
        var scrub = new StackPanel { Margin = new(0, 8, 0, 0) };
        var timeHeading = new DockPanel(); DockPanel.SetDock(_frameTime, Dock.Right); timeHeading.Children.Add(_frameTime);
        timeHeading.Children.Add(new TextBlock { Text = "장면 미리보기", FontSize = 12 }); scrub.Children.Add(timeHeading); scrub.Children.Add(PreviewTime); scrub.Children.Add(_frameStatus);
        _frameStatus.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Grid.SetRow(scrub, 1); previewDeck.Children.Add(scrub); body.Children.Add(previewDeck);

        var scroll = new ScrollViewer { Content = _fields, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 8, 0) };
        Grid.SetColumn(scroll, 2); body.Children.Add(scroll);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 16) }; tabs.Children.Add(FrameTab); tabs.Children.Add(TextTab); _fields.Children.Add(tabs);
        _fields.Children.Add(_frameFields); _fields.Children.Add(_textFields); _fields.Children.Add(_validation);
        FrameTab.Click += (_, _) => ShowTextTools(false); TextTab.Click += (_, _) => ShowTextTools(true); ShowTextTools(false);
        var rangeHeading = new DockPanel(); var rangeButtons = new StackPanel { Orientation = Orientation.Horizontal };
        rangeButtons.Children.Add(SmallButton("선택 확대", () => Range.ZoomToSelection())); rangeButtons.Children.Add(SmallButton("전체", () => Range.ShowAll()));
        DockPanel.SetDock(rangeButtons, Dock.Right); rangeHeading.Children.Add(rangeButtons); rangeHeading.Children.Add(Label("구간 선택")); _frameFields.Children.Add(rangeHeading); _frameFields.Children.Add(Range);
        var times = new Grid(); times.ColumnDefinitions.Add(new()); times.ColumnDefinitions.Add(new() { Width = new GridLength(12) }); times.ColumnDefinitions.Add(new());
        times.Children.Add(TimeField("시작", StartTime)); var end = TimeField("끝", EndTime); Grid.SetColumn(end, 2); times.Children.Add(end); _frameFields.Children.Add(times);
        _estimate.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _frameFields.Children.Add(_estimate);
        _frameFields.Children.Add(Label("영상에서 담을 영역"));
        CropButton.Style = (Style)FindResource("Primary");
        var cropRow = new DockPanel(); var resetCrop = SmallButton("원본으로 초기화", () => SetCrop(ShortsCrop.Full));
        DockPanel.SetDock(resetCrop, Dock.Right); cropRow.Children.Add(resetCrop); cropRow.Children.Add(CropButton); _frameFields.Children.Add(cropRow);
        _cropStatus.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _frameFields.Children.Add(_cropStatus);
        CropButton.Click += (_, _) =>
        {
            if (Preview.SourceFrame is not { } frame) return;
            var picker = new ShortsCropWindow(frame, _crop) { Owner = this };
            if (picker.ShowDialog() == true) SetCrop(picker.Selection);
        };
        _frameFields.Children.Add(Label("세로 화면에 배치")); _frameFields.Children.Add(FitInput);
        var cropHelp = new DockPanel { Margin = new(0, 4, 0, 12) }; var center = SmallButton("가운데 맞춤", () => { _panX = _panY = .5; Refresh(); });
        DockPanel.SetDock(center, Dock.Right); cropHelp.Children.Add(center); cropHelp.Children.Add(_compositionHelp); _frameFields.Children.Add(cropHelp);
        var zoomRow = new DockPanel(); var zoomValue = new TextBlock { FontFamily = new("Consolas"), FontSize = 13, Width = 52, TextAlignment = TextAlignment.Right };
        zoomValue.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Value") { Source = ZoomInput, StringFormat = "P0" });
        DockPanel.SetDock(zoomValue, Dock.Right); zoomRow.Children.Add(zoomValue); zoomRow.Children.Add(Label("영상 확대")); _frameFields.Children.Add(zoomRow); _frameFields.Children.Add(ZoomInput);
        var moveVideo = new CheckBox { Content = "문구 위에서도 영상만 움직이기", Margin = new(0, 8, 0, 14) };
        moveVideo.Checked += (_, _) => Preview.MoveVideoOnly = true; moveVideo.Unchecked += (_, _) => Preview.MoveVideoOnly = false; _frameFields.Children.Add(moveVideo);
        _textFields.Children.Add(Label("올릴 문구")); _textFields.Children.Add(Caption);
        _textFields.Children.Add(new TextBlock { Text = "상업적 이용 가능한 무료 글꼴 6종 · 별도 설치 불필요", FontSize = 11, Margin = new(0, 12, 0, 6), TextWrapping = TextWrapping.Wrap });
        foreach (var font in ShortsFonts.All) FontInput.Items.Add(new ComboBoxItem { Content = font.Name, Tag = font.Id, FontFamily = font.Family, FontWeight = font.Weight, FontSize = 16 });
        FontInput.SelectedIndex = 0; _textFields.Children.Add(FontInput);
        var typeRow = new Grid { Margin = new(0, 5, 0, 3) }; typeRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); typeRow.ColumnDefinitions.Add(new()); typeRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        typeRow.Children.Add(new TextBlock { Text = "크기", FontSize = 12, Margin = new(0, 0, 10, 0) }); Grid.SetColumn(FontSizeInput, 1); typeRow.Children.Add(FontSizeInput);
        var sizeText = new TextBlock { Width = 33, TextAlignment = TextAlignment.Right, FontFamily = new("Consolas") }; sizeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Value") { Source = FontSizeInput, StringFormat = "0" });
        Grid.SetColumn(sizeText, 2); typeRow.Children.Add(sizeText); _textFields.Children.Add(typeRow);
        _textFields.Children.Add(Label("글자색")); _textFields.Children.Add(ColorInput);
        var presets = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (name, y) in new[] { ("위", .18), ("가운데", .5), ("아래", .78) })
            presets.Children.Add(SmallButton(name, () => { _textX = .5; _textY = y; Refresh(); }));
        _textFields.Children.Add(presets); _textFields.Children.Add(TextBoxInput);
        var textHelp = new TextBlock { Text = "Enter로 줄바꿈 · 최대 4줄 · 미리보기에서 문구를 드래그", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new(0, 5, 0, 12) };
        textHelp.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _textFields.Children.Add(textHelp);
        var outputRow = new DockPanel { Margin = new(0, 16, 0, 8) }; DockPanel.SetDock(WidthInput, Dock.Right); outputRow.Children.Add(WidthInput); outputRow.Children.Add(Label("저장 크기 · 30fps")); _fields.Children.Add(outputRow); _fields.Children.Add(AudioInput);

        var footer = new StackPanel { Margin = new(0, 12, 0, 0) }; footer.Children.Add(Feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 10, 0, 0) };
        SaveButton.Style = (Style)FindResource("Primary"); buttons.Children.Add(OpenFolder); buttons.Children.Add(OpenResult); buttons.Children.Add(SaveButton); buttons.Children.Add(_cancel);
        footer.Children.Add(buttons); Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;

        AutomationProperties.SetName(Caption, "쇼츠에 올릴 문구"); AutomationProperties.SetName(PreviewTime, "미리보기 위치"); AutomationProperties.SetName(FontSizeInput, "문구 글자 크기");
        AutomationProperties.SetName(FitInput, "쇼츠 화면 맞춤"); AutomationProperties.SetName(WidthInput, "쇼츠 저장 크기");
        AutomationProperties.SetName(ZoomInput, "쇼츠 영상 확대"); AutomationProperties.SetName(FontInput, "쇼츠 문구 글꼴");
        Loaded += (_, _) => Initialization = InitializeAsync(); Closing += OnClosing;
        Closed += (_, _) => { _closed = true; _previewTimer.Stop(); _lifetime.Cancel(); _lifetime.Dispose(); _workCancel?.Dispose(); };
        SaveButton.Click += async (_, _) => await ExportAsync(); _cancel.Click += (_, _) => { if (_busy) Cancel(); else Close(); };
        OpenResult.Click += (_, _) => Open(false); OpenFolder.Click += (_, _) => Open(true);
        Range.RangeChanged += () => SyncRange(true);
        StartTime.TextChanged += (_, _) => TypedRange(); EndTime.TextChanged += (_, _) => TypedRange();
        Caption.TextChanged += (_, _) => Refresh(); FontSizeInput.ValueChanged += (_, _) => Refresh(); FitInput.SelectionChanged += (_, _) => Refresh(); WidthInput.SelectionChanged += (_, _) => Refresh();
        FontInput.SelectionChanged += (_, _) => Refresh(); ColorInput.Changed += Refresh; ZoomInput.ValueChanged += (_, _) => { if (!_syncing) Refresh(); };
        TextBoxInput.Checked += (_, _) => Refresh(); TextBoxInput.Unchecked += (_, _) => Refresh();
        AudioInput.Checked += (_, _) => Refresh(); AudioInput.Unchecked += (_, _) => Refresh();
        Preview.Edited += value =>
        {
            _panX = value.PanX; _panY = value.PanY; _textX = value.TextX; _textY = value.TextY;
            _syncing = true; try { ZoomInput.Value = value.Zoom; } finally { _syncing = false; } MarkEdited(value);
        };
        PreviewTime.ValueChanged += (_, _) => { _frameTime.Text = GifOptions.Time(PreviewTime.Value); RequestPreview(); };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); StartPreview(); };
    }

    internal void ShowTextTools(bool text)
    {
        _frameFields.Visibility = text ? Visibility.Collapsed : Visibility.Visible; _textFields.Visibility = text ? Visibility.Visible : Visibility.Collapsed;
        FrameTab.Style = (Style)FindResource(text ? typeof(Button) : "Primary"); TextTab.Style = (Style)FindResource(text ? "Primary" : typeof(Button));
        AutomationProperties.SetHelpText(FrameTab, text ? "영상 구간과 화면 영역 조정" : "현재 선택됨");
        AutomationProperties.SetHelpText(TextTab, text ? "현재 선택됨" : "문구, 글꼴과 색상 조정");
    }
    internal void SetCrop(ShortsCrop crop)
    {
        crop.Validate(); _crop = crop; _panX = _panY = .5; ZoomInput.Value = 1;
        FitInput.SelectedIndex = crop == ShortsCrop.Full ? 0 : 1; Refresh();
    }
    private static TextBlock Label(string text) => new() { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 6) };
    private static Button SmallButton(string label, Action action)
    {
        var button = new Button { Content = label, FontSize = 11, Padding = new(8, 4, 8, 4), MinHeight = 28 };
        button.Click += (_, _) => action(); return button;
    }
    private FrameworkElement TimeField(string name, TextBox input)
    {
        var row = new DockPanel(); var label = new TextBlock { Text = name, FontSize = 12, Margin = new(0, 0, 8, 0) };
        DockPanel.SetDock(label, Dock.Left); row.Children.Add(label); input.FontFamily = new("Consolas"); input.FontSize = 12; input.Padding = new(7, 7, 7, 7);
        AutomationProperties.SetName(input, "쇼츠 " + name + " 시간"); row.Children.Add(input); return row;
    }
    private async Task InitializeAsync()
    {
        try
        {
            _fields.IsEnabled = false; Feedback.Show(ExportState.Preparing, "쇼츠 준비 중", "영상을 확인하고 있습니다.");
            _workCancel = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _tools = await CaptureTools.EnsureAsync(new Progress<string>(s => { if (!_closed) Feedback.UpdateProgress(s); }), _workCancel.Token);
            _video = await _load(_workCancel.Token);
            if (_video.VideoIndex < 0 || _video.Duration < .2) throw new InvalidOperationException("쇼츠로 만들 수 있는 영상 트랙이 없습니다.");
            _title.Text = Path.GetFileName(_video.Path);
            double start = Math.Clamp(_initialPosition, 0, Math.Max(0, _video.Duration - .2));
            Range.Initialize(_video.Duration, start, Math.Min(_video.Duration, start + 30));
            _lastStart = Range.Start; _lastEnd = Range.End; SyncRange(true);
            await PreviewAsync();
            Feedback.Show(ExportState.Ready, "쇼츠 저장 준비", "구간과 문구를 정한 뒤 쇼츠 저장을 누르세요.", "동영상 → 신플레이어 쇼츠");
        }
        catch (OperationCanceledException) { Feedback.Show(ExportState.Cancelled, "준비 취소", "영상 준비를 취소했습니다."); }
        catch (Exception ex) { Feedback.Show(ExportState.Failed, "영상을 준비하지 못했습니다", ex.Message); }
        finally { _initializing = false; Finish(); }
    }
    internal ShortsOptions ReadOptions() => new(GifOptions.Parse(StartTime.Text), GifOptions.Parse(EndTime.Text), WidthInput.SelectedIndex == 0 ? 1080 : 720,
        FitInput.SelectedIndex == 0 ? ShortsFit.Fill : ShortsFit.Contain, _panX, _panY, AudioInput.IsChecked == true, Caption.Text,
        FontSizeInput.Value, ColorInput.Value, TextBoxInput.IsChecked == true, _textX, _textY,
        (FontInput.SelectedItem as ComboBoxItem)?.Tag as string ?? ShortsFonts.DefaultId, ZoomInput.Value, _crop);
    private void Refresh()
    {
        try
        {
            var options = ReadOptions(); options.Validate(_video?.Duration ?? Math.Max(.2, options.End)); Preview.Apply(options); MarkEdited(options);
            _compositionHelp.Text = "미리보기에서 드래그로 이동 · 휠로 확대";
            _cropStatus.Text = _crop == ShortsCrop.Full ? "원본 전체 사용 · 원하는 부분만 담으려면 영역 직접 선택" : $"직접 고른 영역 {_crop.Width:P0} × {_crop.Height:P0} 사용";
            CropButton.IsEnabled = Preview.HasFrame && !_busy;
            if (_previewCancel == null && Preview.HasFrame) _frameStatus.Text = PreviewHelp;
            _estimate.Text = $"선택 {GifOptions.DurationText(options.Length)} / 최대 3분";
            _validation.Visibility = Visibility.Collapsed;
            SaveButton.IsEnabled = !_busy && _video != null && _tools != null;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        { _validation.Text = ex.Message; _validation.Visibility = Visibility.Visible; SaveButton.IsEnabled = false; MarkEdited(null); }
    }
    private void MarkEdited(ShortsOptions? options)
    {
        if (!_busy && SavedPath != null && options != _savedOptions) Feedback.MarkEdited();
        UpdateResultActions();
    }
    private void UpdateResultActions()
    {
        bool saved = SavedPath != null && !_busy;
        OpenResult.IsEnabled = OpenFolder.IsEnabled = saved;
        OpenResult.Content = saved ? Feedback.State == ExportState.Succeeded ? "완성된 영상 열기" : "이전 영상 열기" : "영상 열기";
        SaveButton.Content = _busy && !_initializing ? "저장 중…" : SavedPath != null ? "다시 저장" : "쇼츠 저장";
        Feedback.StyleOpenButton(OpenResult);
    }
    private void TypedRange()
    {
        if (_syncing) return;
        try
        {
            double start = GifOptions.Parse(StartTime.Text), end = GifOptions.Parse(EndTime.Text);
            if (end - start <= 180 + .000001 && Range.SetRange(start, end)) SyncRange(false);
        }
        catch (FormatException) { }
        Refresh();
    }
    private void SyncRange(bool writeTimes)
    {
        if (writeTimes)
        {
            _syncing = true;
            try { StartTime.Text = GifOptions.Time(Range.Start); EndTime.Text = GifOptions.Time(Range.End); }
            finally { _syncing = false; }
        }
        double position = Math.Abs(Range.Start - _lastStart) < .0001 && Math.Abs(Range.End - _lastEnd) > .0001 ? Math.Max(Range.Start, Range.End - 1d / 30) : Range.Start;
        _lastStart = Range.Start; _lastEnd = Range.End;
        PreviewTime.Maximum = Math.Max(Range.Start, Range.End - 1d / 30); PreviewTime.Minimum = Range.Start;
        PreviewTime.Value = Math.Clamp(position, PreviewTime.Minimum, PreviewTime.Maximum); Refresh();
    }
    private void RequestPreview()
    {
        if (_initializing || _busy || _closed) return;
        _previewCancel?.Cancel(); _previewGeneration++; _previewTimer.Stop(); _previewTimer.Start();
    }
    private void StartPreview()
    {
        _previewJobs.RemoveAll(t => t.IsCompleted);
        _previewJobs.Add(PreviewAsync());
    }
    internal async Task RefreshPreviewAsync()
    {
        _previewTimer.Stop(); _previewCancel?.Cancel();
        var job = PreviewAsync(); _previewJobs.Add(job); await job;
    }
    private async Task PreviewAsync()
    {
        if (_tools == null || _video == null || _closed) return;
        _previewCancel?.Cancel();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, _initializing && _workCancel != null ? _workCancel.Token : CancellationToken.None);
        _previewCancel = cancel; long generation = ++_previewGeneration;
        try
        {
            _frameStatus.Text = "장면을 불러오는 중…";
            var frame = await ShortsExport.FrameAsync(_tools, _video, PreviewTime.Value, cancel.Token);
            if (generation != _previewGeneration || _closed) return;
            Preview.SetFrame(frame); _frameStatus.Text = PreviewHelp; CropButton.IsEnabled = !_busy || _initializing;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (generation == _previewGeneration && !_closed) _frameStatus.Text = ex.Message; }
        finally { if (ReferenceEquals(_previewCancel, cancel)) _previewCancel = null; }
    }
    internal Task ExportAsync(string? videosRoot = null)
    {
        if (_busy) return _operation;
        return _operation = ExportCoreAsync(videosRoot);
    }
    private async Task ExportCoreAsync(string? videosRoot)
    {
        if (_video == null || _tools == null) return;
        try
        {
            var options = ReadOptions(); options.Validate(_video.Duration); ShortsComposition.RenderLabel(options);
            _workCancel?.Dispose(); _workCancel = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _busy = true; _fields.IsEnabled = Preview.IsEnabled = PreviewTime.IsEnabled = SaveButton.IsEnabled = OpenResult.IsEnabled = OpenFolder.IsEnabled = false;
            _cancel.Content = "취소"; SavedPath = null; _savedOptions = null;
            Feedback.Show(ExportState.Working, "쇼츠 저장 중…", "이 창을 닫지 않아도 다른 작업을 할 수 있습니다. 완료되면 형광색으로 알려드립니다."); UpdateResultActions();
            _previewTimer.Stop(); _previewCancel?.Cancel(); await Task.WhenAll(_previewJobs);
            SavedPath = await ShortsExport.ExportAsync(_tools, _video, options, videosRoot ?? ShortsExport.VideosDirectory,
                new Progress<string>(s => { if (_busy && !_closed) Feedback.UpdateProgress(s); }), _workCancel.Token);
            long bytes = new FileInfo(SavedPath).Length;
            string size = bytes < 1048576 ? $"{Math.Max(1, bytes / 1024d):0.#} KB" : $"{bytes / 1048576d:0.#} MB";
            _savedOptions = options;
            Feedback.Complete($"{options.Width} × {options.Height} · {GifOptions.DurationText(options.Length)} · {size} · 영상 열기로 확인하세요.", Path.GetFileName(SavedPath), SavedPath);
        }
        catch (OperationCanceledException) { Feedback.Show(ExportState.Cancelled, "쇼츠 저장 취소", "미완성 영상은 저장하지 않았습니다. 구간을 확인하고 다시 저장하세요."); }
        catch (Exception ex) { Feedback.Show(ExportState.Failed, "쇼츠 저장 실패", ex.Message); }
        finally { Finish(); }
    }
    private void Finish()
    {
        _busy = false; _fields.IsEnabled = Preview.IsEnabled = PreviewTime.IsEnabled = true;
        _cancel.Content = "닫기"; _cancel.IsEnabled = true; Refresh();
        if (_closeRequested) _ = CloseAfterWorkAsync();
    }
    private const string PreviewHelp = "장면을 보며 영상·문구 드래그 · 휠로 확대";
    internal void Cancel() { if (_closed) return; _workCancel?.Cancel(); _previewCancel?.Cancel(); if (_busy) { Feedback.Show(ExportState.Cancelling, "취소 중…", "미완성 파일을 정리하고 있습니다."); _cancel.IsEnabled = false; } }
    internal async Task StopAsync()
    {
        if (_closed) return;
        _previewTimer.Stop(); Cancel(); await Initialization; await _operation; await Task.WhenAll(_previewJobs);
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closed) return;
        _previewTimer.Stop();
        if (_busy || _previewJobs.Any(t => !t.IsCompleted))
        {
            e.Cancel = true; _closeRequested = true; Cancel();
            if (!_busy) _ = CloseAfterWorkAsync();
        }
    }
    private async Task CloseAfterWorkAsync()
    {
        await Task.WhenAll(_previewJobs);
        if (!_closed && !_busy) Close();
    }
    private void Open(bool folder)
    {
        try { if (SavedPath != null) Process.Start(new ProcessStartInfo(folder ? Path.GetDirectoryName(SavedPath)! : SavedPath) { UseShellExecute = true }); }
        catch (Exception ex) { Feedback.Show(ExportState.Failed, "파일을 열지 못했습니다", "저장된 파일은 유지됩니다. " + ex.Message, Path.GetFileName(SavedPath), SavedPath); UpdateResultActions(); }
    }
}
