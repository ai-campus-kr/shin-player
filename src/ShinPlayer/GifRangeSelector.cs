using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinPlayer;

// All pointer coordinates are relative to this canvas, in WPF device-independent units.
// The grab offset is preserved, so grabbing the edge of a handle never jumps the time.
internal sealed class GifRangeSelector : Canvas
{
    internal enum Part { Start, Selection, End }
    private const double HandleWidth = 20, TrackTop = 12, TrackHeight = 36;
    private readonly Border _track = new() { Height = TrackHeight, CornerRadius = new(4), IsHitTestVisible = false };
    internal readonly Thumb StartHandle, EndHandle, Selection;
    private readonly TextBlock[] _ticks = new TextBlock[5];
    private Part? _drag;
    private double _grabX, _originalStart, _originalEnd;
    internal double Duration { get; private set; }
    internal double Start { get; private set; }
    internal double End { get; private set; }
    internal double ViewStart { get; private set; }
    internal double ViewEnd { get; private set; }
    internal event Action? RangeChanged;

    internal GifRangeSelector()
    {
        Height = 84; Background = Brushes.Transparent;
        _track.SetResourceReference(Border.BackgroundProperty, "Raised"); Children.Add(_track);
        Selection = MakeThumb(Part.Selection, "GIF 선택 구간 이동", "↔", "GifRangeFill", "GifRangeInk", Cursors.SizeAll);
        StartHandle = MakeThumb(Part.Start, "GIF 시작 시간", "Ⅰ", "GifRangeAccent", "GifRangeAccentInk", Cursors.SizeWE);
        EndHandle = MakeThumb(Part.End, "GIF 끝 시간", "Ⅰ", "GifRangeAccent", "GifRangeAccentInk", Cursors.SizeWE);
        foreach (var thumb in new[] { Selection, StartHandle, EndHandle }) Children.Add(thumb);
        for (int i = 0; i < _ticks.Length; i++)
        {
            var tick = _ticks[i] = new TextBlock { FontFamily = new("Consolas"), FontSize = 11, IsHitTestVisible = false };
            tick.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Children.Add(tick);
        }
        SizeChanged += (_, _) => UpdateGeometry();
        IsEnabledChanged += (_, _) => Opacity = IsEnabled ? 1 : .45;
        MouseLeftButtonDown += (_, e) =>
        {
            if (Duration < .2 || e.OriginalSource != this) return;
            var x = e.GetPosition(this).X;
            var part = Math.Abs(x - XAt(Start)) <= Math.Abs(x - XAt(End)) ? Part.Start : Part.End;
            MoveBoundary(part, TimeAt(x));
            (part == Part.Start ? StartHandle : EndHandle).Focus();
            e.Handled = true;
        };
        IsEnabled = false;
    }

