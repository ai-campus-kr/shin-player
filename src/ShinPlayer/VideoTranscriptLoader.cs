using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ShinPlayer;

internal static class VideoTranscriptLoader
{
    internal static async Task<VideoTranscript> ExtractAsync(CaptureTools tools, SubtitleVideo video, SubtitleTrack track, double delay, CancellationToken cancel)
    {
        if (!video.Tracks.Any(t => t.Index == track.Index)) throw new InvalidOperationException("이 영상의 내장 텍스트 자막만 사용할 수 있습니다.");
        var tempRoot = Path.Combine(Path.GetTempPath(), "ShinPlayer-chat");
        var temp = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var file = Path.Combine(temp, "captions.srt");
            await CaptureTools.RunAsync(tools.Ffmpeg, new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-protocol_whitelist", "file,pipe", "-i", video.Path, "-map", $"0:{track.Index}", "-c:s", "srt", "-y", file }, temp, cancel, 120);
            if (new FileInfo(file).Length > 32 * 1024 * 1024) throw new IOException("자막이 너무 큽니다. 32 MB 이하를 지원합니다.");
            var cues = SubtitleCapture.ParseSrt(await File.ReadAllTextAsync(file, cancel), video.Duration, delay)
                .Select(c => c with { Text = WebUtility.HtmlDecode(Regex.Replace(c.Text, @"<[^>]*>|\{[^}]*\}", "")) })
                .Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToArray();
            if (cues.Length == 0) throw new InvalidOperationException("읽을 수 있는 자막 구간이 없습니다.");
            return new(video.Path, Path.GetFileName(video.Path) + " · " + track.Label, cues);
        }
        finally { CaptureTools.RemovePrivateDirectory(temp, tempRoot); }
    }
}
