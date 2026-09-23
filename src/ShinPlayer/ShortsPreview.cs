using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

internal sealed class ShortsPreview : Canvas
{
    private readonly Image _frame = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Image _label = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Border _selection = new() { BorderBrush = Brushes.White, BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(18), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private ShortsOptions _options = new(0, 1);
    private BitmapSource? _labelBitmap;
    private bool _hasOptions, _dragging, _textSelected;
    private Point _grab;
    private ShortsOptions _beforeDrag = new(0, 1);
    internal event Action<ShortsOptions>? Edited;
    internal Rect TextBounds { get; private set; }
    internal Rect FrameBounds { get; private set; }
    internal bool HasFrame => _frame.Source != null;
    internal BitmapSource? SourceFrame => _frame.Source as BitmapSource;
    internal bool MoveVideoOnly { get; set; }

    internal ShortsPreview()
    {
        Width = ShortsOptions.CanvasWidth; Height = ShortsOptions.CanvasHeight;
        Background = Brushes.Black; ClipToBounds = true; Focusable = true; Cursor = Cursors.SizeAll;
        AutomationProperties.SetName(this, "쇼츠 장면 미리보기");
        AutomationProperties.SetHelpText(this, "영상 또는 문구를 드래그하여 위치를 조정합니다. 클릭 후 방향키로도 이동할 수 있습니다.");
        Children.Add(_frame); Children.Add(_label); Children.Add(_selection);
        MouseLeftButtonDown += (_, e) => { Focus(); BeginDrag(e.GetPosition(this)); CaptureMouse(); e.Handled = true; };
        MouseMove += (_, e) => { if (_dragging) DragTo(e.GetPosition(this)); };
        MouseLeftButtonUp += (_, e) => { if (_dragging) EndDrag(e.GetPosition(this)); ReleaseMouseCapture(); };
        LostMouseCapture += (_, _) => _dragging = false;
        LostKeyboardFocus += (_, _) => _selection.Visibility = Visibility.Collapsed;
        PreviewKeyDown += (_, e) =>
        {
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 20 : 4;
            double dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0;
            double dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
            if (dx == 0 && dy == 0) return;
            var center = _textSelected && _labelBitmap != null ? new Point(TextBounds.X + TextBounds.Width / 2, TextBounds.Y + TextBounds.Height / 2) : new Point(0, 0);
            BeginDrag(center, _textSelected); EndDrag(new Point(center.X + dx, center.Y + dy)); e.Handled = true;
        };
        MouseWheel += (_, e) =>
        {
            if (!IsEnabled || !HasFrame || _dragging) return;
            var next = _options with { Zoom = Math.Clamp(Math.Round(_options.Zoom + Math.Sign(e.Delta) * .1, 2), 1, 4) };
            Apply(next); Edited?.Invoke(next); e.Handled = true;
        };
    }

    internal void SetFrame(BitmapSource frame) { _frame.Source = frame; Apply(_options); }
    internal void Apply(ShortsOptions options)
    {
        if (!_hasOptions || options.Text != _options.Text || options.FontSize != _options.FontSize || options.TextColor != _options.TextColor || options.TextBox != _options.TextBox || options.FontId != _options.FontId)
            _labelBitmap = ShortsComposition.RenderLabel(options)?.Bitmap;
        _options = options; _hasOptions = true;
        if (_frame.Source is BitmapSource frame)
        {
            FrameBounds = ShortsComposition.VideoBounds(frame.PixelWidth, frame.PixelHeight, options);
            var crop = options.SourceCrop;
            double width = FrameBounds.Width / crop.Width, height = FrameBounds.Height / crop.Height;
            Place(_frame, new Rect(FrameBounds.X - width * crop.X, FrameBounds.Y - height * crop.Y, width, height));
            _frame.Clip = new RectangleGeometry(new Rect(width * crop.X, height * crop.Y, FrameBounds.Width, FrameBounds.Height));
        }
        _label.Source = _labelBitmap;
        if (_labelBitmap != null)
        {
            TextBounds = ShortsComposition.LabelBounds(_labelBitmap.PixelWidth, _labelBitmap.PixelHeight, options);
            Place(_label, TextBounds); Place(_selection, TextBounds);
        }
        else { TextBounds = Rect.Empty; _selection.Visibility = Visibility.Collapsed; }
    }
    private static void Place(FrameworkElement element, Rect bounds)
    {
        SetLeft(element, bounds.X); SetTop(element, bounds.Y); element.Width = bounds.Width; element.Height = bounds.Height;
    }
    internal void BeginDrag(Point point, bool? selectText = null)
    {
        _textSelected = selectText ?? (!MoveVideoOnly && _labelBitmap != null && TextBounds.Contains(point));
        _selection.Visibility = _textSelected && _labelBitmap != null ? Visibility.Visible : Visibility.Collapsed;
        _grab = point; _beforeDrag = _options; _dragging = true;
        if (_textSelected && _labelBitmap != null)
            _beforeDrag = _beforeDrag with { TextX = (TextBounds.X + TextBounds.Width / 2) / Width, TextY = (TextBounds.Y + TextBounds.Height / 2) / Height };
    }
    internal void DragTo(Point point)
    {
        if (!_dragging || !IsEnabled) return;
        double dx = point.X - _grab.X, dy = point.Y - _grab.Y;
        ShortsOptions next;
        if (_textSelected)
            next = _beforeDrag with { TextX = Math.Clamp(_beforeDrag.TextX + dx / Width, 0, 1), TextY = Math.Clamp(_beforeDrag.TextY + dy / Height, 0, 1) };
        else
        {
            if (!HasFrame) return;
            next = _beforeDrag with
            {
                PanX = Math.Abs(FrameBounds.Width - Width) < .01 ? .5 : Math.Clamp(_beforeDrag.PanX + dx / (Width - FrameBounds.Width), 0, 1),
                PanY = Math.Abs(FrameBounds.Height - Height) < .01 ? .5 : Math.Clamp(_beforeDrag.PanY + dy / (Height - FrameBounds.Height), 0, 1)
            };
        }
        Apply(next); Edited?.Invoke(next);
    }
    internal void EndDrag(Point point) { DragTo(point); _dragging = false; }
}
