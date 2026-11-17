using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class ClipboardTests : E2ETestBase
{
    public ClipboardTests(TestHostFixture fx) : base(fx) { }

    private void ResetTarget()
    {
        var dir = Path.Combine(Fm.Root1, "目标");
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        foreach (var d in Directory.GetDirectories(dir)) Directory.Delete(d, true);
    }

    [Fact]
    public async Task Copy_and_paste_keeps_the_source()
    {
        ResetTarget();
        await Fm.GotoAsync();
        var name = TempName("copy", ".txt");
        var src = Path.Combine(Fm.Root1, name);
        File.WriteAllText(src, "copied");
        await Fm.RefreshAsync();

        await Fm.SelectAsync(name);
        await Page.Keyboard.PressAsync("Control+c");
        await Fm.GoToFolderAsync("/目标");
        await Page.Keyboard.PressAsync("Control+v");

        await Expect(Fm.Row(name)).ToBeVisibleAsync();
        Assert.True(File.Exists(src));
        Assert.Equal("copied", File.ReadAllText(Path.Combine(Fm.Root1, "目标", name)));

        File.Delete(src);
        File.Delete(Path.Combine(Fm.Root1, "目标", name));
    }

    /// <summary>Regression: the client used to post the clipboard mode ("cut")
    /// as a command, which the API rejected with "Unknown command: cut".</summary>
    [Fact]
    public async Task Cut_and_paste_moves_the_file()
    {
        ResetTarget();
        await Fm.GotoAsync();
        var name = TempName("cut", ".txt");
        var src = Path.Combine(Fm.Root1, name);
        File.WriteAllText(src, "moved");
        await Fm.RefreshAsync();

        await Fm.SelectAsync(name);
        await Page.Keyboard.PressAsync("Control+x");
        await Fm.GoToFolderAsync("/目标");
        await Page.Keyboard.PressAsync("Control+v");

        await Expect(Fm.Row(name)).ToBeVisibleAsync();
        await Expect(Page.Locator(".fm-toast-title", new() { HasTextString = "Pasted" }).First)
            .ToBeVisibleAsync();
        Assert.False(File.Exists(src));
        Assert.Equal("moved", File.ReadAllText(Path.Combine(Fm.Root1, "目标", name)));

        File.Delete(Path.Combine(Fm.Root1, "目标", name));
    }

    [Fact]
    public async Task Conflict_dialog_overwrites_when_asked()
    {
        ResetTarget();
        await Fm.GotoAsync();
        var name = TempName("dup", ".txt");
        File.WriteAllText(Path.Combine(Fm.Root1, name), "new content");
        File.WriteAllText(Path.Combine(Fm.Root1, "目标", name), "old content");
        await Fm.RefreshAsync();

        await Fm.SelectAsync(name);
        await Page.Keyboard.PressAsync("Control+c");
        await Fm.GoToFolderAsync("/目标");
        await Page.Keyboard.PressAsync("Control+v");

        await Expect(Fm.Dialog).ToContainTextAsync(name);
        await Fm.Dialog.Locator("[data-choice='overwrite']").ClickAsync();
        await Expect(Fm.Dialog).ToHaveCountAsync(0);

        Assert.Equal("new content", await ReadTextWhenReleasedAsync(Path.Combine(Fm.Root1, "目标", name)));

        File.Delete(Path.Combine(Fm.Root1, name));
        File.Delete(Path.Combine(Fm.Root1, "目标", name));
    }

    /// <summary>Windows keeps a just-written file locked for a few milliseconds
    /// (defender / search indexer), which makes an immediate read flaky.</summary>
    private static async Task<string> ReadTextWhenReleasedAsync(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await File.ReadAllTextAsync(path); }
            catch (IOException) when (attempt < 50) { await Task.Delay(100); }
        }
    }

    [Fact]
    public async Task Conflict_dialog_keep_both_renames_the_copy()
    {
        ResetTarget();
        await Fm.GotoAsync();
        var name = TempName("both", ".txt");
        var stem = Path.GetFileNameWithoutExtension(name);
        File.WriteAllText(Path.Combine(Fm.Root1, name), "source");
        File.WriteAllText(Path.Combine(Fm.Root1, "目标", name), "target");
        await Fm.RefreshAsync();

        await Fm.SelectAsync(name);
        await Page.Keyboard.PressAsync("Control+c");
        await Fm.GoToFolderAsync("/目标");
        await Page.Keyboard.PressAsync("Control+v");
        await Fm.Dialog.Locator("[data-choice='rename']").ClickAsync();

        await Expect(Fm.Row(stem + " (1).txt")).ToBeVisibleAsync();
        Assert.True(File.Exists(Path.Combine(Fm.Root1, "目标", stem + " (1).txt")));

        File.Delete(Path.Combine(Fm.Root1, name));
        File.Delete(Path.Combine(Fm.Root1, "目标", name));
        File.Delete(Path.Combine(Fm.Root1, "目标", stem + " (1).txt"));
    }

    [Fact]
    public async Task Dragging_a_row_onto_a_folder_moves_it()
    {
        ResetTarget();
        await Fm.GotoAsync();
        var name = TempName("drag", ".txt");
        var src = Path.Combine(Fm.Root1, name);
        File.WriteAllText(src, "dragged");
        await Fm.RefreshAsync();

        await Fm.Row(name).DragToAsync(Fm.Row("目标"));
        await Expect(Page.Locator(".fm-toast-title", new() { HasTextString = "Moved" }).First)
            .ToBeVisibleAsync();

        Assert.False(File.Exists(src));
        Assert.True(File.Exists(Path.Combine(Fm.Root1, "目标", name)));
        File.Delete(Path.Combine(Fm.Root1, "目标", name));
    }
}
