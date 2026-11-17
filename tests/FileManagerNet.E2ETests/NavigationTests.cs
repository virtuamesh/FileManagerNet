using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class NavigationTests : E2ETestBase
{
    public NavigationTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task Tree_breadcrumb_and_up_button_navigate()
    {
        await Fm.GotoAsync();

        await Fm.TreeRow("文档").ClickAsync();
        await Expect(Page.Locator(".fm-crumb.current")).ToHaveTextAsync("文档");
        await Expect(Fm.Row("报告.txt")).ToBeVisibleAsync();

        await Page.Locator("[data-act='up']").ClickAsync();
        await Expect(Page.Locator(".fm-crumb.current")).ToHaveTextAsync("Test");

        await Fm.TreeRow("图片").ClickAsync();
        await Expect(Fm.Row("sample.png")).ToBeVisibleAsync();

        await Page.Locator("[data-act='home']").ClickAsync();
        await Expect(Fm.Row("readme.txt")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Refresh_picks_up_changes_made_on_disk()
    {
        await Fm.GotoAsync();
        var name = TempName("external", ".txt");
        File.WriteAllText(Path.Combine(Fm.Root1, name), "created outside the UI");

        await Fm.RefreshAsync();
        await Expect(Fm.Row(name)).ToBeVisibleAsync();

        File.Delete(Path.Combine(Fm.Root1, name));
        await Fm.RefreshAsync();
        await Expect(Fm.Row(name)).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData("ExtraLargeIcons", ".fm-grid.view-xlarge")]
    [InlineData("LargeIcons", ".fm-grid.view-large")]
    [InlineData("MediumIcons", ".fm-grid.view-medium")]
    [InlineData("SmallIcons", ".fm-grid.view-small")]
    [InlineData("List", ".fm-listview")]
    [InlineData("Details", ".fm-table")]
    [InlineData("Tiles", ".fm-grid.view-tiles")]
    public async Task Every_view_layout_renders(string view, string selector)
    {
        await Fm.GotoAsync();
        await Fm.ShowTabAsync("view");
        await Page.Locator($"[data-view=\"{view}\"]").ClickAsync();
        await Expect(Page.Locator(selector)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Clicking_a_column_header_sorts_by_size()
    {
        await Fm.GotoAsync();

        await Page.Locator(".fm-table th[data-sort='size']").ClickAsync();
        await Expect(Page.Locator(".fm-table th[data-sort='size']")).ToContainTextAsync("▲");
        var ascending = await Fm.FileRows.First.GetAttributeAsync("data-name");

        await Page.Locator(".fm-table th[data-sort='size']").ClickAsync();
        await Expect(Page.Locator(".fm-table th[data-sort='size']")).ToContainTextAsync("▼");
        var descending = await Fm.FileRows.First.GetAttributeAsync("data-name");

        Assert.Equal("big.txt", descending);       // 4 MB, largest file
        Assert.NotEqual(ascending, descending);
    }
}
