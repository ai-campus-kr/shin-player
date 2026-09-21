using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ShinPlayer;

internal sealed record UiDesign(string Id, string Name, string Description, string Bg, string Panel,
    string Raised, string Line, string Ink, string Muted, string Accent, string AccentInk,
    string Stage, string Selected, double Radius, double HeaderHeight, string Heading);

internal static class UiDesigns
{
    internal const string DefaultId = "minimal";
    internal static IReadOnlyList<UiDesign> All { get; } = new[]
    {
        new UiDesign("minimal", "미니멀", "모노톤 · 넓은 화면 · 하단 타임라인",
            "#121214", "#121214", "#252528", "#323237", "#EEEEF1", "#A5A5AE", "#E2E2E8", "#171719",
            "#0E0E10", "#303035", 6, 52, "열고, 바로 재생."),
        new UiDesign("studio", "스튜디오", "촘촘한 컨트롤 · 왼쪽 재생목록 · 타임코드",
            "#181B20", "#20242A", "#2C3139", "#3C424B", "#EDF0F4", "#ACB3BF", "#DDBB83", "#272019",
            "#111418", "#423A2D", 3, 48, "재생할 영상을 선택하세요."),
        new UiDesign("light", "라이트", "밝은 화면 · 부드러운 여백 · 큰 재생 버튼",
            "#F0F2F5", "#FFFFFF", "#E5E9F0", "#CBD2DD", "#202733", "#566274", "#315FC1", "#FFFFFF",
            "#FFFFFF", "#DCE7FF", 10, 62, "오늘의 영상을 열어보세요."),
        new UiDesign("lime", "라임", "기존 UI · 라임 포인트 · 카드형 컨트롤",
            "#101211", "#191C19", "#242923", "#333A31", "#F2F4EE", "#A1AA9B", "#CEF480", "#202A12",
            "#0E120D", "#2A3521", 10, 64, "당신의 영상,\n원하는 속도로.")
    };

    internal static UiDesign Get(string? id) => All.FirstOrDefault(x => x.Id == id) ?? All[0];
    internal static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));

    internal static void ApplyPalette(UiDesign design)
    {
        var resources = Application.Current.Resources;
        foreach (var (key, color) in new[] {
            ("Bg", design.Bg), ("Panel", design.Panel), ("Raised", design.Raised), ("Line", design.Line),
            ("Ink", design.Ink), ("Muted", design.Muted), ("Accent", design.Accent), ("AccentInk", design.AccentInk),
            ("StageBrush", design.Stage), ("Selected", design.Selected), ("HoverBrush", design.Ink),
            ("SuccessInk", design.Id == "light" ? "#087A40" : "#35D580"),
            ("WarningInk", design.Id == "light" ? "#A84E00" : "#FFBA66"),
            ("GifRangeAccent", design.Id == "minimal" ? "#66C9BB" : design.Accent),
            ("GifRangeFill", design.Id == "minimal" ? "#244C48" : design.Selected),
            ("GifRangeInk", design.Id == "minimal" ? "#CEF3EB" : design.Ink),
            ("GifRangeAccentInk", design.Id == "minimal" ? "#12332D" : design.AccentInk) })
            resources[key] = Brush(color);
        resources["ButtonRadius"] = new CornerRadius(design.Radius);
    }
}
