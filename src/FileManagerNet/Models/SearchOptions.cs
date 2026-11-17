using System.Text.Json.Serialization;

namespace FileManagerNet.Models;

/// <summary>Parameters of a recursive search request.</summary>
public sealed class SearchOptions
{
    /// <summary>Text that a file or folder name must contain; empty matches every name.</summary>
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    /// <summary>True to search the whole subtree, false for the current folder only.</summary>
    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; } = true;

    /// <summary>Maximum number of results (clamped to 1–5000 by the server).</summary>
    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 500;
}
