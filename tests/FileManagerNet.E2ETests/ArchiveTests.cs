using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class ArchiveTests : E2ETestBase
{
    public ArchiveTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task Compressing_selected_files_creates_the_archive()
    {
        await Fm.GotoAsync();
        await Fm.SelectAsync("readme.txt");
        await Fm.Row("notes.md").ClickAsync(new() { Modifiers = new[] { KeyboardModifier.Control } });

        var name = TempName("arch", ".zip");
        await Fm.ClickRibbonAsync("zip");
        await Expect(Fm.Dialog).ToBeVisibleAsync();
        await Fm.Dialog.Locator("input[type=text]").FillAsync(name);
        await Fm.Dialog.Locator(".fm-btn-primary").ClickAsync();

        var target = Path.Combine(Fm.Root1, name);
        Assert.True(await FmPage.WaitForFileAsync(target, 30000));
        var entries = await ReadZipEntriesAsync(target);
        Assert.Contains("readme.txt", entries);
        Assert.Contains("notes.md", entries);

        File.Delete(target);
    }

    [Fact]
    public async Task Extracting_an_archive_writes_its_entries_next_to_it()
    {
        var dir = Path.Combine(Fm.Root1, "压缩");
        var inside = Path.Combine(dir, "inside.txt");
        if (File.Exists(inside)) File.Delete(inside);

        await Fm.GotoAsync();
        await Fm.GoToFolderAsync("/压缩");
        await Fm.SelectAsync("sample.zip");

        await Fm.ClickRibbonAsync("extract");
        await Fm.ConfirmAsync();

        Assert.True(await FmPage.WaitForFileAsync(inside, 30000),
            "the extracted entry should appear next to the archive");
        Assert.Equal("zipped content", await File.ReadAllTextAsync(inside));
        File.Delete(inside);
    }


    /// <summary>Windows keeps a just-written archive locked for a moment
    /// (defender / search indexer), so opening it immediately is flaky.</summary>
    private static async Task<string[]> ReadZipEntriesAsync(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                return zip.Entries.Select(e => e.FullName).ToArray();
            }
            catch (IOException) when (attempt < 50) { await Task.Delay(100); }
        }
    }

}
