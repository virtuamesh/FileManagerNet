using System.Text.Encodings.Web;
using System.Text.Json;
using FileManagerNet.Infrastructure;
using FileManagerNet.Models;
using FileManagerNet.Services;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace FileManagerNet.TagHelpers;

/// <summary>
/// Renders the FileManagerNet control:
/// <code>
/// &lt;file-manager id="FM1" root-folder="C:\Data" api-endpoint="/filemanager-api" /&gt;
/// &lt;file-manager id="FM2" root-folder="Data=C:\Data;Backup=E:\Backup" /&gt;
/// </code>
/// The instance (its root folder(s) + feature config) is registered on the
/// server; the rendered markup only carries the instance id, never a server path.
/// </summary>
[HtmlTargetElement("file-manager", TagStructure = TagStructure.WithoutEndTag)]
public sealed class FileManagerTagHelper : TagHelper
{
    /// <summary>Asset cache-busting version appended to css/js urls. Derived
    /// from the assembly version so it changes with every release.</summary>
    public static string Version { get; set; } =
        typeof(FileManagerTagHelper).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Used to emit the stylesheet/scripts only once per HTTP request,
    /// however many instances the page contains.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>Application-wide unique id of this control instance.</summary>
    [HtmlAttributeName("id")]
    public string? Id { get; set; }

    /// <summary>Physical root folder(s) on the server for this instance. Give one
    /// folder (<c>root-folder="D:\Data"</c>) or several separated by <c>;</c>,
    /// each optionally prefixed with a display name and <c>=</c>
    /// (<c>root-folder="数据=D:\Data;备份=E:\Backup"</c>). With several folders the
    /// root node lists them as folders; each keeps its own physical root on the
    /// server, so nothing outside them is reachable.</summary>
    [HtmlAttributeName("root-folder")]
    public string? RootFolder { get; set; }

    /// <summary>Alias of <see cref="RootFolder"/> (same syntax), readable when
    /// several folders are configured.</summary>
    [HtmlAttributeName("root-folders")]
    public string? RootFolders { get; set; }

    /// <summary>Display name of the root node (defaults to the folder name).</summary>
    [HtmlAttributeName("root-name")]
    public string? RootName { get; set; }

    /// <summary>API endpoint mapped with MapFileManagerNet.</summary>
    [HtmlAttributeName("api-endpoint")]
    public string ApiEndpoint { get; set; } = "/filemanager-api";

    /// <summary>
    /// Force a UI language (e.g. "en", "zh-CN", "zh-TW", "ja", "ko", "de", "fr", "es", "ru").
    /// When omitted the language is picked from the browser settings. A custom
    /// language can be used by registering its dictionary on
    /// window.FMNetLocales["&lt;id&gt;"] from your own script before initialization.
    /// </summary>
    [HtmlAttributeName("locale")]
    public string? Locale { get; set; }

    /// <summary>Optional per-instance feature configuration.</summary>
    [HtmlAttributeName("config")]
    public FileManagerConfig? Config { get; set; }

    /// <summary>Renders the control wrapper, registers the instance on the
    /// server and emits the stylesheet/scripts once per request.</summary>
    /// <param name="context">Tag-helper context.</param>
    /// <param name="output">Tag-helper output.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        var id = string.IsNullOrWhiteSpace(Id) ? "fm-" + Guid.NewGuid().ToString("N") : Id!;

        /* one folder, or several separated by ';' ("name=path" names them) */
        var roots = FileManagerRoot.ParseMany(RootFolders);
        if (roots.Count == 0) roots = FileManagerRoot.ParseMany(RootFolder);
        if (roots.Count == 0) roots.Add(new FileManagerRoot(AppContext.BaseDirectory));

        var displayName = RootName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = roots.Count == 1
                ? (string.IsNullOrWhiteSpace(roots[0].Name)
                    ? System.IO.Path.GetFileName(roots[0].Path.TrimEnd('\\', '/'))
                    : roots[0].Name)
                : "Home";
        }
        if (string.IsNullOrEmpty(displayName)) displayName = "Home";
        var config = Config ?? new FileManagerConfig();

        /* probe once per render (cached for a minute) so the client knows whether
         * "preview" can actually work for Office files */
        config.OfficePreviewAvailable = config.OfficePreviewEnabled
            && OfficePreviewService.ResolveSoffice(config.LibreOfficePath) is not null;

        FileManagerRegistry.Register(id, roots, displayName, config);

        // The tag helper element is a WRAPPER that persists in the DOM.
        // The inner .fmnet-root div is the mount target the front-end wipes
        // and rebuilds — the <link>/<script> live outside it, so removing the
        // inner HTML never cancels the stylesheet load.
        output.Attributes.SetAttribute("class", "fmnet-wrap");
        output.Attributes.SetAttribute("data-fmnet", "1");
        output.Attributes.SetAttribute("data-instance", id);
        output.Attributes.SetAttribute("data-api", ApiEndpoint);
        output.Attributes.SetAttribute("data-root-name", displayName);
        /* the virtual root only holds the mounts: the client must not offer
         * "create here" / "paste here" / "drop here" actions */
        if (roots.Count > 1)
            output.Attributes.SetAttribute("data-multi-root", "1");
        if (!string.IsNullOrWhiteSpace(Locale))
            output.Attributes.SetAttribute("data-locale", Locale);
        // Raw JSON — the framework HTML-encodes attribute values automatically.
        output.Attributes.SetAttribute("data-config", JsonSerializer.Serialize(config, JsonOptions));

        /* Emit the stylesheet and scripts only for the first instance of this
         * request — a page with several file managers would otherwise load the
         * same assets N times (and the browser would execute them N times). */
        var items = ViewContext?.HttpContext?.Items;
        const string assetsKey = "__fmnet_assets_emitted";
        var firstInstance = items is null || !items.ContainsKey(assetsKey);
        if (items is not null) items[assetsKey] = true;

        output.Content.SetHtmlContent(new HtmlString(
            $"""
             {(firstInstance ? $"<link rel=\"stylesheet\" href=\"/_content/FileManagerNet/css/fmnet.css?v={Version}\" />" : "")}
             <div class="fmnet-root"><div class="fmnet-loading"><span class="fmnet-spinner"></span>Loading…</div></div>
             {(firstInstance ? $"<script src=\"/_content/FileManagerNet/js/fmnet.js?v={Version}\" defer></script>" : "")}
             {(firstInstance ? $"<script src=\"/_content/FileManagerNet/js/fmnet.ui.js?v={Version}\" defer></script>" : "")}
             """));
    }
}
