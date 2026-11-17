using System.Collections.Concurrent;
using FileManagerNet.Models;
using FileManagerNet.Services;

namespace FileManagerNet.Infrastructure;

/// <summary>
/// Registry of file manager instances. Each tag-helper instance registers its
/// root folder(s) here; the API endpoint resolves the roots by instance id, so
/// the client can never pass an arbitrary server path.
/// </summary>
public static class FileManagerRegistry
{
    private static readonly ConcurrentDictionary<string, FileManagerInstance> Instances = new(StringComparer.Ordinal);

    /// <summary>Registers (or refreshes) an instance with a single root folder.</summary>
    public static void Register(string id, string rootPath, string displayName, FileManagerConfig config)
        => Register(id, new[] { new FileManagerRoot(rootPath) }, displayName, config);

    /// <summary>Registers (or refreshes) an instance with one or more root folders.
    ///
    /// A host can call this from <c>Program.cs</c> to attach access rules, then
    /// render the tag helper with the same id: the tag helper re-registers the
    /// instance (to pick up the rendered root path) but keeps the rules it finds,
    /// so page markup never silently drops them.</summary>
    public static void Register(string id, IReadOnlyList<FileManagerRoot> roots, string displayName,
                                FileManagerConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (roots is null || roots.Count == 0)
            throw new ArgumentException("At least one root folder must be registered.", nameof(roots));

        if (Instances.TryGetValue(id, out var existing) && existing.Roots.Count == roots.Count)
        {
            for (var i = 0; i < roots.Count; i++)
                if (roots[i].AccessControls.Count == 0 && existing.Roots[i].AccessControls.Count > 0)
                    roots[i].AccessControls = existing.Roots[i].AccessControls;
        }

        Instances[id] = new FileManagerInstance
        {
            Roots = roots,
            DisplayName = displayName,
            Config = config,
        };
    }

    /// <summary>Returns the feature configuration of an instance, or a default
    /// configuration when the instance is unknown.</summary>
    /// <param name="id">Instance id.</param>
    public static FileManagerConfig GetConfig(string id) =>
        TryGet(id, out var instance) ? instance.Config : new FileManagerConfig();

    /// <summary>Returns the display name of an instance's root node.</summary>
    /// <param name="id">Instance id.</param>
    public static string GetDisplayName(string id) =>
        TryGet(id, out var instance) ? instance.DisplayName : "Home";

    /// <summary>True when the instance exposes more than one root folder, i.e.
    /// its virtual root only contains folders and cannot be written to.</summary>
    public static bool IsMultiRoot(string id) =>
        TryGet(id, out var instance) && instance.Roots.Count > 1;

    /// <summary>Gets (creating once) the file system service bound to an instance's roots.</summary>
    /// <param name="id">Instance id.</param>
    /// <exception cref="KeyNotFoundException">The instance is not registered.</exception>
    public static FileSystemService GetService(string id)
    {
        if (!TryGet(id, out var instance))
            throw new KeyNotFoundException($"File manager instance '{id}' is not registered.");
        instance.Service ??= new FileSystemService(instance.Roots, null,
            instance.Config.ListCacheMilliseconds, instance.Config);
        return instance.Service;
    }

    /// <summary>True when the id is registered with the server.</summary>
    /// <param name="id">Instance id from the request.</param>
    public static bool IsRegistered(string id) => Instances.ContainsKey(id);

    /// <summary>Looks up an instance without throwing.</summary>
    private static bool TryGet(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out FileManagerInstance? instance) =>
        Instances.TryGetValue(id, out instance);
}
