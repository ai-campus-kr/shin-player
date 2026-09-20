using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShinPlayer;

internal sealed class SubtitleCaptureWindow : Window
{
    private readonly string _path;
    private readonly double _delay;
    private readonly ComboBox _tracks = new() { MinHeight = 34, DisplayMemberPath = nameof(SubtitleTrack.Label), Foreground = Brushes.Black };
    private readonly CheckBox _include = new() { Content = "사진에 자막 포함", IsChecked = true, Foreground = Brushes.White, Margin = new(0, 14, 0, 0) };
    private readonly TextBlock _status = new() { Text = "내장 자막을 확인하는 중…", TextWrapping = TextWrapping.Wrap, Margin = new(0, 16, 0, 12) };
    private readonly TextBlock _folder = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.LightSteelBlue, Margin = new(0, 10, 0, 0) };
    private readonly ProgressBar _progress = new() { Height = 7, IsIndeterminate = true };
    private readonly Button _start = new() { Content = "전체 캡처", IsEnabled = false, Margin = new(0, 0, 8, 0) };
    private readonly Button _cancel = new() { Content = "취소", MinWidth = 70 };
    private readonly Button _open = new() { Content = "저장 폴더 열기", IsEnabled = false, Margin = new(0, 0, 8, 0) };
    private CancellationTokenSource _cancellation = new();
    private SubtitleCapture? _capture;
    private SubtitleVideo? _video;
    private bool _busy = true;
    private bool _closeWhenStopped;
    private string? _output;
    private int _saved;
    internal Task Initialization { get; private set; } = Task.CompletedTask;

    internal SubtitleCaptureWindow(string path, double delay)
    {
        _path = path;
        _delay = delay;
        Style = (Style)FindResource(typeof(Window));
        Title = "자막별 일괄 캡처 · 신플레이어";
        Width = 650; Height = 480; MinWidth = 540; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new Grid { Margin = new(24) };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "자막 한 구간마다 사진 한 장", FontSize = 22, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = Path.GetFileName(path), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = path, Margin = new(0, 8, 0, 20), Foreground = Brushes.LightSteelBlue });
        panel.Children.Add(heading);
        var body = new StackPanel();
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); panel.Children.Add(scroll);
        body.Children.Add(new TextBlock { Text = "캡처 기준 내장 자막", Margin = new(0, 0, 0, 8) });
        body.Children.Add(_tracks); body.Children.Add(_include);
        body.Children.Add(new TextBlock { Text = "각 구간의 중간 시점을 PNG로 저장합니다. 재생 위치는 바뀌지 않습니다.\nGPT/API를 사용하지 않습니다. 외부·이미지 자막은 지원하지 않습니다.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.LightSteelBlue, Margin = new(0, 14, 0, 0) });
        body.Children.Add(_status); body.Children.Add(_progress); body.Children.Add(_folder);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 18, 0, 0) };
        _start.Style = (Style)FindResource("Primary");
        buttons.Children.Add(_open); buttons.Children.Add(_start); buttons.Children.Add(_cancel);
        Grid.SetRow(buttons, 2); panel.Children.Add(buttons); Content = panel;
        _folder.Text = "저장 위치: " + SubtitleCapture.PicturesDirectory + "\\날짜_영상이름_사진";
        Loaded += (_, _) => Initialization = InitializeAsync();
        _start.Click += async (_, _) => await StartCaptureAsync();
        _cancel.Click += (_, _) => { if (_busy) Cancel(); else Close(); };
        _open.Click += (_, _) => OpenOutput();
        Closing += OnClosing;
        Closed += (_, _) => _cancellation.Dispose();
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (CaptureTools.FindInstalled() == null) _status.Text = "첫 사용에 필요한 캡처 도구를 다운로드합니다. 영상은 전송하지 않습니다.";
            var tools = await CaptureTools.EnsureAsync(new Progress<string>(message => _status.Text = message), _cancellation.Token);
            _capture = new(tools);
            _video = await _capture.ProbeAsync(_path, _cancellation.Token);
            _tracks.ItemsSource = _video.Tracks;
            _tracks.SelectedItem = _video.Tracks.FirstOrDefault(x => x.Language is "kor" or "ko") ?? _video.Tracks.FirstOrDefault(x => x.Default) ?? _video.Tracks.FirstOrDefault();
            _status.Text = _video.VideoIndex < 0 ? "캡처할 영상 트랙이 없습니다." : _video.Tracks.Count == 0 ? "내장 텍스트 자막이 없어 일괄 캡처할 수 없습니다." : "전체 캡처를 누르면 자막 구간 수만큼 저장합니다.";
        }
        catch (OperationCanceledException) { _status.Text = "준비를 취소했습니다."; }
        catch (Exception ex) { _status.Text = ex.Message; App.Log(ex.ToString()); }
        finally { FinishBusy(); }
    }

    internal async Task StartCaptureAsync(string? picturesRoot = null)
    {
        if (_busy || _capture == null || _video == null || _tracks.SelectedItem is not SubtitleTrack track) return;
        _cancellation.Dispose(); _cancellation = new();
        _busy = true; _saved = 0; _output = null;
        _start.IsEnabled = _tracks.IsEnabled = _include.IsEnabled = _open.IsEnabled = false;
        _cancel.Content = "취소"; _cancel.IsEnabled = true; _progress.IsIndeterminate = true;
        var progress = new Progress<CaptureProgress>(value =>
        {
            if (!_busy) return;
            _saved = value.Completed; _status.Text = value.Message;
            _progress.IsIndeterminate = value.Total == 0;
            _progress.Maximum = Math.Max(1, value.Total); _progress.Value = value.Completed;
            if (value.Directory != null) { _output = value.Directory; _folder.Text = "저장 위치: " + _output; }
        });
        try
        {
            var result = await _capture.ExportAsync(_video, track, picturesRoot ?? SubtitleCapture.PicturesDirectory, _include.IsChecked == true, _delay, progress, _cancellation.Token);
            _output = result.Directory; _saved = result.Count; _folder.Text = "저장 위치: " + _output;
            _status.Text = $"완료 · {_saved:N0}장을 저장했습니다.";
            _progress.Value = _progress.Maximum;
        }
        catch (OperationCanceledException) { _status.Text = $"취소됨 · 저장된 {_saved:N0}장은 폴더에 남아 있습니다."; }
        catch (Exception ex) { _status.Text = $"{ex.Message}\n저장된 {_saved:N0}장은 보존됩니다."; App.Log(ex.ToString()); }
        finally { FinishBusy(); }
    }
    internal void Cancel()
    {
        if (!_busy) return;
        _status.Text = "취소하는 중…"; _cancel.IsEnabled = false; _cancellation.Cancel();
    }
    private void FinishBusy()
    {
        _busy = false; _progress.IsIndeterminate = false;
        _cancel.Content = "닫기"; _cancel.IsEnabled = true;
        _tracks.IsEnabled = _include.IsEnabled = true;
        _start.IsEnabled = _video is { VideoIndex: >= 0 } && _video.Tracks.Count > 0;
        _open.IsEnabled = _output != null && Directory.Exists(_output);
        if (_closeWhenStopped) Close();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_busy) return;
        e.Cancel = true; _closeWhenStopped = true; Cancel();
    }
    private void OpenOutput()
    {
        try { if (_output != null && Directory.Exists(_output)) Process.Start(new ProcessStartInfo(_output) { UseShellExecute = true }); }
        catch (Exception ex) { _status.Text = "폴더를 열지 못했습니다: " + ex.Message; }
    }
}
