using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShinPlayer;

internal enum ExportState { Preparing, Ready, Working, Cancelling, Succeeded, Edited, Cancelled, Failed }

// The result stays outside the editor's scrolling content until the next operation or edit.
internal sealed class ExportFeedback : Border
{
    internal const string Neon = "#B6FF3B", NeonInk = "#162109";
    private readonly string _kind;
    private readonly TextBlock _icon = new() { FontFamily = new("Segoe UI Symbol"), FontSize = 24, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Border _mark = new() { Width = 40, Height = 40, CornerRadius = new(20), Margin = new(0, 0, 13, 0), VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _heading = new() { FontFamily = new("Segoe UI Variable Display, Malgun Gothic"), FontSize = 20, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _detail = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 48, Margin = new(0, 4, 0, 0) };
    private readonly TextBlock _file = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0, 5, 0, 0) };
    private readonly ProgressBar _progress = new() { Height = 3, Margin = new(0, 10, 0, 0), Visibility = Visibility.Collapsed };
    internal ExportState State { get; private set; }
    internal string Heading => _heading.Text;
    internal string Detail => _detail.Text;
    internal bool IsProgressVisible => _progress.Visibility == Visibility.Visible && _progress.IsIndeterminate;

    internal ExportFeedback(string kind)
    {
        _kind = kind; Padding = new(14, 12, 14, 12); CornerRadius = new(8); BorderThickness = new(1);
        var layout = new Grid(); layout.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); layout.ColumnDefinitions.Add(new());
        _mark.Child = _icon; layout.Children.Add(_mark);
        var text = new StackPanel(); text.Children.Add(_heading); text.Children.Add(_detail); text.Children.Add(_file); text.Children.Add(_progress);
        Grid.SetColumn(text, 1); layout.Children.Add(text); Child = layout;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Show(ExportState.Preparing, "준비 중", "영상을 확인하고 있습니다.");
    }

    internal void Show(ExportState state, string heading, string detail, string? file = null, string? fullPath = null)
    {
        State = state; _heading.Text = heading; _detail.Text = detail; _detail.ToolTip = detail;
        _file.Text = file ?? ""; _file.ToolTip = fullPath ?? file; _file.Visibility = string.IsNullOrEmpty(file) ? Visibility.Collapsed : Visibility.Visible;
        bool success = state == ExportState.Succeeded;
        bool working = state is ExportState.Preparing or ExportState.Working or ExportState.Cancelling;
        _progress.IsIndeterminate = working; _progress.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
        _icon.Text = state switch { ExportState.Succeeded => "✓", ExportState.Failed => "!", ExportState.Edited => "↻", ExportState.Cancelled => "—", _ => working ? "⋯" : "↓" };
        if (success)
        {
            Background = UiDesigns.Brush(Neon); BorderBrush = UiDesigns.Brush(Neon);
            _mark.Background = UiDesigns.Brush(NeonInk); _icon.Foreground = UiDesigns.Brush(Neon);
            _heading.Foreground = _detail.Foreground = _file.Foreground = UiDesigns.Brush(NeonInk);
        }
        else
        {
            SetResourceReference(BackgroundProperty, "Raised"); SetResourceReference(BorderBrushProperty, "Line");
            _heading.SetResourceReference(TextBlock.ForegroundProperty, "Ink"); _detail.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _file.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            _mark.Background = UiDesigns.Brush("#16212B");
            _icon.Foreground = UiDesigns.Brush(state == ExportState.Failed ? "#FF7183" : state is ExportState.Edited or ExportState.Cancelled or ExportState.Cancelling ? "#FFCC55" : "#66CEFF");
        }
        AutomationProperties.SetName(this, heading + ". " + detail);
        if (IsLoaded && UIElementAutomationPeer.FromElement(this) is { } peer) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    internal void UpdateProgress(string detail)
    {
        // A queued progress callback must not replace success, cancellation or a newer ready state.
        if (State is not (ExportState.Preparing or ExportState.Working)) return;
        _detail.Text = detail; _detail.ToolTip = detail;
    }

    internal void MarkEdited()
    {
        if (State == ExportState.Succeeded)
            Show(ExportState.Edited, "설정 변경 · 다시 저장 필요", "바뀐 설정은 아직 저장하지 않았습니다. 이전 결과는 아래에서 열 수 있습니다.", _file.Text, _file.ToolTip as string);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    internal void Complete(string detail, string file, string path) => Show(ExportState.Succeeded, _kind + " 저장 완료", detail, file, path);

    internal void StyleOpenButton(Button button)
    {
        if (State == ExportState.Succeeded)
        {
            button.Background = UiDesigns.Brush(Neon); button.Foreground = UiDesigns.Brush(NeonInk); button.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            button.ClearValue(Control.BackgroundProperty); button.ClearValue(Control.ForegroundProperty); button.ClearValue(Control.FontWeightProperty);
        }
    }
}
