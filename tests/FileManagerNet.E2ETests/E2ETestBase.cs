using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

/// <summary>One browser context per test: storage, view state and selection are
/// isolated, while the file tree on disk is shared (tests use distinct names).</summary>
[Collection("e2e")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly TestHostFixture Fx;
    protected IBrowserContext Context = null!;
    protected IPage Page = null!;
    protected FmPage Fm = null!;

    protected E2ETestBase(TestHostFixture fx) => Fx = fx;

    public async Task InitializeAsync()
    {
        Context = await Fx.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
            Locale = "en-US",
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });
        Page = await Context.NewPageAsync();
        Fm = new FmPage(Page, Fx);
    }

    public async Task DisposeAsync() => await Context.CloseAsync();

    protected static string TempName(string prefix, string ext = "") =>
        prefix + "-" + Guid.NewGuid().ToString("N")[..6] + ext;
}
