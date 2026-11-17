using System.Text.Json.Serialization;

namespace FileManagerNet.Models;

/// <summary>
/// Feature switches and defaults shared by one control instance.
/// Setting a switch to <c>false</c> hides the matching ribbon entry and
/// context-menu item and makes the server reject the corresponding API call.
/// </summary>
public sealed class FileManagerConfig
{
    /// <summary>Allow downloading files. Also required for previews.</summary>
    public bool AllowDownload { get; set; } = true;

    /// <summary>Allow uploading files.</summary>
    public bool AllowUpload { get; set; } = true;

    /// <summary>Allow deleting files and folders.</summary>
    public bool AllowDelete { get; set; } = true;

    /// <summary>Allow renaming files and folders.</summary>
    public bool AllowRename { get; set; } = true;

    /// <summary>Allow creating folders.</summary>
    public bool AllowCreateFolder { get; set; } = true;

    /// <summary>Allow creating empty files.</summary>
    public bool AllowCreateFile { get; set; } = true;

    /// <summary>Allow copying, cutting and pasting items.</summary>
    public bool AllowCopyMove { get; set; } = true;

    /// <summary>Allow creating and extracting archives (zip / extract).</summary>
    public bool AllowArchive { get; set; } = true;

    /// <summary>Allow recursive search.</summary>
    public bool AllowSearch { get; set; } = true;

    /// <summary>
    /// Per-file upload limit in bytes; <c>0</c> means unlimited. Chunked
    /// uploads are bounded only by disk space, so a multi-gigabyte file works
    /// when this is 0.
    /// </summary>
    public long MaxUploadBytes { get; set; } = 512 * 1024 * 1024;

    /// <summary>
    /// How long a finished folder listing may be reused, in milliseconds.
    /// Concurrent requests for the same folder are always collapsed into a
    /// single directory enumeration, independent of this value.
    /// <para>
    /// The default <c>0</c> reuses nothing, so a folder changed by another user
    /// or by an external program is visible on the next listing. Raise it (for
    /// example 1000) on very large, mostly read-only folders to skip repeated
    /// metadata scans; mutations through the control clear the cache either way.
    /// </para>
    /// </summary>
    public int ListCacheMilliseconds { get; set; }

    /// <summary>View layout shown on first load.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ViewLayout DefaultView { get; set; } = ViewLayout.Details;

    /// <summary>Semicolon-separated glob patterns to hide, for example "*.log;*.tmp".</summary>
    public string HiddenPatterns { get; set; } = "";

    /// <summary>Show file name extensions in the list (View tab toggle).</summary>
    public bool ShowFileExtensions { get; set; }

    /// <summary>Show hidden (dot-prefixed) files and folders initially (View tab toggle).</summary>
    public bool ShowHiddenFiles { get; set; }

    /// <summary>Show the navigation (folder tree) pane initially (View tab toggle).</summary>
    public bool ShowFoldersPane { get; set; } = true;

    /// <summary>
    /// Force a UI language: <c>en</c>, <c>zh-CN</c>, <c>zh-TW</c>, <c>ja</c>,
    /// <c>ko</c>, <c>de</c>, <c>fr</c>, <c>es</c> or <c>ru</c>. Empty follows the
    /// browser language.
    /// </summary>
    public string Locale { get; set; } = "";

    /// <summary>
    /// Enable online preview of Office documents (Word / Excel / PowerPoint /
    /// OpenDocument). Files are converted to PDF by a LibreOffice headless
    /// process and served inline; the result is cached.
    /// </summary>
    public bool OfficePreviewEnabled { get; set; }

    /// <summary>
    /// Set by the server: Office preview is enabled <em>and</em> a LibreOffice
    /// executable was found. When false the client disables preview for Office
    /// files instead of failing at request time.
    /// </summary>
    public bool OfficePreviewAvailable { get; set; }

    /// <summary>
    /// Use the icon Windows associates with each extension instead of the
    /// bundled vector icon set. Off by default because system icons are only
    /// 32 px and soften when scaled up.
    /// </summary>
    public bool UseSystemIcons { get; set; }

    /// <summary>
    /// Full path to the LibreOffice executable (for example
    /// <c>C:\Program Files\LibreOffice\program\soffice.exe</c> or
    /// <c>/usr/bin/soffice</c>). Empty auto-detects the usual locations.
    /// Server-side only; never sent to the browser.
    /// </summary>
    [JsonIgnore]
    public string LibreOfficePath { get; set; } = "";

    /// <summary>Conversion timeout in seconds; a hung conversion is killed.</summary>
    [JsonIgnore]
    public int OfficeConvertTimeoutSeconds { get; set; } = 60;

    /// <summary>Maximum source file size that may be converted, in bytes.</summary>
    [JsonIgnore]
    public long OfficePreviewMaxBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// How long a converted PDF stays cached, in minutes. <c>0</c> keeps it
    /// until the source file changes or the cache folder is cleaned.
    /// </summary>
    [JsonIgnore]
    public int OfficeCacheMinutes { get; set; } = 1440;

    /// <summary>Folder for converted PDFs; empty uses <c>%TEMP%/fmnet-office-cache</c>.</summary>
    [JsonIgnore]
    public string OfficeCacheFolder { get; set; } = "";
}
