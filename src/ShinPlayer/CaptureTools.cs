using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ShinPlayer;

internal sealed record CaptureTools(string Ffmpeg, string Ffprobe)
{
    internal const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.zip";
    internal const string ArchiveHash = "db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec";
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };
    internal static string CacheDirectory => Path.Combine(PlayerSettings.DirectoryPath, "capture-tools", "ffmpeg-8.1.2");

    internal static CaptureTools? FindInstalled()
    {
        // Never resolve executables from a video's folder or the current directory.
        var folders = new[] { CacheDirectory }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (var folder in folders)
        {
            var path = folder.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(path)) continue;
            var ffmpeg = Path.Combine(path, "ffmpeg.exe");
            var ffprobe = Path.Combine(path, "ffprobe.exe");
            if (File.Exists(ffmpeg) && File.Exists(ffprobe)) return new(ffmpeg, ffprobe);
        }
        return null;
    }

    internal static async Task<CaptureTools> EnsureAsync(IProgress<string> progress, CancellationToken cancel)
    {
        if (FindInstalled() is { } installed) return installed;
        await DownloadLock.WaitAsync(cancel);
        string? staging = null;
        try
        {
            if (FindInstalled() is { } available) return available;
            staging = Path.Combine(PlayerSettings.DirectoryPath, "capture-tools", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var archive = Path.Combine(staging, "tools.zip");
            using (var response = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancel))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancel);
                await using var output = File.Create(archive);
                var bytes = new byte[81920];
                long total = 0;
                var clock = Stopwatch.StartNew();
                int count;
                while ((count = await input.ReadAsync(bytes, cancel)) > 0)
                {
                    await output.WriteAsync(bytes.AsMemory(0, count), cancel);
                    total += count;
                    if (clock.ElapsedMilliseconds < 200) continue;
                    progress.Report($"캡처 도구 준비 중 · {total / 1048576d:0} MB 다운로드");
                    clock.Restart();
                }
            }
            progress.Report("캡처 도구 무결성 확인 중…");
            await using (var file = File.OpenRead(archive))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancel));
                if (!hash.Equals(ArchiveHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("캡처 도구의 SHA256이 일치하지 않습니다. 다시 시도해 주세요.");
            }
            var ready = Path.Combine(staging, "ready");
            Directory.CreateDirectory(ready);
            using (var zip = ZipFile.OpenRead(archive))
            {
                foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe", "LICENSE", "README.txt" })
                {
                    var entry = zip.Entries.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (entry == null)
                    {
                        if (name.EndsWith(".exe", StringComparison.Ordinal)) throw new IOException("다운로드한 도구 파일이 올바르지 않습니다.");
                        continue;
                    }
                    await using var source = entry.Open();
                    await using var target = File.Create(Path.Combine(ready, name));
                    await source.CopyToAsync(target, cancel);
                }
            }
            cancel.ThrowIfCancellationRequested();
            // Only publish a complete verified pair of executables.
            if (Directory.Exists(CacheDirectory)) throw new IOException("캡처 도구 캐시가 불완전합니다. capture-tools 폴더를 확인해 주세요.");
            Directory.Move(ready, CacheDirectory);
            return new(Path.Combine(CacheDirectory, "ffmpeg.exe"), Path.Combine(CacheDirectory, "ffprobe.exe"));
        }
        finally
        {
            if (staging != null) RemovePrivateDirectory(staging, Path.Combine(PlayerSettings.DirectoryPath, "capture-tools"));
            DownloadLock.Release();
        }
    }

    internal static async Task<string> RunAsync(string executable, string[] arguments, string workingDirectory, CancellationToken cancel, int timeoutSeconds = 120)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = workingDirectory, StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        timeout.Token.ThrowIfCancellationRequested();
        process.Start();
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
        using var registration = timeout.Token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
        });
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var error = await stderr;
            timeout.Token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new InvalidOperationException("영상 처리에 실패했습니다. " + (error.Length > 1200 ? error[^1200..] : error).Trim());
            return output;
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new TimeoutException("영상 처리 시간이 초과되었습니다. 저장 공간과 파일 상태를 확인해 주세요.");
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            await process.WaitForExitAsync();
            try { await Task.WhenAll(stdout, stderr); } catch (OperationCanceledException) { }
        }
    }

    internal static void RemovePrivateDirectory(string directory, string parent)
    {
        var full = Path.GetFullPath(directory);
        var root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("임시 폴더 경로가 올바르지 않습니다.");
        try { if (Directory.Exists(full)) Directory.Delete(full, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { App.Log(ex.Message); }
    }
}
