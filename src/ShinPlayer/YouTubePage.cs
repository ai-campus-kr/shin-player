using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShinPlayer;

internal static class YouTubePage
{
    internal static bool IsYouTube(Uri uri) => uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) && uri.IsDefaultPort &&
        uri.Host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be";

    internal static bool TryNormalizeAddress(string input, out string url)
    {
        input = input.Trim();
        if (!input.Contains("://", StringComparison.Ordinal)) input = "https://" + input;
        url = "";
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || !IsYouTube(uri)) return false;
        url = uri.AbsoluteUri;
        return true;
    }

    internal static string VideoId(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !IsYouTube(uri)) return "";
        string id = "";
        if (uri.Host == "youtu.be") id = uri.AbsolutePath.Trim('/');
        else if (uri.AbsolutePath is "/watch")
            id = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).FirstOrDefault(p => p.Length == 2 && p[0] == "v")?.Last() ?? "";
        else
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length == 2 && parts[0] is "shorts" or "live" or "embed") id = parts[1];
        }
        return Regex.IsMatch(id, @"^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant) ? id : "";
    }
    internal static bool TryNormalize(string input, out string url)
    {
        input = input.Trim();
        if (input.StartsWith("www.youtube.com/", StringComparison.OrdinalIgnoreCase) || input.StartsWith("youtu.be/", StringComparison.OrdinalIgnoreCase))
            input = "https://" + input;
        var id = VideoId(input);
        url = id.Length == 0 ? "" : "https://www.youtube.com/watch?v=" + id;
        if (url.Length == 0) return false;
        var uri = new Uri(input);
        var time = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
            .FirstOrDefault(p => p.Length == 2 && p[0] is "t" or "start")?.Last() ?? "";
        if (Regex.IsMatch(time, @"^\d{1,7}(s)?$")) url += "&t=" + time;
        return true;
    }

    // Read only the transcript rendered by YouTube's normal page. No private API,
    // stream URL, cookies, browser profile, or network response is extracted.
    internal const string ReadTranscriptScript = """
    (() => {
      const url = new URL(location.href);
      const id = url.searchParams.get('v') || url.pathname.split('/')[2] || '';
      const watch = document.querySelector('ytd-watch-flexy');
      if (watch && watch.getAttribute('video-id') && watch.getAttribute('video-id') !== id)
        return {error:'영상 전환 중입니다. 잠시 후 다시 시도하세요.'};
      const panel = [...document.querySelectorAll('ytd-engagement-panel-section-list-renderer')]
        .find(e => e.getAttribute('target-id') === 'engagement-panel-searchable-transcript' &&
          e.getAttribute('visibility') === 'ENGAGEMENT_PANEL_VISIBILITY_EXPANDED');
      if (!panel) return {error:'유튜브 영상 설명의 더보기 → 스크립트 표시를 먼저 열어주세요. 스크립트가 없는 영상은 AI 검색을 사용할 수 없습니다.'};
      const segments = [...panel.querySelectorAll('ytd-transcript-segment-renderer, .ytwTranscriptSegmentViewModelHost')];
      if (segments.length > 20000) return {error:'자막이 20,000개를 넘습니다. 이 스크립트는 지원 범위를 벗어났습니다.'};
      const parseTime = value => {
        const parts = value.trim().split(':').map(Number);
        return parts.length >= 2 && parts.length <= 3 && parts.every(Number.isFinite)
          ? parts.reduce((total,n) => total*60+n,0) : -1;
      };
      const cues = segments.map(e => {
        const time = e.querySelector('.segment-timestamp, .ytwTranscriptSegmentViewModelTimestamp');
        const text = e.querySelector('.segment-text, .ytwTranscriptSegmentViewModelText');
        return {start:parseTime(time?.textContent || ''), text:(text?.textContent || '').trim()};
      }).filter(c => c.start >= 0 && c.text);
      if (!cues.length) return {error:'현재 스크립트를 읽지 못했습니다. 스크립트가 표시되는지 확인하세요. 유튜브 화면 구조가 변경된 경우에는 업데이트가 필요합니다.'};
      const language = panel.querySelector('ytd-transcript-footer-renderer button, #footer button')?.textContent?.trim() || '선택된 스크립트 언어';
      return {videoId:id,title:document.querySelector('h1.ytd-watch-metadata')?.textContent?.trim() || document.title,language,cues};
    })()
    """;

    internal static VideoTranscript ParseTranscript(string json, string expectedId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetString());
        if (root.GetProperty("videoId").GetString() != expectedId) throw new InvalidOperationException("영상이 바뀌었습니다. 현재 영상의 스크립트를 다시 불러오세요.");
        var rows = root.GetProperty("cues").EnumerateArray()
            .Select(row => new { Start = row.GetProperty("start").GetDouble(), Text = row.GetProperty("text").GetString() ?? "" })
            .Where(row => double.IsFinite(row.Start) && row.Start >= 0 && row.Start < MediaFiles.MaxSeconds && !string.IsNullOrWhiteSpace(row.Text))
            .OrderBy(row => row.Start).ToArray();
        if (rows.Length == 0 || rows.Length > 20000 || rows.Sum(r => (long)r.Text.Length) > 4_000_000)
            throw new InvalidOperationException("스크립트 크기가 지원 범위를 벗어났습니다.");
        var cues = rows.Select((row, i) => new SubtitleCue(row.Start, i + 1 < rows.Length ? Math.Max(row.Start + .1, rows[i + 1].Start) : row.Start + 5, row.Text)).ToArray();
        var title = root.GetProperty("title").GetString() ?? "YouTube";
        var language = root.GetProperty("language").GetString() ?? "";
        return new("https://www.youtube.com/watch?v=" + expectedId, (title.Length > 300 ? title[..300] : title) + " · " + (language.Length > 80 ? language[..80] : language), cues);
    }

    internal static string SeekScript(string id, double seconds)
    {
        if (!Regex.IsMatch(id, @"^[A-Za-z0-9_-]{11}$") || !double.IsFinite(seconds) || seconds < 0) throw new ArgumentException("유효하지 않은 재생 위치입니다.");
        return """
        (() => {
          const url = new URL(location.href);
          const id = url.searchParams.get('v') || url.pathname.split('/')[2] || '';
          if (id !== ID_VALUE) return '영상이 바뀌었습니다. 스크립트를 다시 불러오세요.';
          const watch = document.querySelector('ytd-watch-flexy');
          if (watch?.getAttribute('video-id') && watch.getAttribute('video-id') !== id)
            return '영상 전환 중입니다. 잠시 후 다시 시도하세요.';
          const player = document.querySelector('#movie_player');
          if (player?.classList.contains('ad-showing') || player?.classList.contains('ad-interrupting'))
            return '광고가 끝난 뒤 결과의 시간 버튼을 다시 눌러주세요.';
          const video = player?.querySelector('video');
          if (!video || !Number.isFinite(video.duration) || video.duration <= 0)
            return '먼저 유튜브에서 영상을 재생해 주세요.';
          const time = TIME_VALUE;
          if (time >= video.duration) return '현재 영상의 길이를 벗어난 위치입니다.';
          video.currentTime = time;
          video.play().catch(() => {});
          return '';
        })()
        """.Replace("ID_VALUE", JsonSerializer.Serialize(id)).Replace("TIME_VALUE", seconds.ToString("0.###", CultureInfo.InvariantCulture));
    }
}
