using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class SelectionTests : E2ETestBase
{
    public SelectionTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task Ctrl_click_adds_to_the_selection()
    {
        await Fm.GotoAsync();
        await Fm.SelectAsync("readme.txt");
        await Fm.Row("notes.md").ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Control } });

        Assert.Equal(2, await Fm.SelectionCountAsync());
        await Expect(Fm.StatusBar).ToContainTextAsync("2");
    }

    [Fact]
    public async Task Shift_click_selects_a_range()
    {
        await Fm.GotoAsync();
        await Fm.SelectAsync("readme.txt");
        await Fm.Row("data.csv").ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Shift } });

        Assert.True(await Fm.SelectionCountAsync() >= 2);
    }

    [Fact]
    public async Task Select_all_none_and_invert()
    {
        await Fm.GotoAsync();
        var total = await Fm.Rows.CountAsync();

        await Page.Keyboard.PressAsync("Control+a");
        Assert.Equal(total, await Fm.SelectionCountAsync());

        await Fm.ClickRibbonAsync("selectNone");
        Assert.Equal(0, await Fm.SelectionCountAsync());

        await Fm.SelectAsync("readme.txt");
        await Fm.ClickRibbonAsync("invertSelection");
        Assert.Equal(total - 1, await Fm.SelectionCountAsync());
    }

    [Fact]
    public async Task Rubber_band_selection_picks_rows_under_the_rectangle()
    {
        await Fm.GotoAsync();
        var rows = await Fm.Rows.CountAsync();
        var first = await Fm.Rows.First.BoundingBoxAsync();
        var last = await Fm.Rows.Last.BoundingBoxAsync();
        Assert.NotNull(first);
        Assert.NotNull(last);

        /* start in the empty space under the last row, drag up across rows */
        var startX = first!.X + 300;
        var startY = last!.Y + last.Height + 24;
        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(first.X + 20, first.Y + 2, new() { Steps = 10 });
        await Page.Mouse.UpAsync();

        Assert.True(await Fm.SelectionCountAsync() >= 2);
    }
}
