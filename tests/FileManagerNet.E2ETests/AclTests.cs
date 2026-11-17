using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

/// <summary>
/// Access rules: the same instance with folders that are read-only, writable,
/// type-filtered and quota-limited. Every case checks both sides — what the UI
/// offers and what the server accepts — because the server must not trust the
/// client.
/// </summary>
public sealed class AclTests : E2ETestBase
{
    public AclTests(TestHostFixture fx) : base(fx) { }

    private string AclRoot => Path.Combine(Fm.Root1, "_acl");

    private void ResetTree()
    {
        Directory.CreateDirectory(AclRoot);
        foreach (var dir in new[] { "docs", "public", "public/images", "secret" })
            Directory.CreateDirectory(Path.Combine(AclRoot, dir));

        File.WriteAllText(Path.Combine(AclRoot, "docs", "readme.txt"), "read only");
        File.WriteAllText(Path.Combine(AclRoot, "public", "notes.txt"), "writable");
        File.WriteAllText(Path.Combine(AclRoot, "secret", "secret.txt"), "hidden");

        var images = Path.Combine(AclRoot, "public", "images");
        foreach (var f in Directory.GetFiles(images)) File.Delete(f);
        File.WriteAllText(Path.Combine(images, "photo.jpg"), "jpeg");
        File.WriteAllText(Path.Combine(images, "readme.txt"), "not an image");
    }

    private ILocator RibbonAct(string act) => Page.Locator($".fm-ribbon-body:not([hidden]) [data-act=\"{act}\"]");

    private async Task<(int Status, string Body)> CommandAsync(object payload)
    {
        var resp = await Page.APIRequest.PostAsync(
            $"{Fx.BaseUrl}/filemanager-api?instance=FM_ACL",
            new() { DataObject = payload });
        return (resp.Status, await resp.TextAsync());
    }

    [Fact]
    public async Task Read_only_folder_disables_the_write_controls()
    {
        ResetTree();
        await Fm.GotoAsync("/Acl");

        foreach (var act in new[] { "delete", "rename", "newFolder", "newFile", "upload", "uploadFolder", "zip" })
            await Expect(RibbonAct(act)).ToBeDisabledAsync();

        /* browsing, downloading and previewing stay available */
        await Expect(RibbonAct("download")).ToBeEnabledAsync();
        await Expect(RibbonAct("open")).ToBeEnabledAsync();
    }

    [Fact]
    public async Task Writable_subfolder_restores_the_controls()
    {
        ResetTree();
        await Fm.GotoAsync("/Acl");
        await Expect(RibbonAct("delete")).ToBeDisabledAsync();

        await Fm.GoToFolderAsync("/public");

        foreach (var act in new[] { "delete", "rename", "newFolder", "upload" })
            await Expect(RibbonAct(act)).ToBeEnabledAsync();
    }

    [Fact]
    public async Task Context_menu_omits_actions_the_folder_forbids()
    {
        ResetTree();
        await Fm.GotoAsync("/Acl");

        await Fm.ContextMenuAsync("docs");
        var items = Page.Locator(".fm-ctx .fm-ctx-item");
        await Expect(items.First).ToBeVisibleAsync();

        var labels = await items.AllInnerTextsAsync();
        Assert.DoesNotContain(labels, t => t.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(labels, t => t.Contains("Rename", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(labels, t => t.Contains("Open", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Server_rejects_writes_in_a_read_only_folder()
    {
        ResetTree();

        var (createStatus, createBody) = await CommandAsync(
            new { command = "createFolder", path = "/docs/newfolder" });
        Assert.Equal(403, createStatus);
        Assert.Contains("not allowed", createBody, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(AclRoot, "docs", "newfolder")));

        var (deleteStatus, _) = await CommandAsync(
            new { command = "delete", paths = new[] { "/docs/readme.txt" } });
        Assert.Equal(403, deleteStatus);
        Assert.True(File.Exists(Path.Combine(AclRoot, "docs", "readme.txt")));
    }

    [Fact]
    public async Task Server_accepts_writes_in_a_writable_folder()
    {
        ResetTree();

        var (status, _) = await CommandAsync(new { command = "createFolder", path = "/public/newfolder" });
        Assert.Equal(200, status);
        Assert.True(Directory.Exists(Path.Combine(AclRoot, "public", "newfolder")));

        var (delStatus, _) = await CommandAsync(
            new { command = "delete", paths = new[] { "/public/newfolder" }, path = "/public" });
        Assert.Equal(200, delStatus);
        Assert.False(Directory.Exists(Path.Combine(AclRoot, "public", "newfolder")));
    }

    [Fact]
    public async Task Folder_without_access_cannot_be_listed()
    {
        ResetTree();

        var (status, body) = await CommandAsync(new { command = "list", path = "/secret" });
        Assert.Equal(403, status);
        Assert.Contains("permission", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Files_of_a_denied_type_are_not_listed()
    {
        ResetTree();
        await Fm.GotoAsync("/Acl");
        await Fm.GoToFolderAsync("/public/images");

        await Expect(Fm.Row("photo.jpg")).ToBeVisibleAsync();
        /* readme.txt exists on disk but the rule only allows images */
        Assert.True(File.Exists(Path.Combine(AclRoot, "public", "images", "readme.txt")));
        await Expect(Fm.Row("readme.txt")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Upload_of_a_denied_type_is_rejected_before_transfer()
    {
        ResetTree();
        var payload = "not an image"u8.ToArray();

        var init = await Page.APIRequest.PostAsync(
            $"{Fx.BaseUrl}/filemanager-api/upload/init?instance=FM_ACL",
            new()
            {
                DataObject = new
                {
                    path = "/public/images", name = "note.txt", relativePath = "note.txt",
                    size = payload.Length, lastModified = 0
                }
            });

        Assert.Equal(403, init.Status);
        Assert.Contains("not an allowed file type", await init.TextAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quota_stops_an_upload_that_would_exceed_it()
    {
        ResetTree();
        var payload = new byte[6 * 1024 * 1024];      // limit is 5 MB

        var init = await Page.APIRequest.PostAsync(
            $"{Fx.BaseUrl}/filemanager-api/upload/init?instance=FM_ACL",
            new()
            {
                DataObject = new
                {
                    path = "/public/images", name = "big.jpg", relativePath = "big.jpg",
                    size = payload.Length, lastModified = 0
                }
            });

        Assert.Equal(403, init.Status);
        var body = await init.TextAsync();
        Assert.Contains("5 MB", body);
        Assert.False(File.Exists(Path.Combine(AclRoot, "public", "images", "big.jpg")));
    }

    [Fact]
    public async Task Nearest_rule_wins_over_the_parent()
    {
        ResetTree();
        await Fm.GotoAsync("/Acl");

        /* /public allows deleting … */
        await Fm.GoToFolderAsync("/public");
        await Expect(RibbonAct("delete")).ToBeEnabledAsync();

        /* … but /public/images only grants read + upload + create */
        await Fm.GoToFolderAsync("/public/images");
        await Expect(RibbonAct("delete")).ToBeDisabledAsync();
        await Expect(RibbonAct("upload")).ToBeEnabledAsync();
    }
}
