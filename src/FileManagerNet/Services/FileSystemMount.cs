namespace FileManagerNet.Services;

/// <summary>
/// One physical folder mounted into a file manager instance, together with the
/// access rules that apply to it.
/// </summary>
/// <param name="Name">First segment of every virtual path below this mount.</param>
/// <param name="PhysicalPath">Full physical folder on the server.</param>
/// <param name="Access">Resolver built from the mount's access-control entries.</param>
internal sealed record FileSystemMount(string Name, string PhysicalPath, AccessControlResolver Access);
