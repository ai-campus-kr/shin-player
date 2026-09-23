using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ShinPlayer;

internal sealed class ShortsColorPicker : StackPanel
{
    internal readonly TextBox HexInput = new() { Text = "#FFFFFF", MaxLength = 7, Width = 100, FontFamily = new("Consolas"), FontSize = 13, Padding = new(8, 5, 8, 5) };
    private readonly List<(string Hex, Border Swatch)> _swatches = new();
    internal event Action? Changed;
    internal string Value => HexInput.Text.Trim().ToUpperInvariant();
    internal static readonly (string Name, string Hex)[] Colors =
    [
        ("흰색", "#FFFFFF"), ("검정", "#17171A"), ("노랑", "#FFE03B"), ("주황", "#FFB21A"), ("다홍", "#FF5A36"), ("빨강", "#F02D3A"),
        ("핫핑크", "#FF3B6B"), ("분홍", "#F973C9"), ("연보라", "#C77DFF"), ("보라", "#603BFF"), ("파랑", "#477BFF"), ("하늘", "#00B5FF"),
        ("청록", "#25D0D8"), ("민트", "#55E3C2"), ("초록", "#03C75A"), ("라임", "#B7FF00"), ("연회색", "#CBD5E1"), ("크림", "#FFF2D1")
    ];
    internal ShortsColorPicker()
    {
        var palette = new System.Windows.Controls.Primitives.UniformGrid { Columns = 9 };
        foreach (var (name, hex) in Colors)
        {
            var swatch = new Border { Width = 22, Height = 22, Background = UiDesigns.Brush(hex), CornerRadius = new(5), BorderBrush = Brushes.Gray, BorderThickness = new(1) };
            var button = new Button { Content = swatch, Padding = new(3), MinHeight = 30, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 0, 3, 4), ToolTip = name + " " + hex };
            AutomationProperties.SetName(button, "글자 " + name); button.Click += (_, _) => HexInput.Text = hex;
            _swatches.Add((hex, swatch)); palette.Children.Add(button);
        }
        Children.Add(palette);
        var custom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 3, 0, 8) };
        AutomationProperties.SetName(HexInput, "직접 입력할 글자색 HEX"); custom.Children.Add(HexInput);
        var choose = new Button { Content = "색상 직접 선택…", FontSize = 12, Padding = new(8, 5, 8, 5), Margin = new(8, 0, 0, 0) };
        choose.Click += (_, _) => Choose(); custom.Children.Add(choose); Children.Add(custom);
        HexInput.TextChanged += (_, _) => { UpdateSwatches(); Changed?.Invoke(); }; UpdateSwatches();
    }
    private void UpdateSwatches()
    {
        foreach (var (hex, swatch) in _swatches)
        {
            swatch.BorderThickness = new(hex == Value ? 3 : 1);
            swatch.BorderBrush = hex == Value ? UiDesigns.Brush("#777777") : Brushes.Gray;
        }
    }
    private void Choose()
    {
        var owner = Window.GetWindow(this); if (owner == null) return;
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
        if (ShortsOptions.IsTextColor(Value)) dialog.Color = System.Drawing.ColorTranslator.FromHtml(Value);
        if (dialog.ShowDialog(new ColorOwner(new WindowInteropHelper(owner).Handle)) == System.Windows.Forms.DialogResult.OK)
            HexInput.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }
    private sealed record ColorOwner(IntPtr Handle) : System.Windows.Forms.IWin32Window;
}
