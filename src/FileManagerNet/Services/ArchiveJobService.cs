using System.Collections.Concurrent;

namespace FileManagerNet.Services;

/// <summary>
/// In-memory registry of archive jobs. Finished jobs are swept after 15
/// minutes so a long-running server does not keep them forever.
/// </summary>
internal static class ArchiveJobService
{
    private static readonly ConcurrentDictionary<string, ArchiveJob> Jobs = new(StringComparer.Ordinal);
    private static long _lastSweepTicks;

    /// <summary>Creates and registers a new job.</summary>
    /// <param name="kind">Operation kind ("zip" or "extract").</param>
    /// <param name="label">Archive name shown in the progress dialog.</param>
    /// <returns>The registered job.</returns>
    public static ArchiveJob Create(string kind, string label)
    {
        Sweep();
        var job = new ArchiveJob(kind, label);
        Jobs[job.Id] = job;
        return job;
    }

    /// <summary>Returns a job by id, or <c>null</c> when it is unknown or expired.</summary>
    /// <param name="id">Job id previously returned to the client.</param>
    public static ArchiveJob? Get(string id) =>
        string.IsNullOrEmpty(id) ? null : Jobs.TryGetValue(id, out var job) ? job : null;

    /// <summary>Removes jobs that finished more than 15 minutes ago.</summary>
    private static void Sweep()
    {
        var now = DateTime.UtcNow;
        var last = new DateTime(Interlocked.Read(ref _lastSweepTicks), DateTimeKind.Utc);
        if ((now - last).TotalMinutes < 5) return;
        Interlocked.Exchange(ref _lastSweepTicks, now.Ticks);

        foreach (var kv in Jobs)
        {
            var finished = kv.Value.FinishedAt;
            if (finished.HasValue && (now - finished.Value).TotalMinutes > 15)
                Jobs.TryRemove(kv.Key, out _);
        }
    }
}
