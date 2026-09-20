using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ShinPlayer;

// All playback operations are queued through libmpv's asynchronous API.
// The event thread owns event memory, copying it before the next mpv_wait_event.
public sealed class MpvPlayer : IDisposable
{
    private IntPtr _handle;
    private readonly Thread _events;
    private readonly object _lifetime = new();
    private readonly SemaphoreSlim _audioChanges = new(1, 1);
    internal double AudioBoostDb { get; private set; }
    private volatile bool _stopping;
    private long _requestId;
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> _pending = new();
    private readonly ConcurrentDictionary<string, string> _properties = new();
    public event Action? Changed;
    public event Action? FileLoaded;
    public event Action<string>? Error;
    public event Action<string>? Input;
    public event Action<string>? Log;
    public event Action<string[], bool>? FilesDropped;
    public event Action? PlaybackRestarted;
    private long _restartCount;
    private long _seekRequestCount;
    internal long SeekRequestCount => Interlocked.Read(ref _seekRequestCount);
    public long RestartCount => Interlocked.Read(ref _restartCount);
    public string Version { get; }

    private static readonly string[] Observed = { "time-pos", "duration", "pause", "speed", "volume", "mute", "eof-reached", "idle-active", "path", "media-title", "video-params/w", "video-params/h", "video-codec", "audio-codec-name", "hwdec-current", "estimated-vf-fps", "sid", "aid", "sub-visibility", "sub-delay", "audio-delay", "ab-loop-a", "ab-loop-b", "audio-pitch-correction", "loop-file", "vo-configured" };
    public MpvPlayer(IntPtr hwnd, PlayerSettings settings)
    {
        _handle = Native.mpv_create();
        if (_handle == IntPtr.Zero) throw new InvalidOperationException("재생 엔진을 만들 수 없습니다.");
        try
        {
            Option("wid", unchecked((uint)hwnd.ToInt64()).ToString(CultureInfo.InvariantCulture));
            Option("vo", "gpu");
            Option("gpu-api", "d3d11");
            Option("gpu-context", "d3d11");
            Option("hwdec", settings.HardwareDecode ? "auto" : "no");
            Option("keep-open", "yes");
            Option("idle", "yes");
            Option("force-window", "immediate");
            Option("osc", "no");
            Option("terminal", "no");
            Option("config", "no");
            Option("load-scripts", "no");
            Option("ytdl", "no");
            Option("input-default-bindings", "no");
            Option("input-vo-keyboard", "yes");
            Option("input-cursor", "yes");
            Option("input-builtin-drag-and-drop", "no");
            Option("cursor-autohide", "1000");
            Option("audio-pitch-correction", settings.PitchCorrection ? "yes" : "no");
            Option("volume", settings.Volume.ToString(CultureInfo.InvariantCulture));
            Option("volume-max", "100");
            AudioBoostDb = AudioBoost.Normalize(settings.AudioBoostDb);
            if (AudioBoostDb > 0) Option("af", AudioBoost.Filter(AudioBoostDb));
            Option("mute", settings.Muted ? "yes" : "no");
            Option("speed", (settings.RememberSpeed ? settings.Speed : 1).ToString(CultureInfo.InvariantCulture));
            Option("sub-auto", "fuzzy");
            Option("sub-font", "Malgun Gothic");
            Option("sub-codepage", "auto");
            Option("osd-font", "Malgun Gothic");
            Option("osd-level", "0");
            Option("screenshot-format", "png");
            Option("demuxer-max-bytes", "64MiB");
            Option("demuxer-max-back-bytes", "16MiB");
            Check(Native.mpv_initialize(_handle));
            Version = GetString("mpv-version");
            foreach (var name in Observed) Check(Native.mpv_observe_property(_handle, 0, name, 1));
            Check(Native.mpv_observe_property(_handle, 0, "dropped-files", 6));
            Native.mpv_request_log_messages(_handle, "warn");
            _events = new Thread(EventLoop) { IsBackground = true, Name = "ShinPlayer mpv events" };
            _events.Start();
            _ = InstallBindings();
        }
        catch { Native.mpv_terminate_destroy(_handle); _handle = IntPtr.Zero; throw; }
    }
    private async Task InstallBindings()
    {
        var bindings = new Dictionary<string, string> {
            ["SPACE"]="play", ["p"]="play", ["MBTN_LEFT"]="click", ["MBTN_LEFT_DBL"]="fullscreen", ["MBTN_RIGHT"]="menu",
            ["RIGHT"]="forward", ["LEFT"]="backward", ["Shift+RIGHT"]="forward-long", ["Shift+LEFT"]="backward-long",
            ["UP"]="volume-up", ["DOWN"]="volume-down", ["WHEEL_UP"]="volume-up", ["WHEEL_DOWN"]="volume-down",
            ["["]="slower", ["]"]="faster", ["{"]="slower-fine", ["}"]="faster-fine", ["BS"]="normal-speed", ["r"]="normal-speed",
            ["f"]="fullscreen", ["F11"]="fullscreen", ["ESC"]="escape", ["m"]="mute", ["s"]="subtitles",
            ["Ctrl+o"]="open", ["Ctrl+O"]="open", ["Ctrl+l"]="playlist", ["Ctrl+L"]="playlist", ["Ctrl+q"]="quit",
            ["Ctrl+u"]="open-link", ["Ctrl+U"]="open-link", ["Alt+d"]="open-link", ["Alt+D"]="open-link", ["Ctrl+j"]="chat", ["Ctrl+J"]="chat",
            ["a"]="ab", ["."]="frame-next", [","]="frame-back", ["Ctrl+s"]="screenshot", ["Ctrl+Shift+s"]="subtitle-capture", ["Ctrl+S"]="subtitle-capture", ["n"]="next", ["Shift+n"]="previous",
            ["HOME"]="start", ["F1"]="help"
        };
        try
        {
            await CommandAsync("define-section", "shin", string.Join("\n", bindings.Select(x => $"{x.Key} script-message shin {x.Value}")), "force");
            await CommandAsync("enable-section", "shin", "allow-hide-cursor");
        }
        catch (Exception ex) { Log?.Invoke("Input bindings: " + ex.Message); }
    }
    public string Text(string name, string fallback = "") => _properties.TryGetValue(name, out var value) && value.Length > 0 ? value : fallback;
    public double Number(string name, double fallback = 0) => double.TryParse(Text(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : fallback;
    public bool Flag(string name) => Text(name) is "yes" or "true";
    private void Option(string name, string value) => Check(Native.mpv_set_option_string(_handle, name, value));
    private string GetString(string name)
    {
        var ptr = Native.mpv_get_property_string(_handle, name);
        if (ptr == IntPtr.Zero) return "";
        try { return Marshal.PtrToStringUTF8(ptr) ?? ""; }
        finally { Native.mpv_free(ptr); }
    }
    public Task SetAsync(string name, double value) => CommandAsync("set", name, value.ToString("0.#####", CultureInfo.InvariantCulture));
    internal string ReadProperty(string name)
    {
        lock (_lifetime)
        {
            if (_stopping || _handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(MpvPlayer));
            return GetString(name);
        }
    }
    internal async Task SetAudioBoostAsync(double db)
    {
        db = AudioBoost.Normalize(db);
        await _audioChanges.WaitAsync();
        try
        {
            if (Math.Abs(db - AudioBoostDb) < .01) return;
            if (db > 0) await CommandAsync("af", "add", AudioBoost.Filter(db));
            else await CommandAsync("af", "remove", "@" + AudioBoost.Label);
            AudioBoostDb = db;
        }
        finally { _audioChanges.Release(); }
    }
    public Task SetAsync(string name, bool value) => CommandAsync("set", name, value ? "yes" : "no");
    public Task SetAsync(string name, string value) => CommandAsync("set", name, value);
    public Task CommandAsync(params string[] args)
    {
        lock (_lifetime)
        {
            if (_stopping || _handle == IntPtr.Zero) return Task.FromException(new ObjectDisposedException(nameof(MpvPlayer)));
            if (args.Length > 0 && args[0] == "seek") Interlocked.Increment(ref _seekRequestCount);
            var id = (ulong)Interlocked.Increment(ref _requestId);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = completion;
            var strings = args.Select(Marshal.StringToCoTaskMemUTF8).ToArray();
            var argv = Marshal.AllocHGlobal((strings.Length + 1) * IntPtr.Size);
            try
            {
                for (int i = 0; i < strings.Length; i++) Marshal.WriteIntPtr(argv, i * IntPtr.Size, strings[i]);
                Marshal.WriteIntPtr(argv, strings.Length * IntPtr.Size, IntPtr.Zero);
                var result = Native.mpv_command_async(_handle, id, argv);
                if (result < 0 && _pending.TryRemove(id, out var failed)) failed.TrySetException(new InvalidOperationException(ErrorText(result)));
            }
            finally { foreach (var ptr in strings) Marshal.FreeCoTaskMem(ptr); Marshal.FreeHGlobal(argv); }
            return completion.Task;
        }
    }
    private void EventLoop()
    {
        while (!_stopping)
        {
            var data = Native.mpv_wait_event(_handle, -1);
            if (_stopping) break;
            var evt = Marshal.PtrToStructure<MpvEvent>(data);
            try
            {
                switch (evt.Id)
                {
                    case 5: // MPV_EVENT_COMMAND_REPLY
                        if (_pending.TryRemove(evt.Userdata, out var task))
                        { if (evt.Error < 0) task.TrySetException(new InvalidOperationException(ErrorText(evt.Error))); else task.TrySetResult(true); }
                        break;
                    case 22: // MPV_EVENT_PROPERTY_CHANGE
                        var prop = Marshal.PtrToStructure<MpvProperty>(evt.Data);
                        var name = Marshal.PtrToStringUTF8(prop.Name) ?? "";
                        if (name == "dropped-files") { if (prop.Format == 6 && prop.Data != IntPtr.Zero) DispatchDrop(prop.Data); break; }
                        var value = prop.Format == 1 && prop.Data != IntPtr.Zero ? Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(prop.Data)) ?? "" : "";
                        _properties[name] = value;
                        Changed?.Invoke();
                        break;
                    case 8:
                        // Property notifications may follow file-loaded. Publish a coherent
                        // initial snapshot so the UI cannot reuse the previous file's EOF/time.
                        foreach (var property in new[] { "path", "duration", "time-pos", "eof-reached", "pause", "idle-active" })
                            _properties[property] = GetString(property);
                        FileLoaded?.Invoke();
                        break;
                    case 21: Interlocked.Increment(ref _restartCount); PlaybackRestarted?.Invoke(); break;
                    case 7: // MPV_EVENT_END_FILE
                        var end = Marshal.PtrToStructure<MpvEndFile>(evt.Data);
                        if (end.Reason == 4) Error?.Invoke("이 파일을 재생할 수 없습니다. " + ErrorText(end.Error));
                        break;
                    case 16: // MPV_EVENT_CLIENT_MESSAGE
                        var msg = Marshal.PtrToStructure<MpvMessage>(evt.Data);
                        if (msg.Count >= 2 && Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(msg.Args)) == "shin")
                            Input?.Invoke(Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(msg.Args, IntPtr.Size)) ?? "");
                        break;
                    case 2:
                        var log = Marshal.PtrToStructure<MpvLog>(evt.Data);
                        Log?.Invoke($"{Marshal.PtrToStringUTF8(log.Prefix)}: {Marshal.PtrToStringUTF8(log.Text)}".Trim());
                        break;
                }
            }
            catch (Exception ex) { Log?.Invoke(ex.Message); }
        }
    }
    private void DispatchDrop(IntPtr data)
    {
        var node = Marshal.PtrToStructure<MpvNode>(data);
        if (node.Format != 8 || node.Value == IntPtr.Zero) return;
        var map = Marshal.PtrToStructure<MpvNodeList>(node.Value);
        string[] files = Array.Empty<string>();
        bool append = false;
        int nodeSize = Marshal.SizeOf<MpvNode>();
        for (int i = 0; i < map.Count; i++)
        {
            var key = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(map.Keys, i * IntPtr.Size));
            var value = Marshal.PtrToStructure<MpvNode>(IntPtr.Add(map.Values, i * nodeSize));
            if (key == "action" && value.Format == 1) append = Marshal.PtrToStringUTF8(value.Value) == "append";
            if (key == "files" && value.Format == 7)
            {
                var list = Marshal.PtrToStructure<MpvNodeList>(value.Value);
                var paths = new List<string>();
                for (int j = 0; j < list.Count; j++)
                {
                    var item = Marshal.PtrToStructure<MpvNode>(IntPtr.Add(list.Values, j * nodeSize));
                    if (item.Format == 1 && Marshal.PtrToStringUTF8(item.Value) is string path) paths.Add(path);
                }
                files = paths.ToArray();
            }
        }
        if (files.Length > 0) FilesDropped?.Invoke(files, append);
    }
    public void Dispose()
    {
        lock (_lifetime) { if (_stopping) return; _stopping = true; Native.mpv_wakeup(_handle); }
        _events.Join();
        foreach (var task in _pending.Values) task.TrySetCanceled();
        _pending.Clear();
        Native.mpv_terminate_destroy(_handle);
        _handle = IntPtr.Zero;
    }
    private static string ErrorText(int error) => Marshal.PtrToStringUTF8(Native.mpv_error_string(error)) ?? $"오류 {error}";
    private static void Check(int error) { if (error < 0) throw new InvalidOperationException(ErrorText(error)); }
    [StructLayout(LayoutKind.Sequential)] private struct MpvEvent { public int Id; public int Error; public ulong Userdata; public IntPtr Data; }
    [StructLayout(LayoutKind.Sequential)] private struct MpvProperty { public IntPtr Name; public int Format; public IntPtr Data; }
    [StructLayout(LayoutKind.Sequential)] private struct MpvEndFile { public int Reason; public int Error; public long PlaylistEntryId; public long InsertId; public int InsertCount; }
    [StructLayout(LayoutKind.Sequential)] private struct MpvMessage { public int Count; public IntPtr Args; }
    [StructLayout(LayoutKind.Sequential)] private struct MpvLog { public IntPtr Prefix; public IntPtr Level; public IntPtr Text; public int LogLevel; }
    [StructLayout(LayoutKind.Sequential)] private struct MpvNode { public IntPtr Value; public int Format; }
    [StructLayout(LayoutKind.Sequential)] private struct MpvNodeList { public int Count; public IntPtr Values; public IntPtr Keys; }
    private static class Native
    {
        private const string Dll = "libmpv-2.dll";
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr mpv_create();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_initialize(IntPtr ctx);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void mpv_terminate_destroy(IntPtr ctx);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void mpv_wakeup(IntPtr ctx);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_set_option_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_observe_property(IntPtr ctx, ulong id, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_request_log_messages(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string level);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int mpv_command_async(IntPtr ctx, ulong id, IntPtr args);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void mpv_free(IntPtr data);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr mpv_error_string(int error);
    }
}
