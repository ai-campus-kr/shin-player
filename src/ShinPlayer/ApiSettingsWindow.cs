using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShinPlayer;

internal sealed class ApiSettingsWindow : Window
{
    private readonly ApiKeyStore _keys;
    private readonly VideoChatClient _client;
    private readonly Action<bool> _changed;
    private readonly CancellationTokenSource _cancel = new();
    internal readonly PasswordBox KeyInput = new() { MaxLength = 512, Padding = new(10), MinHeight = 38 };
    internal readonly TextBlock KeyState = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 14, 0, 5), FontWeight = FontWeights.SemiBold };
    internal readonly TextBlock ConnectionState = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 14) };
    internal readonly Button SaveKey = new() { Content = "키 저장" };
    internal readonly Button RemoveKey = new() { Content = "키 삭제" };
    internal readonly Button CheckConnection = new() { Content = "연결 확인", ToolTip = "저장된 키로 짧은 API 요청을 보내 확인합니다. 소량의 토큰이 사용됩니다." };
    private bool _closed, _checking;

    internal ApiSettingsWindow(ApiKeyStore keys, VideoChatClient client, Action<bool> changed, bool connected = false)
    {
        _keys = keys; _client = client; _changed = changed;
        Style = (Style)FindResource(typeof(Window));
        Title = "OpenAI API 설정 · 신플레이어";
        Width = 470; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var body = new StackPanel { Margin = new(24) };
        body.Children.Add(new TextBlock { Text = "API 연결", FontSize = 23, FontWeight = FontWeights.SemiBold });
        var model = new TextBlock { Text = VideoChatClient.Model, Margin = new(0, 5, 0, 20) };
        model.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); body.Children.Add(model);
        body.Children.Add(new TextBlock { Text = "새 키 입력 · 기존 키는 표시하지 않습니다", FontSize = 12, Margin = new(0, 0, 0, 8) });
        KeyInput.SetResourceReference(BackgroundProperty, "Raised"); KeyInput.SetResourceReference(ForegroundProperty, "Ink");
        KeyInput.SetResourceReference(PasswordBox.CaretBrushProperty, "Accent"); KeyInput.SetResourceReference(BorderBrushProperty, "Line");
        body.Children.Add(KeyInput);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0) };
        SaveKey.Style = (Style)FindResource("Primary"); SaveKey.Padding = new(14, 8, 14, 8);
        CheckConnection.Margin = new(6, 0, 6, 0);
        buttons.Children.Add(SaveKey); buttons.Children.Add(CheckConnection); buttons.Children.Add(RemoveKey); body.Children.Add(buttons);
        body.Children.Add(KeyState); body.Children.Add(ConnectionState);
        var note = new TextBlock { Text = "키는 이 Windows 사용자 계정으로 암호화해 저장합니다.\n연결 확인은 소량의 토큰을 사용합니다. 영상·자막은 전송하지 않습니다.", FontSize = 11, TextWrapping = TextWrapping.Wrap };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); body.Children.Add(note);
        var close = new Button { Content = "닫기", HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 16, 0, 0) };
        close.Click += (_, _) => Close(); body.Children.Add(close); Content = body;
        RefreshSavedState();
        if (connected && _keys.Load().Length > 0)
        {
            ConnectionState.Text = "✓ 연결 확인됨 · " + VideoChatClient.Model;
            ConnectionState.SetResourceReference(TextBlock.ForegroundProperty, "SuccessInk");
        }
        SaveKey.Click += (_, _) =>
        {
            try { _keys.Save(KeyInput.Password); KeyInput.Clear(); RefreshSavedState(); _changed(false); }
            catch (Exception ex) { ConnectionState.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "키를 저장하지 못했습니다. 저장 위치의 권한을 확인하세요."; ConnectionState.SetResourceReference(TextBlock.ForegroundProperty, "WarningInk"); }
        };
        RemoveKey.Click += (_, _) =>
        {
            try { _keys.Delete(); KeyInput.Clear(); RefreshSavedState(); _changed(false); }
            catch { ConnectionState.Text = "저장된 키를 삭제하지 못했습니다."; }
        };
        CheckConnection.Click += async (_, _) => await CheckAsync();
        KeyInput.PasswordChanged += (_, _) => RefreshButtons();
        Closed += (_, _) => { _closed = true; _cancel.Cancel(); KeyInput.Clear(); if (!_checking) _cancel.Dispose(); };
    }

    private void RefreshSavedState()
    {
        bool saved = _keys.Load().Length > 0;
        KeyState.Text = saved ? "✓ 키 저장됨 · 암호화 보관" : _keys.HasSavedKey ? "! 저장된 키를 읽지 못했습니다" : "API 키가 저장되어 있지 않습니다";
        KeyState.SetResourceReference(TextBlock.ForegroundProperty, saved ? "SuccessInk" : "Muted");
        ConnectionState.Text = saved ? "연결은 아직 확인하지 않았습니다. ‘연결 확인’을 눌러 주세요." : "키를 입력하고 ‘키 저장’을 눌러 주세요.";
        ConnectionState.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        RefreshButtons();
    }
    private void RefreshButtons()
    {
        SaveKey.IsEnabled = !_checking && !string.IsNullOrWhiteSpace(KeyInput.Password);
        CheckConnection.IsEnabled = !_checking && _keys.Load().Length > 0 && KeyInput.Password.Length == 0;
        RemoveKey.IsEnabled = !_checking && _keys.HasSavedKey; KeyInput.IsEnabled = !_checking;
    }
    internal async Task CheckAsync()
    {
        if (_checking || _closed) return;
        _checking = true; RefreshButtons(); ConnectionState.Text = "API 연결 확인 중…";
        ConnectionState.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        try
        {
            string key = _keys.Load();
            await _client.CheckConnectionAsync(key, _cancel.Token);
            if (_closed) return;
            if (_keys.Load() != key) { RefreshSavedState(); _changed(false); return; }
            ConnectionState.Text = "✓ 연결 확인됨 · " + VideoChatClient.Model;
            ConnectionState.SetResourceReference(TextBlock.ForegroundProperty, "SuccessInk"); _changed(true);
        }
        catch (OperationCanceledException)
        {
            if (!_closed) { ConnectionState.Text = "연결 시간이 초과되었습니다. 인터넷 연결을 확인하고 다시 시도하세요."; ConnectionState.SetResourceReference(TextBlock.ForegroundProperty, "WarningInk"); _changed(false); }
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                ConnectionState.Text = ex is InvalidOperationException ? ex.Message : ex is HttpRequestException ? "OpenAI에 연결하지 못했습니다. 인터넷·방화벽 설정을 확인하세요." : "연결 확인을 완료하지 못했습니다. 다시 시도하세요.";
                ConnectionState.SetResourceReference(TextBlock.ForegroundProperty, "WarningInk"); _changed(false);
            }
        }
        finally { _checking = false; if (!_closed) RefreshButtons(); else _cancel.Dispose(); }
    }
}
