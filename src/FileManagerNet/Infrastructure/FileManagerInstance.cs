using FileManagerNet.Models;
using FileManagerNet.Services;

namespace FileManagerNet.Infrastructure;

/// <summary>Server-side state of one registered file manager instance.</summary>
internal sealed class FileManagerInstance
{
    /// <summary>Browsable root folders with their access rules.</summary>
    public required IReadOnlyList<FileManagerRoot> Roots { get; init; }

    /// <summary>Display name of the root node in the tree pane.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Feature switches of the instance.</summary>
    public required FileManagerConfig Config { get; init; }

    /// <summary>Service shared by all requests of the instance, created on first use.</summary>
    public FileSystemService? Service { get; set; }
}
