namespace FileManagerNet.Models;

/// <summary>
/// Options for <c>AddFileManagerNet()</c>. They act as defaults for every
/// control instance whose tag helper does not override them.
/// </summary>
public sealed class FileManagerOptions
{
    /// <summary>
    /// Default root folder (physical path) used when the tag helper does not
    /// specify one. Several folders may be given separated by <c>;</c>,
    /// optionally named with <c>name=path</c> — the same syntax as the tag
    /// helper's <c>root-folder</c> attribute. With more than one folder the
    /// control shows them as virtual folders under the root node.
    /// </summary>
    public string RootFolder { get; set; } = "";

    /// <summary>
    /// Typed alternative to a <c>;</c>-separated <see cref="RootFolder"/>.
    /// Takes precedence when not empty.
    /// </summary>
    public List<FileManagerRoot> RootFolders { get; set; } = new();

    /// <summary>Adds a browsable root folder and returns these options, for chaining.</summary>
    /// <param name="path">Physical folder on the server.</param>
    /// <param name="name">Optional display name; empty uses the folder name.</param>
    /// <returns>These options, for fluent chaining.</returns>
    public FileManagerOptions AddRoot(string path, string? name = null)
    {
        RootFolders.Add(new FileManagerRoot(path, name));
        return this;
    }

    /// <summary>Display name for the root node in the tree pane.</summary>
    public string RootDisplayName { get; set; } = "Home";

    /// <summary>Default API endpoint route used when the tag helper does not specify one.</summary>
    public string ApiEndpoint { get; set; } = "/filemanager-api";

    /// <summary>Default feature switches for instances created from these options.</summary>
    public FileManagerConfig Config { get; set; } = new();
}
