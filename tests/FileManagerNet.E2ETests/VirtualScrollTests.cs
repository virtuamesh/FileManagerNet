using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class VirtualScrollTests : E2ETestBase
{
    public VirtualScrollTests(TestHostFixture fx) : base(fx) { }

    private Task<int> ScrollHeightAsync() =>
        Page.EvaluateAsync<int>("() => document.querySelector('[data-fmnet]').fmnet.$.listwrap.scrollHeight");

    private Task<int> ScrollTopAsync() =>
        Page.EvaluateAsync<int>("() => document.querySelector('[data-fmnet]').fmnet.$.listwrap.scrollTop");

    [Fact]
    public async Task A_folder_with_1200_entries_only_renders_a_window()
    {
        await Fm.GotoAsync();
        await Fm.GoToFolderAsync("/大量文件");

        var rendered = await Fm.Rows.CountAsync();
        Assert.True(rendered < 200, $"expected a small window, rendered {rendered} rows");
        Assert.True(await ScrollHeightAsync() > 20_000, "the scrollbar must still cover the whole list");

        Assert.Equal("file-0000.txt", await Fm.FileRows.First.GetAttributeAsync("data-name"));
    }

    [Fact]
    public async Task Scrolling_swaps_the_window_and_keeps_the_position_on_selection()
    {
        await Fm.GotoAsync();
        await Fm.GoToFolderAsync("/大量文件");

        await Page.EvaluateAsync(
            "() => { const w = document.querySelector('[data-fmnet]').fmnet.$.listwrap; w.scrollTop = w.scrollHeight / 2; }");
        await Page.WaitForTimeoutAsync(400);

        var middle = await Fm.FileRows.First.GetAttributeAsync("data-name");
        Assert.NotEqual("file-0000.txt", middle);
        Assert.True(await Fm.Rows.CountAsync() < 200);

        /* re-rendering the window (what a selection change does) must not move
         * the scroll position — Playwright's own click() would scroll the row
         * into view first, so drive the re-render directly */
        var before = await ScrollTopAsync();
        await Page.EvaluateAsync("() => document.querySelector('[data-fmnet]').fmnet.renderList()");
        await Page.WaitForTimeoutAsync(300);
        var after = await ScrollTopAsync();
        Assert.InRange(Math.Abs(after - before), 0, 40);

        await Fm.FileRows.Nth(4).ClickAsync();
        await Page.WaitForTimeoutAsync(200);
        Assert.Equal(1, await Fm.SelectionCountAsync());
    }
}
