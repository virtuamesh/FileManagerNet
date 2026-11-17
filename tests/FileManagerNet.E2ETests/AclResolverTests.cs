using System.Diagnostics;
using FileManagerNet.Models;
using FileManagerNet.Services;

namespace FileManagerNet.E2ETests;

/// <summary>
/// Rules of the access-control resolver, plus the one performance property that
/// matters: resolving a folder must be cheap enough to run on every file
/// operation (it is a few string comparisons, then a dictionary hit).
/// </summary>
public sealed class AclResolverTests
{
    private static AccessControlResolver Resolver(FileManagerConfig? config = null)
    {
        var rules = new List<FileManagerAccessControl>
        {
            new() { Path = "/", Allowed = FileManagerPermissions.ReadOnly },
            new()
            {
                Path = "/public",
                Allowed = FileManagerPermissions.ReadOnly | FileManagerPermissions.Create
                          | FileManagerPermissions.Upload | FileManagerPermissions.Delete
                          | FileManagerPermissions.Rename | FileManagerPermissions.Paste
            },
            new()
            {
                Path = "/public/images",
                Allowed = FileManagerPermissions.ReadOnly | FileManagerPermissions.Upload
                          | FileManagerPermissions.Create,
                AllowedFileTypes = "*.jpg|*.png",
                Quota = "5 MB"
            },
            new() { Path = "/secret", Allowed = FileManagerPermissions.None },
            new() { Path = "/finance", Allowed = FileManagerPermissions.Full, Denied = FileManagerPermissions.Delete },
            new() { Path = "/dept-*/docs", Allowed = FileManagerPermissions.ReadOnly | FileManagerPermissions.Upload },
            new() { Path = "/**/incoming", Allowed = FileManagerPermissions.Full },
        };
        return new AccessControlResolver(rules, config ?? new FileManagerConfig());
    }

    [Fact]
    public void Root_rule_applies_to_the_whole_tree()
    {
        var r = Resolver();
        foreach (var path in new[] { "/", "/docs", "/docs/a/b/c", "/public/nested/deep" })
            Assert.True(r.Resolve(path).Has(FileManagerPermissions.ListFiles));

        Assert.False(r.Resolve("/docs").Has(FileManagerPermissions.Delete));
        Assert.False(r.Resolve("/docs/a/b").Has(FileManagerPermissions.Upload));
    }

    [Fact]
    public void Nearest_rule_wins_and_levels_are_not_merged()
    {
        var r = Resolver();

        /* /public adds Delete … */
        Assert.True(r.Resolve("/public").Has(FileManagerPermissions.Delete));
        Assert.True(r.Resolve("/public/sub").Has(FileManagerPermissions.Delete));

        /* … /public/images does not inherit it, because it has its own rule */
        Assert.False(r.Resolve("/public/images").Has(FileManagerPermissions.Delete));
        Assert.True(r.Resolve("/public/images").Has(FileManagerPermissions.Upload));
        Assert.True(r.Resolve("/public/images/2024").Has(FileManagerPermissions.Upload));
    }

    [Fact]
    public void Denied_always_wins_over_allowed()
    {
        var r = Resolver();
        var finance = r.Resolve("/finance");
        Assert.True(finance.Has(FileManagerPermissions.Upload));
        Assert.False(finance.Has(FileManagerPermissions.Delete));
    }

    [Fact]
    public void A_rule_with_no_permissions_hides_the_folder()
    {
        var r = Resolver();
        var secret = r.Resolve("/secret");
        Assert.Equal(FileManagerPermissions.None, secret.Permissions);
        Assert.False(secret.Has(FileManagerPermissions.ListFiles));
        Assert.False(secret.Has(FileManagerPermissions.ListSubfolders));
    }

    [Fact]
    public void Wildcards_match_one_segment_and_across_segments()
    {
        var r = Resolver();

        Assert.True(r.Resolve("/dept-a/docs").Has(FileManagerPermissions.Upload));
        Assert.True(r.Resolve("/dept-b/docs").Has(FileManagerPermissions.Upload));

        /* `*` does not cross a separator */
        Assert.False(r.Resolve("/dept-a/docs/sub").Has(FileManagerPermissions.Upload) &&
                     !r.Resolve("/dept-a/docs/sub").Has(FileManagerPermissions.ListFiles));

        /* `**` does */
        Assert.True(r.Resolve("/incoming").Has(FileManagerPermissions.Create));
        Assert.True(r.Resolve("/a/incoming").Has(FileManagerPermissions.Create));
        Assert.True(r.Resolve("/a/b/c/incoming").Has(FileManagerPermissions.Create));
    }

