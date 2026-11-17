using System.Text.Json.Serialization;

namespace FileManagerNet.Models;

/// <summary>
/// Result of a folder listing: the entries of one folder, folders first and
/// then sorted by name.
/// </summary>
public sealed class ListResult
{
    /// <summary>Entries of the folder.</summary>
    [JsonPropertyName("entries")]
    public List<FileSystemEntryDto> Entries { get; set; } = new();
}
