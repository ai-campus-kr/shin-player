using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShinPlayer;

internal sealed class DesignPickerWindow : Window
{
    private readonly Dictionary<string, (Border Card, TextBlock Selected)> _cards = new();
    private readonly Action<string> _apply;
    internal string SelectedId { get; private set; }

    internal DesignPickerWindow(string selectedId, Action<string> apply)
    {
        _apply = apply; SelectedId = selectedId;
        Style = (Style)FindResource(typeof(Window));
        Title = "UI 선택 · 신플레이어";
        Width = 780; Height = 660; MinWidth = 660; MinHeight = 510;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        heading.Children.Add(new TextBlock { Text = "나에게 맞는 플레이어", FontSize = 24, FontWeight = FontWeights.SemiBold });
        var description = new TextBlock { Text = "색감부터 컨트롤 배치까지, 원하는 화면을 선택하세요.", Margin = new Thickness(0, 7, 0, 0) };
        description.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); heading.Children.Add(description); root.Children.Add(heading);
        var choices = new UniformGrid { Columns = 2, Rows = 2 };
        foreach (var design in UiDesigns.All)
        {
            var body = new StackPanel();
            body.Children.Add(new Viewbox { Child = Thumbnail(design), Height = 126, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 12) });
            var title = new DockPanel();
            var selected = new TextBlock { Text = "✓ 사용 중", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
            selected.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); DockPanel.SetDock(selected, Dock.Right); title.Children.Add(selected);
            title.Children.Add(new TextBlock { Text = design.Name, FontSize = 17, FontWeight = FontWeights.SemiBold }); body.Children.Add(title);
            var detail = new TextBlock { Text = design.Description, FontSize = 11, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
            detail.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); body.Children.Add(detail);
            var card = new Border { Child = body, Padding = new Thickness(14), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2) };
            card.SetResourceReference(Border.BackgroundProperty, "Panel");
            var button = new Button { Content = card, Tag = design.Id, Padding = new Thickness(0), Margin = new Thickness(0, 0, 12, 12), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(button, design.Name + " UI 선택");
            button.Click += (_, _) => SelectDesign(design.Id);
            choices.Children.Add(button); _cards.Add(design.Id, (card, selected));
        }
        var scroll = new ScrollViewer { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        var footer = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var close = new Button { Content = "완료", MinWidth = 80, IsCancel = true, IsDefault = true };
        close.SetResourceReference(BackgroundProperty, "Raised"); close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); footer.Children.Add(close);
        var hint = new TextBlock { Text = "선택 즉시 적용 · 다음 실행에도 유지됩니다.", FontSize = 12 };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); footer.Children.Add(hint);
        Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
        UpdateSelection();
    }

    internal void SelectDesign(string id)
    {
        SelectedId = UiDesigns.Get(id).Id;
        _apply(SelectedId);
        UpdateSelection();
    }
    private void UpdateSelection()
    {
        foreach (var (id, parts) in _cards)
        {
            parts.Card.SetResourceReference(Border.BorderBrushProperty, id == SelectedId ? "Accent" : "Line");
            parts.Selected.Visibility = id == SelectedId ? Visibility.Visible : Visibility.Hidden;
        }
    }

    // Small layout illustrations use the same palette and control order as the player.
    private static Canvas Thumbnail(UiDesign design)
    {
        var canvas = new Canvas { Width = 300, Height = 144, Background = UiDesigns.Brush(design.Bg), ClipToBounds = true };
        void Box(double x, double y, double w, double h, string color, double radius = 0)
        {
            var box = new Rectangle { Width = w, Height = h, RadiusX = radius, RadiusY = radius, Fill = UiDesigns.Brush(color) };
            Canvas.SetLeft(box, x); Canvas.SetTop(box, y); canvas.Children.Add(box);
        }
        Box(10, 8, 5, 5, design.Accent, 1); Box(20, 9, 43, 3, design.Muted);
        Box(265, 8, 23, 5, design.Raised, 2);
        bool studio = design.Id == "studio", minimal = design.Id == "minimal", light = design.Id == "light";
        double sx = studio ? 64 : minimal ? 0 : 10, sw = studio ? 226 : minimal ? 300 : 280;
        Box(sx, 22, sw, 76, design.Stage, minimal || studio ? 0 : 3);
        if (studio) { Box(10, 22, 47, 76, design.Panel); Box(16, 30, 32, 9, design.Selected); Box(16, 46, 24, 2, design.Muted); Box(16, 56, 24, 2, design.Muted); }
        var triangle = new Path { Data = Geometry.Parse("M0,0 L0,20 L15,10Z"), Fill = UiDesigns.Brush(design.Accent), Opacity = .8 };
        Canvas.SetLeft(triangle, sx + sw / 2 - 7); Canvas.SetTop(triangle, 48); canvas.Children.Add(triangle);
        Box(10, 104, 280, 35, design.Panel, design.Radius / 2);
        double railY = minimal ? 136 : 109;
        Box(18, railY, 264, 1.5, design.Line); Box(18, railY, 86, 1.5, design.Accent);
        double playX = studio ? 44 : 144, playY = light ? 126 : minimal ? 109 : 118;
        Box(playX, playY, 13, 11, design.Accent, design.Radius / 2);
        Box(playX - 14, playY + 4, 7, 2, design.Muted); Box(playX + 20, playY + 4, 7, 2, design.Muted);
        for (int i = 0; i < 5; i++) Box(100 + i * 22, minimal ? 126 : light ? 117 : 134, 14, 2, i == 1 ? design.Accent : design.Line);
        return canvas;
    }
}
