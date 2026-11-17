using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

/// <summary>Small page object around the control: selectors and the handful of
/// gestures the tests repeat (navigate, select, ribbon, dialogs, toasts).</summary>
public sealed class FmPage
{
    public IPage Page { get; }
    private readonly TestHostFixture _fx;

    public FmPage(IPage page, TestHostFixture fx)
    {
        Page = page;
        _fx = fx;
        Page.SetDefaultTimeout(15000);
    }

    public string Root1 => _fx.Root1;
    public string Root2 => _fx.Root2;
    public TestHostFixture Fx => _fx;

    public ILocator Rows => Page.Locator(".fm-listinner [data-path]");
    public ILocator FileRows => Page.Locator(".fm-listinner [data-path]:not([data-dir])");
    public ILocator DirRows => Page.Locator(".fm-listinner [data-path][data-dir='1']");
    public ILocator TreeRows => Page.Locator(".fm-tree [data-path]");
    public ILocator StatusBar => Page.Locator(".fm-statusbar");
    public ILocator Dialog => Page.Locator(".fm-dialog").Last;

    public ILocator Row(string name) => Page.Locator($".fm-listinner [data-name=\"{name}\"]");
    public ILocator TreeRow(string name) => Page.Locator($".fm-tree [data-name=\"{name}\"]");

    public async Task GotoAsync(string path = "/Files", string query = "")
    {
        await Page.GotoAsync(_fx.BaseUrl + path + query);
        await Expect(Rows.First).ToBeVisibleAsync(new() { Timeout = 25000 });
    }

    /// <summary>Jumps straight to a folder (avoids depending on the tree in tests
    /// that are not about navigation).</summary>
    public async Task GoToFolderAsync(string path)
    {
        await Page.EvaluateAsync(
            "p => document.querySelector('[data-fmnet]').fmnet.navigate(p)", path);
        await Page.WaitForTimeoutAsync(250);
    }

    public async Task<string> CurrentPathAsync() =>
        await Page.EvaluateAsync<string>("() => document.querySelector('[data-fmnet]').fmnet.path");

    public async Task<int> SelectionCountAsync() =>
        await Page.EvaluateAsync<int>("() => document.querySelector('[data-fmnet]').fmnet.selection.size");

    public async Task ShowTabAsync(string tab)
    {
        await Page.Locator($".fm-tab[data-tab=\"{tab}\"]").ClickAsync();
        await Page.WaitForTimeoutAsync(120);
    }

    public Task ClickRibbonAsync(string act) =>
        Page.Locator($".fm-ribbon-body:not([hidden]) [data-act=\"{act}\"]").ClickAsync();

    public async Task SelectAsync(string name)
    {
        await Row(name).ClickAsync();
        await Page.WaitForTimeoutAsync(80);
    }

    public async Task OpenFolderAsync(string name)
    {
        await Row(name).DblClickAsync();
        await Expect(Page.Locator(".fm-crumb.current")).ToHaveTextAsync(name);
    }

    public async Task ContextMenuAsync(string name)
    {
        await Row(name).ClickAsync(new() { Button = MouseButton.Right });
        await Expect(Page.Locator(".fm-ctx").First).ToBeVisibleAsync();
    }

    public Task ClickMenuAsync(string label) =>
        Page.Locator(".fm-ctx .fm-ctx-item", new() { HasTextString = label }).First.ClickAsync();

    public async Task ExpectToastAsync(string text, int timeout = 8000)
    {
        await Expect(Page.Locator(".fm-toast-title", new() { HasTextString = text }).First)
            .ToBeVisibleAsync(new() { Timeout = timeout });
    }

    public async Task FillPromptAsync(string value)
    {
        var input = Dialog.Locator("input[type=text]");
        await Expect(input).ToBeVisibleAsync();
        await input.FillAsync(value);
        await Dialog.Locator(".fm-btn-primary").ClickAsync();
    }

    public async Task ConfirmAsync()
    {
        await Expect(Dialog).ToBeVisibleAsync();
        await Dialog.Locator(".fm-btn-primary").ClickAsync();
    }

    public async Task CancelAsync()
    {
        await Expect(Dialog).ToBeVisibleAsync();
        await Dialog.Locator(".fm-btn-secondary").First.ClickAsync();
    }

    public async Task WaitDialogClosedAsync()
    {
        await Expect(Page.Locator(".fm-dialog")).ToHaveCountAsync(0, new() { Timeout = 15000 });
    }

    public async Task<IDownload> DownloadAsync(Func<Task> action)
    {
        var download = await Page.RunAndWaitForDownloadAsync(action);
        return download;
    }

    public async Task RefreshAsync()
    {
        await Page.Locator("[data-act='refresh']").ClickAsync();
        await Page.WaitForTimeoutAsync(250);
    }

    public async Task<string> ReadSelectionJsonAsync() =>
        await Page.EvaluateAsync<string>(
            "() => JSON.stringify(Array.from(document.querySelector('[data-fmnet]').fmnet.selection))");

    public static async Task<bool> WaitForFileAsync(string path, int timeoutMs = 15000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path)) return true;
            await Task.Delay(150);
        }
        return File.Exists(path);
    }
}
