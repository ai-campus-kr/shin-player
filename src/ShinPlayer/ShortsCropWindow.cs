using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

internal sealed class ShortsCropSelector : FrameworkElement
{
    private readonly BitmapSource _frame;
    private ShortsCrop _selection = ShortsCrop.Full;
    private ShortsCrop _before = ShortsCrop.Full;
    private Point _grab;
    private int _part;
    private bool _dragging, _drawNew;
    internal ShortsCrop Selection => _selection;
    internal event Action? Changed;
    private double Handle => Math.Max(Width, Height) / 55;

    internal ShortsCropSelector(BitmapSource frame, ShortsCrop selection)
    {
        _frame = frame; Width = frame.PixelWidth; Height = frame.PixelHeight; SetSelection(selection);
        Focusable = true; Cursor = Cursors.Cross;
        AutomationProperties.SetName(this, "쇼츠 원본 영역 선택");
        AutomationProperties.SetHelpText(this, "드래그로 영역 선택, 선택 안쪽은 이동, 모서리는 크기 조절. 방향키로 이동, Shift와 방향키로 크기 조절. Escape로 드래그 취소.");
        MouseLeftButtonDown += (_, e) => { Focus(); BeginDrag(e.GetPosition(this)); CaptureMouse(); e.Handled = true; };
        MouseMove += (_, e) => { if (_dragging) DragTo(e.GetPosition(this)); else Cursor = Hit(e.GetPosition(this)) == 16 ? Cursors.SizeAll : Cursors.Cross; };
        MouseLeftButtonUp += (_, e) => { if (_dragging) EndDrag(e.GetPosition(this)); ReleaseMouseCapture(); e.Handled = true; };
        LostMouseCapture += (_, _) => _dragging = false;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _dragging) { SetSelection(_before); _dragging = false; ReleaseMouseCapture(); e.Handled = true; return; }
            double dx = e.Key == Key.Left ? -.005 : e.Key == Key.Right ? .005 : 0;
            double dy = e.Key == Key.Up ? -.005 : e.Key == Key.Down ? .005 : 0;
            if (dx == 0 && dy == 0) return;
            var s = _selection;
            SetSelection(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? s with { Width = Math.Clamp(s.Width + dx, .02, 1 - s.X), Height = Math.Clamp(s.Height + dy, .02, 1 - s.Y) }
                : s with { X = Math.Clamp(s.X + dx, 0, 1 - s.Width), Y = Math.Clamp(s.Y + dy, 0, 1 - s.Height) });
            e.Handled = true;
        };
    }

    internal void Reset() { _drawNew = true; SetSelection(ShortsCrop.Full); }
    internal void DrawNew() { _drawNew = true; Cursor = Cursors.Cross; }
    internal void SetSelection(ShortsCrop selection)
    {
        selection.Validate();
        // Edge subtraction can make an exact 2% selection a few ulps smaller.
        double x = Math.Clamp(selection.X, 0, .98), y = Math.Clamp(selection.Y, 0, .98);
        _selection = new(x, y, Math.Clamp(selection.Width, .02, 1 - x), Math.Clamp(selection.Height, .02, 1 - y));
        InvalidateVisual(); Changed?.Invoke();
    }
    private Rect Bounds => new(_selection.X * Width, _selection.Y * Height, _selection.Width * Width, _selection.Height * Height);
    private int Hit(Point p)
    {
        if (_drawNew || _selection == ShortsCrop.Full) return 0;
        var r = Bounds; var hit = r; hit.Inflate(Handle, Handle);
        if (!hit.Contains(p)) return 0;
        int edges = (Math.Abs(p.X - r.Left) < Handle ? 1 : Math.Abs(p.X - r.Right) < Handle ? 2 : 0) |
            (Math.Abs(p.Y - r.Top) < Handle ? 4 : Math.Abs(p.Y - r.Bottom) < Handle ? 8 : 0);
        return edges == 0 && r.Contains(p) ? 16 : edges;
    }
    internal void BeginDrag(Point point)
    {
        _part = Hit(point); _drawNew = false; _before = _selection;
        _grab = new(Math.Clamp(point.X / Width, 0, 1), Math.Clamp(point.Y / Height, 0, 1)); _dragging = true;
    }
    internal void DragTo(Point point)
    {
        if (!_dragging) return;
        double x = Math.Clamp(point.X / Width, 0, 1), y = Math.Clamp(point.Y / Height, 0, 1);
        var s = _before;
        if (_part == 16)
        {
            SetSelection(s with { X = Math.Clamp(s.X + x - _grab.X, 0, 1 - s.Width), Y = Math.Clamp(s.Y + y - _grab.Y, 0, 1 - s.Height) }); return;
        }
        double left = s.X, top = s.Y, right = s.X + s.Width, bottom = s.Y + s.Height;
        if (_part == 0)
        {
            left = Math.Min(_grab.X, x); right = Math.Max(_grab.X, x); top = Math.Min(_grab.Y, y); bottom = Math.Max(_grab.Y, y);
            if (right - left < .02) { left = Math.Min(left, .98); right = left + .02; }
            if (bottom - top < .02) { top = Math.Min(top, .98); bottom = top + .02; }
        }
        else
        {
            // Preserve the grab offset so a handle does not jump on the first move.
            if ((_part & 1) != 0) left = Math.Clamp(s.X + x - _grab.X, 0, right - .02);
            if ((_part & 2) != 0) right = Math.Clamp(right + x - _grab.X, left + .02, 1);
            if ((_part & 4) != 0) top = Math.Clamp(s.Y + y - _grab.Y, 0, bottom - .02);
            if ((_part & 8) != 0) bottom = Math.Clamp(bottom + y - _grab.Y, top + .02, 1);
        }
        SetSelection(new(left, top, right - left, bottom - top));
    }
    internal void EndDrag(Point point) { DragTo(point); _dragging = false; }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawImage(_frame, new Rect(0, 0, Width, Height));
        var r = Bounds; var shade = new SolidColorBrush(Color.FromArgb(175, 0, 0, 0));
        dc.DrawRectangle(shade, null, new Rect(0, 0, Width, r.Top));
        dc.DrawRectangle(shade, null, new Rect(0, r.Bottom, Width, Math.Max(0, Height - r.Bottom)));
        dc.DrawRectangle(shade, null, new Rect(0, r.Top, r.Left, r.Height));
        dc.DrawRectangle(shade, null, new Rect(r.Right, r.Top, Math.Max(0, Width - r.Right), r.Height));
        var pen = new Pen(UiDesigns.Brush("#B7FF00"), Handle / 7);
        dc.DrawRectangle(null, pen, r);
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)), Handle / 16);
        for (int i = 1; i <= 2; i++)
        {
            dc.DrawLine(gridPen, new(r.Left + r.Width * i / 3, r.Top), new(r.Left + r.Width * i / 3, r.Bottom));
            dc.DrawLine(gridPen, new(r.Left, r.Top + r.Height * i / 3), new(r.Right, r.Top + r.Height * i / 3));
        }
        foreach (double x in new[] { r.Left, r.Left + r.Width / 2, r.Right })
        foreach (double y in new[] { r.Top, r.Top + r.Height / 2, r.Bottom })
            if (x != r.Left + r.Width / 2 || y != r.Top + r.Height / 2)
                dc.DrawRectangle(Brushes.White, pen, new Rect(x - Handle / 2, y - Handle / 2, Handle, Handle));
    }
}

