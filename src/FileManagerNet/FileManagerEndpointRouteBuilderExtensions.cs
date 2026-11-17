using System.Text.Json;
using FileManagerNet.Infrastructure;
using FileManagerNet.Models;
using FileManagerNet.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FileManagerNet;

/// <summary>
/// Endpoint mapping for the FileManagerNet HTTP API. All routes live under the
/// mapped pattern and resolve their instance (and root folders) through
/// <see cref="FileManagerRegistry"/>, so the client can never pass a physical
/// server path.
/// </summary>
public static class FileManagerEndpointRouteBuilderExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps the FileManagerNet API under <paramref name="pattern"/>:
    /// <c>POST {pattern}</c> — JSON commands; <c>GET {pattern}/download</c> —
    /// file or zip download; <c>GET {pattern}/thumb</c> — inline preview;
    /// <c>POST {pattern}/upload</c> — multipart upload.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder to map onto.</param>
    /// <param name="pattern">Base route of the API.</param>
    /// <param name="configure">Optional configuration of the default roots and switches.</param>
    /// <returns>A convention builder for further endpoint configuration.</returns>
    public static IEndpointConventionBuilder MapFileManagerNet(this IEndpointRouteBuilder endpoints,
        string pattern = "/filemanager-api",
        Action<FileManagerOptions>? configure = null)
    {
        var options = new FileManagerOptions();
        configure?.Invoke(options);
        var roots = options.RootFolders.Count > 0
            ? options.RootFolders
            : FileManagerRoot.ParseMany(options.RootFolder);
        if (roots.Count == 0)
            throw new ArgumentException("At least one root folder must be configured for MapFileManagerNet.");
        if (string.IsNullOrWhiteSpace(options.ApiEndpoint))
            options.ApiEndpoint = pattern;

        options.Config.OfficePreviewAvailable = options.Config.OfficePreviewEnabled
            && OfficePreviewService.ResolveSoffice(options.Config.LibreOfficePath) is not null;

        FileManagerRegistry.Register(options.ApiEndpoint, roots,
            options.RootDisplayName, options.Config);

        var group = endpoints.MapGroup(pattern);

        group.MapPost("/", async (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            CommandRequest req;
            try
            {
                req = await JsonSerializer.DeserializeAsync<CommandRequest>(ctx.Request.Body, JsonOptions)
                      ?? new CommandRequest();
            }
            catch (JsonException)
            {
                return Results.Json(ApiResponse<object>.Failure("Malformed JSON body."), statusCode: 400);
            }
            var cfg = FileManagerRegistry.GetConfig(id);
            var svc = FileManagerRegistry.GetService(id);
            var path = string.IsNullOrEmpty(req.Path) ? "/" : req.Path;
            try
            {
                object data = req.Command switch
                {
                    "list" => ListWithAccess(svc, path, req.NoCache),
                    "tree" => svc.GetChildren(path),
                    "stats" => svc.GetStats(path, req.Deep),
                    "createFolder" => Guard(cfg.AllowCreateFolder, () => { svc.CreateFolder(path); return Obj(true); }),
                    "createFile" => Guard(cfg.AllowCreateFile, () => { svc.CreateFile(path); return Obj(true); }),
                    "rename" => Guard(cfg.AllowRename, () => { svc.Rename(path, req.NewName ?? ""); return Obj(true); }),
                    "delete" => Guard(cfg.AllowDelete, () => { DeleteMany(svc, req.Paths, path); return Obj(true); }),
                    "copy" => Guard(cfg.AllowCopyMove, () => { CopyMany(svc, req, path); return Obj(true); }),
                    "move" => Guard(cfg.AllowCopyMove, () => { MoveMany(svc, req, path); return Obj(true); }),
                    /* "cut" is the clipboard mode on the client; accept it as an
                     * alias so API callers can post either name */
                    "cut" => Guard(cfg.AllowCopyMove, () => { MoveMany(svc, req, path); return Obj(true); }),
                    "search" => Guard(cfg.AllowSearch, () => Obj(svc.Search(path, req.Search ?? new SearchOptions()))),
                    "zip" => Guard(cfg.AllowArchive, () => Obj(StartArchive(svc, req, path))),
                    "extract" => Guard(cfg.AllowArchive, () => Obj(StartArchive(svc, req, path))),
                    _ => throw new ArgumentException("Unknown command: " + req.Command),
                };
                return Results.Json(ApiResponse<object>.Success(data));
            }
            catch (FileConflictException ex)
            {
                /* the client asks the user: overwrite, keep both, or cancel */
                return Results.Json(new
                {
                    ok = false,
                    error = ex.Message,
                    data = (object?)null,
                    conflicts = ex.Conflicts
                }, statusCode: 409);
            }
            catch (UnauthorizedAccessException denied)
            {
                return Results.Json(ApiResponse<object>.Failure(denied.Message), statusCode: 403);
            }
            catch (Exception ex) when (ex is IOException or FileNotFoundException or DirectoryNotFoundException or ArgumentException or InvalidOperationException)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message));
            }
        });

        // ---------------- archive job progress ----------------
        group.MapGet("/job", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            var job = ArchiveJobService.Get(ctx.Request.Query["id"].ToString());
            if (job is null)
                return Results.Json(ApiResponse<object>.Failure("Job not found or expired."), statusCode: 404);
            return Results.Json(ApiResponse<object>.Success(new
            {
                id = job.Id,
                kind = job.Kind,
                label = job.Label,
                state = job.State,
                total = job.Total,
                done = job.Done,
                current = job.Current,
                error = job.Error,
                elapsedMs = (long)((job.FinishedAt ?? DateTime.UtcNow) - job.StartedAt).TotalMilliseconds
            }));
        });

        // ---------------- download (single file or zip of many) ----------------
        group.MapGet("/download", async (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            var cfg = FileManagerRegistry.GetConfig(id);
            if (!cfg.AllowDownload) return Results.Json(ApiResponse<object>.Failure("Download is disabled."), statusCode: 403);
            var svc = FileManagerRegistry.GetService(id);

            try
            {
                var paths = (ctx.Request.Query["paths"].ToString())
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (paths.Length > 1)
                {
                    /* packing several items into one zip is a download, so it
                     * needs Download on each item, not Compress on the folder */
                    foreach (var p in paths)
                    {
                        var itemAccess = svc.AccessAt(p);
                        if (!itemAccess.Has(FileManagerPermissions.Download))
                            return Results.Json(
                                ApiResponse<object>.Failure($"Downloading '{p}' is not allowed."), statusCode: 403);
                        var itemName = p[(p.LastIndexOf('/') + 1)..];
                        if (!itemAccess.IsFileAllowed(itemName))
                            return Results.Json(
                                ApiResponse<object>.Failure($"'{itemName}' cannot be downloaded."), statusCode: 403);
                    }
                    /* the temp archive must live inside a mount: with several
                     * root folders "/" has no physical counterpart */
                    var first = paths[0];
                    var slash = first.IndexOf('/', 1);
                    var mountSegment = slash < 0 ? "" : first[..slash];
                    var zipVirtual = mountSegment + "/.fmnet-download-" + Guid.NewGuid().ToString("N") + ".zip";
                    svc.CreateArchive(zipVirtual, paths, null, enforcePermission: false);
                    var zipPhysical = svc.Resolve(zipVirtual);
                    var bytes = await System.IO.File.ReadAllBytesAsync(zipPhysical);
                    try { System.IO.File.Delete(zipPhysical); } catch { }
                    return Results.File(bytes, "application/zip", "download.zip");
                }

                var single = ctx.Request.Query["path"].ToString();
                var resolved = svc.ResolveForDownload(single);
                if (resolved is null) return Results.NotFound();
                var (physical, entry) = resolved.Value;
                var inline = ctx.Request.Query["inline"].ToString() == "1";
                var contentType = inline ? GuessContentType(entry.Name) : "application/octet-stream";
                /* range processing is what lets <video>/<audio> seek; without it
                 * the browser can only play from the start of a buffered file */
                return Results.File(System.IO.File.OpenRead(physical), contentType,
                    fileDownloadName: inline ? null : entry.Name,
                    enableRangeProcessing: true);
            }
            catch (UnauthorizedAccessException denied)
            {
                return Results.Json(ApiResponse<object>.Failure(denied.Message), statusCode: 403);
            }
            catch (Exception ex) when (ex is IOException or FileNotFoundException or ArgumentException)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message));
            }
        });

        // ---------------- Office → PDF preview (LibreOffice) ----------------
        group.MapGet("/office", async (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            var cfg = FileManagerRegistry.GetConfig(id);
            if (!cfg.AllowDownload)
                return Results.Json(ApiResponse<object>.Failure("Download is disabled."), statusCode: 403);
            if (!cfg.OfficePreviewEnabled)
                return Results.Json(ApiResponse<object>.Failure("Office preview is disabled."), statusCode: 403);

            var svc = FileManagerRegistry.GetService(id);
            try
            {
                var path = ctx.Request.Query["path"].ToString();
                if (!OfficePreviewService.IsOfficeFile(path))
                    return Results.Json(ApiResponse<object>.Failure("Not an Office document."), statusCode: 415);

                var physical = svc.Resolve(path);
                if (!File.Exists(physical)) return Results.NotFound();
                var length = new FileInfo(physical).Length;
                if (cfg.OfficePreviewMaxBytes > 0 && length > cfg.OfficePreviewMaxBytes)
                    return Results.Json(ApiResponse<object>.Failure("File is too large to preview."), statusCode: 413);

                var (pdf, error) = await OfficePreviewService.GetPdfAsync(
                    physical,
                    OfficePreviewService.ResolveSoffice(cfg.LibreOfficePath),
                    OfficePreviewService.ResolveCacheFolder(cfg.OfficeCacheFolder),
                    cfg.OfficeConvertTimeoutSeconds,
                    cfg.OfficeCacheMinutes,
                    ctx.RequestAborted);

                if (pdf is null)
                    return Results.Json(ApiResponse<object>.Failure(error ?? "Conversion failed."), statusCode: 500);

                return Results.File(pdf, "application/pdf", enableRangeProcessing: true);
            }
            catch (UnauthorizedAccessException denied)
            {
                return Results.Json(ApiResponse<object>.Failure(denied.Message), statusCode: 403);
            }
            catch (Exception ex) when (ex is FileNotFoundException or ArgumentException or InvalidOperationException)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message));
            }
        });

        // ---------------- inline image preview ----------------
        group.MapGet("/thumb", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            if (!FileManagerRegistry.GetConfig(id).AllowDownload)
                return Results.Json(ApiResponse<object>.Failure("Download is disabled."), statusCode: 403);
            var svc = FileManagerRegistry.GetService(id);
            try
            {
                var thumbPath = ctx.Request.Query["path"].ToString();
                var thumbAccess = svc.AccessAt(thumbPath);
                if (!thumbAccess.Has(FileManagerPermissions.Preview) &&
                    !thumbAccess.Has(FileManagerPermissions.Download))
                    return Results.Json(ApiResponse<object>.Failure("Previewing is not allowed in this folder."),
                        statusCode: 403);
                var thumbName = thumbPath[(thumbPath.LastIndexOf('/') + 1)..];
                if (!thumbAccess.IsFileAllowed(thumbName))
                    return Results.Json(ApiResponse<object>.Failure($"'{thumbName}' cannot be previewed."),
                        statusCode: 403);

                var physical = svc.Resolve(thumbPath);
                if (!System.IO.File.Exists(physical)) return Results.NotFound();
                /* range support so audio/video elements can seek */
                return Results.File(System.IO.File.OpenRead(physical),
                    GuessContentType(thumbName), enableRangeProcessing: true);
            }
            catch (UnauthorizedAccessException denied)
            {
                return Results.Json(ApiResponse<object>.Failure(denied.Message), statusCode: 403);
            }
            catch (Exception ex) when (ex is FileNotFoundException or ArgumentException)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message));
            }
        });

        // ---------------- system file-type icon ----------------
        group.MapGet("/icon", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            if (!FileManagerRegistry.GetConfig(id).UseSystemIcons || !SystemIconService.IsSupported)
                return Results.NotFound();
            var size = int.TryParse(ctx.Request.Query["size"], out var s) ? s : 32;
            var png = SystemIconService.GetPng(ctx.Request.Query["ext"].ToString(), size);
            if (png is null) return Results.NotFound();
            ctx.Response.Headers.CacheControl = "public, max-age=604800";
            return Results.File(png, "image/png");
        });

        // ---------------- multipart upload ----------------
        group.MapPost("/upload", async (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            var cfg = FileManagerRegistry.GetConfig(id);
            if (!cfg.AllowUpload)
                return Results.Json(ApiResponse<object>.Failure("Upload is disabled."), statusCode: 403);
            var svc = FileManagerRegistry.GetService(id);

            var folder = ctx.Request.Form["path"].ToString();
            if (string.IsNullOrEmpty(folder)) folder = "/";
            var overwrite = ctx.Request.Form["overwrite"].ToString() == "1";
            var uploaded = new List<string>();
            try
            {
                if (svc.IsVirtualRoot(folder))
                    return Results.Json(ApiResponse<object>.Failure("Choose one of the folders first."),
                        statusCode: 400);
                foreach (var file in ctx.Request.Form.Files)
                {
                    if (cfg.MaxUploadBytes > 0 && file.Length > cfg.MaxUploadBytes)
                        throw new IOException($"'{file.FileName}' exceeds the upload size limit.");
                    var target = (folder == "/" ? "" : folder) + "/" + file.FileName;
                    using var stream = file.OpenReadStream();
                    svc.SaveUploaded(stream, target, overwrite);
                    uploaded.Add(target);
                }
                return Results.Json(ApiResponse<object>.Success(new { uploaded }));
            }
            catch (UnauthorizedAccessException denied)
            {
                return Results.Json(ApiResponse<object>.Failure(denied.Message), statusCode: 403);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message));
            }
        });

        // ---------------- chunked / resumable upload ----------------
        /* Three steps: init (session + already-received bytes), chunk (raw body,
         * one request per 8 MB by default), complete (moves the assembled file
         * into place, creating folders for folder uploads). Because a chunk is a
         * plain body — not multipart — file size is bounded only by disk space,
         * and re-selecting the same file after a drop resumes automatically. */
        group.MapPost("/upload/init", async (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            var cfg = FileManagerRegistry.GetConfig(id);
            if (!cfg.AllowUpload)
                return Results.Json(ApiResponse<object>.Failure("Upload is disabled."), statusCode: 403);
            var svc = FileManagerRegistry.GetService(id);

            UploadInitRequest req;
            try
            {
                req = await JsonSerializer.DeserializeAsync<UploadInitRequest>(ctx.Request.Body, JsonOptions)
                      ?? new UploadInitRequest();
            }
            catch (JsonException)
            {
                return Results.Json(ApiResponse<object>.Failure("Malformed JSON body."), statusCode: 400);
            }

            try
            {
                var folder = string.IsNullOrEmpty(req.Path) ? "/" : req.Path;
                if (svc.IsVirtualRoot(folder) || svc.IsMountRootLevel(folder))
                    return Results.Json(ApiResponse<object>.Failure("Choose one of the folders first."),
                        statusCode: 400);

                var rel = NormalizeUploadName(req.RelativePath ?? req.Name);
                var target = folder == "/" ? "/" + rel : folder.TrimEnd('/') + "/" + rel;
                var physical = svc.Resolve(target);        // validates every segment

                if (cfg.MaxUploadBytes > 0 && req.Size > cfg.MaxUploadBytes)
                    return Results.Json(ApiResponse<object>.Failure(
                        $"'{req.Name}' exceeds the upload size limit."), statusCode: 413);

                /* fail before a single byte is transferred, not after */
                svc.EnsureCanWrite(target, false, req.Size);

                var (sid, received) = UploadSessionService.Begin(id, physical, req.Size, req.Name);
                return Results.Json(ApiResponse<object>.Success(new
                {
                    id = sid,
                    received,
                    chunkSize = UploadSessionService.DefaultChunkSize,
                    path = target
                }));
            }
            catch (UnauthorizedAccessException denied)
            {
                return Results.Json(ApiResponse<object>.Failure(denied.Message), statusCode: 403);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message));
            }
        });

        group.MapPost("/upload/chunk", async (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            if (!FileManagerRegistry.GetConfig(id).AllowUpload)
                return Results.Json(ApiResponse<object>.Failure("Upload is disabled."), statusCode: 403);

            var sid = ctx.Request.Query["id"].ToString();
            if (!UploadSessionService.IsValidId(sid))
                return Results.Json(ApiResponse<object>.Failure("Invalid upload id."), statusCode: 400);

            var offset = long.TryParse(ctx.Request.Query["offset"], out var o) ? o : 0;
            try
            {
                var received = await UploadSessionService.AppendAsync(sid, offset, ctx.Request.Body,
                    ctx.RequestAborted);
                return Results.Json(ApiResponse<object>.Success(new { received }));
            }
            catch (UploadOffsetMismatchException ex)
            {
                /* the client asks "how much do you have?" and continues from there */
                return Results.Json(new
                {
                    ok = false,
                    error = "Offset mismatch.",
                    received = ex.Received,
                    data = (object?)null
                }, statusCode: 409);
            }
            catch (UnauthorizedAccessException denied)
            {
                return Results.Json(ApiResponse<object>.Failure(denied.Message), statusCode: 403);
            }
            catch (Exception ex) when (ex is IOException)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message));
            }
        });

        group.MapPost("/upload/complete", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            if (!FileManagerRegistry.GetConfig(id).AllowUpload)
                return Results.Json(ApiResponse<object>.Failure("Upload is disabled."), statusCode: 403);
            var svc = FileManagerRegistry.GetService(id);

            var sid = ctx.Request.Query["id"].ToString();
            if (!UploadSessionService.IsValidId(sid))
                return Results.Json(ApiResponse<object>.Failure("Invalid upload id."), statusCode: 400);

            var overwrite = ctx.Request.Query["overwrite"].ToString() == "1";
            var rename = ctx.Request.Query["rename"].ToString() == "1";
            try
            {
                /* the assembled file is moved into place outside SaveUploaded, so
                 * check the target here — otherwise a chunked upload would slip
                 * past the folder's permissions, type filter and quota */
                var session = UploadSessionService.Peek(sid)
                    ?? throw new FileNotFoundException("Upload session not found or expired.");
                svc.EnsureCanWrite(svc.ToVirtualPath(session.Target), overwrite, session.Size);

                var final = UploadSessionService.Finish(sid, overwrite, rename);
                var finalVirtual = svc.ToVirtualPath(final);
                /* the bytes landed outside SaveUploaded: make the quota re-measure */
                svc.InvalidateUsage(finalVirtual);
                return Results.Json(ApiResponse<object>.Success(new { path = finalVirtual }));
            }
            catch (UploadConflictException ex)
            {
                return Results.Json(new
                {
                    ok = false,
                    error = ex.Message,
                    data = (object?)null,
                    conflicts = new[] { ex.Name }
                }, statusCode: 409);
            }
            catch (FileNotFoundException ex)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message), statusCode: 410);
            }
            catch (UnauthorizedAccessException denied)
            {
                return Results.Json(ApiResponse<object>.Failure(denied.Message), statusCode: 403);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
                return Results.Json(ApiResponse<object>.Failure(ex.Message));
            }
        });

        group.MapDelete("/upload", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["instance"].ToString();
            if (!Check(id, out var fail)) return Results.Json(fail, statusCode: 400);
            var sid = ctx.Request.Query["id"].ToString();
            if (UploadSessionService.IsValidId(sid)) UploadSessionService.Discard(sid);
            return Results.Json(ApiResponse<object>.Success(true));
        });

        return group;
    }

    /// <summary>Validates an upload's relative name/path: no traversal, no drive
    /// letters, no empty segments.</summary>
    private static string NormalizeUploadName(string? relative)
    {
        var rel = (relative ?? "").Replace('\\', '/').Trim().TrimStart('/');
        if (rel.Length == 0) throw new ArgumentException("A file name is required.");
        if (rel.Contains(':') || rel.Contains('\0'))
            throw new UnauthorizedAccessException("Invalid file name.");
        var parts = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(p => p is "." or ".."))
            throw new UnauthorizedAccessException("Invalid file name.");
        return string.Join('/', parts);
    }

    /// <summary>True when the instance is registered; otherwise returns a
    /// failure envelope for a 400 response.</summary>
    private static bool Check(string id, out ApiResponse<object> fail)
    {
        if (FileManagerRegistry.IsRegistered(id))
        {
            fail = null!;
            return true;
        }
        fail = ApiResponse<object>.Failure("Unknown file manager instance.");
        return false;
    }

    /// <summary>Boxes a value as <see cref="object"/> for a uniform command result.</summary>
    private static object Obj(object v) => v;

    /// <summary>A listing plus the permissions of that folder, so the browser
    /// can hide what it may not do (the server enforces them regardless).</summary>
    private static object ListWithAccess(IFileSystemService svc, string path, bool noCache)
    {
        var list = svc.List(path, "*", noCache);
        var access = svc.AccessAt(path);
        return new
        {
            entries = list.Entries,
            permissions = access.ToClientFlags(),
            quotaBytes = access.QuotaBytes,
        };
    }

    /// <summary>Throws a 403 when the feature switch is off, otherwise runs the action.</summary>
    private static object Guard(bool allowed, Func<object> action)
    {
        if (!allowed) throw new UnauthorizedAccessException("This feature is disabled.");
        return action();
    }

    /// <summary>Deletes every path in the request (or <paramref name="fallback"/>).</summary>
    private static void DeleteMany(IFileSystemService svc, List<string>? paths, string fallback)
    {
        foreach (var p in (paths is { Count: > 0 } ? paths : new List<string> { fallback }))
            svc.Delete(p);
    }

    /// <summary>Runs zip/extract inline, or in the background when the caller
    /// asked for progress reporting (a job id is returned instead of the result).</summary>
    private static object StartArchive(IFileSystemService svc, CommandRequest req, string path)
    {
        var isZip = req.Command == "zip";
        var paths = req.Paths ?? new List<string>();
        var destination = req.Destination ?? path;

        if (!req.Async)
        {
            if (isZip) return svc.CreateArchive(path, paths);
            svc.ExtractArchive(path, destination, req.Overwrite);
            return true;
        }

        var label = Path.GetFileName(path.TrimEnd('/'));
        var job = ArchiveJobService.Create(isZip ? "zip" : "extract", label);
        _ = Task.Run(() =>
        {
            job.State = "running";
            try
            {
                if (isZip) svc.CreateArchive(path, paths, job.Progress, enforcePermission: true);
                else svc.ExtractArchive(path, destination, req.Overwrite, job.Progress);
                job.State = "done";
            }
            catch (Exception ex)
            {
                job.Error = ex.Message;
                job.State = "failed";
            }
            finally
            {
                job.FinishedAt = DateTime.UtcNow;
            }
        });
        return new { jobId = job.Id };
    }

    /// <summary>Copies every requested item.</summary>
    private static void CopyMany(IFileSystemService svc, CommandRequest req, string fallback)
        => TransferMany(svc, req, fallback, move: false);

    /// <summary>Moves every requested item (also used by the "cut" alias).</summary>
    private static void MoveMany(IFileSystemService svc, CommandRequest req, string fallback)
        => TransferMany(svc, req, fallback, move: true);

    /// <summary>Copies or moves a batch. Unless the caller already decided to
    /// overwrite or rename, the whole batch is pre-checked so the client can ask
    /// the user before anything is written.</summary>
    private static void TransferMany(IFileSystemService svc, CommandRequest req, string fallback, bool move)
    {
        var dest = string.IsNullOrEmpty(req.Destination) ? fallback : req.Destination;
        var paths = req.Paths is { Count: > 0 } ? req.Paths : new List<string> { fallback };

        if (!req.Overwrite && !req.RenameOnConflict)
        {
            var conflicts = svc.FindConflicts(paths, dest);
            if (conflicts.Count > 0) throw new FileConflictException(conflicts);
        }

        foreach (var p in paths)
        {
            if (move) svc.Move(p, dest, req.Overwrite, req.RenameOnConflict);
            else svc.Copy(p, dest, req.Overwrite, req.RenameOnConflict);
        }
    }

    /// <summary>Maps a file extension to a MIME type for inline previews.</summary>
    private static string GuessContentType(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            ".pdf" => "application/pdf",
            ".txt" or ".log" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".md" => "text/markdown",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream",
        };
    }
}
