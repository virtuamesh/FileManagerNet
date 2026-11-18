using FileManagerNet.Models;
using FileManagerNet.Services;

namespace FileManagerNet.E2ETests;

/// <summary>
/// Path handling that must keep working on Windows while also accepting the
/// characters Linux/macOS allow in file names and root folders.
/// </summary>
public sealed class PathCompatibilityTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "fmnet-paths-" + Guid.NewGuid().ToString("N"));

    public PathCompatibilityTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    [Fact]
    public void ParseMany_keeps_equals_inside_a_rooted_path()
    {
        var roots = FileManagerRoot.ParseMany("/srv/data=archive;Name=/srv/backup");

        Assert.Equal(2, roots.Count);
        Assert.Equal("/srv/data=archive", roots[0].Path);
        Assert.Equal("Name", roots[1].Name);
        Assert.Equal("/srv/backup", roots[1].Path);
    }

    [Fact]
    public void Listing_accepts_double_dots_inside_a_name()
    {
        File.WriteAllText(Path.Combine(_root, "report..final.txt"), "x");
        var svc = new FileSystemService(_root);

        var list = svc.List("/");

        Assert.Contains(list.Entries, e => e.Name == "report..final.txt");
    }

    [Fact]
    public void Traversal_segments_are_still_rejected()
    {
        var svc = new FileSystemService(_root);

        Assert.Throws<UnauthorizedAccessException>(() => svc.Resolve("/../outside"));
        Assert.Throws<UnauthorizedAccessException>(() => svc.Resolve("/sub/../outside"));
    }
}
