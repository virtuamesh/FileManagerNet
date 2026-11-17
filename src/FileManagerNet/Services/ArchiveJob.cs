namespace FileManagerNet.Services;

/// <summary>
/// State of one long-running archive operation. The browser starts the job,
/// then polls <c>GET {api}/job?id=…</c> to render a progress bar.
/// </summary>
internal sealed class ArchiveJob
{
    /// <summary>Unique job id used by the polling endpoint.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Operation kind: "zip" or "extract".</summary>
    public string Kind { get; init; } = "zip";

    /// <summary>Archive name shown in the progress dialog.</summary>
    public string Label { get; init; } = "";

    /// <summary>Current state: queued, running, done or failed.</summary>
    public volatile string State;

    private int _total = -1;
    private int _done;

    /// <summary>Creates a job in the queued state.</summary>
    /// <param name="kind">Operation kind ("zip" or "extract").</param>
    /// <param name="label">Archive name shown in the dialog.</param>
    public ArchiveJob(string kind, string label)
    {
        Kind = kind;
        Label = label;
        State = "queued";
    }

    /// <summary>Number of entries to process, or -1 when the format cannot be
    /// pre-scanned (streamed tar.gz / bz2 / single-stream archives).</summary>
    public int Total
    {
        get => Volatile.Read(ref _total);
        set => Volatile.Write(ref _total, value);
    }

    /// <summary>Number of entries processed so far.</summary>
    public int Done
    {
        get => Volatile.Read(ref _done);
        set => Volatile.Write(ref _done, value);
    }

    /// <summary>Entry currently being processed.</summary>
    public string Current { get; set; } = "";

    /// <summary>Failure message when <see cref="State"/> is "failed".</summary>
    public string? Error { get; set; }

    /// <summary>UTC time the job was created.</summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>UTC time the job finished, or <c>null</c> while it is running.</summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>Thread-safe progress sink handed to the file-system service.</summary>
    public Action<int, int, string> Progress => (done, total, current) =>
    {
        Done = done;
        if (total >= 0) Total = total;
        if (current != null) Current = current;
    };
}
