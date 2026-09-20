using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ShinPlayer;

public partial class App : Application
{
    static App() { }
    public static readonly Stopwatch StartupWatch = Stopwatch.StartNew();
    private Forms.NotifyIcon? _tray;
    private MainWindow? _window;
    private readonly CancellationTokenSource _stop = new();
    private bool _standbyWarming;
    private bool _showRequested;
    public bool IsExiting { get; private set; }
    public bool IsTest { get; private set; }
    public bool IsDiagnosticSession { get; private set; }
    public PlayerSettings Settings { get; private set; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, evt) => { Log(evt.Exception.ToString()); MessageBox.Show(evt.Exception.Message, "신플레이어", MessageBoxButton.OK, MessageBoxImage.Error); evt.Handled = true; };
        IsTest = e.Args.Contains("--self-test");
        IsDiagnosticSession = e.Args.Contains("--diagnostic-session");
        Settings = IsTest || IsDiagnosticSession ? new PlayerSettings { Resume = false, Muted = true } : PlayerSettings.Load();
        if (!IsTest)
        {
            CreateTray();
            _ = ListenAsync();
        }
        if (e.Args.Contains("--background"))
        {
            await WarmStandbyAsync();
            return;
        }
        ShowPlayer();
        if (IsTest)
        {
            int i = Array.IndexOf(e.Args, "--self-test");
            await _window!.RunSelfTestAsync(e.Args[i + 1], e.Args[i + 2]);
            Quit();
        }
        else await HandleArgs(e.Args);
    }
    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(Program.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                timeout.CancelAfter(3000);
                using var reader = new StreamReader(server, Encoding.UTF8);
                // Bound local IPC payloads, including a client that never sends a newline.
                var message = new StringBuilder();
                var buffer = new char[1024];
                while (message.Length < 65536)
                {
                    var count = await reader.ReadAsync(buffer.AsMemory(), timeout.Token);
                    if (count == 0) break;
                    message.Append(buffer, 0, count);
                    if (message.ToString().Contains('\n')) break;
                }
                if (message.Length >= 65536) continue;
                var args = JsonSerializer.Deserialize<string[]>(message.ToString());
                if (args?.Contains("--status") == true)
                {
                    var status = await Dispatcher.InvokeAsync(() => _window?.GetStatus() ?? new { running = true, visible = false, engineReady = false });
                    using var writer = new StreamWriter(server, new UTF8Encoding(false));
                    await writer.WriteLineAsync(JsonSerializer.Serialize(status));
                    await writer.FlushAsync();
                }
                else if (args != null) await Dispatcher.InvokeAsync(() => { _ = HandleForwardedArgsAsync(args); });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log(ex.ToString()); }
        }
    }
    private async Task HandleForwardedArgsAsync(string[] args)
    {
        try { await HandleArgs(args); }
        catch (Exception ex) { Log(ex.ToString()); }
    }
    private async Task HandleArgs(string[] args)
    {
        if (args.Contains("--exit")) { Quit(); return; }
        if (args.Contains("--hide")) { if (_window != null) await _window.HideToTrayAsync(); return; }
        if (args.Contains("--background")) return;
        ShowPlayer();
        if (args.Contains("--default-apps")) WindowsIntegration.OpenDefaults();
        var files = args.Where(x => !x.StartsWith("--", StringComparison.Ordinal)).ToArray();
        if (files.Length > 0) await _window!.OpenFilesAsync(files);
    }
    public void ShowPlayer()
    {
        _showRequested = true;
        if (_window == null) { _window = new MainWindow(); MainWindow = _window; }
        if (_standbyWarming) RestoreStandbyWindow();
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }
    private async Task WarmStandbyAsync()
    {
        _standbyWarming = true;
        _showRequested = false;
        _window = new MainWindow { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000 };
        MainWindow = _window;
        _window.Show();
        try { await _window.WarmupAsync(); }
        catch (Exception ex) { Log("Standby preparation: " + ex); }
        finally
        {
            if (!_showRequested) { _window.Hide(); RestoreStandbyWindow(); }
            _standbyWarming = false;
        }
    }
    private void RestoreStandbyWindow()
    {
        if (_window == null) return;
        var area = SystemParameters.WorkArea;
        _window.Left = area.Left + (area.Width - _window.Width) / 2;
        _window.Top = area.Top + (area.Height - _window.Height) / 2;
        _window.ShowInTaskbar = true;
        _window.ShowActivated = true;
    }
    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("신플레이어 열기", null, (_, _) => Dispatcher.Invoke(ShowPlayer));
        menu.Items.Add("영상 열기…", null, (_, _) => Dispatcher.Invoke(() => { ShowPlayer(); _window!.OpenDialog(); }));
        menu.Items.Add("기본 앱 설정", null, (_, _) => WindowsIntegration.OpenDefaults());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("완전히 종료", null, (_, _) => Dispatcher.Invoke(Quit));
        using var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/ShinPlayer.ico"))!.Stream;
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(stream), Text = "신플레이어 · 영상 파일을 더블클릭해 재생", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowPlayer);
    }
    public void SaveSettings()
    {
        if (IsTest || IsDiagnosticSession) return;
        try { Settings.Save(); } catch (Exception ex) { Log(ex.ToString()); }
    }
    public void Quit()
    {
        if (IsExiting) return;
        IsExiting = true;
        _window?.PrepareExit();
        SaveSettings();
        _window?.Close();
        Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _stop.Cancel();
        _tray?.Dispose();
        base.OnExit(e);
    }
    public static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(PlayerSettings.DirectoryPath);
            var path = Path.Combine(PlayerSettings.DirectoryPath, "player.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {text}\n");
        }
        catch { }
    }
}
