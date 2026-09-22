using System;
using System.IO;
using System.Linq;

namespace ShinPlayer;

internal static class ExportFiles
{
    internal static string SafeName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        string name = new(title.Where(c => !invalid.Contains(c) && !char.IsControl(c)).Take(65).ToArray());
        name = name.Trim().TrimEnd('.');
        return name.Length == 0 ? "영상" : name;
    }

    // Commit only a completed file. Concurrent exports never replace an existing result.
    internal static string Commit(string partial, string folder, string stem, string extension)
    {
        for (int suffix = 0; ; suffix++)
        {
            string path = Path.Combine(folder, stem + (suffix == 0 ? "" : $"_{suffix}") + extension);
            try { File.Move(partial, path, false); return path; }
            catch (IOException) when (File.Exists(path)) { }
        }
    }
}
