using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class MultiRootTests : E2ETestBase
{
    public MultiRootTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task The_root_lists_the_mounted_folders()
    {
        await Page.GotoAsync(Fx.BaseUrl + "/Roots");
        await Expect(Fm.Row("First")).ToBeVisibleAsync();
        await Expect(Fm.Row("Second")).ToBeVisibleAsync();

        await Fm.Row("First").DblClickAsync();
        await Expect(Page.Locator(".fm-crumb.current")).ToHaveTextAsync("First");
        await Expect(Fm.Row("readme.txt")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Creating_an_item_at_the_virtual_root_is_refused()
    {
        await Page.GotoAsync(Fx.BaseUrl + "/Roots");
        var before = Directory.GetDirectories(Fx.Root1).Length;

        await Fm.ClickRibbonAsync("newFolder");
        await Expect(Page.Locator(".fm-toast-title", new() { HasTextString = "Open one of the folders first" }).First)
            .ToBeVisibleAsync();
        await Expect(Page.Locator(".fm-dialog")).ToHaveCountAsync(0);
        Assert.Equal(before, Directory.GetDirectories(Fx.Root1).Length);
    }

    [Fact]
    public async Task A_folder_inside_a_mount_accepts_new_items()
    {
        await Page.GotoAsync(Fx.BaseUrl + "/Roots");
        await Fm.Row("First").DblClickAsync();
        await Expect(Page.Locator(".fm-crumb.current")).ToHaveTextAsync("First");

        var name = TempName("inmount");
        await Fm.ClickRibbonAsync("newFolder");
        await Fm.FillPromptAsync(name);

        await Expect(Fm.Row(name)).ToBeVisibleAsync();
        Assert.True(Directory.Exists(Path.Combine(Fx.Root1, name)));
        Directory.Delete(Path.Combine(Fx.Root1, name));
    }
}
