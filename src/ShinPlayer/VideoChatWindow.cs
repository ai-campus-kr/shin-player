using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinPlayer;

internal sealed partial class VideoChatPanel : UserControl
{
    private readonly Grid _root = new() { Margin = new(16) };
    private readonly StackPanel _heading = new();
    private readonly TextBlock _title = new() { Text = "영상 보며, 바로 질문", FontSize = 20, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _hint = new() { Text = "“이 내용 설명해줘” · “그 부분으로 이동해줘”", Margin = new(0, 6, 0, 8), TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _footer;
    private readonly VideoChatClient _client;
    private readonly ApiKeyStore _keys;
    private readonly Func<CancellationToken, Task<VideoTranscript>> _load;
    private readonly Func<VideoTranscript, double, Task> _seek;
    private readonly StackPanel _messages = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 60, Margin = new(0, 8, 0, 8) };
    private readonly TextBox _question = new() { MinHeight = 56, MaxHeight = 120, MaxLength = 2000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new(10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button _send = new() { Content = "질문 보내기", IsEnabled = false };
    private readonly Button _cancel = new() { Content = "취소", IsEnabled = false, Visibility = Visibility.Collapsed };
    private readonly TextBlock _keyState = new() { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, FontSize = 12 };
    private ApiSettingsWindow? _apiWindow;
    private bool _connectionVerified;
    private VideoTranscript? _transcript;
    private CancellationTokenSource _work = new();
    private bool _closed, _busy, _transcriptUnavailable;
    private int _sourceRevision;
    private string _previousQuestion = "";
    private string _previousAnswer = "";
    private IReadOnlyList<ChatMatch> _previousMatches = Array.Empty<ChatMatch>();
    internal VideoChatPanel(Func<CancellationToken, Task<VideoTranscript>> load, Func<VideoTranscript, double, Task> seek,
        VideoChatClient? client = null, ApiKeyStore? keys = null)
    {
        _load = load; _seek = seek;
        _client = client ?? new VideoChatClient(); _keys = keys ?? new ApiKeyStore();
        SetResourceReference(ForegroundProperty, "Ink");
        SetResourceReference(BackgroundProperty, "Panel");
        _root.ColumnDefinitions.Add(new()); _root.ColumnDefinitions.Add(new() { Width = new(0) });
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new());
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _heading.Children.Add(_title); _heading.Children.Add(_hint);
        var apiRow = new DockPanel { Margin = new(0, 4, 0, 8) };
        var apiSettings = new Button { Content = "API 설정", Padding = new(9, 5, 9, 5), FontSize = 12 };
        DockPanel.SetDock(apiSettings, Dock.Right); apiRow.Children.Add(apiSettings); apiRow.Children.Add(_keyState);
        _heading.Children.Add(apiRow);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(_cancel);
        _heading.Children.Add(actions); _heading.Children.Add(_status);
        _root.Children.Add(_heading);
        _footer = new StackPanel();
        _question.ToolTip = "질문과 자막 텍스트만 OpenAI로 전송합니다. 영상·음성은 보내지 않으며 API 요금은 본인 계정에 적용됩니다. Enter: 보내기 · Shift+Enter: 줄바꿈";
        var disclosure = new TextBlock { Text = "질문·자막만 AI로 전송 · API 요금 별도", FontSize = 11, Margin = new(0, 0, 0, 6), ToolTip = _question.ToolTip };
        disclosure.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _footer.Children.Add(disclosure);
        _footer.Children.Add(_question);
        _send.Style = (Style)FindResource("Primary"); _send.Margin = new(0, 8, 0, 0); _footer.Children.Add(_send);
        Grid.SetRow(_footer, 2); _root.Children.Add(_footer);
        _scroll = new ScrollViewer { Content = _messages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(_scroll, 1); _root.Children.Add(_scroll); Content = _root;
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        RefreshKeyState(false);
        _status.Text = "영상의 자막을 자동으로 확인합니다.";
        _status.SetBinding(ToolTipProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = _status });
        apiSettings.Click += (_, _) => OpenApiSettings();
        _send.Click += async (_, _) => await AskAsync();
        _cancel.Click += (_, _) => _work.Cancel();
        _question.TextChanged += (_, _) => UpdateSendState();
        _question.PreviewKeyDown += async (_, e) => { if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; await AskAsync(); } };
        SizeChanged += (_, _) => UpdatePanelLayout();
    }

    private void UpdatePanelLayout()
    {
        bool horizontal = ActualWidth >= 640;
        _root.ColumnDefinitions[0].Width = horizontal ? new GridLength(270) : new GridLength(1, GridUnitType.Star);
        _root.ColumnDefinitions[1].Width = horizontal ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _root.RowDefinitions[0].Height = horizontal ? new GridLength(0) : GridLength.Auto;
        Grid.SetRowSpan(_heading, horizontal ? 3 : 1);
        Grid.SetColumn(_scroll, horizontal ? 1 : 0); Grid.SetColumn(_footer, horizontal ? 1 : 0);
        _heading.Margin = horizontal ? new Thickness(0, 0, 18, 0) : new Thickness(0, 0, 0, 8);
        _hint.Visibility = horizontal ? Visibility.Collapsed : Visibility.Visible;
        _title.FontSize = horizontal ? 18 : 20;
    }
    private void RefreshKeyState(bool connected)
    {
        bool saved = _keys.Load().Length > 0;
        _connectionVerified = connected && saved;
        _keyState.Text = _connectionVerified ? "✓ API 연결 확인됨" : saved ? "✓ API 키 저장됨" : _keys.HasSavedKey ? "! 키 확인 필요" : "API 키 미등록";
        _keyState.SetResourceReference(TextBlock.ForegroundProperty, saved ? "SuccessInk" : "WarningInk");
        _keyState.ToolTip = _connectionVerified ? "이 세션에서 API 요청에 성공했습니다." : saved ? "키를 암호화해 저장했습니다. API 설정의 ‘연결 확인’에서 실제 연결을 시험할 수 있습니다." : "API 설정에서 키를 저장해 주세요.";
    }
    private void OpenApiSettings()
    {
        if (_apiWindow != null) { _apiWindow.Activate(); return; }
        _apiWindow = new ApiSettingsWindow(_keys, _client, RefreshKeyState, _connectionVerified) { Owner = Window.GetWindow(this) };
        _apiWindow.Closed += (_, _) => _apiWindow = null;
        _apiWindow.Show();
    }
    internal void FocusQuestion() => _question.Focus();
    internal void Close()
    {
        if (_closed) return;
        _closed = true; _sourceRevision++; _work.Cancel(); _work.Dispose(); _apiWindow?.Close();
    }
    internal void ResetTranscript(string message)
    {
        if (_closed) return;
        _sourceRevision++; _work.Cancel(); _transcript = null; _transcriptUnavailable = false; _previousQuestion = _previousAnswer = "";
        _previousMatches = Array.Empty<ChatMatch>();
        _messages.Children.Clear(); _question.Clear(); Busy(false); _status.Text = message;
    }

    private void Busy(bool busy)
    {
        _busy = busy; UpdateSendState(); _cancel.IsEnabled = busy; _cancel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
    private void UpdateSendState()
    {
        _send.IsEnabled = !_busy && !_closed && !_transcriptUnavailable && !string.IsNullOrWhiteSpace(_question.Text);
        _send.Content = _transcriptUnavailable ? "자막 없음" : _busy && _transcript == null ? "자막 확인 중…" : "질문 보내기";
    }
    private CancellationToken BeginWork()
    {
        _work.Dispose(); _work = new(); Busy(true); return _work.Token;
    }
    internal async Task LoadAsync()
    {
        if (_busy || _closed) return;
        int revision = _sourceRevision;
        var token = BeginWork();
        _transcript = null; _transcriptUnavailable = false; _previousQuestion = _previousAnswer = ""; _messages.Children.Clear();
        _previousMatches = Array.Empty<ChatMatch>();
        _status.Text = "자막을 자동으로 불러오는 중…";
        try
        {
            var transcript = await _load(token);
            token.ThrowIfCancellationRequested();
            if (_closed || revision != _sourceRevision) return;
            _transcript = transcript;
            _status.Text = $"✓ 자막 {transcript.Cues.Count:N0}개 · 질문할 준비가 됐습니다";
            AddMessage("자막 자동 연결", $"{transcript.Label}\n찾고 싶은 내용을 질문하세요.");
        }
        catch (NoTranscriptException) { if (!_closed && revision == _sourceRevision) { _transcriptUnavailable = true; _status.Text = "자막이 없는 영상입니다"; } }
        catch (OperationCanceledException) { if (!_closed && revision == _sourceRevision) _status.Text = "자막 읽기를 취소했습니다."; }
        catch (Exception ex) { if (!_closed && revision == _sourceRevision) { _status.Text = ex.Message; AddMessage("자막을 불러오지 못했습니다", ex.Message); } }
        finally { if (!_closed && revision == _sourceRevision) Busy(false); }
    }
    private async Task AskAsync()
    {
        if (_busy || _closed || _transcriptUnavailable || string.IsNullOrWhiteSpace(_question.Text)) return;
        int revision = _sourceRevision;
        var question = _question.Text.Trim();
        if (_transcript == null)
        {
            if (_keys.Load().Length == 0) { OpenApiSettings(); _status.Text = "API 설정에서 키를 저장한 뒤 질문을 다시 보내세요."; return; }
            await LoadAsync();
            if (_closed || revision != _sourceRevision || _transcript == null) return;
        }
        // A direct follow-up uses the already validated evidence without another API request.
        string compact = string.Concat(question.Where(c => !char.IsWhiteSpace(c))).TrimEnd('.', '!', '?', '。').ToLowerInvariant();
        if (_previousMatches.Count > 0 && new[] { "그부분으로이동해줘", "방금설명한부분으로이동해줘", "그부분틀어줘", "거기로이동해줘", "그부분으로가줘", "gotothatpart", "jumptothatpart", "その部分へ移動して", "跳到那里" }.Contains(compact))
        {
            Busy(true); AddMessage("나", question); _question.Clear();
            try
            {
                await _seek(_transcript, _previousMatches[0].Start);
                if (_closed || revision != _sourceRevision) return;
                string message = $"{MediaFiles.Time(_previousMatches[0].Start)} 구간으로 이동했습니다.";
                AddMessage("신플레이어 AI", message);
                _status.Text = "이전 답변의 근거 구간 · 추가 API 호출 없음";
                _lastReplyForTest = new VideoChatReply(message, _previousMatches, 0, 0, false, true);
            }
            catch (Exception ex) { if (!_closed && revision == _sourceRevision) _status.Text = ex.Message; }
            finally { if (!_closed && revision == _sourceRevision) Busy(false); }
            return;
        }
        var key = _keys.Load();
        if (key.Length == 0) { OpenApiSettings(); _status.Text = "API 설정에서 본인의 OpenAI API 키를 저장하세요."; return; }
        var token = BeginWork();
        var transcript = _transcript;
        AddMessage("나", question); _question.Clear();
        _status.Text = "자막에서 관련 구간을 찾는 중…";
        try
        {
            var reply = await _client.AskAsync(key, transcript, question, _previousQuestion, token, _previousAnswer);
            token.ThrowIfCancellationRequested();
            if (_closed || revision != _sourceRevision || !ReferenceEquals(transcript, _transcript)) return;
            _previousQuestion = question;
            _previousAnswer = reply.Answer;
            _previousMatches = reply.Matches;
            RefreshKeyState(_keys.Load() == key);
            AddMessage("신플레이어 AI", reply.Answer);
            foreach (var match in reply.Matches)
            {
                var button = new Button { Content = new TextBlock { Text = $"{MediaFiles.Time(match.Start)}  ·  {match.Quote}", TextWrapping = TextWrapping.Wrap }, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new(0, 0, 0, 6) };
                button.Click += async (_, _) => { if (_closed || revision != _sourceRevision) return; try { await _seek(transcript, match.Start); } catch (Exception ex) { if (!_closed && revision == _sourceRevision) _status.Text = ex.Message; } };
                _messages.Children.Add(button);
            }
            _status.Text = $"이번 요청 · 입력 {reply.InputTokens:N0} / 출력 {reply.OutputTokens:N0} 토큰" + (reply.Partial ? "\n긴 자막은 후보 구간만 검색했습니다. 구체적인 단어로 다시 물어볼 수 있습니다." : "");
            if (reply.SeekRequested && reply.Matches.Count > 0)
            {
                await _seek(transcript, reply.Matches[0].Start);
                if (!_closed && revision == _sourceRevision) _status.Text += $"\n{MediaFiles.Time(reply.Matches[0].Start)} 구간으로 이동했습니다.";
            }
            else if (reply.SeekRequested) _status.Text += "\n근거 구간을 찾지 못해 재생 위치를 유지했습니다.";
            _lastReplyForTest = reply;
        }
        catch (OperationCanceledException) { if (!_closed && revision == _sourceRevision) _status.Text = token.IsCancellationRequested ? "요청을 취소했습니다." : "AI 응답 시간이 초과되었습니다."; }
        catch (Exception ex)
        {
            if (!_closed && revision == _sourceRevision)
            {
                RefreshKeyState(false); _status.Text = ex.Message; AddMessage("요청을 완료하지 못했습니다", ex.Message);
                if (string.IsNullOrWhiteSpace(_question.Text)) _question.Text = question;
            }
        }
        finally { if (!_closed && revision == _sourceRevision) { Busy(false); _scroll.ScrollToEnd(); } }
    }
    private void AddMessage(string who, string text)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = who, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 6) });
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        var border = new Border { Child = panel, Padding = new(14), CornerRadius = new(10), Margin = new(0, 0, 0, 10) };
        border.SetResourceReference(BackgroundProperty, "Raised"); _messages.Children.Add(border); _scroll.ScrollToEnd();
    }
}

internal sealed class VideoChatWindow : Window
{
    internal VideoChatWindow(Func<CancellationToken, Task<VideoTranscript>> load, Func<VideoTranscript, double, Task> seek)
    {
        Style = (Style)FindResource(typeof(Window));
        Title = "영상 AI 채팅 · 신플레이어";
        Width = 600; Height = 760; MinWidth = 480; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new VideoChatPanel(load, seek);
        Content = panel;
        Loaded += async (_, _) => await panel.LoadAsync();
        Closed += (_, _) => panel.Close();
    }
}
