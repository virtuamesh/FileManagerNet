using System.IO.Compression;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class UploadTests : E2ETestBase
{
    public UploadTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task Uploading_a_file_uses_the_chunked_endpoint_and_lands_on_disk()
    {
        await Fm.GotoAsync();
        var src = Path.Combine(Fx.UploadSource, "upload-me.txt");
        var name = TempName("upload", ".txt");
        var temp = Path.Combine(Fx.UploadSource, name);
        File.Copy(src, temp);

        await Page.Locator("input[data-role=fileInput]").SetInputFilesAsync(temp);
        await Fm.ExpectToastAsync("Uploaded");

        await Expect(Fm.Row(name)).ToBeVisibleAsync(new() { Timeout = 20000 });
        var target = Path.Combine(Fm.Root1, name);
        Assert.True(await FmPage.WaitForFileAsync(target));
        Assert.Equal("uploaded from playwright", await File.ReadAllTextAsync(target));

        File.Delete(temp);
        File.Delete(target);
    }

    [Fact]
    public async Task Uploading_a_6mb_file_sends_several_chunks()
    {
        await Fm.GotoAsync();
        var name = TempName("big", ".bin");
        var source = Path.Combine(Fx.UploadSource, "big-upload.bin");

        var chunkCalls = 0;
        Page.Request += (_, req) =>
        {
            if (req.Url.Contains("/upload/chunk")) Interlocked.Increment(ref chunkCalls);
        };

        await Page.Locator("input[data-role=fileInput]").SetInputFilesAsync(new FilePayload
        {
            Name = name,
            MimeType = "application/octet-stream",
            Buffer = await File.ReadAllBytesAsync(source)
        });
        await Fm.ExpectToastAsync("Uploaded", 30000);

        var target = Path.Combine(Fm.Root1, name);
        Assert.True(await FmPage.WaitForFileAsync(target, 20000));
        Assert.Equal(new FileInfo(source).Length, new FileInfo(target).Length);
        Assert.True(chunkCalls > 1, $"expected several chunks, saw {chunkCalls}");

        File.Delete(target);
    }

    [Fact]
    public async Task Folder_upload_keeps_the_hierarchy()
    {
        await Fm.GotoAsync();
        var root = TempName("dir");

        /* simulate what a dropped folder produces: a list of {file, rel} */
        await Page.EvaluateAsync(@"([root, txt]) => {
            const fm = document.querySelector('[data-fmnet]').fmnet;
            const a = new File([txt], 'a.txt', { type: 'text/plain' });
            const b = new File(['inner'], 'b.txt', { type: 'text/plain' });
            fm.uploadFiles([
                { file: a, rel: root + '/a.txt' },
                { file: b, rel: root + '/inner/b.txt' }
            ]);
        }", new object[] { root, "outer" });

        await Fm.ExpectToastAsync("Uploaded", 20000);
        await Expect(Fm.Row(root)).ToBeVisibleAsync(new() { Timeout = 20000 });

        var inner = Path.Combine(Fm.Root1, root, "inner", "b.txt");
        Assert.True(await FmPage.WaitForFileAsync(inner));
        Assert.Equal("inner", await File.ReadAllTextAsync(inner));
        Assert.Equal("outer", await File.ReadAllTextAsync(Path.Combine(Fm.Root1, root, "a.txt")));

        Directory.Delete(Path.Combine(Fm.Root1, root), true);
    }

    [Fact]
    public async Task Dropping_files_onto_the_list_uploads_them()
    {
        await Fm.GotoAsync();
        var name = TempName("dropped", ".txt");

        await Page.EvaluateAsync(@"name => {
            const dt = new DataTransfer();
            dt.items.add(new File(['dropped content'], name, { type: 'text/plain' }));
            document.querySelector('.fm-listwrap')
                .dispatchEvent(new DragEvent('drop', { dataTransfer: dt, bubbles: true, cancelable: true }));
        }", name);

        var target = Path.Combine(Fm.Root1, name);
        Assert.True(await FmPage.WaitForFileAsync(target, 20000));
        Assert.Equal("dropped content", await File.ReadAllTextAsync(target));
        File.Delete(target);
    }
}
