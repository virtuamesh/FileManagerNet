using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class FileOperationTests : E2ETestBase
{
    public FileOperationTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task New_folder_creates_a_directory()
    {
        await Fm.GotoAsync();
        var name = TempName("folder");

        await Fm.ClickRibbonAsync("newFolder");
        await Fm.FillPromptAsync(name);

        await Expect(Fm.Row(name)).ToBeVisibleAsync();
        Assert.True(Directory.Exists(Path.Combine(Fm.Root1, name)));

        Directory.Delete(Path.Combine(Fm.Root1, name));
    }

    [Fact]
    public async Task New_file_creates_an_empty_file()
    {
        await Fm.GotoAsync();
        var name = TempName("file", ".txt");

        await Fm.ClickRibbonAsync("newFile");
        await Fm.FillPromptAsync(name);

        await Expect(Fm.Row(name)).ToBeVisibleAsync();
        Assert.True(File.Exists(Path.Combine(Fm.Root1, name)));

        File.Delete(Path.Combine(Fm.Root1, name));
    }

    [Fact]
    public async Task F2_renames_the_selected_item()
    {
        await Fm.GotoAsync();
        var original = TempName("before", ".txt");
        var renamed = TempName("after", ".txt");
        File.WriteAllText(Path.Combine(Fm.Root1, original), "x");
        await Fm.RefreshAsync();

        await Fm.SelectAsync(original);
        await Page.Keyboard.PressAsync("F2");
        await Fm.FillPromptAsync(renamed);

        await Expect(Fm.Row(renamed)).ToBeVisibleAsync();
        Assert.False(File.Exists(Path.Combine(Fm.Root1, original)));
        Assert.True(File.Exists(Path.Combine(Fm.Root1, renamed)));

        File.Delete(Path.Combine(Fm.Root1, renamed));
    }

    [Fact]
    public async Task Delete_removes_the_file_after_confirmation()
    {
        await Fm.GotoAsync();
        var name = TempName("delete", ".txt");
        File.WriteAllText(Path.Combine(Fm.Root1, name), "x");
        await Fm.RefreshAsync();

        await Fm.SelectAsync(name);
        await Fm.ClickRibbonAsync("delete");
        await Expect(Fm.Dialog).ToContainTextAsync(name);
        await Fm.ConfirmAsync();

        await Expect(Fm.Row(name)).ToHaveCountAsync(0);
        Assert.False(File.Exists(Path.Combine(Fm.Root1, name)));
    }

    [Fact]
    public async Task Cancelling_the_delete_dialog_keeps_the_file()
    {
        await Fm.GotoAsync();
        var name = TempName("keep", ".txt");
        File.WriteAllText(Path.Combine(Fm.Root1, name), "x");
        await Fm.RefreshAsync();

        await Fm.SelectAsync(name);
        await Fm.ClickRibbonAsync("delete");
        await Fm.CancelAsync();

        await Expect(Fm.Row(name)).ToBeVisibleAsync();
        Assert.True(File.Exists(Path.Combine(Fm.Root1, name)));
        File.Delete(Path.Combine(Fm.Root1, name));
    }

    [Fact]
    public async Task Select_all_then_delete_removes_every_visible_item()
    {
        await Fm.GotoAsync();
        var dir = TempName("bulk");
        Directory.CreateDirectory(Path.Combine(Fm.Root1, dir));
        File.WriteAllText(Path.Combine(Fm.Root1, dir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(Fm.Root1, dir, "b.txt"), "b");

        await Fm.GoToFolderAsync("/" + dir);
        await Page.Keyboard.PressAsync("Control+a");
        Assert.Equal(2, await Fm.SelectionCountAsync());

        await Fm.ClickRibbonAsync("delete");
        await Expect(Fm.Dialog).ToContainTextAsync("2");
        await Fm.ConfirmAsync();
        await Expect(Fm.Rows).ToHaveCountAsync(0, new() { Timeout = 15000 });

        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(Fm.Root1, dir)));
        Directory.Delete(Path.Combine(Fm.Root1, dir));
    }
}
