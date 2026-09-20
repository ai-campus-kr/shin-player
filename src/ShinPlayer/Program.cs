using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ShinPlayer;

public static class Program
{
    public static string PipeName { get; private set; } = "";

    [STAThread]
    public static int Main(string[] args)
    {
        // Forward a double-click before creating Application or loading XAML/WPF.
        // Keep the UI startup in a non-inlined method so the forwarding path stays small.
        if (args.Contains("--register")) { WindowsIntegration.Register(); return 0; }
        if (args.Contains("--unregister")) { WindowsIntegration.Unregister(); return 0; }
        if (args.Contains("--enable-startup")) { WindowsIntegration.SetStartup(true); return 0; }
        if (args.Contains("--live-chat-test"))
        {
            int index = Array.IndexOf(args, "--live-chat-test");
            if (args.Length <= index + 2) return 2;
            return LiveChatTest.RunAsync(args[index + 1], args[index + 2]).GetAwaiter().GetResult();
        }
        if (args.Contains("--self-test"))
        {
            int index = Array.IndexOf(args, "--self-test");
            if (args.Length <= index + 2) { Console.Error.WriteLine("Usage: --self-test <fixture-directory> <report-directory>"); return 2; }
            return RunUi();
        }
        if (args.Contains("--live-youtube-test"))
        {
            int index = Array.IndexOf(args, "--live-youtube-test");
            if (args.Length <= index + 3) return 2;
            return RunUi();
        }
        PipeName = "ShinPlayer-" + (WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName);
        using var mutex = new Mutex(true, "Local\\" + PipeName, out bool owns);
        if (!owns)
        {
            if (args.Contains("--background")) return 0;
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                client.Connect(5000);
                using var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, true);
                writer.WriteLine(JsonSerializer.Serialize(args));
                writer.Flush();
                if (args.Contains("--status"))
                {
                    using var reader = new StreamReader(client, Encoding.UTF8, false, 1024, true);
                    Console.WriteLine(reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
                }
                return 0;
            }
            catch (Exception ex)
            {
                if (args.Any(x => x is "--status" or "--exit" or "--hide")) Console.Error.WriteLine(ex.Message);
                else ReportError(ex.Message);
                return 1;
            }
        }
        try
        {
            if (args.Contains("--status")) { Console.WriteLine("{\"running\":false}"); return 0; }
            return args.Contains("--exit") || args.Contains("--hide") ? 0 : RunUi();
        }
        finally { mutex.ReleaseMutex(); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunUi()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
        return Environment.ExitCode;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReportError(string message) => System.Windows.MessageBox.Show("실행 중인 신플레이어에 연결하지 못했습니다.\n" + message, "신플레이어");
}
