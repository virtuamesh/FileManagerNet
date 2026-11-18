using FileManagerNet;
using FileManagerNet.Infrastructure;
using FileManagerNet.Models;

var builder = WebApplication.CreateBuilder(args);

// FileManagerNet —— 注册控件默认选项
builder.Services.AddFileManagerNet();
builder.Services.AddRazorPages();

var app = builder.Build();

// FileManagerNet —— 映射 API 端点（root 由服务端固定，客户端不可传入任意路径）
app.MapFileManagerNet("/filemanager-api", o =>
{
    o.RootFolder = Path.Combine(app.Environment.ContentRootPath, "App_Data", "demo-files");
    o.RootDisplayName = "Demo Files";
    o.Config = new FileManagerConfig
    {
        DefaultView = ViewLayout.Details,
        // 分块上传：0 = 不限制单文件大小（受磁盘空间限制），10GB+ 也可以
        MaxUploadBytes = 0,
    };
});

// FileManagerNet —— 权限示例（ACL）：一个根目录下，不同子目录不同权限。
// 页面 /Acl 用同一个 id 渲染，模板注册时不会把这里的规则冲掉。
// 用内容根下的可写目录，Windows / Linux / macOS 都能直接跑。
var aclDemoRoot = Path.Combine(app.Environment.ContentRootPath, "App_Data", "acl-demo");
foreach (var sub in new[] { "public/images", "docs", "secret" })
    Directory.CreateDirectory(Path.Combine(aclDemoRoot, sub));

var aclRoot = new FileManagerRoot(aclDemoRoot, "ACL 演示");
aclRoot
    // 整棵树默认只读：可以浏览、下载、预览，不能改动
    .Allow("/", FileManagerPermissions.ReadOnly)
    // public 可写：新增、上传、删除、改名、粘贴
    .Allow("/public", FileManagerPermissions.ReadOnly
        | FileManagerPermissions.Create
        | FileManagerPermissions.Upload
        | FileManagerPermissions.Delete
        | FileManagerPermissions.Rename
        | FileManagerPermissions.Paste)
    // public/images 只收图片，且整个目录最多 5 MB
    .Allow("/public/images",
        FileManagerPermissions.ReadOnly | FileManagerPermissions.Upload | FileManagerPermissions.Create,
        allowedFileTypes: "*.jpg|*.png|*.gif|*.webp",
        quota: "5 MB")
    // secret 既不能列出也不能进去
    .Allow("/secret", FileManagerPermissions.None);

FileManagerRegistry.Register("FM_ACL", new[] { aclRoot }, "ACL 演示", new FileManagerConfig
{
    DefaultView = ViewLayout.Details,
    MaxUploadBytes = 0,
});

app.UseStaticFiles();
app.MapStaticAssets();   // .NET 9/10: serves RCL static assets (_content/...)
app.MapRazorPages();

app.Run();