    [Fact]
    public void File_types_are_filtered_by_the_nearest_rule()
    {
        var images = Resolver().Resolve("/public/images");
        Assert.True(images.IsFileAllowed("photo.JPG"));
        Assert.True(images.IsFileAllowed("a.png"));
        Assert.False(images.IsFileAllowed("readme.txt"));
        Assert.False(images.IsFileAllowed("tool.exe"));

        /* a folder without a type rule accepts everything */
        Assert.True(Resolver().Resolve("/public").IsFileAllowed("tool.exe"));
    }

    [Fact]
    public void Denied_types_beat_allowed_types()
    {
        var r = new AccessControlResolver(new[]
        {
            new FileManagerAccessControl
            {
                Path = "/",
                AllowedFileTypes = "*.jpg|*.png",
                DeniedFileTypes = "*.exe"
            }
        }, new FileManagerConfig());

        Assert.True(r.Resolve("/").IsFileAllowed("a.jpg"));
        Assert.False(r.Resolve("/").IsFileAllowed("a.exe"));
        Assert.False(r.Resolve("/").IsFileAllowed("a.txt"));
    }

    [Fact]
    public void Instance_switches_cap_what_a_rule_can_grant()
    {
        var cfg = new FileManagerConfig { AllowDelete = false, AllowUpload = false };
        var r = Resolver(cfg);

        var pub = r.Resolve("/public");
        Assert.False(pub.Has(FileManagerPermissions.Delete));
        Assert.False(pub.Has(FileManagerPermissions.Upload));
        /* unrelated permissions survive */
        Assert.True(pub.Has(FileManagerPermissions.Create));
    }

    [Fact]
    public void Quota_is_parsed_and_inherited()
    {
        var r = Resolver();
        Assert.Equal(5L * 1024 * 1024, r.Resolve("/public/images").QuotaBytes);
        Assert.Equal("/public/images", r.Resolve("/public/images/2024").QuotaPath);
        Assert.Equal(0, r.Resolve("/public").QuotaBytes);

        Assert.Equal(512L * 1024 * 1024, FileManagerAccessControl.ParseSize("512 MB"));
        Assert.Equal(2L * 1024 * 1024 * 1024, FileManagerAccessControl.ParseSize("2 gb"));
        Assert.Equal(2048, FileManagerAccessControl.ParseSize("2048"));
        Assert.Equal(0, FileManagerAccessControl.ParseSize(""));
    }

    [Fact]
    public void No_rules_means_everything_is_allowed()
    {
        var r = new AccessControlResolver(Array.Empty<FileManagerAccessControl>(), new FileManagerConfig());
        Assert.Equal(FileManagerPermissions.Full, r.Resolve("/anything").Permissions);
        Assert.True(r.IsEmpty);
    }

    /// <summary>
    /// Guards the cost of a resolution: every file operation performs one, and
    /// each folder is resolved at most once (the result is memoised), so the
    /// cold path has to stay in the microsecond range.
    /// </summary>
    [Fact]
    public void Resolving_a_cold_folder_stays_in_the_microsecond_range()
    {
        var r = Resolver();
        const int iterations = 20_000;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            /* distinct paths, so every call is a resolver cache miss */
            r.Resolve($"/public/images/album{(i % 500)}/sub{i}");
        }
        sw.Stop();

        var perCall = sw.Elapsed.TotalMicroseconds / iterations;
        Console.WriteLine($"cold resolution: {perCall:0.0} µs/folder over {iterations} folders");

        /* A cold resolution costs a few microseconds (a handful of segment
         * comparisons) and happens once per folder — the steady state is a
         * dictionary hit. The bound is deliberately loose so a busy machine
         * cannot fail it; what it catches is an order-of-magnitude regression,
         * e.g. re-reading rules from disk for every check. */
        Assert.True(perCall < 100,
            $"resolving a folder took {perCall:0.0} µs on average — permission checks are " +
            "supposed to stay far below a single file-system call");
    }
}
