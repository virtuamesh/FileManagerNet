using System.Text.Json.Serialization;

namespace FileManagerNet.Models;

/// <summary>
/// One folder node of the navigation tree. Children are loaded lazily by
/// requesting the node's <see cref="Path"/> again.
/// </summary>
public sealed class TreeNodeDto
{
    /// <summary>Display name of the folder.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Virtual, root-relative path of the folder.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    /// <summary>
    /// True when the folder may have children. The client uses this hint to
    /// render an expand arrow and only confirms it when the node is opened.
    /// </summary>
    [JsonPropertyName("hasChildren")]
    public bool HasChildren { get; set; }
}
