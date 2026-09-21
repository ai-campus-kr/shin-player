using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShinPlayer;

internal sealed record GifOptions(double Start, double End, int Width, int Fps, double Speed = 1)
{
    internal double Length => End - Start;
    internal double OutputLength => Length / Speed;
    internal int WorkerTimeoutSeconds => (int)Math.Clamp(Math.Ceiling(Length * 2 + 60), 120, 7200);
    internal void Validate(double duration)
    {
        if (!double.IsFinite(Speed) || Speed < .25 || Speed > 100)
            throw new InvalidOperationException("GIF 배속은 0.25~100 사이의 숫자로 입력하세요.");
        if (!double.IsFinite(duration) || duration < .2 || !double.IsFinite(Start) || !double.IsFinite(End) || Start < 0 || End > duration + .001 || Length < .2 - .000001 || Length > 3600 + .000001)
            throw new InvalidOperationException("영상 안에서 0.2초~1시간 구간을 선택하세요. 끝은 시작보다 뒤여야 합니다.");
        if (OutputLength < .2 - .000001 || OutputLength > 300 + .000001)
            throw new InvalidOperationException("완성 GIF 길이는 0.2초~5분입니다. 배속이나 시작·끝 시간을 조정하세요.");
        if (Width is not (360 or 480 or 720) || Fps is not (10 or 12 or 15 or 20)) throw new InvalidOperationException("크기와 프레임 수를 다시 선택하세요.");
    }
    internal static double ParseSpeed(string text)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var speed) || !double.IsFinite(speed) || speed < .25 || speed > 100)
            throw new FormatException("GIF 배속은 0.25~100 사이의 숫자로 입력하세요. 예: 10 또는 2.5");
        return speed;
    }
    internal static string DurationText(double seconds)
    {
        // Round before splitting, so a value near a minute never displays as 60 seconds.
        var milliseconds = (long)Math.Round(seconds * 1000);
        var minutes = milliseconds / 60000;
        double remainder = milliseconds % 60000 / 1000.0;
        return minutes == 0 ? $"{remainder:0.###}초" : remainder == 0 ? $"{minutes}분" : $"{minutes}분 {remainder:0.###}초";
    }
    internal static string Time(double time)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, time));
        return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
    }
    internal static double Parse(string text)
    {
        var parts = text.Trim().Split(':');
        if (parts.Length is < 1 or > 3) throw new FormatException("시간은 초 또는 시:분:초 형식으로 입력하세요.");
        double seconds = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value < 0 || (i > 0 && value >= 60) || (i < parts.Length - 1 && value != Math.Floor(value)))
                throw new FormatException("시간은 초 또는 시:분:초 형식으로 입력하세요. 예: 01:23.500");
            seconds = seconds * 60 + value;
        }
        return seconds;
    }
}

internal abstract class GifSource(string title, double duration, bool browser)
{
    internal string Title { get; } = title;
    internal double Duration { get; } = duration;
    internal bool IsBrowser { get; } = browser;
    internal abstract Task<double> PositionAsync();
    internal abstract Task<string> ExportAsync(CaptureTools tools, GifOptions options, string pictures, IProgress<string> progress, CancellationToken cancel);
}

internal sealed class LocalGifSource(SubtitleVideo video, Func<double> position) : GifSource(Path.GetFileNameWithoutExtension(video.Path), video.Duration, false)
{
    internal override Task<double> PositionAsync() => Task.FromResult(position());
    internal override Task<string> ExportAsync(CaptureTools tools, GifOptions options, string pictures, IProgress<string> progress, CancellationToken cancel)
    {
        options.Validate(Duration);
        if (video.VideoIndex < 0) throw new InvalidOperationException("GIF로 만들 영상 트랙이 없습니다.");
        string[] input = ["-protocol_whitelist", "file,pipe", "-ss", GifExport.Number(options.Start), "-t", GifExport.Number(options.Length), "-i", video.Path];
        return GifExport.EncodeAsync(tools, input, $"0:{video.VideoIndex}", options, Title, pictures, progress, cancel);
    }
}

