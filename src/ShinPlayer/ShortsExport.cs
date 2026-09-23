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

// Ratios refer to the upright, square-pixel source, before fitting to the output canvas.
internal sealed record ShortsCrop(double X = 0, double Y = 0, double Width = 1, double Height = 1)
{
    internal static readonly ShortsCrop Full = new();
    internal void Validate()
    {
        if (new[] { X, Y, Width, Height }.Any(v => !double.IsFinite(v)) || X < 0 || Y < 0 ||
            Width < .02 - 1e-9 || Height < .02 - 1e-9 || X + Width > 1.000001 || Y + Height > 1.000001)
            throw new InvalidOperationException("영상 안에서 가로·세로 2% 이상의 영역을 선택하세요.");
    }
}

// All composition coordinates use a 1080 × 1920 canvas, independent of display DPI.
internal sealed record ShortsOptions(double Start, double End, int Width = 1080, ShortsFit Fit = ShortsFit.Fill,
    double PanX = .5, double PanY = .5, bool Audio = true, string Text = "", double FontSize = 64,
    string TextColor = "#FFFFFF", bool TextBox = true, double TextX = .5, double TextY = .18,
    string FontId = ShortsFonts.DefaultId, double Zoom = 1, ShortsCrop? Crop = null)
{
    internal const int CanvasWidth = 1080, CanvasHeight = 1920;
    internal double Length => End - Start;
    internal int Height => Width * 16 / 9;
    internal ShortsCrop SourceCrop => Crop ?? ShortsCrop.Full;
    internal void Validate(double duration)
    {
        if (!double.IsFinite(duration) || duration < .2 || !double.IsFinite(Start) || !double.IsFinite(End) ||
            Start < 0 || End > duration + .001 || Length < .2 - .000001 || Length > 180 + .000001)
            throw new InvalidOperationException("영상 안에서 0.2초~3분 구간을 선택하세요.");
        if (Width is not (720 or 1080) || !Enum.IsDefined(Fit)) throw new InvalidOperationException("저장 크기와 화면 맞춤을 확인하세요.");
        if (new[] { PanX, PanY, TextX, TextY }.Any(x => !double.IsFinite(x) || x < 0 || x > 1))
            throw new InvalidOperationException("영상과 글자 위치를 확인하세요.");
        if (!double.IsFinite(FontSize) || FontSize < 32 || FontSize > 100 || Text.Length > 160 ||
            Text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
            throw new InvalidOperationException("문구는 160자 이하, 글자 크기는 32~100으로 설정하세요.");
        if (!IsTextColor(TextColor)) throw new InvalidOperationException("글자색을 고르거나 #RRGGBB 형식으로 입력하세요. 예: #FF5A36");
        if (!double.IsFinite(Zoom) || Zoom < 1 || Zoom > 4) throw new InvalidOperationException("영상 확대는 100~400%로 설정하세요.");
        SourceCrop.Validate(); ShortsFonts.Get(FontId);
    }
    internal static bool IsTextColor(string? value) => value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
}

internal sealed record ShortsLabel(BitmapSource Bitmap, Rect Bounds);

internal static class ShortsComposition
{
    internal static Rect VideoBounds(double sourceWidth, double sourceHeight, ShortsOptions options)
    {
        var crop = options.SourceCrop; sourceWidth *= crop.Width; sourceHeight *= crop.Height;
        double sx = ShortsOptions.CanvasWidth / sourceWidth, sy = ShortsOptions.CanvasHeight / sourceHeight;
        double scale = (options.Fit == ShortsFit.Fill ? Math.Max(sx, sy) : Math.Min(sx, sy)) * options.Zoom;
        double w = sourceWidth * scale, h = sourceHeight * scale;
        return new((ShortsOptions.CanvasWidth - w) * options.PanX, (ShortsOptions.CanvasHeight - h) * options.PanY, w, h);
    }

    internal static Rect LabelBounds(double width, double height, ShortsOptions options) => new(
        Math.Clamp(options.TextX * ShortsOptions.CanvasWidth - width / 2, 42, ShortsOptions.CanvasWidth - width - 42),
        Math.Clamp(options.TextY * ShortsOptions.CanvasHeight - height / 2, 80, ShortsOptions.CanvasHeight - height - 80), width, height);

    internal static ShortsLabel? RenderLabel(ShortsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Text)) return null;
        var font = ShortsFonts.Get(options.FontId);
        var text = new TextBlock
        {
            Text = options.Text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ').Trim(),
            FontFamily = font.Family, FontWeight = font.Weight, FontSize = options.FontSize,
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
            var crop = options.SourceCrop;
            string geometry = crop == ShortsCrop.Full ? "" : $"crop=w='iw*{N(crop.Width)}':h='ih*{N(crop.Height)}':x='iw*{N(crop.X)}':y='ih*{N(crop.Y)}':exact=1,";
            if (options.Fit == ShortsFit.Fill)
            {
                // Crop before upscaling: a thin custom region must not allocate a gigantic intermediate frame.
                geometry += $"crop=w='max(1,min(iw,ih*9/16)/{N(options.Zoom)})':h='max(1,min(ih,iw*16/9)/{N(options.Zoom)})':" +
                    $"x='(iw-ow)*{N(options.PanX)}':y='(ih-oh)*{N(options.PanY)}':exact=1,scale=1080:1920";
            }
            else
            {
                // Contain has a bounded intermediate of at most 4320 × 7680 at 400%.
                geometry += $"scale={N(1080 * options.Zoom)}:{N(1920 * options.Zoom)}:force_original_aspect_ratio=decrease:force_divisible_by=2," +
                    $"crop=w='min(iw,1080)':h='min(ih,1920)':x='(iw-ow)*{N(options.PanX)}':y='(ih-oh)*{N(options.PanY)}'," +
                    $"pad=1080:1920:x='(ow-iw)*{N(options.PanX)}':y='(oh-ih)*{N(options.PanY)}':color=black";
            }
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
            string safe = ExportFiles.SafeName(Path.GetFileNameWithoutExtension(video.Path));
            string stem = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safe}_{N(options.Start)}-{N(options.End)}_쇼츠";
            return ExportFiles.Commit(partial, folder, stem, ".mp4");
        }
        finally
        {
            if (partial != null && File.Exists(partial)) File.Delete(partial);
            CaptureTools.RemovePrivateDirectory(temp, TempRoot);
        }
    }
}
