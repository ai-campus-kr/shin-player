using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

internal enum ShortsFit { Fill, Contain }

// All composition coordinates use a 1080 × 1920 canvas, independent of display DPI.
internal sealed record ShortsOptions(double Start, double End, int Width = 1080, ShortsFit Fit = ShortsFit.Fill,
    double PanX = .5, double PanY = .5, bool Audio = true, string Text = "", double FontSize = 64,
    string TextColor = "#FFFFFF", bool TextBox = true, double TextX = .5, double TextY = .18)
{
    internal const int CanvasWidth = 1080, CanvasHeight = 1920;
    internal double Length => End - Start;
    internal int Height => Width * 16 / 9;
    internal void Validate(double duration)
    {
        if (!double.IsFinite(duration) || duration < .2 || !double.IsFinite(Start) || !double.IsFinite(End) ||
            Start < 0 || End > duration + .001 || Length < .2 - .000001 || Length > 180 + .000001)
            throw new InvalidOperationException("영상 안에서 0.2초~3분 구간을 선택하세요.");
        if (Width is not (720 or 1080) || !Enum.IsDefined(Fit)) throw new InvalidOperationException("저장 크기와 화면 맞춤을 확인하세요.");
        if (new[] { PanX, PanY, TextX, TextY }.Any(x => !double.IsFinite(x) || x < 0 || x > 1))
            throw new InvalidOperationException("영상과 글자 위치를 확인하세요.");
        if (!double.IsFinite(FontSize) || FontSize < 32 || FontSize > 100 || Text.Length > 160 ||
            Text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')) ||
            TextColor is not ("#FFFFFF" or "#FFE03B" or "#55E3C2"))
            throw new InvalidOperationException("문구는 160자 이하, 글자 크기는 32~100으로 설정하세요.");
    }
}

internal sealed record ShortsLabel(BitmapSource Bitmap, Rect Bounds);

internal static class ShortsComposition
{
    internal static Rect VideoBounds(double sourceWidth, double sourceHeight, ShortsOptions options)
    {
        double sx = ShortsOptions.CanvasWidth / sourceWidth, sy = ShortsOptions.CanvasHeight / sourceHeight;
        double scale = options.Fit == ShortsFit.Fill ? Math.Max(sx, sy) : Math.Min(sx, sy);
        double w = sourceWidth * scale, h = sourceHeight * scale;
        return options.Fit == ShortsFit.Fill
            ? new(-(w - ShortsOptions.CanvasWidth) * options.PanX, -(h - ShortsOptions.CanvasHeight) * options.PanY, w, h)
            : new((ShortsOptions.CanvasWidth - w) / 2, (ShortsOptions.CanvasHeight - h) / 2, w, h);
    }

    internal static Rect LabelBounds(double width, double height, ShortsOptions options) => new(
        Math.Clamp(options.TextX * ShortsOptions.CanvasWidth - width / 2, 42, ShortsOptions.CanvasWidth - width - 42),
        Math.Clamp(options.TextY * ShortsOptions.CanvasHeight - height / 2, 80, ShortsOptions.CanvasHeight - height - 80), width, height);

    internal static ShortsLabel? RenderLabel(ShortsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Text)) return null;
        var text = new TextBlock
        {
            Text = options.Text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ').Trim(),
            FontFamily = new FontFamily("Malgun Gothic"), FontWeight = FontWeights.Bold, FontSize = options.FontSize,
            LineHeight = options.FontSize * 1.3, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            Foreground = UiDesigns.Brush(options.TextColor), MaxWidth = 900
        };
        text.Measure(new Size(900, double.PositiveInfinity));
        if (text.DesiredSize.Height > options.FontSize * 1.3 * 4 + 1)
            throw new InvalidOperationException("문구가 네 줄을 넘습니다. 글자를 줄이거나 크기를 낮춰 주세요.");
        var border = new Border
        {
            Child = text, Padding = new Thickness(24, 16, 24, 20), CornerRadius = new CornerRadius(18),
            Background = options.TextBox ? new SolidColorBrush(Color.FromArgb(205, 0, 0, 0)) : Brushes.Transparent
        };
        border.Measure(new Size(948, double.PositiveInfinity));
        int width = (int)Math.Ceiling(border.DesiredSize.Width), height = (int)Math.Ceiling(border.DesiredSize.Height);
        border.Arrange(new Rect(0, 0, width, height)); border.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(border); bitmap.Freeze();
        return new(bitmap, LabelBounds(width, height, options));
    }

    internal static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    internal static BitmapSource LoadPng(string path)
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
}

