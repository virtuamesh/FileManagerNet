# FileManagerNet 配置与使用说明

适用于 FileManagerNet **1.0.1**（ASP.NET Core 10 / .NET 10，Razor Pages 或 MVC 均可）。

这份文档覆盖：怎么接进来、每个配置项的确切含义与默认值、权限（ACL）怎么配、
HTTP API 长什么样、常见场景的配方，以及配置错了会看到什么现象。

> 英文版功能清单见根目录 [README.md](../README.md)；本文是面向使用者的完整配置手册。
> 官方网站：<https://www.virtuamesh.com/>

---

## 目录

1. [三步接入](#1-三步接入)
2. [页面标签属性](#2-页面标签属性)
3. [根目录](#3-根目录)
4. [功能开关 FileManagerConfig](#4-功能开关-filemanagerconfig)
5. [权限（ACL）](#5-权限acl)
6. [Office 在线预览](#6-office-在线预览)
7. [外观、视图与本地化](#7-外观视图与本地化)
8. [HTTP API](#8-http-api)
9. [常见场景配方](#9-常见场景配方)
10. [性能与并发](#10-性能与并发)
11. [安全须知](#11-安全须知)
12. [故障排查](#12-故障排查)
13. [完整示例](#13-完整示例)

---

## 1. 三步接入

### 1.1 引用工程

```xml
<ItemGroup>
  <ProjectReference Include="..\src\FileManagerNet\FileManagerNet.csproj" />
</ItemGroup>
```

### 1.2 注册服务并映射 API

```csharp
using FileManagerNet;
using FileManagerNet.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFileManagerNet();       // 必须
builder.Services.AddRazorPages();

var app = builder.Build();

app.MapFileManagerNet("/filemanager-api", o =>
{
    // 这里配置的是 MapFileManagerNet 注册的“默认实例”（id = api-endpoint）
    o.RootFolder = Path.Combine(app.Environment.ContentRootPath, "App_Data", "files");
    o.RootDisplayName = "Files";
    o.Config = new FileManagerConfig { DefaultView = ViewLayout.Details };
});

app.UseStaticFiles();
app.MapStaticAssets();      // .NET 9/10：提供 RCL 里的 _content/FileManagerNet/*
app.MapRazorPages();
app.Run();
```

`MapFileManagerNet` 返回 `IEndpointConventionBuilder`，可以继续 `.RequireAuthorization()`：

```csharp
app.MapFileManagerNet("/filemanager-api", o => { ... })
   .RequireAuthorization("StaffOnly");
```

> **实例边界**：上面 `o` 里的根目录和 `Config` 只属于 `MapFileManagerNet` 注册的那个默认实例
> （id 等于 `api-endpoint`）。页面里的 `<file-manager id="FM1">` 是独立实例，根目录和开关只来自
> 标签属性（`root-folder`、`config`），**不会继承** `o.RootFolder` / `o.Config`。
> 同 `id` 的 `FileManagerRegistry.Register` 只用于预先挂 ACL：标签助手会保留这些规则，
> 但根目录仍以标签属性为准。

### 1.3 在页面挂载

```cshtml
@addTagHelper *, FileManagerNet

<div style="height: 80vh">
    <file-manager id="FM1"
                  root-folder="D:\Data"
                  root-name="资料库"
                  api-endpoint="/filemanager-api" />
</div>
```

控件会撑满父容器，**父容器必须有确定高度**（`height` 或 flex 布局），否则列表区会塌陷。

---

## 2. 页面标签属性

| 属性 | 必填 | 默认 | 说明 |
|---|---|---|---|
| `id` | 否 | 自动生成 `fm-<guid>` | 实例标识，**页面内唯一**。前端只把它发给服务端，物理路径永远不出现在 HTML 里 |
| `root-folder` | 否 | `AppContext.BaseDirectory` | 单个物理目录，或 `;` 分隔的多个目录（见第 3 节）。相对路径按进程工作目录解析，生产环境建议写绝对路径 |
| `root-folders` | 否 | — | `root-folder` 的别名；**同时出现时以它为准** |
| `root-name` | 否 | 单根取文件夹名，多根为 `Home` | 左侧树与面包屑里显示的根节点名称 |
| `api-endpoint` | 否 | `/filemanager-api` | 必须与 `MapFileManagerNet` 的路径一致 |
| `locale` | 否 | 跟随浏览器 | 强制界面语言，见 [7.3](#73-本地化) |
| `config` | 否 | `new FileManagerConfig()` | 绑一个 `FileManagerConfig` 对象（Razor 里用 `config="fmConfig"`）；提供后**整体替换**默认配置 |

`config` 在 Razor 页面里的写法：

```cshtml
@{
    var fmConfig = new FileManagerConfig
    {
        DefaultView = ViewLayout.LargeIcons,
        MaxUploadBytes = 2L * 1024 * 1024 * 1024,
        ShowHiddenFiles = false
    };
}
<file-manager id="FM1" root-folder="D:\Data" config="fmConfig" api-endpoint="/filemanager-api" />
```

> 服务端会把 `FileManagerConfig` 序列化成 `data-config` 属性发给浏览器，
> 但带 `[JsonIgnore]` 的字段（LibreOffice 路径、缓存目录等）不会下发。

---

## 3. 根目录

### 3.1 一个实例挂一个目录

```cshtml
<file-manager id="FM1" root-folder="D:\Data" root-name="数据" api-endpoint="/filemanager-api" />
```

目录本身就是根，虚拟路径 `/` 直接对应它的内容。

Linux / macOS 使用系统原生绝对路径即可：

```cshtml
<file-manager id="FM1" root-folder="/srv/data" root-name="数据" api-endpoint="/filemanager-api" />
```

### 3.2 一个实例挂多个目录

```cshtml
<file-manager id="FM2"
              root-folder="资料=D:\Data;备份=E:\Backup;C:\Shared"
              root-name="我的文件"
              api-endpoint="/filemanager-api" />
```

语法：`;`（或换行）分隔，`名称=路径` 可选，不写名称则取文件夹名。
Linux / macOS 示例：`root-folder="资料=/srv/data;备份=/mnt/backup"`。绝对路径本身含 `=`
也没问题（例如 `/srv/a=b`），只有非绝对路径才会按 `名称=路径` 解析。

行为差异：

| 项目 | 单根 | 多根 |
|---|---|---|
| 虚拟路径 | `/` 即目录本身 | `/<挂载名>/...`，`/` 只列出这些文件夹 |
| 根下写入 | 允许 | **禁止**（新建、粘贴、上传都会被拒绝，提示"请先打开一个文件夹"）|
| 挂载点操作 | — | 不能删除、重命名、复制、移动 |
| 跨根复制/移动 | — | 允许；跨盘移动自动退化为"复制 + 删除" |
| 搜索 | 当前目录递归 | 根目录下搜索会遍历所有挂载 |

C# 侧等价写法（`MapFileManagerNet` 的 `o`，作用于默认实例）：

```csharp
o.RootFolders.Add(new FileManagerRoot(@"D:\Data", "资料"));
o.RootFolders.Add(new FileManagerRoot(@"E:\Backup", "备份"));
// 或 o.AddRoot(@"D:\Data", "资料");
```

`RootFolders` 非空时优先于 `RootFolder` 字符串。页面里的标签实例不会读取这些选项；
根目录以标签属性为准；要预挂 ACL，用 `FileManagerRegistry.Register("FM1", roots, ...)`
注册同 `id` 的实例（见 5.4）。

### 3.3 同页多个实例

一个页面可以放任意多个 `<file-manager>`。每个实例的选中、剪贴板、视图布局、排序、搜索都独立；
样式与脚本**每个请求只输出一次**；键盘快捷键只作用于最后点击的那个实例。

```cshtml
<file-manager id="FM_A" root-folder="D:\A" api-endpoint="/filemanager-api" />
<file-manager id="FM_B" root-folder="D:\B" api-endpoint="/filemanager-api" />
```

---

## 4. 功能开关 `FileManagerConfig`

这些开关是**实例级**的"总闸门"，作用于整个实例。更细的按目录控制见第 5 节权限。

| 字段 | 默认 | 作用 |
|---|---|---|
| `AllowDownload` | `true` | 下载按钮、预览、缩略图、Office 预览 |
| `AllowUpload` | `true` | 上传、上传文件夹、拖放上传、覆盖已有文件 |
| `AllowDelete` | `true` | 删除（含批量删除） |
| `AllowRename` | `true` | 重命名（F2） |
| `AllowCreateFolder` | `true` | 新建文件夹 |
| `AllowCreateFile` | `true` | 新建空白文件 |
| `AllowCopyMove` | `true` | 复制、剪切、粘贴、拖拽移动 |
| `AllowArchive` | `true` | 压缩（zip/tar/tar.gz/tar.bz2）、解压 |
| `AllowSearch` | `true` | 路径栏搜索框；关闭后搜索框不渲染，`search` 命令返回 403 |
| `MaxUploadBytes` | `512 MB` | 单文件上限；`0` = 不限制（分块上传只受磁盘限制，10 GB+ 可用）|
| `ListCacheMilliseconds` | `0` | 目录列表复用窗口，见 [10.2](#102-列表缓存) |
| `DefaultView` | `Details` | 首次打开的视图：`ExtraLargeIcons` / `LargeIcons` / `MediumIcons` / `SmallIcons` / `List` / `Details` / `Tiles` |
| `HiddenPatterns` | `""` | 隐藏的文件名通配，`;` 分隔，如 `*.log;*.tmp;~$*` |
| `ShowFileExtensions` | `false` | 初始是否显示扩展名（"查看"选项卡可切换）|
| `ShowHiddenFiles` | `false` | 初始是否显示点开头的隐藏项 |
| `ShowFoldersPane` | `true` | 初始是否显示左侧目录树 |
| `Locale` | `""` | 强制语言，空 = 跟随浏览器 |

关闭某个开关后：按钮从功能区与右键菜单消失，直接调 API 也会被拒绝并返回 **403**。

`OfficePreviewAvailable` 是服务端在注册实例时计算出的只读结果（开关打开 **且** 找到
LibreOffice），不要手工在配置里设置；`Config` 里的 `[JsonIgnore]` 字段（`LibreOfficePath`、
缓存目录等）只留在服务端，不会下发到浏览器。

---

## 5. 权限（ACL）

实例开关是"整个实例能不能做"，ACL 是"**这个目录能不能做**"。两者叠加：**ACL 只能收紧，不能突破实例开关**。

### 5.1 权限位

`FileManagerPermissions` 是 `[Flags]` 枚举，用 `|` 组合：

| 位 | 允许什么 |
|---|---|
| `ListSubfolders` | 看到子目录（列表与左侧树）|
| `ListFiles` | 看到文件 |
| `Create` | 新建文件夹 / 新建空白文件 |
| `Delete` | 删除 |
| `Rename` | 重命名 |
| `Edit` | 覆盖已有文件的内容（上传覆盖、解压覆盖）|
| `Upload` | 上传新文件 |
| `Download` | 下载原文件 |
| `Compress` | 压缩 |
| `Extract` | 解压 |
| `Cut` | 把项目从这个目录**移出** |
| `Copy` | 把项目从这个目录**复制出去** |
| `Paste` | 把项目**写进**这个目录 |
| `Preview` | 打开预览 / 查看器 |

预置组合：

- `FileManagerPermissions.ReadOnly` = `ListSubfolders | ListFiles | Download | Preview`
- `FileManagerPermissions.Full` = 除 `None` 外全部

### 5.2 规则字段

`FileManagerAccessControl` 的每一项是一个规则：

| 字段 | 说明 |
|---|---|
| `Path` | 挂载在哪个虚拟目录。`/` 表示整棵树；支持通配：`*` 匹配单段内任意字符，`**` 跨层级 |
| `Allowed` | 授予的权限位组合 |
| `Denied` | 扣掉的权限位组合，**永远优先于 `Allowed`** |
| `AllowedFileTypes` | 只允许这些文件类型，如 `"*.jpg|*.png"`；留空 = 全部。目录始终可见 |
| `DeniedFileTypes` | 拒绝这些类型，如 `"*.exe|*.dll"`；**优先于 `AllowedFileTypes`** |
| `Quota` | 该目录下内容的容量上限，`"500 MB"` / `"2 GB"` / `"1048576"`；留空 = 不限 |
| `LimitDiskUsage` | 设为 `false` 可解除继承来的配额（只对当前子树）|

### 5.3 匹配规则

1. **范围内继承**：一条规则覆盖它自己的目录及其所有子目录。
2. **就近继承，不合并**：子目录只采用**最深**（最具体）的那条匹配规则，不会把各级 `Allowed` 叠加。
   因此子目录既能放宽（父级只读、子级可写），也能收紧（父级可删、子级不可删）。
3. **拒绝优先**：`Denied` 从 `Allowed` 中扣除。
4. **类型过滤是"全动作"的**：被拒的文件类型不出现在列表里，也不能下载、预览、上传 —— 等效于"看不见"。
5. **实例开关是上限**：最终权限 = 规则结果 ∩ 实例开关。实例关掉上传时，ACL 里写了 `Upload` 也不会生效。
6. **同深度冲突**：多条规则匹配到同一深度时，**后添加的规则覆盖先添加的**，便于在末尾追加例外。

### 5.4 配置方式

**方式一（推荐）：`Program.cs` 里注册，页面上照常写标签。**

```csharp
using FileManagerNet.Infrastructure;

var root = new FileManagerRoot(@"D:\Shared", "共享盘");
root
    // 整棵树只读
    .Allow("/", FileManagerPermissions.ReadOnly)
    // 这个目录可写
    .Allow("/public", FileManagerPermissions.ReadOnly
        | FileManagerPermissions.Create | FileManagerPermissions.Upload
        | FileManagerPermissions.Delete | FileManagerPermissions.Rename
        | FileManagerPermissions.Paste)
    // 只收图片，且整个目录最多 500 MB
    .Allow("/public/images",
        FileManagerPermissions.ReadOnly | FileManagerPermissions.Upload | FileManagerPermissions.Create,
        allowedFileTypes: "*.jpg|*.png|*.webp",
        quota: "500 MB")
    // 完全不能打开
    .Allow("/secret", FileManagerPermissions.None)
    // 财务目录：能上传，但不能删、不能下载
    .Allow("/finance", FileManagerPermissions.Full,
        denied: FileManagerPermissions.Delete | FileManagerPermissions.Download)
    // 通配：任何 dept-xxx 下的 docs 都能上传
    .Allow("/dept-*/docs", FileManagerPermissions.ReadOnly | FileManagerPermissions.Upload);

FileManagerRegistry.Register("FM_DOCS", new[] { root }, "共享盘", new FileManagerConfig());
```

页面：

```cshtml
<file-manager id="FM_DOCS" root-folder="D:\Shared" root-name="共享盘" api-endpoint="/filemanager-api" />
```

> 模板渲染时会用同一个 `id` 重新注册实例（以拿到页面上的真实路径），
> 但会**保留**你在 `Program.cs` 里挂的规则（前提是根目录数量一致）—— 不会静默丢失。

**方式二：完整的对象写法**（等价，便于从配置/数据库生成）。

```csharp
root.AccessControls.Add(new FileManagerAccessControl
{
    Path = "/public/images",
    Allowed = FileManagerPermissions.ReadOnly | FileManagerPermissions.Upload,
    Denied = FileManagerPermissions.Delete,
    AllowedFileTypes = "*.jpg|*.png",
    DeniedFileTypes = "*.gif",
    Quota = "500 MB"
});
```

### 5.5 权限 → 界面表现

服务端在每次 `list` 时返回当前目录的有效权限，功能区与右键菜单据此调整：

| 权限缺失 | 界面变化 |
|---|---|
| `Download` | 下载按钮变灰；预览、缩略图不可用 |
| `Upload` | 上传 / 上传文件夹变灰；拖放上传被拒绝 |
| `Delete` | 删除按钮与右键"删除"消失 |
| `Rename` | 重命名消失，F2 无效 |
| `Create` | 新建文件夹 / 新建文件消失 |
| `Copy` 且 `Cut` 都没有 | 复制、剪切、粘贴消失 |
| `Compress` 且 `Extract` 都没有 | 压缩、解压消失 |
| `Preview` | 预览按钮消失，双击文件不会打开查看器 |
| `ListFiles` | 文件不显示（只剩文件夹）|
| `ListSubfolders` | 子文件夹不显示 |
| 全部缺失 | 整个目录打不开，返回 403 |

权限是按**当前目录**算的，所以"切到另一个目录按钮就恢复"是正常行为，不是 bug。

### 5.6 配额

- 检查时机：上传（`init` 阶段就拒绝，不浪费传输）、覆盖写入、新建文件。
- 计量方式：首次扫描该目录，之后随写入/删除增量维护；60 秒后重新测量一次，修正外部改动造成的偏差。
- 超限返回 **403**，消息带当前已用量，例如
  `This folder is limited to 5 MB and has 4.7 MB in use.`
- **不拦截**绕过控件的写入（别的进程直接往磁盘拷文件）；这类写入会在下一次测量时体现为超限。

### 5.7 已知边界

- 规则**只按路径匹配，不感知登录用户**。需要"每个用户看到不同目录/权限"时，
  按当前请求构造带不同规则的实例再注册（比如在中间件或 PageModel 里注册一个带用户维度 `id` 的实例）。
- 规则在实例注册后是**不可变的**（解析结果按目录缓存）。改规则需要重新 `Register` 同 `id` 的实例。
- 一条规则只影响"能不能做"，不改变文件本身的权限（ACL 是控件层的，不是 NTFS ACL）。

---

## 6. Office 在线预览

Office 文档（doc/docx/xls/xlsx/ppt/pptx/odt/ods/odp 等）由服务端的 **LibreOffice 无头进程**转成 PDF
后用 `iframe` 内嵌显示，转换结果按 `SHA1(路径|大小|修改时间)` 缓存。

```csharp
o.Config = new FileManagerConfig
{
    OfficePreviewEnabled = true,
    LibreOfficePath = @"C:\Program Files\LibreOffice\program\soffice.exe",  // 留空自动探测
    OfficeConvertTimeoutSeconds = 60,
    OfficePreviewMaxBytes = 100L * 1024 * 1024,
    OfficeCacheMinutes = 1440,
    OfficeCacheFolder = ""                 // 留空 = %TEMP%/fmnet-office-cache
};
```

| 字段 | 默认 | 说明 |
|---|---|---|
| `OfficePreviewEnabled` | `false` | 总开关 |
| `LibreOfficePath` | `""` | 可执行文件路径；空 = 自动探测常见安装位置。仅服务端使用 |
| `OfficeConvertTimeoutSeconds` | `60` | 单次转换超时，超时会杀掉整个进程树 |
| `OfficePreviewMaxBytes` | `100 MB` | 超过此大小的文档不转换（返回 413）|
| `OfficeCacheMinutes` | `1440` | 转换结果保留时长；`0` = 一直保留到源文件变化 |
| `OfficeCacheFolder` | `""` | PDF 缓存目录；空 = 系统临时目录下 |

`OfficePreviewAvailable` 由服务端计算（开关打开 **且** 找到 LibreOffice），前端据此决定
是否允许双击预览 Office 文件。没装 LibreOffice 时，Office 文件双击无反应、右键"预览"提示不可用 ——
这是预期行为，不是故障。

安装 LibreOffice 后无需重启进程，下一次页面渲染会重新探测（结果缓存 1 分钟）。

---

## 7. 外观、视图与本地化

### 7.1 视图布局

七种，与 Windows 资源管理器一致：`ExtraLargeIcons`、`LargeIcons`、`MediumIcons`、
`SmallIcons`、`List`、`Details`（表头可点击排序）、`Tiles`。

用户切换后的布局按实例记在浏览器 `localStorage`；`DefaultView` 只影响首次。

### 7.2 图标

默认使用内置的 **Material Icon Theme**（MIT）矢量图标，任何尺寸都清晰。
若想用 Windows 为扩展名关联的图标：

```csharp
o.Config.UseSystemIcons = true;      // 仅 Windows；其他平台自动回退到内置图标
```

系统图标只有 32px，放大后偏模糊，所以默认关闭。

### 7.3 本地化

内置 9 种语言：`en`、`zh-CN`、`zh-TW`、`ja`、`ko`、`de`、`fr`、`es`、`ru`。

优先级：`FileManagerConfig.Locale` > 标签 `locale` 属性 > 浏览器 `navigator.languages` > `en`。
两者同时设置时以 `FileManagerConfig.Locale` 为准。

想加自己的翻译，在页面里挂一份同结构的对象即可（会覆盖内置语言包）：

```html
<script>
window.FMNetLocales = window.FMNetLocales || {};
window.FMNetLocales["zh-CN"] = { ... };   // 只需写要改的键，其余回退到 en
</script>
```

---

## 8. HTTP API

所有端点都在 `api-endpoint` 下，且必须带 `?instance=<id>`。
实例 id 是页面渲染时注册的，客户端无法指定任意服务器路径。

JSON 端点返回统一信封 `{ ok, error, data }`；`download`、`thumb`、`office`、`icon`
直接返回字节流，不使用信封，目标不存在时是空的 **404**。

### 8.1 端点

| 端点 | 方法 | 说明 |
|---|---|---|
| `{api}` | POST | JSON 命令入口（见 8.2）|
| `{api}/download?path=` | GET | 下载单个文件；`&inline=1` 用于预览（支持 Range 206）|
| `{api}/download?paths=a\|b` | GET | 多选打包成 zip 下载 |
| `{api}/thumb?path=` | GET | 预览/缩略图内容（支持 Range）|
| `{api}/office?path=` | GET | Office 文档转成的 PDF |
| `{api}/icon?ext=docx&size=32` | GET | 系统图标 PNG（需 `UseSystemIcons = true`）|
| `{api}/upload` | POST | 传统 multipart 上传（小文件 / 第三方客户端）|
| `{api}/upload/init` | POST | 开始分块会话，返回 `{id, received, chunkSize, path}`；`received > 0` 表示可续传 |
| `{api}/upload/chunk?id=&offset=` | POST | 上传一块（原始二进制）。偏移不符返回 **409 + 真实 received**，客户端对齐后继续 |
| `{api}/upload/complete?id=&overwrite=&rename=` | POST | 合并落盘，自动创建目录；重名返回 **409 + conflicts** |
| `{api}/upload?id=` | DELETE | 中止会话并删除临时分片 |
| `{api}/job?id=` | GET | 压缩/解压任务进度：`state`、`total`、`done`、`current`、`error` |

### 8.2 命令

请求体为 JSON：`{ "command": "...", ...参数 }`。

| 命令 | 参数 | 说明 |
|---|---|---|
| `list` | `path`、`noCache`(可选) | 返回 `{ entries, permissions, quotaBytes }`；`noCache: true` 强制重新扫描 |
| `tree` | `path` | 子目录（左侧树懒加载）|
| `stats` | `path`、`deep`(可选) | 条目数与卷空间；`deep: false`（默认）只统计当前层 |
| `createFolder` | `path` | |
| `createFile` | `path` | |
| `rename` | `path`、`newName` | |
| `delete` | `paths[]` | 批量删除 |
| `copy` / `move` / `cut` | `paths[]`、`destination`、`overwrite`、`renameOnConflict` | 重名且两个标志都没给时返回 **409 + `conflicts`** |
| `search` | `path`、`search{pattern, maxResults}` | 始终递归整棵子树；`maxResults` 默认 500、上限 5000（`recursive` 字段目前保留、未生效）|
| `zip` | `path`(目标压缩包)、`paths[]` | 格式由扩展名决定 |
| `extract` | `path`(压缩包)、`destination`、`overwrite` | 有 zip-slip 防护 |

长耗时命令可加 `"async": true`，返回 `{ jobId }` 后用 `{api}/job?id=` 轮询进度。

### 8.3 响应与错误码

成功：

```json
{ "ok": true, "error": null, "data": { ... } }
```

失败：

```json
{ "ok": false, "error": "Deleting is not allowed in this folder.", "data": null }
```

| 状态码 | 含义 |
|---|---|
| 200 | 成功；命令接口里的文件系统失败也是 200 + `ok:false` |
| 400 | JSON 格式错误、未注册的实例、上传 id / 参数非法 |
| 403 | **权限不足或被开关禁用**（ACL、实例开关都会走到这里）|
| 404 | 下载/预览目标不存在、任务不存在（文件类端点返回空 404，无信封）|
| 409 | 重名冲突（`conflicts` 里是冲突的名字）；分块偏移不符（`received` 是实际已收字节数）|
| 410 | 上传会话过期或不存在 |
| 413 | 超过上传或 Office 预览的大小上限 |
| 415 | Office 预览收到了非 Office 文件 |
| 500 | Office 转换失败（例如 LibreOffice 进程报错）|

---

## 9. 常见场景配方

### 9.1 部门共享：各自目录各自权限

```csharp
var root = new FileManagerRoot(@"D:\Company", "公司盘");
root.Allow("/", FileManagerPermissions.ReadOnly)
    .Allow("/研发", FileManagerPermissions.Full)
    .Allow("/市场", FileManagerPermissions.Full, denied: FileManagerPermissions.Delete)
    .Allow("/制度", FileManagerPermissions.ReadOnly);
```

### 9.2 对外上传口：只收图片、限容量、不能看别处

```csharp
root.Allow("/", FileManagerPermissions.None)          // 根目录本身不给进
    .Allow("/upload", FileManagerPermissions.ListFiles | FileManagerPermissions.Upload
        | FileManagerPermissions.Create,
        allowedFileTypes: "*.jpg|*.png|*.webp|*.pdf",
        quota: "2 GB");
```

> 注意：`/` 设成 `None` 会让根目录打不开。若要"能进根、但看不到其他子目录"，
> 用 `ListSubfolders` 缺失来隐藏子目录即可。

### 9.3 只读归档区

```csharp
root.ReadOnly("/archive");      // 等价于 Allow("/archive", FileManagerPermissions.ReadOnly)
```

### 9.4 隐藏敏感目录

```csharp
root.Allow("/finance", FileManagerPermissions.None);
```

该目录仍会出现在父目录列表里（父目录允许 `ListSubfolders`），但点进去返回 403。
要连名字都不出现，需要父目录不放行 `ListSubfolders` —— 那会连带隐藏其他目录，通常不划算。

### 9.5 允许大文件上传

```csharp
o.Config.MaxUploadBytes = 0;                    // 不限制（受磁盘约束）
// 服务器端若有反向代理，还要放开请求体上限：
// nginx:            client_max_body_size 0;
// IIS:              请求筛选 → 允许的最大内容长度
```

分块上传本身不分大小，瓶颈是磁盘空间与网络。

### 9.6 隐藏临时文件与日志

```csharp
o.Config.HiddenPatterns = "*.log;*.tmp;~$*;.DS_Store;Thumbs.db";
```

---

## 10. 性能与并发

### 10.1 同一目录的并发请求会被合并

多个用户同时打开同一个文件夹时，只有一个请求真正读盘，其余等待并共享结果。
这一步**始终生效**，与任何配置无关。

实测（1000 项目录）：单次 25 ms，16 个并发请求 790 ms → **121 ms**；
SSD 上 16 个并发与单次耗时相同（10 ms 量级）。

### 10.2 列表缓存

`ListCacheMilliseconds` 控制"**已完成**的列表结果可以复用多久"：

| 值 | 行为 | 适用 |
|---|---|---|
| `0`（默认）| 不复用。别的用户或外部程序改了目录，下一次 `list` 立刻能看到 | 多用户写入频繁、强一致优先 |
| `1000` 左右 | 1 秒内重复打开同一目录不再读盘 | 超大、以读为主的目录 |

无论取值多少：通过控件做的修改会**立即**让缓存失效；用户点"刷新"一定强制重新扫描。

### 10.3 各操作的成本

| 操作 | 成本 | 缓解 |
|---|---|---|
| `list` 超大目录 | O(条目数)，约 10–30 ms / 1000 项 | 调大 `ListCacheMilliseconds`；拆分目录 |
| `search` | O(子树) 同步遍历 | 用 `maxResults` 限制；在更窄的目录里搜 |
| `stats deep` | O(子树) | 默认已关闭，别轻易打开 |
| 权限检查 | 每个目录解析一次（~17 µs，Debug 构建），之后是字典查询 | 无需处理 |
| Office 预览 | 每个未缓存文档启动一次 LibreOffice | 内置并发限制 + 结果缓存 |
| 压缩/解压 | CPU 与磁盘密集 | 后台任务 + 进度轮询 |

### 10.4 线程与部署

文件系统命令是同步调用。几十个并发用户、本地磁盘没有问题；
上千并发会话时，建议提高线程池下限，避免阻塞式 `Directory.Enumerate*` 饿死请求处理：

```csharp
ThreadPool.SetMinThreads(200, 200);   // 在 Program.cs 最前面，按实际压测结果调整
```

或者把 API 放到队列后面。

---

## 11. 安全须知

1. **物理路径永不外泄**：浏览器只知道实例 id，无法传入任意服务器路径；
   所有虚拟路径都会做穿越校验（`..`、盘符、空字节、`//` 一律拒绝）。
2. **权限在服务端强制**：隐藏按钮只是体验，直接构造 HTTP 请求同样会被拒绝（403）。
3. **必须自己做身份认证**：本控件不含登录。请用
   `app.MapFileManagerNet(...).RequireAuthorization()` 或中间件保护端点，
   否则任何能访问该 URL 的人都能按 ACL 允许的范围内操作文件。
4. **压缩包解压有 zip-slip 防护**，但来自不可信来源的压缩包仍建议先在隔离目录解压。
5. **上传大小限制**是双层的：`MaxUploadBytes`（控件）+ 反向代理（nginx/IIS）。
   只改一处不生效。
6. **Office 预览会启动外部进程**。若部署在多租户环境，建议关闭或限制
   `OfficePreviewMaxBytes`，并确保缓存目录不对外暴露。
7. **ACL 不替代操作系统权限**：它在应用层生效，磁盘上的文件本身仍是进程身份可读的。

---

## 12. 故障排查

| 现象 | 原因 | 处理 |
|---|---|---|
| 列表区高度塌陷 / 只有一条线 | 父容器没有确定高度 | 给外层加 `height`（如 `80vh`）或 flex |
| 页面空白，控制台 404 | 静态资源路径不对 | 确认 `app.MapStaticAssets()` 已调用，且 RCL 被正确引用 |
| 所有按钮都变灰 | 当前目录权限为只读 | 检查 ACL；或 `FileManagerConfig` 里关了开关 |
| 切目录后按钮又恢复/变灰 | 正常：权限按目录算 | 见 [5.5](#55-权限--界面表现) |
| 删除/上传报 403 | ACL 拒绝或实例开关关闭 | 看返回的 `error` 文案，会指明是哪种 |
| 上传报 403 但目录看起来可写 | 文件类型被 `AllowedFileTypes` 排除，或超配额 | 检查规则里的类型与 `Quota` |
| 上传到一半 409 | 分块偏移不符（网络中断后重试）| 客户端会读取 `received` 自动续传；自研客户端需照做 |
| Office 文件双击没反应 | 未装 LibreOffice 或 `OfficePreviewEnabled=false` | 装 LibreOffice 或打开开关；看 `OfficePreviewAvailable` |
| 视频不能拖动进度条 | 反向代理剥离了 Range 头 | 让代理透传 `Range` / `Accept-Ranges` |
| 中文文件名乱码 | 上传端未用 UTF-8 编码文件名 | 统一 UTF-8；文本预览可手动切换编码 |
| 多实例第二个不工作 | 手动挂载时漏了 `.fmnet-root` 容器 | 用标签助手，或确保内层有 `.fmnet-root` |
| 改了 ACL 不生效 | 规则在实例注册时固化 | 用同 `id` 重新 `Register` |
| 标签实例没继承 `Program.cs` 里的根目录或开关 | `MapFileManagerNet` 的 `o` 只配置默认实例 | 在标签上写 `root-folder` / `config`；ACL 用同 `id` 的 `Register` 预挂 |

---

## 13. 完整示例

### 13.1 `Program.cs`

```csharp
using FileManagerNet;
using FileManagerNet.Infrastructure;
using FileManagerNet.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFileManagerNet();
builder.Services.AddRazorPages();

var app = builder.Build();

// ① 默认实例（id = "/filemanager-api"；页面标签实例不会继承这里的配置）
app.MapFileManagerNet("/filemanager-api", o =>
{
    o.RootFolder = Path.Combine(app.Environment.ContentRootPath, "App_Data", "files");
    o.RootDisplayName = "文件";
    o.Config = new FileManagerConfig
    {
        DefaultView = ViewLayout.Details,
        MaxUploadBytes = 0,                       // 不限（受磁盘约束）
        HiddenPatterns = "*.tmp;~$*",
        OfficePreviewEnabled = true,              // 需安装 LibreOffice
        ListCacheMilliseconds = 0
    };
});

// ② 带权限的实例：Program.cs 里注册规则，页面上用同一个 id 渲染
var shared = new FileManagerRoot(@"D:\Shared", "共享盘");
shared
    .Allow("/", FileManagerPermissions.ReadOnly)
    .Allow("/public", FileManagerPermissions.ReadOnly
        | FileManagerPermissions.Create | FileManagerPermissions.Upload
        | FileManagerPermissions.Delete | FileManagerPermissions.Rename
        | FileManagerPermissions.Paste)
    .Allow("/public/images",
        FileManagerPermissions.ReadOnly | FileManagerPermissions.Upload | FileManagerPermissions.Create,
        allowedFileTypes: "*.jpg|*.png|*.webp",
        quota: "500 MB")
    .Allow("/secret", FileManagerPermissions.None);

FileManagerRegistry.Register("FM_SHARED", new[] { shared }, "共享盘", new FileManagerConfig
{
    DefaultView = ViewLayout.Details,
    MaxUploadBytes = 0
});

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorPages();
app.Run();
```

### 13.2 `Pages/Files.cshtml`

```cshtml
@page
@using FileManagerNet.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, FileManagerNet
@{
    Layout = null;
    var fmConfig = new FileManagerConfig
    {
        DefaultView = ViewLayout.Details,
        MaxUploadBytes = 2L * 1024 * 1024 * 1024,
        ShowFileExtensions = true
    };
}
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>文件管理</title>
    <style>
        html, body { margin: 0; height: 100%; }
        .page { display: flex; flex-direction: column; height: 100%; }
        .page-main { flex: 1; min-height: 0; padding: 12px; box-sizing: border-box; }
        .fm-host { height: 100%; }
    </style>
</head>
<body>
<div class="page">
    <div class="page-main">
        <div class="fm-host">
            <!-- 普通实例：不写规则时全部允许（受 config 限制） -->
            <file-manager id="FM1"
                          root-folder="D:\Data"
                          root-name="数据"
                          config="fmConfig"
                          api-endpoint="/filemanager-api" />
        </div>
    </div>
</div>
</body>
</html>
```

带权限的页面只需要换 `id` 与 `root-folder`：

```cshtml
<file-manager id="FM_SHARED"
              root-folder="D:\Shared"
              root-name="共享盘"
              config="fmConfig"
              api-endpoint="/filemanager-api" />
```

> Linux / macOS 部署时把示例里的 `D:\...` 换成 `/srv/...` 之类的原生绝对路径即可，
> 其余配置不变。

---

## 附录：默认值速查

| 配置 | 默认 |
|---|---|
| `MaxUploadBytes` | 512 MB |
| `ListCacheMilliseconds` | 0（不复用）|
| `DefaultView` | `Details` |
| `ShowFoldersPane` | `true` |
| `ShowFileExtensions` / `ShowHiddenFiles` | `false` |
| `UseSystemIcons` | `false` |
| `OfficePreviewEnabled` | `false` |
| `OfficeConvertTimeoutSeconds` | 60 |
| `OfficePreviewMaxBytes` | 100 MB |
| `OfficeCacheMinutes` | 1440 |
| 所有 `Allow*` | `true` |
| `locale` | 跟随浏览器 |
| `root-folder` 未指定 | `AppContext.BaseDirectory` |
| 无 ACL 规则 | 全部允许（受实例开关限制）|
