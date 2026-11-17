using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class RibbonTests : E2ETestBase
{
    public RibbonTests(TestHostFixture fx) : base(fx) { }

    /// <summary>A disabled search must not leave a box that always fails.</summary>
    [Fact]
    public async Task Search_box_is_hidden_when_search_is_disabled()
    {
        await Fm.GotoAsync("/Files", "?nosearch=1");

        await Expect(Page.Locator(".fm-search")).ToHaveCountAsync(0);
        await Expect(Fm.Rows.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Home_tab_shows_every_group_and_action()
    {
        await Fm.GotoAsync();

        var acts = new[]
        {
            "copy", "paste", "cut", "delete", "rename", "newFolder", "newFile",
            "download", "upload", "uploadFolder", "open", "preview",
            "zip", "extract", "selectAll", "selectNone", "invertSelection"
        };
        foreach (var act in acts)
            await Expect(Page.Locator($".fm-ribbon-body:not([hidden]) [data-act=\"{act}\"]"))
                .ToBeVisibleAsync();

        // clipboard / organize / new / transfer / open / compression / select
        await Expect(Page.Locator(".fm-ribbon-body:not([hidden]) .fm-rgroup-cap")).ToHaveCountAsync(7);
    }

    [Fact]
    public async Task View_tab_offers_layouts_panes_and_show_hide()
    {
        await Fm.GotoAsync();
        await Fm.ShowTabAsync("view");

        foreach (var view in new[] { "ExtraLargeIcons", "LargeIcons", "MediumIcons", "SmallIcons",
                                     "List", "Details", "Tiles" })
            await Expect(Page.Locator($"[data-view=\"{view}\"]")).ToBeVisibleAsync();

        foreach (var act in new[] { "toggleNav", "togglePreview", "toggleDetails",
                                    "toggleExtensions", "toggleHidden", "sortBy" })
            await Expect(Page.Locator($".fm-ribbon-body:not([hidden]) [data-act=\"{act}\"]"))
                .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Ribbon_collapses_and_expands_again()
    {
        await Fm.GotoAsync();

        await Page.Locator(".fm-ribbon-collapse").ClickAsync();
        await Expect(Page.Locator(".fm-ribbon")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("collapsed"));
        await Expect(Page.Locator(".fm-ribbon-body:not([hidden])")).ToBeHiddenAsync();

        await Page.Locator(".fm-ribbon-collapse").ClickAsync();
        await Expect(Page.Locator(".fm-ribbon-body:not([hidden])")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Context_menu_lists_the_core_actions()
    {
        await Fm.GotoAsync();
        await Fm.ContextMenuAsync("readme.txt");

        foreach (var label in new[] { "Open", "Preview", "Download", "Copy", "Cut", "Rename", "Delete" })
            await Expect(Page.Locator(".fm-ctx .fm-ctx-item", new() { HasTextString = label }).First)
                .ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(".fm-ctx")).ToHaveCountAsync(0);
    }
}