internal static class GifExport
{
    internal static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    internal static string TempRoot => Path.Combine(Path.GetTempPath(), "ShinPlayer-gif");
    internal static string CreateTemp()
    {
        string path = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
    internal static async Task<string> EncodeAsync(CaptureTools tools, string[] input, string stream, GifOptions options, string title,
        string pictures, IProgress<string> progress, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        string temp = CreateTemp();
        string folder = Path.Combine(Path.GetFullPath(pictures), "신플레이어 GIF");
        string? partial = null;
        try
        {
            Directory.CreateDirectory(folder);
            // A second pass bounds memory: never retain an entire decoded clip for palette generation.
            // Change the timeline before selecting output frames: 600 source seconds / 10 = 60 GIF seconds.
            var scale = $"settb=AVTB,setpts=(PTS-STARTPTS)/{Number(options.Speed)},fps={options.Fps},scale=trunc(iw*sar/2)*2:ih,setsar=1,scale=w='min({options.Width},iw)':h='min({options.Width},ih)':force_original_aspect_ratio=decrease:flags=lanczos";
            string[] common = ["-hide_banner", "-loglevel", "error", "-nostdin", "-threads", "2", "-filter_threads", "1", "-filter_complex_threads", "1"];
            var palette = Path.Combine(temp, "palette.png");
            progress.Report("1/2 · GIF 색상을 최적화하는 중…");
            await CaptureTools.RunAsync(tools.Ffmpeg, [.. common, .. input, "-map", stream, "-an", "-sn", "-vf", scale + ",palettegen=stats_mode=full", "-frames:v", "1", "-update", "1", "-y", palette], temp, cancel, options.WorkerTimeoutSeconds);
            progress.Report("2/2 · GIF로 저장하는 중…");
            partial = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".partial");
            await CaptureTools.RunAsync(tools.Ffmpeg, [.. common, .. input, "-i", palette, "-t", Number(options.OutputLength), "-filter_complex", $"[{stream}]{scale}[v];[v][1:v]paletteuse=dither=sierra2_4a", "-an", "-sn", "-loop", "0", "-f", "gif", "-y", partial], temp, cancel, options.WorkerTimeoutSeconds);
            cancel.ThrowIfCancellationRequested();
            if (new FileInfo(partial).Length < 30) throw new InvalidOperationException("GIF에 저장할 화면이 없습니다.");
            var invalid = Path.GetInvalidFileNameChars();
            string safe = new(title.Where(c => !invalid.Contains(c) && !char.IsControl(c)).Take(65).ToArray());
            safe = safe.Trim().TrimEnd('.'); if (safe.Length == 0) safe = "영상";
            string prefix = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safe}_{Number(options.Start)}-{Number(options.End)}_{Number(options.Speed)}x";
            for (int suffix = 0; ; suffix++)
            {
                string output = Path.Combine(folder, prefix + (suffix == 0 ? "" : $"_{suffix}") + ".gif");
                try { File.Move(partial, output, false); return output; }
                catch (IOException) when (File.Exists(output)) { }
            }
        }
        finally
        {
            if (partial != null && File.Exists(partial)) File.Delete(partial);
            CaptureTools.RemovePrivateDirectory(temp, TempRoot);
        }
    }

    internal static async Task<string> EncodeFramesAsync(CaptureTools tools, IReadOnlyList<(string File, double Time)> frames,
        string temp, GifOptions options, string title, string pictures, IProgress<string> progress, CancellationToken cancel)
    {
        if (frames.Count == 0) throw new InvalidOperationException("캡처된 화면이 없습니다.");
        var manifest = new StringBuilder("ffconcat version 1.0\n");
        for (int i = 0; i < frames.Count; i++)
        {
            // Only generated numeric filenames are permitted in this manifest.
            var name = Path.GetFileName(frames[i].File);
            if (name != $"frame{i:D5}.png") throw new InvalidOperationException("잘못된 캡처 파일입니다.");
            var end = i + 1 < frames.Count ? frames[i + 1].Time : options.Length;
            manifest.Append($"file '{name}'\nduration {Number(Math.Max(.001, end - frames[i].Time))}\n");
        }
        manifest.Append($"file '{Path.GetFileName(frames[^1].File)}'\n");
        string list = Path.Combine(temp, "frames.ffconcat");
        await File.WriteAllTextAsync(list, manifest.ToString(), new UTF8Encoding(false), cancel);
        return await EncodeAsync(tools, ["-protocol_whitelist", "file,pipe", "-f", "concat", "-safe", "1", "-i", list], "0:v:0", options, title, pictures, progress, cancel);
    }
}
