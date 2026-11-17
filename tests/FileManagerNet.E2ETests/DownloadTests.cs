using System.IO.Compression;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class DownloadTests : E2ETestBase
{
    public DownloadTests(TestHostFixture fx) : base(fx) { }

    /* Downloads are verified at the HTTP level: this environment's Chrome
     * cancels every download (even a data: URL), so the assertions check the URL
     * the button builds and the response the endpoint returns. */
    [Fact]
    public async Task Download_button_requests_the_selected_file()
    {
        await Fm.GotoAsync();
        await Fm.SelectAsync("readme.txt");

        /* capture the URL the UI builds instead of relying on the browser's
         * download plumbing, which this headless Chrome cancels outright */
        await Page.EvaluateAsync(@"() => {
            const fm = document.querySelector('[data-fmnet]').fmnet;
            window.__dlUrls = [];
            fm._triggerDownload = url => window.__dlUrls.push(url);
        }");
        await Fm.ClickRibbonAsync("download");
        await Page.WaitForTimeoutAsync(300);

        var url = Assert.Single(await Page.EvaluateAsync<string[]>("() => window.__dlUrls"));
        var decoded = Uri.UnescapeDataString(url);
        Assert.Contains("path=/readme.txt", decoded);
        Assert.DoesNotContain("paths=", decoded);

        var resp = await Page.APIRequest.GetAsync(new Uri(new Uri(Fx.BaseUrl), url).ToString());
        Assert.Equal(200, resp.Status);
        Assert.StartsWith("attachment", resp.Headers["content-disposition"]);
        Assert.Contains("Hello from the test host", await resp.TextAsync());
    }

    [Fact]
    public async Task Downloading_several_items_requests_a_zip_with_all_of_them()
    {
        await Fm.GotoAsync();
        await Fm.SelectAsync("readme.txt");
        await Fm.Row("notes.md").ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Control } });

        await Page.EvaluateAsync(@"() => {
            const fm = document.querySelector('[data-fmnet]').fmnet;
            window.__dlUrls = [];
            fm._triggerDownload = url => window.__dlUrls.push(url);
        }");
        await Fm.ClickRibbonAsync("download");
        await Page.WaitForTimeoutAsync(300);

        var url = Assert.Single(await Page.EvaluateAsync<string[]>("() => window.__dlUrls"));
        var decoded = Uri.UnescapeDataString(url);
        Assert.True(decoded.Contains("paths="), "expected a multi-item download, got: " + decoded);
        Assert.Contains("readme.txt", decoded);
        Assert.Contains("notes.md", decoded);

        var resp = await Page.APIRequest.GetAsync(new Uri(new Uri(Fx.BaseUrl), url).ToString());
        Assert.Equal(200, resp.Status);
        Assert.Equal("application/zip", resp.Headers["content-type"]);

        using var stream = new MemoryStream(await resp.BodyAsync());
        using var zip = new ZipArchive(stream);
        var names = zip.Entries.Select(e => e.FullName).ToArray();
        Assert.Contains("readme.txt", names);
        Assert.Contains("notes.md", names);
    }
}
