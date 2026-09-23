using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ShinPlayer;

internal sealed record ShortsFont(string Id, string Name, string FamilyName, FontWeight Weight)
{
    internal FontFamily Family { get; } = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#" + FamilyName);
    public override string ToString() => Name;
}

internal static class ShortsFonts
{
    internal const string DefaultId = "pretendard";
    internal static readonly ShortsFont[] All =
    [
        new(DefaultId, "프리텐다드 · 또렷한 기본", "Pretendard", FontWeights.Bold),
        new("black-han", "검은고딕 · 강한 제목", "Black Han Sans", FontWeights.Normal),
        new("dohyeon", "도현 · 단단한 제목", "Do Hyeon", FontWeights.Normal),
        new("jua", "주아 · 둥근 글씨", "Jua", FontWeights.Normal),
        new("myeongjo", "나눔명조 · 차분한 문장", "NanumMyeongjo", FontWeights.Bold),
        new("gaegu", "개구 · 손글씨", "Gaegu", FontWeights.Bold)
    ];

    internal static ShortsFont Get(string id) => All.FirstOrDefault(f => f.Id == id)
        ?? throw new InvalidOperationException("문구에 사용할 글꼴을 선택하세요.");
}
