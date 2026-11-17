using FileManagerNet;
using FileManagerNet.Infrastructure;
using FileManagerNet.Models;

/* Host used only by the Playwright end-to-end tests. Two roots come from the
 * environment so every run works on throw-away folders instead of real data. */
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddFileManagerNet();

var app = builder.Build();

var root1 = Environment.GetEnvironmentVariable("FMNET_TEST_ROOT")
            ?? Path.Combine(Path.GetTempPath(), "fmnet-e2e-root");
var root2 = Environment.GetEnvironmentVariable("FMNET_TEST_ROOT2") ?? root1;
Directory.CreateDirectory(root1);
Directory.CreateDirectory(root2);

app.MapFileManagerNet("/filemanager-api", o =>
{
    o.RootFolder = root1;
    o.RootDisplayName = "Test root";
    o.Config = new FileManagerConfig
    {
        DefaultView = ViewLayout.Details,
        MaxUploadBytes = 0,          // chunked uploads: the tests push a 6 MB file
        OfficePreviewEnabled = false // LibreOffice must not be required on CI
    };
});

/* Access-control fixture: one root whose folders differ by permission, so the
 * tests can check both the UI and the server-side enforcement.
 *   /                  read-only
 *   /public            writable
 *   /public/images     writable, images only, 5 MB quota
 *   /secret            no access at all
 */
var aclBase = Path.Combine(root1, "_acl");
Directory.CreateDirectory(aclBase);

var aclRoot = new FileManagerRoot(aclBase, "ACL root");
aclRoot
    .Allow("/", FileManagerPermissions.ReadOnly)
    .Allow("/public", FileManagerPermissions.ReadOnly
        | FileManagerPermissions.Create
        | FileManagerPermissions.Upload
        | FileManagerPermissions.Delete
        | FileManagerPermissions.Rename
        | FileManagerPermissions.Paste)
    .Allow("/public/images",
        FileManagerPermissions.ReadOnly | FileManagerPermissions.Upload | FileManagerPermissions.Create,
        allowedFileTypes: "*.jpg|*.png",
        quota: "5 MB")
    .Allow("/secret", FileManagerPermissions.None);

FileManagerRegistry.Register("FM_ACL", new[] { aclRoot }, "ACL root", new FileManagerConfig
{
    DefaultView = ViewLayout.Details,
    MaxUploadBytes = 0
});

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorPages();

app.MapGet("/health", () => Results.Ok(new { ok = true }));
app.MapGet("/__test/paths", () => Results.Ok(new { root1, root2 }));

app.Run();

/// <summary>Lets the test project locate this assembly's build output.</summary>
public sealed class TestHostMarker { }
