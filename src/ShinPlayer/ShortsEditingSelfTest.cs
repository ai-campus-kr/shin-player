using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinPlayer;

public partial class MainWindow
{
    private async Task RunShortsEditingTestsAsync(Func<string, Func<Task<object?>>, Task> test, CaptureTools tools, SubtitleVideo video, string fixtures, string output)
    {
        await test("shorts-six-embedded-fonts-and-custom-rgb-raster", () =>
        {
            var hashes = new System.Collections.Generic.HashSet<string>();
            foreach (var font in ShortsFonts.All)
            {
                var typeface = new Typeface(font.Family, FontStyles.Normal, font.Weight, FontStretches.Normal);
                ShortsCheck(typeface.TryGetGlyphTypeface(out var glyph) && glyph.FontUri.OriginalString.Contains("assets/fonts", StringComparison.OrdinalIgnoreCase), "Font silently fell back to an installed font: " + font.Id);
                ShortsCheck("한글쇼츠제작".All(c => glyph.CharacterToGlyphMap.ContainsKey(c)), "Bundled font misses common Hangul: " + font.Id);
                var settings = new ShortsOptions(0, 1, Text: "한글 쇼츠, 내 스타일로!", TextColor: "#19A3E1", FontId: font.Id, TextBox: false);
                var label = ShortsComposition.RenderLabel(settings)!;
                string png = Path.Combine(output, "shorts-font-" + font.Id + ".png"); ShortsComposition.SavePng(label.Bitmap, png);
                var data = LoadPixels(png);
                int matching = Enumerable.Range(0, data.Pixels.Length / 4).Count(i => data.Pixels[i * 4 + 3] > 250 &&
                    Math.Abs(data.Pixels[i * 4] - 225) < 3 && Math.Abs(data.Pixels[i * 4 + 1] - 163) < 3 && Math.Abs(data.Pixels[i * 4 + 2] - 25) < 3);
                ShortsCheck(matching > 250, "Selected RGB is missing from caption pixels: " + font.Id);
                hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(png))));
            }
            ShortsCheck(hashes.Count == 6, "Distinct fonts render identically");
            foreach (var invalid in new[] { new ShortsOptions(0, 1, Zoom: double.NaN), new ShortsOptions(0, 1, Zoom: 5),
                new ShortsOptions(0, 1, FontId: "missing"), new ShortsOptions(0, 1, Crop: new(.9, 0, .2, 1)), new ShortsOptions(0, 1, Crop: new(0, 0, .01, 1)),
                new ShortsOptions(0, 1, Crop: new(double.NaN)), new ShortsOptions(0, 1, TextColor: "#12345678") })
            {
                try { invalid.Validate(2); throw new Exception("Invalid composition accepted"); } catch (InvalidOperationException) { }
            }
            return Task.FromResult<object?>(new { fonts = 6, embedded = true, distinctRasters = hashes.Count, customRgb = true });
        });

        await test("shorts-source-region-draw-resize-offset-and-move", async () =>
        {
            var frame = await ShortsExport.FrameAsync(tools, video, .1, CancellationToken.None);
            var picker = new ShortsCropSelector(frame, ShortsCrop.Full);
            Point P(double x, double y) => new(x * picker.Width, y * picker.Height);
            picker.BeginDrag(P(.8, .9)); picker.EndDrag(P(.2, .1));
            ShortsCheck(Math.Abs(picker.Selection.X - .2) < .001 && Math.Abs(picker.Selection.Width - .6) < .001, "Reverse rectangle drag picked the wrong pixels");
            picker.BeginDrag(P(.795, .895)); picker.EndDrag(P(.695, .795));
            ShortsCheck(Math.Abs(picker.Selection.Width - .5) < .001 && Math.Abs(picker.Selection.Height - .7) < .001, "Resize jumped from the handle grab offset");
            picker.BeginDrag(P(.4, .4)); picker.EndDrag(P(2, 2));
            ShortsCheck(Math.Abs(picker.Selection.X + picker.Selection.Width - 1) < .001 && Math.Abs(picker.Selection.Y + picker.Selection.Height - 1) < .001, "Selection move escaped source bounds");
            picker.Reset(); picker.BeginDrag(P(.999, .999)); picker.EndDrag(P(1, 1)); picker.Selection.Validate();
            ShortsCheck(picker.Selection.Width >= .02 - .000001, "Tiny selection failed to keep the minimum size");
            for (int i = 1; i <= 98; i++)
            {
                double at = i / 100d;
                picker.Reset(); picker.BeginDrag(P(at, at)); picker.EndDrag(P(at + .0001, at + .0001)); picker.Selection.Validate();
                ShortsCheck(picker.Selection.Width >= .02 && picker.Selection.Height >= .02, "Minimum-region rounding failed at " + at);
                picker.BeginDrag(P(at, at)); picker.EndDrag(P(1, 1)); picker.Selection.Validate();
            }
            var showcase = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "신플레이어 쇼츠 데모.mp4"), CancellationToken.None);
            var displayFrame = await ShortsExport.FrameAsync(tools, showcase, 1, CancellationToken.None);
            var dialog = new ShortsCropWindow(displayFrame, new(.2, .1, .55, .7)) { Owner = this };
            try
            {
                dialog.Show(); dialog.UpdateLayout(); OnlineSelfTest.Capture(dialog, Path.Combine(output, "shorts-select-region.png"));
                dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight; dialog.UpdateLayout();
                ShortsCheck(dialog.Selector.ActualWidth > 0, "Crop source disappeared at minimum size");
            }
            finally { dialog.Close(); }
            return new { drawnBothDirections = true, grabOffset = true, moveClamped = true, minimumRegion = true };
        });

        await test("shorts-custom-crop-zoom-pan-preview-matches-mp4", async () =>
        {
            var rotated = await new SubtitleCapture(tools).ProbeAsync(Path.Combine(fixtures, "shorts-rotated-90.mp4"), CancellationToken.None);
            var scenarios = new[]
            {
                (video, new ShortsOptions(0, .4, 720, ShortsFit.Contain, PanY: .8, Text: "", Audio: false, Crop: new(.55, .08, .4, .8), Zoom: 1.35)),
                (video, new ShortsOptions(0, .4, 720, PanX: .2, PanY: .7, Text: "", Audio: false, Crop: new(.1, .1, .7, .8), Zoom: 2)),
                (rotated, new ShortsOptions(0, .4, 720, PanX: .8, PanY: .2, Text: "", Audio: false, Crop: new(.13, .1, .7, .7), Zoom: 1.6)),
                (video, new ShortsOptions(0, .2, 720, Text: "", Audio: false, Crop: new(.05, .5, .8, .02), Zoom: 4))
            };
            int scenario = 0;
            foreach (var (input, options) in scenarios)
            {
                var frame = await ShortsExport.FrameAsync(tools, input, .1, CancellationToken.None);
                var preview = new ShortsPreview(); preview.SetFrame(frame); preview.Apply(options);
                preview.Measure(new Size(1080, 1920)); preview.Arrange(new Rect(0, 0, 1080, 1920)); preview.UpdateLayout();
                var raster = new RenderTargetBitmap(1080, 1920, 96, 96, PixelFormats.Pbgra32); raster.Render(preview);
                var name = "shorts-region-case-" + scenario++;
                string expectedPath = Path.Combine(output, name + "-preview.png"); ShortsComposition.SavePng(raster, expectedPath);
                var path = await ShortsExport.ExportAsync(tools, input, options, Path.Combine(output, "shorts-region-output"), new ImmediateProgress<string>(_ => { }), CancellationToken.None);
                await VerifyShortsAsync(tools, path, options, false, output);
                var expected = LoadPixels(expectedPath); var actual = LoadPixels(await ShortsFrameAsync(tools, path, name + "-export.png", output));
                int matched = 0, samples = 0;
                for (int row = 1; row < 20; row++)
                for (int col = 1; col < 12; col++)
                {
                    int a = ((expected.Height * row / 20) * expected.Width + expected.Width * col / 12) * 4;
                    int b = ((actual.Height * row / 20) * actual.Width + actual.Width * col / 12) * 4;
                    samples++;
                    if (Enumerable.Range(0, 3).All(c => Math.Abs(expected.Pixels[a + c] - actual.Pixels[b + c]) < 35)) matched++;
                }
                ShortsCheck(matched >= samples * .92, $"Preview and export selected different source pixels: {name}, {matched}/{samples}");
            }
            return new { scenarios = scenario, arbitraryRegion = true, zoom = true, pan = true, rotated = true, thinRegion = true };
        });
    }
}