internal sealed class ShortsCropWindow : Window
{
    internal readonly ShortsCropSelector Selector;
    internal ShortsCrop Selection => Selector.Selection;
    internal ShortsCropWindow(BitmapSource frame, ShortsCrop selection)
    {
        Style = (Style)FindResource(typeof(Window)); Title = "쇼츠에 담을 영역 · 신플레이어";
        Width = Math.Min(980, SystemParameters.WorkArea.Width - 40); Height = Math.Min(760, SystemParameters.WorkArea.Height - 40);
        MinWidth = 560; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Selector = new(frame, selection);
        var root = new Grid { Margin = new(22) }; root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock { Text = "담고 싶은 부분만, 직접 고르세요.", FontSize = 22, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "드래그로 영역 선택 · 안쪽을 잡아 이동 · 가장자리와 모서리로 크기 조절", FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new(0, 7, 0, 0) }); root.Children.Add(heading);
        var view = new Viewbox { Child = Selector, Stretch = Stretch.Uniform, Margin = new(10) };
        var surface = new Border { Background = UiDesigns.Brush("#101114"), Child = view }; Grid.SetRow(surface, 1); root.Children.Add(surface);
        var footer = new StackPanel { Margin = new(0, 14, 0, 0) }; var selected = new TextBlock { FontSize = 12, Margin = new(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
        void Update() => selected.Text = $"선택 영역 {Selection.Width:P0} × {Selection.Height:P0} · 선택한 장면으로 구도를 정하며, 같은 영역이 영상 전체 구간에 적용됩니다.";
        Selector.Changed += Update; Update(); footer.Children.Add(selected);
        var buttons = new DockPanel();
        var apply = new Button { Content = "이 영역 사용", Style = (Style)FindResource("Primary"), MinWidth = 112, IsDefault = true };
        apply.Click += (_, _) => DialogResult = true; DockPanel.SetDock(apply, Dock.Right); buttons.Children.Add(apply);
        var cancel = new Button { Content = "취소", IsCancel = true, Margin = new(0, 0, 8, 0) }; DockPanel.SetDock(cancel, Dock.Right); buttons.Children.Add(cancel);
        var reset = new Button { Content = "원본 전체", HorizontalAlignment = HorizontalAlignment.Left }; reset.Click += (_, _) => Selector.Reset(); DockPanel.SetDock(reset, Dock.Left); buttons.Children.Add(reset);
        var draw = new Button { Content = "다시 선택", HorizontalAlignment = HorizontalAlignment.Left, Margin = new(8, 0, 0, 0) }; draw.Click += (_, _) => Selector.DrawNew(); buttons.Children.Add(draw);
        footer.Children.Add(buttons); Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
    }
}
