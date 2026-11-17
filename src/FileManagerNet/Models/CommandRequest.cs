namespace FileManagerNet.Models;

/// <summary>Request payload of the JSON command endpoint (<c>POST {api}</c>).</summary>
internal sealed class CommandRequest
{
    /// <summary>Command name, for example "list", "rename" or "copy".</summary>
    public string Command { get; set; } = "";

    /// <summary>Virtual path the command operates on.</summary>
    public string Path { get; set; } = "/";

    /// <summary>New name for the rename command.</summary>
    public string? NewName { get; set; }

    /// <summary>Destination folder for copy / move / extract.</summary>
    public string? Destination { get; set; }

    /// <summary>Replaces existing items with the same name.</summary>
    public bool Overwrite { get; set; }

    /// <summary>Virtual paths of the items a batch command operates on.</summary>
    public List<string>? Paths { get; set; }

    /// <summary>Search parameters for the search command.</summary>
    public SearchOptions? Search { get; set; }

    /// <summary>Runs long archive commands in the background and returns a job
    /// id that can be polled via <c>GET {api}/job?id=…</c>.</summary>
    public bool Async { get; set; }

    /// <summary>On a name clash, keep both items by generating "name (1).ext".</summary>
    public bool RenameOnConflict { get; set; }

    /// <summary>For <c>list</c>: force a fresh enumeration instead of sharing a
    /// result with a listing that was already running.</summary>
    public bool NoCache { get; set; }

    /// <summary>For <c>stats</c>: walk the whole subtree instead of the current
    /// folder only. Off by default because the status bar only needs the volume
    /// figures, and a deep walk is an O(subtree) disk scan.</summary>
    public bool Deep { get; set; }
}
