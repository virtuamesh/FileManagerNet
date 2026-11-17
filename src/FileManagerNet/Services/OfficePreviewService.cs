using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace FileManagerNet.Services;

/// <summary>
/// Converts Office documents (Word / Excel / PowerPoint / OpenDocument) to PDF
/// with a LibreOffice headless process so the browser can preview them inline.
/// Converted PDFs are cached by source path + size + last-write time.
/// </summary>
public static class OfficePreviewService
{
    /// <summary>Extensions handled by this service.</summary>
    public static readonly string[] Extensions =
    {
        ".doc", ".docx", ".docm", ".dot", ".dotx", ".wps", ".rtf", ".odt",
        ".xls", ".xlsx", ".xlsm", ".xlsb", ".et", ".csv", ".ods",
        ".ppt", ".pptx", ".pps", ".ppsx", ".pot", ".potx", ".dps", ".odp"
    };

    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly object CleanupLock = new();
    private static DateTime _lastCleanup = DateTime.MinValue;

    /// <summary>True when <paramref name="path"/> has an extension this service can convert.</summary>
    /// <param name="path">File name or path to test.</param>
    public static bool IsOfficeFile(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Returns the configured LibreOffice executable or probes the
    /// usual install locations.</summary>
    /// <param name="configuredPath">Explicit path from configuration; empty auto-detects.</param>
    /// <returns>The executable path, or <c>null</c> when nothing was found.</returns>
    public static string? ResolveSoffice(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return File.Exists(configuredPath) ? configuredPath : null;

        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(pf))
                candidates.Add(Path.Combine(pf, "LibreOffice", "program", "soffice.exe"));
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(pf86))
                candidates.Add(Path.Combine(pf86, "LibreOffice", "program", "soffice.exe"));
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates.Add("/Applications/LibreOffice.app/Contents/MacOS/soffice");
        }
        else
        {
            candidates.Add("/usr/bin/soffice");
            candidates.Add("/usr/local/bin/soffice");
            candidates.Add("/usr/lib/libreoffice/program/soffice");
            candidates.Add("/snap/bin/libreoffice");
        }
        candidates.Add("soffice");
        var found = candidates.FirstOrDefault(File.Exists);
        return found ?? FindOnPath("soffice");
    }

    /// <summary>Looks up an executable on the PATH.</summary>
    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            foreach (var candidate in new[]
            {
                Path.Combine(dir, name),
                Path.Combine(dir, name + ".exe"),
                Path.Combine(dir, name + ".cmd"),
                Path.Combine(dir, name + ".bat")
            })
            {
                try { if (File.Exists(candidate)) return candidate; } catch { /* ignore */ }
            }
        }
        return null;
    }

    /// <summary>Returns the configured PDF cache folder, or the default temp folder.</summary>
    /// <param name="configured">Folder from configuration; empty uses the default.</param>
    public static string ResolveCacheFolder(string configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "fmnet-office-cache")
            : configured;

    /// <summary>Converts a file to PDF, or returns the cached PDF.</summary>
    /// <param name="physicalFile">Full physical path of the Office document.</param>
    /// <param name="soffice">LibreOffice executable, or <c>null</c> when unavailable.</param>
    /// <param name="cacheFolder">Folder for converted PDFs.</param>
    /// <param name="timeoutSeconds">Conversion timeout in seconds.</param>
    /// <param name="cacheMinutes">How long a converted PDF stays cached; 0 keeps it.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>The PDF path, or an error message when conversion failed.</returns>
    public static async Task<(string? Pdf, string? Error)> GetPdfAsync(
        string physicalFile, string? soffice, string cacheFolder,
        int timeoutSeconds, int cacheMinutes, CancellationToken ct = default)
    {
        Directory.CreateDirectory(cacheFolder);
        var info = new FileInfo(physicalFile);
        var target = Path.Combine(cacheFolder, CacheKey(physicalFile, info) + ".pdf");
        if (File.Exists(target)) return (target, null);
        if (string.IsNullOrEmpty(soffice))
            return (null, "LibreOffice was not found. Set FileManagerConfig.LibreOfficePath.");

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(target)) return (target, null);

            var work = Path.Combine(cacheFolder, "work-" + Guid.NewGuid().ToString("N"));
            var profile = Path.Combine(cacheFolder, "profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = soffice,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var arg in new[]
                {
                    "--headless", "--norestore", "--nolockcheck", "--nodefault", "--nologo",
                    "-env:UserInstallation=" + new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri,
                    "--convert-to", "pdf", "--outdir", work, physicalFile
                })
                    psi.ArgumentList.Add(arg);

                using var process = StartProcess(psi, out var startError);
                if (process is null) return (null, startError);
                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds)));
                try
                {
                    await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    return (null, "Conversion timed out after " + timeoutSeconds + "s.");
                }

                var produced = Directory.EnumerateFiles(work, "*.pdf").FirstOrDefault();
                if (produced == null)
                {
                    var err = (await stderrTask.ConfigureAwait(false)).Trim();
                    if (err.Length == 0) err = (await stdoutTask.ConfigureAwait(false)).Trim();
                    return (null, err.Length == 0 ? "Conversion failed." : err);
                }

                File.Move(produced, target, overwrite: true);
                CleanupCache(cacheFolder, cacheMinutes);
                return (target, null);
            }
            finally
            {
                TryDeleteDirectory(work);
                TryDeleteDirectory(profile);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Starts the conversion process, turning launch failures into a message.</summary>
    private static Process? StartProcess(ProcessStartInfo psi, out string? error)
    {
        try { error = null; return Process.Start(psi); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            error = "Could not start LibreOffice (" + psi.FileName + "): " + ex.Message;
            return null;
        }
    }

    /// <summary>Builds a cache file name from the path, size and last-write time,
    /// so editing a document invalidates its converted PDF.</summary>
    private static string CacheKey(string path, FileInfo info)
    {
        var raw = path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    /// <summary>Deletes cached PDFs older than <paramref name="minutes"/> (at
    /// most once every ten minutes).</summary>
    private static void CleanupCache(string folder, int minutes)
    {
        if (minutes <= 0) return;
        lock (CleanupLock)
        {
            if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(10)) return;
            _lastCleanup = DateTime.UtcNow;
            try
            {
                var limit = DateTime.UtcNow.AddMinutes(-minutes);
                foreach (var f in Directory.EnumerateFiles(folder, "*.pdf"))
                {
                    try { if (File.GetLastWriteTimeUtc(f) < limit) File.Delete(f); } catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>Deletes a temporary folder when it exists; never throws.</summary>
    private static void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* ignore */ }
    }
}
