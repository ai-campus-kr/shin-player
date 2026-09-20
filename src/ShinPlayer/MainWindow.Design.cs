using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace ShinPlayer;

public partial class MainWindow
{
    private DesignPickerWindow? _designPicker;
    private UiDesign CurrentDesign => UiDesigns.Get(Settings.UiDesign);

    private void Design_Click(object sender, RoutedEventArgs e) => ShowDesignPicker();
    private void ShowDesignPicker()
    {
        if (_designPicker != null) { _designPicker.Activate(); return; }
        _designPicker = new DesignPickerWindow(Settings.UiDesign, id => ApplyUiDesign(id, true)) { Owner = this };
        _designPicker.Closed += (_, _) => _designPicker = null;
        _designPicker.Show();
    }

    private void ApplyUiDesign(string id, bool save = false)
    {
        CancelSeek();
        Settings.UiDesign = UiDesigns.Get(id).Id;
        UiDesigns.ApplyPalette(CurrentDesign);
        ApplyDesignLayout();
        PaintSpeed(_requestedSpeed);
        if (save)
        {
            _app.SaveSettings();
            StatusText.Text = $"{CurrentDesign.Name} UI 적용됨";
        }
    }

    private void ApplyDesignLayout()
    {
        var design = CurrentDesign;
        bool minimal = design.Id == "minimal", studio = design.Id == "studio", light = design.Id == "light", lime = design.Id == "lime";
        TitleRow.Height = new GridLength(_fullScreen ? 0 : design.HeaderHeight);
        WindowChrome.GetWindowChrome(this).CaptionHeight = _fullScreen ? 0 : design.HeaderHeight;
        DesignButton.ToolTip = $"현재 UI: {design.Name} · 클릭해서 변경";

        StageLayout.Margin = minimal ? new Thickness(0) : studio ? new Thickness(12, 0, 12, 8) : light ? new Thickness(20, 0, 20, 12) : new Thickness(16, 0, 16, 10);
        StageFrame.CornerRadius = new CornerRadius(minimal || studio ? 0 : light ? 10 : 14);
        StageFrame.BorderThickness = minimal ? new Thickness(0) : new Thickness(1);
        EmptyBackdrop.CornerRadius = StageFrame.CornerRadius;
        PlaylistPanel.CornerRadius = new CornerRadius(studio ? 2 : light ? 10 : 14);
        Grid.SetColumn(StageFrame, studio ? 1 : 0);
        Grid.SetColumn(PlaylistPanel, studio ? 0 : 1);
        StageLayout.ColumnDefinitions[0].Width = studio ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        StageLayout.ColumnDefinitions[1].Width = studio ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        PlaylistPanel.Margin = studio ? new Thickness(0, 0, 10, 0) : new Thickness(12, 0, 0, 0);

        ControlPanel.Margin = minimal ? new Thickness(0) : studio ? new Thickness(12, 0, 12, 0) : light ? new Thickness(20, 0, 20, 0) : new Thickness(16, 0, 16, 0);
        ControlPanel.CornerRadius = new CornerRadius(minimal || studio ? 0 : light ? 10 : 16);
        ControlPanel.BorderThickness = minimal ? new Thickness(0, 1, 0, 0) : light ? new Thickness(0) : new Thickness(1);
        ControlPanel.Padding = new Thickness(minimal ? 28 : 20, lime ? 10 : 6, minimal ? 28 : 20, lime ? 9 : 6);
        Grid.SetRow(SeekDeck, minimal ? 2 : 0);
        Grid.SetRow(TransportDeck, minimal ? 0 : light ? 2 : 1);
        Grid.SetRow(SpeedDeck, minimal || light ? 1 : 2);
        TimeRow.Height = new GridLength(studio ? 24 : minimal ? 16 : 18);
        SeekHint.Visibility = minimal ? Visibility.Collapsed : Visibility.Visible;
        SeekHint.Text = studio ? "← →  5초 이동     , .  프레임 이동" : "클릭해서 이동 · 드래그로 탐색";
        PositionText.FontSize = studio ? 18 : 12;
        DurationText.FontSize = studio ? 14 : 12;
        TransportDeck.Height = minimal || studio ? 44 : 56;
        SpeedDeck.Height = lime ? 42 : 36;
        SpeedDeck.Margin = new Thickness(0, lime ? 5 : 0, 0, 0);
        SpeedValue.FontSize = lime ? 23 : studio ? 20 : 18;
        PlayButton.Width = PlayButton.Height = light ? 48 : lime ? 48 : 38;
        PlayButton.FontSize = light ? 22 : 18;
        PlayButton.Margin = new Thickness(studio ? 4 : 10, 0, studio ? 4 : 10, 0);
        Grid.SetColumn(TransportButtons, studio ? 0 : 1);
        Grid.SetColumn(VolumeDock, studio ? 1 : 0);
        TransportDeck.ColumnDefinitions[0].Width = studio ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        TransportDeck.ColumnDefinitions[1].Width = studio ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        VolumeDock.HorizontalAlignment = studio ? HorizontalAlignment.Center : HorizontalAlignment.Left;

        LimeGlow.Visibility = lime ? Visibility.Visible : Visibility.Collapsed;
        EmptyCopy.HorizontalAlignment = minimal ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        EmptyCopy.Margin = minimal ? new Thickness(30, 22, 30, 22) : new Thickness(studio ? 32 : 46, 22, 28, 22);
        EmptyTitle.TextAlignment = EmptyDescription.TextAlignment = minimal ? TextAlignment.Center : TextAlignment.Left;
        EmptyActions.HorizontalAlignment = minimal ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        DesignEyebrow.HorizontalAlignment = minimal ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        DesignEyebrow.Text = minimal ? "SHINPLAYER" : studio ? "SHINPLAYER  /  STUDIO" : light ? "신플레이어" : "PLAY AT YOUR PACE";
        DesignEyebrow.SetResourceReference(TextBlock.ForegroundProperty, lime || studio ? "Accent" : "Muted");
        if (UiDesigns.All.Any(x => x.Heading == EmptyTitle.Text)) SetIdleHeading();
        UpdateDesignEmptyLayout();
    }

    private void SetIdleHeading()
    {
        EmptyTitle.Text = CurrentDesign.Heading;
        EmptyDescription.Text = "영상 파일을 이곳에 놓거나 파일을 선택하세요.";
    }

    private void UpdateDesignEmptyLayout()
    {
        bool lime = CurrentDesign.Id == "lime";
        EmptyArtwork.Visibility = lime && Stage.ActualWidth >= 920 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.FontSize = lime ? Stage.ActualHeight < 360 ? 32 : 43 : CurrentDesign.Id == "studio" ? 25 : 30;
        EmptyTitle.LineHeight = EmptyTitle.FontSize * 1.3;
    }
}
