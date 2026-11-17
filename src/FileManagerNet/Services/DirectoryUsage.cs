using System.Collections.Concurrent;

namespace FileManagerNet.Services;

/// <summary>
/// Tracks how much data a folder holds, for quota checks.
///
/// Measuring a folder means walking its whole subtree — exactly the O(subtree)
/// scan this project removed from <c>stats</c> because it does not scale. A
/// quota check must therefore not scan on every write: the size is measured
/// once and then kept up to date incrementally (writes add, deletes subtract),
/// so the steady-state cost of a check is a dictionary lookup. A folder that is
/// only changed from outside the control is re-measured when its cached figure
/// is older than <see cref="Ttl"/>, which bounds the drift without paying the
/// scan repeatedly.
/// </summary>
internal static class DirectoryUsage
{
    private static readonly ConcurrentDictionary<string, DirectoryUsageSlot> Cache = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static readonly long Ttl = (long)TimeSpan.FromSeconds(60).TotalMilliseconds;

    /// <summary>Returns the cached size of a folder, measuring it when the
    /// cached figure is older than one minute.</summary>
    /// <param name="physicalDir">Full physical folder path.</param>
    /// <returns>Total bytes below the folder; 0 when it is missing or unreadable.</returns>
    public static long GetUsage(string physicalDir)
    {
        if (Cache.TryGetValue(physicalDir, out var slot) && Environment.TickCount64 - slot.MeasuredAt < Ttl)
            return slot.Bytes;

        var bytes = Measure(physicalDir);
        Cache[physicalDir] = new DirectoryUsageSlot(bytes, Environment.TickCount64);
        return bytes;
    }

    /// <summary>Applies a byte delta after a write or a delete.</summary>
    /// <param name="physicalDir">Full physical folder path.</param>
    /// <param name="delta">Bytes added (positive) or removed (negative).</param>
    public static void Add(string physicalDir, long delta)
    {
        if (delta == 0) return;
        Cache.AddOrUpdate(physicalDir,
            _ => new DirectoryUsageSlot(Math.Max(0, delta), Environment.TickCount64),
            (_, old) => new DirectoryUsageSlot(Math.Max(0, old.Bytes + delta), Environment.TickCount64));
    }

    /// <summary>Drops the cached size of a folder so the next check re-measures.</summary>
    /// <param name="physicalDir">Full physical folder path.</param>
    public static void Forget(string physicalDir) => Cache.TryRemove(physicalDir, out _);

    /// <summary>Walks the subtree once and returns its total size.</summary>
    private static long Measure(string physicalDir)
    {
        try
        {
            if (!Directory.Exists(physicalDir)) return 0;
            long total = 0;
            foreach (var f in new DirectoryInfo(physicalDir)
                         .EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { total += f.Length; }
                catch (IOException) { /* vanished while enumerating */ }
            }
            return total;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
