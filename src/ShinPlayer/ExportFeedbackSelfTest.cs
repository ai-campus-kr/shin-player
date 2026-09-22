using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShinPlayer;

public partial class MainWindow
{
    private async Task VerifyExportCompletionAsync(Window dialog, ExportFeedback feedback, Button open, Button folder, string prefix, string output)
    {
        if (feedback.State != ExportState.Succeeded || feedback.IsProgressVisible || !open.IsEnabled || !folder.IsEnabled || !open.Content.ToString()!.Contains("완성된"))
            throw new Exception("Completed export is not clearly actionable");
        string heading = feedback.Heading;
        feedback.UpdateProgress("late queued progress");
        await Task.Delay(120);
        if (feedback.State != ExportState.Succeeded || feedback.Heading != heading || feedback.Detail == "late queued progress")
            throw new Exception("Late progress replaced a completed export");
        double width = dialog.Width, height = dialog.Height;
        foreach (var design in UiDesigns.All)
        {
            ApplyUiDesign(design.Id);
            dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight; dialog.UpdateLayout();
            foreach (FrameworkElement element in new FrameworkElement[] { feedback, open, folder })
            {
                var bounds = element.TransformToAncestor(dialog).TransformBounds(new Rect(element.RenderSize));
                if (!new Rect(0, 0, dialog.ActualWidth, dialog.ActualHeight).Contains(bounds) || bounds.Height < 20)
                    throw new Exception("Export result is clipped at minimum window size: " + design.Id);
            }
            // Success must stay fixed while the editor is scrolled in either direction.
            foreach (var scroller in VisualChildren<ScrollViewer>(dialog)) scroller.ScrollToBottom();
            dialog.UpdateLayout();
            if (feedback.State != ExportState.Succeeded || !feedback.IsVisible)
                throw new Exception("Scrolling lost export confirmation");
            var position = feedback.TransformToAncestor(dialog).Transform(new Point(6, 6));
            string path = Path.Combine(output, prefix + "-saved-" + design.Id + ".png");
            OnlineSelfTest.Capture(dialog, path);
            var pixels = LoadPixels(path); int index = ((int)position.Y * pixels.Width + (int)position.X) * 4;
            var neon = (Color)ColorConverter.ConvertFromString(ExportFeedback.Neon);
            if (Math.Abs(pixels.Pixels[index] - neon.B) > 4 || Math.Abs(pixels.Pixels[index + 1] - neon.G) > 4 || Math.Abs(pixels.Pixels[index + 2] - neon.R) > 4)
                throw new Exception("Neon completion panel is hidden or not rendered: " + design.Id);
            foreach (var scroller in VisualChildren<ScrollViewer>(dialog)) scroller.ScrollToTop();
        }
        ApplyUiDesign("minimal"); dialog.Width = width; dialog.Height = height; dialog.UpdateLayout();
    }
}
