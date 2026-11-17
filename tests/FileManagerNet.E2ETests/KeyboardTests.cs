using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class KeyboardTests : E2ETestBase
{
    public KeyboardTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task Ctrl_a_selects_everything_and_f2_opens_rename()
    {
        await Fm.GotoAsync();
        var total = await Fm.Rows.CountAsync();

        await Page.Keyboard.PressAsync("Control+a");
        Assert.Equal(total, await Fm.SelectionCountAsync());

        await Fm.SelectAsync("readme.txt");
        await Page.Keyboard.PressAsync("F2");
        await Expect(Fm.Dialog.Locator("input[type=text]")).ToHaveValueAsync("readme.txt");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(".fm-dialog")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Delete_key_opens_the_confirmation_dialog()
    {
        await Fm.GotoAsync();
        await Fm.SelectAsync("notes.md");
        await Page.Keyboard.PressAsync("Delete");

        await Expect(Fm.Dialog).ToContainTextAsync("notes.md");
        await Fm.CancelAsync();
        Assert.True(File.Exists(Path.Combine(Fm.Root1, "notes.md")));
    }
}
