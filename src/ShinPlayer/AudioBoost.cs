using System;
using System.Globalization;

namespace ShinPlayer;

internal static class AudioBoost
{
    internal const string Label = "shin-boost";
    internal const double MaximumDb = 12;
    internal static readonly (string Name, double Db)[] Presets =
    {
        ("끄기 · 원래 음량", 0), ("약하게 · +3 dB", 3), ("보통 · +6 dB", 6),
        ("강하게 · +9 dB", 9), ("최대 · +12 dB", 12)
    };

    internal static double Normalize(double db) => double.IsFinite(db) ? Math.Round(Math.Clamp(db, 0, MaximumDb), 1) : 0;
    // Limiting happens after gain and before mpv's normal volume control (0–100).
    // Disable automatic makeup gain and compensate the 5 ms lookahead delay.
    internal static string Filter(double db) => $"@{Label}:lavfi=[volume={Normalize(db).ToString("0.#", CultureInfo.InvariantCulture)}dB,alimiter=limit=0.95:level=false:attack=5:release=50:latency=true]";
}
