using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;

namespace FileManagerNet.E2ETests;

/// <summary>
/// Starts the test host on a random loopback port, seeds a throw-away file tree
/// and keeps one headless Chrome instance for the whole run. Every test gets a
/// fresh browser context, so localStorage / selection state never leaks.
/// </summary>
public sealed class TestHostFixture : IAsyncLifetime
{
    public string Root1 { get; private set; } = "";
    public string Root2 { get; private set; } = "";
    public string UploadSource { get; private set; } = "";
    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    private Process? _host;
    private readonly StringBuilder _hostLog = new();

    public async Task InitializeAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        Root1 = Path.Combine(Path.GetTempPath(), "fmnet-e2e", tag, "root1");
        Root2 = Path.Combine(Path.GetTempPath(), "fmnet-e2e", tag, "root2");
        UploadSource = Path.Combine(Path.GetTempPath(), "fmnet-e2e", tag, "upload-src");
        Directory.CreateDirectory(Root1);
        Directory.CreateDirectory(Root2);
        Directory.CreateDirectory(UploadSource);
        Seed();

        Port = FreePort();
        StartHost();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            /* use the machine's Chrome — no `playwright install` step needed */
            Channel = "chrome",
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        Playwright?.Dispose();

        if (_host is { HasExited: false })
        {
            try { _host.Kill(entireProcessTree: true); } catch { }
            try { await _host.WaitForExitAsync(new CancellationTokenSource(5000).Token); } catch { }
        }
        _host?.Dispose();

        try { Directory.Delete(Path.GetDirectoryName(Root1)!, true); } catch { }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private void StartHost()
    {
        /* run the host from its own output folder: that is where its static
         * web assets manifest lives, so _content/FileManagerNet/... resolves */
        var marker = Path.Combine(AppContext.BaseDirectory, "testhost-path.txt");
        var hostDirectory = File.Exists(marker)
            ? File.ReadAllText(marker).Trim()
            : Path.GetDirectoryName(typeof(TestHostMarker).Assembly.Location)!;
        var dll = Path.Combine(hostDirectory, "FileManagerNet.TestHost.dll");

        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\" --urls {BaseUrl}")
        {
            WorkingDirectory = hostDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["FMNET_TEST_ROOT"] = Root1;
        psi.Environment["FMNET_TEST_ROOT2"] = Root2;

        _host = Process.Start(psi)!;
        _host.OutputDataReceived += (_, e) => Append(e.Data);
        _host.ErrorDataReceived += (_, e) => Append(e.Data);
        _host.BeginOutputReadLine();
        _host.BeginErrorReadLine();

        var deadline = DateTime.UtcNow.AddSeconds(90);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        while (DateTime.UtcNow < deadline)
        {
            if (_host.HasExited)
                throw new InvalidOperationException($"Test host exited with {_host.ExitCode}.\n{_hostLog}");
            try
            {
                var res = http.GetAsync($"{BaseUrl}/health").GetAwaiter().GetResult();
                if (res.IsSuccessStatusCode) return;
            }
            catch { /* not listening yet */ }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Test host did not start on {BaseUrl}.\n{_hostLog}");
    }

    private void Append(string? line)
    {
        if (line is null) return;
        lock (_hostLog)
        {
            _hostLog.AppendLine(line);
            if (_hostLog.Length > 20000) _hostLog.Remove(0, _hostLog.Length - 20000);
        }
    }

    // ------------------------------------------------------------------ seed
    private void Seed()
    {
        File.WriteAllText(Path.Combine(Root1, "readme.txt"),
            "Hello from the test host\nsecond line\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(Root1, "notes.md"), "# Notes\n\n- one\n- two\n");
        File.WriteAllText(Path.Combine(Root1, "data.csv"), "name,size\na,1\nb,2\n");
        File.WriteAllText(Path.Combine(Root1, "big.txt"), new string('x', 4 * 1024 * 1024 + 2048));

        var docs = Path.Combine(Root1, "文档");
        Directory.CreateDirectory(Path.Combine(docs, "子目录"));
        File.WriteAllText(Path.Combine(docs, "报告.txt"), "季度报告内容：一二三\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(docs, "子目录", "深层.txt"), "深层文件");
        File.WriteAllText(Path.Combine(docs, "sample.pdf"),
            "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
            "2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n" +
            "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]>>endobj\n" +
            "trailer<</Root 1 0 R>>\n%%EOF\n");

        var pics = Path.Combine(Root1, "图片");
        Directory.CreateDirectory(pics);
        File.WriteAllBytes(Path.Combine(pics, "sample.png"), OnePixelPng);

        var zipDir = Path.Combine(Root1, "压缩");
        Directory.CreateDirectory(zipDir);
        var zipPath = Path.Combine(zipDir, "sample.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("inside.txt");
            using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            w.Write("zipped content");
        }

        Directory.CreateDirectory(Path.Combine(Root1, "目标"));
        Directory.CreateDirectory(Path.Combine(Root1, "空目录"));

        var many = Path.Combine(Root1, "大量文件");
        Directory.CreateDirectory(many);
        for (var i = 0; i < 1200; i++)
            File.WriteAllText(Path.Combine(many, $"file-{i:0000}.txt"), "x");

        File.WriteAllText(Path.Combine(Root2, "other.txt"), "second root");
        Directory.CreateDirectory(Path.Combine(Root2, "second"));

        // upload sources (outside the managed root)
        Directory.CreateDirectory(Path.Combine(UploadSource, "folder", "inner"));
        File.WriteAllText(Path.Combine(UploadSource, "upload-me.txt"), "uploaded from playwright");
        File.WriteAllText(Path.Combine(UploadSource, "folder", "a.txt"), "a");
        File.WriteAllText(Path.Combine(UploadSource, "folder", "inner", "b.txt"), "b");
        File.WriteAllText(Path.Combine(UploadSource, "big-upload.bin"),
            new string('z', 6 * 1024 * 1024));
    }

    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
