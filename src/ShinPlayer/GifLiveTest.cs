using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ShinPlayer;

internal sealed partial class YouTubeWindow
{
    // Explicit, isolated live smoke test. It never reads an API key or sends an AI request.
    internal static async Task<int> RunGifLiveTestAsync(Window owner, string url, string output)
    {
        Directory.CreateDirectory(output);
        YouTubeWindow? window = null;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(output, "live-gif-phase.json"), "{\"phase\":\"starting\"}");
            window = new YouTubeWindow(url, Path.Combine(output, "browser-profile"), new ApiKeyStore(Path.Combine(output, "test-key")))
                { Owner = owner, ShowActivated = false, ShowInTaskbar = false };
            window.Show(); await window.Initialization.WaitAsync(TimeSpan.FromSeconds(30));
            await WaitForContentAsync(window, 180).WaitAsync(TimeSpan.FromSeconds(100));
            // Starting playback can trigger a pre-roll after metadata initially looks ready.
            // Wait for the requested content to actually advance before opening the GIF dialog.
            int stable = 0;
            for (int attempt = 0; attempt < 180 && stable < 10; attempt++)
            {
                var content = await window._browser.ExecuteScriptAsync("""
                    (()=>{
                      const p=document.querySelector('#movie_player'),v=p?.querySelector('video');if(!v)return false;
                      v.muted=true;v.play().catch(()=>{});
                      const skip=[...p.querySelectorAll('button, [role="button"], .ytpSkipAdButton, .ytp-skip-ad-button, .ytp-ad-skip-button-modern')].find(e=>e.getBoundingClientRect().height>0 && /건너뛰기|skip ad|skip$/i.test(e.innerText||e.getAttribute('aria-label')||''));
                      if(skip)skip.click();
                      const ad=p.classList.contains('ad-showing') || p.classList.contains('ad-interrupting');
                      return {ready:!ad && !v.paused && !v.seeking && v.readyState>=2 && v.duration>30,ad,paused:v.paused,time:v.currentTime,duration:v.duration};
                    })()
                    """).WaitAsync(TimeSpan.FromSeconds(10));
                if (attempt % 10 == 0) await File.WriteAllTextAsync(Path.Combine(output, "live-gif-phase.json"), content);
                using var status = JsonDocument.Parse(content);
                stable = status.RootElement.ValueKind == JsonValueKind.Object && status.RootElement.GetProperty("ready").GetBoolean() ? stable + 1 : 0;
                await Task.Delay(500);
            }
            if (stable < 10) throw new InvalidOperationException("YouTube did not reach stable content playback.");
            await File.WriteAllTextAsync(Path.Combine(output, "live-gif-phase.json"), "{\"phase\":\"capturing\"}");
            await window._browser.ExecuteScriptAsync("document.querySelector('video')?.pause()");
            double before = JsonSerializer.Deserialize<double>(await window._browser.ExecuteScriptAsync("document.querySelector('video').currentTime"));
            var revision = window._revision; var id = window._videoId;
            var source = await BrowserGifSource.CreateAsync(window._browser, window, () => !window._closed && window._revision == revision && window._videoId == id, CancellationToken.None);
            var tools = await CaptureTools.EnsureAsync(new Progress<string>(), CancellationToken.None);
            var path = await source.ExportAsync(tools, new GifOptions(10, 13, 480, 12), output, new Progress<string>(message => window._status.Text = message), CancellationToken.None);
            await Task.Delay(250);
            double after = JsonSerializer.Deserialize<double>(await window._browser.ExecuteScriptAsync("document.querySelector('video').currentTime"));
            if (Math.Abs(after - before) > .2 || await window._browser.ExecuteScriptAsync("document.querySelector('video').paused") != "true") throw new InvalidOperationException("Live playback state was not restored.");
            await window.CaptureLiveAsync(Path.Combine(output, "youtube-gif-live.png"));
            await File.WriteAllTextAsync(Path.Combine(output, "live-gif.json"), JsonSerializer.Serialize(new { passed = true, path, before, after, apiCalled = false, realYouTube = true, start = 10, end = 13 }));
            return 0;
        }
        catch (Exception ex)
        {
            if (window?._ready == true)
            {
                try { await window.CaptureLiveAsync(Path.Combine(output, "youtube-gif-failure.png")); } catch { }
            }
            await File.WriteAllTextAsync(Path.Combine(output, "live-gif.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString(), apiCalled = false }));
            return 1;
        }
        finally { window?.Close(); }
    }
}
