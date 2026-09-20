using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ShinPlayer;

public static class WindowsIntegration
{
    public static string ExePath => Environment.ProcessPath ?? throw new InvalidOperationException();
    private const string ProgId = "ShinPlayer.Video";
    public static void Register()
    {
        var command = $"\"{ExePath}\" \"%1\"";
        using (var caps = Registry.CurrentUser.CreateSubKey(@"Software\ShinPlayer\Capabilities"))
        {
            caps.SetValue("ApplicationName", "신플레이어");
            caps.SetValue("ApplicationDescription", "빠른 실행, 세밀한 배속, 다양한 영상 형식을 지원하는 영상 플레이어");
            caps.SetValue("ApplicationIcon", $"{ExePath},0");
            using var associations = caps.CreateSubKey("FileAssociations");
            foreach (var ext in MediaFiles.VideoExtensions) associations.SetValue(ext, ProgId);
        }
        using (var apps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications")) apps.SetValue("ShinPlayer", @"Software\ShinPlayer\Capabilities");
        using (var prog = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
        {
            prog.SetValue("", "신플레이어 영상");
            using (var icon = prog.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{ExePath}\",0");
            using (var open = prog.CreateSubKey(@"shell\open\command")) open.SetValue("", command);
        }
        using (var app = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\ShinPlayer.exe"))
        {
            app.SetValue("FriendlyAppName", "신플레이어");
            using (var open = app.CreateSubKey(@"shell\open\command")) open.SetValue("", command);
            using var supported = app.CreateSubKey("SupportedTypes");
            foreach (var ext in MediaFiles.VideoExtensions) supported.SetValue(ext, "");
        }
        foreach (var ext in MediaFiles.VideoExtensions)
        {
            using var openWith = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ext + @"\OpenWithProgids");
            openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }
        using (var paths = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\ShinPlayer.exe")) paths.SetValue("", ExePath);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }
    public static void Unregister()
    {
        using (var apps = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true)) apps?.DeleteValue("ShinPlayer", false);
        foreach (var ext in MediaFiles.VideoExtensions)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ext + @"\OpenWithProgids", true);
            key?.DeleteValue(ProgId, false);
        }
        foreach (var key in new[] { @"Software\ShinPlayer", @"Software\Classes\" + ProgId, @"Software\Classes\Applications\ShinPlayer.exe", @"Software\Microsoft\Windows\CurrentVersion\App Paths\ShinPlayer.exe" }) Registry.CurrentUser.DeleteSubKeyTree(key, false);
        SetStartup(false);
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }
    public static bool StartupEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("ShinPlayer") is string; }
    }
    public static void SetStartup(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) run.SetValue("ShinPlayer", $"\"{ExePath}\" --background"); else run.DeleteValue("ShinPlayer", false);
    }
    public static void OpenDefaults() => Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=ShinPlayer") { UseShellExecute = true });
    [DllImport("shell32.dll")] private static extern void SHChangeNotify(uint evt, uint flags, IntPtr item1, IntPtr item2);
}
