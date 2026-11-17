using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class PreviewTests : E2ETestBase
{
    public PreviewTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task Text_preview_shows_content_and_detected_encoding()
    {
        await Fm.GotoAsync();
        await Fm.GoToFolderAsync("/文档");

        await Fm.Row("报告.txt").DblClickAsync();
        var pre = Page.Locator(".fm-preview-body pre");
        await Expect(pre).ToBeVisibleAsync();
        await Expect(pre).ToContainTextAsync("季度报告内容");
        await Expect(Page.Locator(".fm-enc-detected")).ToHaveTextAsync("UTF-8");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(".fm-overlay")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Image_viewer_zooms_and_rotates_then_closes_with_escape()
    {
        await Fm.GotoAsync();
        await Fm.GoToFolderAsync("/图片");

        await Fm.Row("sample.png").DblClickAsync();
        var viewer = Page.Locator(".fm-imgview");
        await Expect(viewer).ToBeVisibleAsync();

        var img = Page.Locator(".fm-imgstage img");
        await Expect(img).ToBeVisibleAsync();
        var before = await img.GetAttributeAsync("style") ?? "";

        await Page.Locator("[data-img='in']").ClickAsync();
        await Page.Locator("[data-img='in']").ClickAsync();
        var zoomed = await img.GetAttributeAsync("style") ?? "";
        Assert.NotEqual(before, zoomed);
        Assert.Contains("scale(", zoomed);

        await Page.Locator("[data-img='rotR']").ClickAsync();
        Assert.Contains("rotate(90deg)", await img.GetAttributeAsync("style") ?? "");

        await Page.Locator("[data-img='flipH']").ClickAsync();
        Assert.Contains("-", await img.GetAttributeAsync("style") ?? "");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(".fm-overlay")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Files_without_a_viewer_show_a_toast_instead_of_a_dialog()
    {
        await Fm.GotoAsync();
        await Fm.GoToFolderAsync("/压缩");
        await Fm.SelectAsync("sample.zip");

        await Fm.ClickRibbonAsync("preview");

        await Expect(Page.Locator(".fm-toast-title", new() { HasTextString = "No preview available" }).First)
            .ToBeVisibleAsync();
        await Expect(Page.Locator(".fm-overlay")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Pdf_opens_inside_the_preview_dialog()
    {
        await Fm.GotoAsync();
        await Fm.GoToFolderAsync("/文档");
        await Fm.SelectAsync("sample.pdf");

        await Fm.ClickRibbonAsync("preview");
        await Expect(Page.Locator(".fm-preview-body iframe")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }
}
