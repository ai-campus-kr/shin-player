using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShinPlayer;

public sealed record MediaItem(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string Extension => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
}

public sealed class PlayerSettings
{
    public double Volume { get; set; } = 70;
    public double Speed { get; set; } = 1;
    public bool Muted { get; set; }
    public bool Resume { get; set; } = true;
    public bool PitchCorrection { get; set; } = true;
    public bool HardwareDecode { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool RememberSpeed { get; set; } = true;
    public double Width { get; set; } = 1180;
    public double Height { get; set; } = 760;
    public List<RecentFile> Recent { get; set; } = new();
    public static string DirectoryPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShinPlayer");
    public static string FilePath => System.IO.Path.Combine(DirectoryPath, "settings.json");

    public static PlayerSettings Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<PlayerSettings>(File.ReadAllText(FilePath)) ?? new();
            value.Normalize();
            return value;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Normalize()
    {
        Volume = Clamp(Volume, 0, 100, 70);
        Speed = Clamp(Speed, .25, 8, 1);
        Width = Clamp(Width, 820, 3840, 1180);
        Height = Clamp(Height, 560, 2160, 760);
        Recent = (Recent ?? new()).Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Path))
            .DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Take(40)
            .Select(x => x with { Position = Clamp(x.Position, 0, MediaFiles.MaxSeconds, 0) }).ToList();
    }
    private static double Clamp(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
    public void Remember(string path, double position, double duration)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!double.IsFinite(position) || !double.IsFinite(duration) || position > MediaFiles.MaxSeconds) position = 0;
        Recent.RemoveAll(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
        Recent.Insert(0, new(path, position >= 5 && duration - position > 5 ? position : 0, DateTimeOffset.UtcNow));
        if (Recent.Count > 40) Recent.RemoveRange(40, Recent.Count - 40);
    }
}

public sealed record RecentFile(string Path, double Position, DateTimeOffset LastOpened);
public static class MediaFiles
{
    public const double MaxSeconds = 3155760000; // 100 years, safely representable for display.
    public static readonly string[] VideoExtensions = ".mp4 .mkv .avi .mov .wmv .webm .m4v .mpg .mpeg .ts .mts .m2ts .flv .vob .ogv .3gp .3g2 .asf .divx .m2v .mpe .mpv .mxf .rm .rmvb .f4v .hevc .h264 .h265 .av1".Split(' ');
    public static readonly string[] AudioExtensions = ".mp3 .flac .wav .m4a .aac .ogg .opus .wma .aiff .ape".Split(' ');
    public static bool IsMedia(string path) => VideoExtensions.Concat(AudioExtensions).Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static bool IsSubtitle(string path) => new[] { ".srt", ".ass", ".ssa", ".smi", ".vtt", ".sub", ".sup" }.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static string Time(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) seconds = 0;
        var span = TimeSpan.FromSeconds(Math.Min(seconds, MaxSeconds));
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
    }
}
