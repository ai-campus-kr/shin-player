using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ShinPlayer;

internal sealed record SubtitleTrack(int Index, string Codec, string Language, string Title, bool Default)
{
    public string Label => $"{(string.IsNullOrWhiteSpace(Language) ? "언어 미지정" : Language)} · {(!string.IsNullOrWhiteSpace(Title) ? Title + " · " : "")}트랙 {Index} ({Codec})";
}
internal sealed record SubtitleVideo(string Path, double Duration, int VideoIndex, IReadOnlyList<SubtitleTrack> Tracks);
internal sealed record SubtitleCue(double Start, double End, string Text)
{
    public double Midpoint => Start + (End - Start) / 2;
}
internal sealed record CaptureProgress(int Completed, int Total, string? Directory, string Message);
internal sealed record CaptureResult(string Directory, int Count);

internal sealed class SubtitleCapture(CaptureTools tools)
{
    private static readonly HashSet<string> TextCodecs = new(StringComparer.OrdinalIgnoreCase) { "subrip", "srt", "ass", "ssa", "mov_text", "webvtt", "text", "sami", "subviewer", "subviewer1", "microdvd", "mpl2", "jacosub", "vplayer", "realtext", "pjs", "stl", "ttml" };
    private static readonly Regex Timing = new(@"^(\d{1,6}):([0-5]\d):([0-5]\d)[,.](\d{3})\s*-->\s*(\d{1,6}):([0-5]\d):([0-5]\d)[,.](\d{3})(?:\s.*)?$", RegexOptions.CultureInvariant);
    internal static string PicturesDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    internal async Task<SubtitleVideo> ProbeAsync(string path, CancellationToken cancel)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("영상 파일을 찾을 수 없습니다.", path);
        var output = await CaptureTools.RunAsync(tools.Ffprobe, new[] { "-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "format=duration:stream=index,codec_type,codec_name:stream_tags=language,title:stream_disposition=default,attached_pic", "-of", "json", "-i", path }, AppContext.BaseDirectory, cancel);
        using var json = JsonDocument.Parse(output);
        var root = json.RootElement;
        var duration = root.TryGetProperty("format", out var format) && double.TryParse(Text(format, "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds) && seconds > 0 ? Math.Min(seconds, MediaFiles.MaxSeconds) : 0;
        var video = -1;
        var tracks = new List<SubtitleTrack>();
        foreach (var stream in root.GetProperty("streams").EnumerateArray())
        {
            int index = stream.GetProperty("index").GetInt32();
            var disposition = stream.TryGetProperty("disposition", out var d) ? d : default;
            if (Text(stream, "codec_type") == "video" && Number(disposition, "attached_pic") == 0 && video < 0) video = index;
            var codec = Text(stream, "codec_name");
            if (Text(stream, "codec_type") != "subtitle" || !TextCodecs.Contains(codec)) continue;
            var tags = stream.TryGetProperty("tags", out var t) ? t : default;
            tracks.Add(new(index, codec, Text(tags, "language"), Text(tags, "title"), Number(disposition, "default") == 1));
        }
        return new(path, duration, video, tracks);
    }
    private static string Text(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static int Number(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    internal static IReadOnlyList<SubtitleCue> ParseSrt(string text, double duration, double delay = 0)
    {
        var result = new List<SubtitleCue>();
        foreach (var block in Regex.Split(text.Replace("\r\n", "\n").TrimStart('\uFEFF'), @"\n[ \t]*\n"))
        {
            var lines = block.Trim().Split('\n');
            var at = Array.FindIndex(lines, x => x.Contains("-->"));
            if (at < 0) continue;
            var match = Timing.Match(lines[at].Trim());
            if (!match.Success) continue;
            double Time(int offset) => int.Parse(match.Groups[offset].Value, CultureInfo.InvariantCulture) * 3600d + int.Parse(match.Groups[offset + 1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(match.Groups[offset + 2].Value, CultureInfo.InvariantCulture) + int.Parse(match.Groups[offset + 3].Value, CultureInfo.InvariantCulture) / 1000d;
            var start = Math.Max(0, Time(1) + delay);
            var end = Math.Min(duration > 0 ? duration : MediaFiles.MaxSeconds, Time(5) + delay);
            var caption = string.Join("\n", lines.Skip(at + 1)).Trim();
            if (end <= start || string.IsNullOrWhiteSpace(Regex.Replace(caption, @"<[^>]*>|\{[^}]*\}", ""))) continue;
            result.Add(new(start, end, caption));
        }
        return result.OrderBy(x => x.Start).ThenBy(x => x.End).ToArray();
    }

    internal async Task<CaptureResult> ExportAsync(SubtitleVideo video, SubtitleTrack track, string picturesRoot, bool includeSubtitles, double delay, IProgress<CaptureProgress> progress, CancellationToken cancel)
    {
        if (video.VideoIndex < 0 || !video.Tracks.Contains(track)) throw new InvalidOperationException("캡처할 영상과 내장 텍스트 자막을 선택해 주세요.");
        if (string.IsNullOrWhiteSpace(picturesRoot) || !System.IO.Path.IsPathFullyQualified(picturesRoot)) throw new IOException("Windows 사진 폴더를 찾을 수 없습니다.");
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ShinPlayer-capture");
        var temp = System.IO.Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string? destination = null;
        var completed = new List<object>();
        var state = "failed";
        try
        {
            progress.Report(new(0, 0, null, "내장 자막 시간표를 읽는 중…"));
            var raw = System.IO.Path.Combine(temp, "extracted.srt");
            await CaptureTools.RunAsync(tools.Ffmpeg, new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-protocol_whitelist", "file,pipe", "-i", video.Path, "-map", $"0:{track.Index}", "-c:s", "srt", "-y", raw }, temp, cancel, 600);
            if (new FileInfo(raw).Length > 32 * 1024 * 1024) throw new IOException("자막 데이터가 너무 큽니다. 32 MB 이하의 자막을 지원합니다.");
            var cues = ParseSrt(await File.ReadAllTextAsync(raw, cancel), video.Duration, delay);
            if (cues.Count == 0) throw new InvalidOperationException("캡처할 수 있는 자막 구간이 없습니다.");
            await File.WriteAllTextAsync(System.IO.Path.Combine(temp, "captions.srt"), string.Join("\n\n", cues.Select((cue, i) => $"{i + 1}\n{Timestamp(cue.Start, true)} --> {Timestamp(cue.End, true)}\n{cue.Text}")) + "\n", new UTF8Encoding(false), cancel);
            cancel.ThrowIfCancellationRequested();
            destination = CreateOutputDirectory(picturesRoot, video.Path);
            for (var i = 0; i < cues.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                var cue = cues[i];
                progress.Report(new(i, cues.Count, destination, $"자막별 캡처 중 · {i + 1:N0} / {cues.Count:N0}"));
                var time = cue.Midpoint.ToString("0.######", CultureInfo.InvariantCulture);
                var filter = "scale=trunc(iw*sar/2)*2:ih,setsar=1";
                if (includeSubtitles) filter = $"setpts=PTS-STARTPTS+{time}/TB,{filter},subtitles=captions.srt:force_style='FontName=Malgun Gothic'";
                var frame = System.IO.Path.Combine(temp, "frame.png");
                await CaptureTools.RunAsync(tools.Ffmpeg, new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-threads", "2", "-filter_threads", "1", "-protocol_whitelist", "file,pipe", "-ss", time, "-i", video.Path, "-map", $"0:{video.VideoIndex}", "-an", "-sn", "-vf", filter, "-frames:v", "1", "-fps_mode", "passthrough", "-threads", "2", "-update", "1", "-y", frame }, temp, cancel);
                if (!File.Exists(frame) || new FileInfo(frame).Length < 32) throw new IOException($"{MediaFiles.Time(cue.Midpoint)}에서 영상 프레임을 읽을 수 없습니다.");
                cancel.ThrowIfCancellationRequested();
                var name = $"{i + 1:D5}_{Timestamp(cue.Midpoint, false)}.png";
                File.Move(frame, System.IO.Path.Combine(destination, name));
                completed.Add(new { file = name, start = cue.Start, end = cue.End, capture = cue.Midpoint, text = cue.Text });
                progress.Report(new(i + 1, cues.Count, destination, $"{i + 1:N0} / {cues.Count:N0}장 저장"));
            }
            state = "complete";
            return new(destination, completed.Count);
        }
        catch (OperationCanceledException) { state = "cancelled"; throw; }
        finally
        {
            try
            {
                if (destination != null)
                    await File.WriteAllTextAsync(System.IO.Path.Combine(destination, "캡처목록.json"), JsonSerializer.Serialize(new { state, video = System.IO.Path.GetFileName(video.Path), track = track.Label, includeSubtitles, subtitleDelay = delay, captures = completed }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            }
            finally { CaptureTools.RemovePrivateDirectory(temp, tempRoot); }
        }
    }

    internal static string CreateOutputDirectory(string root, string videoPath)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var name = new string(System.IO.Path.GetFileNameWithoutExtension(videoPath).Select(x => invalid.Contains(x) || char.IsControl(x) ? '_' : x).ToArray()).Trim().TrimEnd('.');
        if (name.Length > 70) name = name[..70];
        if (name.Length == 0) name = "영상";
        var stem = $"{DateTime.Now:yyyy-MM-dd}_{name}_사진";
        for (var number = 1; number < 10000; number++)
        {
            var path = System.IO.Path.Combine(root, stem + (number == 1 ? "" : $" ({number})"));
            if (Directory.Exists(path) || File.Exists(path)) continue;
            Directory.CreateDirectory(path);
            return path;
        }
        throw new IOException("사용 가능한 저장 폴더 이름을 만들 수 없습니다.");
    }
    private static string Timestamp(double seconds, bool srt)
    {
        var span = TimeSpan.FromMilliseconds(Math.Round(seconds * 1000));
        return srt ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00},{span.Milliseconds:000}" : $"{(int)span.TotalHours:00}-{span.Minutes:00}-{span.Seconds:00}-{span.Milliseconds:000}";
    }
}