    private Thumb MakeThumb(Part part, string name, string grip, string fill, string ink, Cursor cursor)
    {
        var thumb = new Thumb { Height = TrackHeight, Focusable = true, Cursor = cursor, ToolTip = name + " · ← → 0.1초 · Shift 1초 · Ctrl 10초" };
        AutomationProperties.SetName(thumb, name);
        AutomationProperties.SetHelpText(thumb, "방향키로 조정. Shift 1초, Ctrl 10초. Home/End로 경계까지 이동.");
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetResourceReference(Border.BackgroundProperty, fill);
        border.SetResourceReference(Border.BorderBrushProperty, "GifRangeAccent");
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.TextProperty, grip); text.SetValue(TextBlock.FontSizeProperty, 16.0);
        text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetResourceReference(TextBlock.ForegroundProperty, ink); border.AppendChild(text);
        var style = new Style(typeof(Thumb));
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Thumb)) { VisualTree = border }));
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(OpacityProperty, .8)); style.Triggers.Add(hover);
        var focusBorder = new FrameworkElementFactory(typeof(Border));
        focusBorder.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        focusBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        focusBorder.SetValue(MarginProperty, new Thickness(-4)); focusBorder.SetResourceReference(Border.BorderBrushProperty, "GifRangeAccent");
        var focusStyle = new Style(typeof(Control));
        focusStyle.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control)) { VisualTree = focusBorder }));
        style.Setters.Add(new Setter(FocusVisualStyleProperty, focusStyle));
        thumb.Style = style;
        KeyboardNavigation.SetTabIndex(thumb, part == Part.Start ? 0 : part == Part.End ? 1 : 2);
        thumb.DragStarted += (_, _) => BeginDrag(part, Mouse.GetPosition(this).X);
        thumb.DragDelta += (_, _) => DragTo(Mouse.GetPosition(this).X);
        thumb.DragCompleted += (_, e) => EndDrag(e.Canceled ? null : Mouse.GetPosition(this).X);
        thumb.PreviewKeyDown += (_, e) =>
        {
            if (AdjustKey(part, e.Key, Keyboard.Modifiers)) e.Handled = true;
        };
        return thumb;
    }

    internal void Initialize(double duration, double start, double end)
    {
        Duration = double.IsFinite(duration) && duration >= .2 ? duration : 0;
        ViewStart = 0; ViewEnd = Duration; IsEnabled = Duration > 0;
        SetRange(start, end);
    }

    // Invalid/incomplete typed values leave the last valid visual selection in place.
    internal bool SetRange(double start, double end)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end > Duration + .001 || end - start < .2 - .000001) return false;
        Start = start; End = Math.Min(Duration, end);
        if (Start < ViewStart || End > ViewEnd) { ViewStart = 0; ViewEnd = Duration; }
        UpdateGeometry(); return true;
    }

    internal void ZoomToSelection()
    {
        if (Duration <= 0) return;
        double span = Math.Min(Duration, Math.Max(2, (End - Start) * 1.5));
        ViewStart = Math.Clamp((Start + End - span) / 2, 0, Duration - span); ViewEnd = ViewStart + span;
        UpdateGeometry();
    }
    internal void ShowAll() { ViewStart = 0; ViewEnd = Duration; UpdateGeometry(); }
    internal double XAt(double seconds) => HandleWidth + (seconds - ViewStart) / Math.Max(.001, ViewEnd - ViewStart) * TrackWidth;
    private double TrackWidth => Math.Max(1, ActualWidth - HandleWidth * 2);
    private double TimeAt(double x) => ViewStart + Math.Clamp((x - HandleWidth) / TrackWidth, 0, 1) * (ViewEnd - ViewStart);

    internal void BeginDrag(Part part, double x)
    {
        if (!IsEnabled || Duration <= 0) return;
        _drag = part; _grabX = x; _originalStart = Start; _originalEnd = End;
    }
    internal void DragTo(double x)
    {
        if (_drag == null || !IsEnabled) return;
        double delta = (x - _grabX) / TrackWidth * (ViewEnd - ViewStart);
        if (_drag == Part.Selection) MoveSelection(_originalStart + delta, _originalEnd - _originalStart);
        else MoveBoundary(_drag.Value, (_drag == Part.Start ? _originalStart : _originalEnd) + delta);
    }
    internal void EndDrag(double? releaseX = null)
    {
        if (releaseX.HasValue) DragTo(releaseX.Value);
        _drag = null;
    }
    private void MoveSelection(double start, double length)
    {
        Start = Math.Clamp(Math.Round(start, 3), ViewStart, Math.Max(ViewStart, ViewEnd - length)); End = Start + length; Changed();
    }
    private void MoveBoundary(Part part, double value)
    {
        value = Math.Round(value, 3);
        if (part == Part.Start)
        {
            double lower = Math.Max(ViewStart, End - 3600);
            Start = Math.Clamp(value, lower, Math.Max(lower, End - .2));
        }
        else
        {
            double upper = Math.Min(ViewEnd, Start + 3600);
            End = Math.Clamp(value, Math.Min(upper, Start + .2), upper);
        }
        Changed();
    }
    internal bool AdjustKey(Part part, Key key, ModifierKeys modifiers)
    {
        if (!IsEnabled) return false;
        double step = modifiers.HasFlag(ModifierKeys.Control) ? 10 : modifiers.HasFlag(ModifierKeys.Shift) ? 1 : .1;
        double value = part == Part.End ? End : Start;
        switch (key)
        {
            case Key.Left: case Key.Down: value -= step; break;
            case Key.Right: case Key.Up: value += step; break;
            case Key.Home: value = ViewStart; break;
            case Key.End: value = ViewEnd; break;
            default: return false;
        }
        if (part == Part.Selection) MoveSelection(value, End - Start); else MoveBoundary(part, value);
        return true;
    }
    private void Changed() { UpdateGeometry(); RangeChanged?.Invoke(); }
    private void UpdateGeometry()
    {
        if (ActualWidth <= HandleWidth * 2) return;
        SetLeft(_track, HandleWidth); SetTop(_track, TrackTop); _track.Width = TrackWidth;
        double a = XAt(Start), b = XAt(End);
        SetLeft(Selection, a); SetTop(Selection, TrackTop); Selection.Width = Math.Max(1, b - a); Selection.ClipToBounds = true;
        SetLeft(StartHandle, a - HandleWidth); SetLeft(EndHandle, b);
        StartHandle.Width = EndHandle.Width = HandleWidth;
        SetTop(StartHandle, TrackTop); SetTop(EndHandle, TrackTop);
        AutomationProperties.SetItemStatus(StartHandle, GifOptions.Time(Start));
        AutomationProperties.SetItemStatus(EndHandle, GifOptions.Time(End));
        for (int i = 0; i < _ticks.Length; i++)
        {
            double time = ViewStart + (ViewEnd - ViewStart) * i / (_ticks.Length - 1);
            var tick = _ticks[i]; string formatted = GifOptions.Time(time);
            tick.Text = formatted[(Duration < 3600 ? 3 : 0)..(ViewEnd - ViewStart < 10 ? formatted.Length : formatted.Length - 4)];
            tick.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            SetLeft(tick, Math.Clamp(XAt(time) - tick.DesiredSize.Width / 2, 0, Math.Max(0, ActualWidth - tick.DesiredSize.Width))); SetTop(tick, 59);
        }
    }
}