internal static class ShortsExport
{
    internal static string TempRoot => Path.Combine(Path.GetTempPath(), "ShinPlayer-shorts");
    internal static string VideosDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    private static string N(double number) => number.ToString("0.######", CultureInfo.InvariantCulture);
    private static string CreateTemp()
    {
        string path = Path.Combine(TempRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path;
    }

    internal static async Task<BitmapSource> FrameAsync(CaptureTools tools, SubtitleVideo video, double position, CancellationToken cancel)
    {
        string temp = CreateTemp();
        try
        {
            string frame = Path.Combine(temp, "frame.png");
            await CaptureTools.RunAsync(tools.Ffmpeg,
                ["-hide_banner", "-loglevel", "error", "-nostdin", "-threads", "2", "-filter_threads", "1",
                 "-protocol_whitelist", "file,pipe", "-ss", N(Math.Clamp(position, 0, Math.Max(0, video.Duration - .05))), "-i", video.Path,
                 "-map", $"0:{video.VideoIndex}", "-an", "-sn", "-vf", "scale=trunc(iw*sar/2)*2:ih,setsar=1,scale=1280:1280:force_original_aspect_ratio=decrease",
                 "-frames:v", "1", "-update", "1", "-y", frame], temp, cancel);
            cancel.ThrowIfCancellationRequested();
            if (!File.Exists(frame)) throw new InvalidOperationException("이 위치의 장면을 읽을 수 없습니다. 미리보기 위치를 조금 앞당겨 주세요.");
            return ShortsComposition.LoadPng(frame);
        }
        finally { CaptureTools.RemovePrivateDirectory(temp, TempRoot); }
    }

    internal static async Task<string> ExportAsync(CaptureTools tools, SubtitleVideo video, ShortsOptions options,
        string videosRoot, IProgress<string> progress, CancellationToken cancel)
    {
        options.Validate(video.Duration);
        if (video.VideoIndex < 0 || !File.Exists(video.Path)) throw new InvalidOperationException("쇼츠로 만들 로컬 영상 파일을 찾을 수 없습니다.");
        if (!Path.IsPathFullyQualified(videosRoot)) throw new IOException("Windows 동영상 폴더를 찾을 수 없습니다.");
        cancel.ThrowIfCancellationRequested();
        var label = ShortsComposition.RenderLabel(options);
        string temp = CreateTemp(), folder = Path.Combine(videosRoot, "신플레이어 쇼츠");
        string? partial = null;
        try
        {
            Directory.CreateDirectory(folder);
            var args = new System.Collections.Generic.List<string>
            {
                "-hide_banner", "-loglevel", "error", "-nostdin", "-threads", "2", "-filter_complex_threads", "1",
                "-protocol_whitelist", "file,pipe", "-ss", N(options.Start), "-t", N(options.Length), "-i", video.Path
            };
            if (label != null)
            {
                ShortsComposition.SavePng(label.Bitmap, Path.Combine(temp, "label.png"));
                args.AddRange(["-loop", "1", "-framerate", "30", "-i", "label.png"]);
            }
            string geometry = options.Fit == ShortsFit.Fill
                ? $"scale=1080:1920:force_original_aspect_ratio=increase:force_divisible_by=2,crop=1080:1920:x='(iw-ow)*{N(options.PanX)}':y='(ih-oh)*{N(options.PanY)}'"
                : "scale=1080:1920:force_original_aspect_ratio=decrease:force_divisible_by=2,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:color=black";
            string filter = $"[0:{video.VideoIndex}]setpts=PTS-STARTPTS,scale=trunc(iw*sar/2)*2:ih,setsar=1,{geometry},fps=30[base];";
            filter += label == null ? "[base]" : $"[base][1:v]overlay=x={N(label.Bounds.X)}:y={N(label.Bounds.Y)}:shortest=1:format=auto,";
            filter += $"scale={options.Width}:{options.Height},setsar=1,format=yuv420p[v]";
            args.AddRange(["-filter_complex", filter, "-map", "[v]"]);
            // Preserve the audio's offset relative to the seek point; first_pts pads delayed audio.
            if (options.Audio) args.AddRange(["-map", "0:a:0?", "-af", "aresample=async=1:first_pts=0,apad", "-c:a", "aac", "-b:a", "160k"]);
            else args.Add("-an");
            partial = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".partial");
            args.AddRange(["-sn", "-map_metadata", "-1", "-map_chapters", "-1", "-t", N(options.Length), "-c:v", "libx264",
                "-preset", "fast", "-crf", "20", "-threads", "2", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-f", "mp4", "-y", partial]);
            progress.Report("세로 영상으로 저장하는 중… 길이에 따라 시간이 걸릴 수 있습니다.");
            await CaptureTools.RunAsync(tools.Ffmpeg, args.ToArray(), temp, cancel, 1800);
            cancel.ThrowIfCancellationRequested();
            if (!File.Exists(partial) || new FileInfo(partial).Length < 100) throw new IOException("완성된 쇼츠 파일을 확인할 수 없습니다.");
            string safe = new(Path.GetFileNameWithoutExtension(video.Path).Where(c => !Path.GetInvalidFileNameChars().Contains(c) && !char.IsControl(c)).Take(65).ToArray());
            safe = safe.Trim().TrimEnd('.'); if (safe.Length == 0) safe = "영상";
            string stem = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safe}_{N(options.Start)}-{N(options.End)}_쇼츠";
            for (int suffix = 0; ; suffix++)
            {
                string target = Path.Combine(folder, stem + (suffix == 0 ? "" : $"_{suffix}") + ".mp4");
                try { File.Move(partial, target, false); return target; }
                catch (IOException) when (File.Exists(target)) { }
            }
        }
        finally
        {
            if (partial != null && File.Exists(partial)) File.Delete(partial);
            CaptureTools.RemovePrivateDirectory(temp, TempRoot);
        }
    }
}
