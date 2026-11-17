using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FileManagerNet.Services;

/// <summary>
/// Temp storage for chunked and resumable uploads.
/// <para>
/// The browser asks for a session (init), sends the file in chunks, and asks
/// the server to move the assembled file into place (complete). A session id is
/// a hash of instance + target + size, so re-selecting the same file after a
/// network drop resumes where it stopped instead of starting over.
/// </para>
/// <para>
/// Chunks never go through multipart parsing, so the request size limit that
/// would break a multi-gigabyte file is irrelevant: each request is one chunk.
/// </para>
/// </summary>
public static class UploadSessionService
{
    /// <summary>Chunk size suggested to the client, in bytes.</summary>
    public const int DefaultChunkSize = 8 * 1024 * 1024;

    private static string RootDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "fmnet-uploads");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string PartPath(string id) => Path.Combine(RootDir, id + ".part");
    private static string MetaPath(string id) => Path.Combine(RootDir, id + ".json");

    /// <summary>True when <paramref name="id"/> is a well-formed session id.
    /// Ids are hex digests, so they are safe as file names and cannot escape
    /// the temp folder.</summary>
    /// <param name="id">Session id from the request.</param>
    public static bool IsValidId(string? id) =>
        id is { Length: 40 } && id.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

    /// <summary>Creates (or re-opens) a session and reports how many bytes the
    /// server already has for it.</summary>
    /// <param name="scope">Instance id that scopes the session.</param>
    /// <param name="targetPhysical">Full physical path the file will be moved to.</param>
    /// <param name="size">Expected total size in bytes.</param>
    /// <param name="name">Original file name.</param>
    /// <returns>The session id and the number of bytes already received.</returns>
    public static (string Id, long Received) Begin(string scope, string targetPhysical, long size, string name)
    {
        Cleanup();

        var id = Hash(scope + "|" + targetPhysical + "|" + size);
        var meta = MetaPath(id);
        if (!File.Exists(meta))
        {
            File.WriteAllText(meta, JsonSerializer.Serialize(new UploadSessionMetadata
            {
                Target = targetPhysical, Size = size, Name = name
            }));
        }

        long received = 0;
        var part = PartPath(id);
        if (File.Exists(part)) received = new FileInfo(part).Length;

        // a stale or oversized part would never finish
        if (received > size) { received = 0; TryDelete(part); }
        return (id, received);
    }

    /// <summary>Appends one chunk at <paramref name="offset"/>. The offset must
    /// match the current length, so a retried chunk can never corrupt the file.</summary>
    /// <param name="id">Session id.</param>
    /// <param name="offset">Byte offset the chunk starts at.</param>
    /// <param name="body">Chunk content.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>The new number of bytes received.</returns>
    /// <exception cref="UploadOffsetMismatchException">The offset is behind or
    /// ahead of the server's current length.</exception>
    public static async Task<long> AppendAsync(string id, long offset, Stream body, CancellationToken ct = default)
    {
        var path = PartPath(id);
        await using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write,
            FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (offset != fs.Length) throw new UploadOffsetMismatchException(fs.Length);
        fs.Seek(offset, SeekOrigin.Begin);
        await body.CopyToAsync(fs, 1024 * 1024, ct);
        await fs.FlushAsync(ct);
        return fs.Length;
    }

    /// <summary>Returns the target recorded for a session, so the caller can
    /// check permissions <em>before</em> the assembled file is moved into place.</summary>
    /// <param name="id">Session id.</param>
    /// <returns>Target path, name and size; <c>null</c> when the session is unknown.</returns>
    public static (string Target, string Name, long Size)? Peek(string id)
    {
        var meta = ReadMeta(id);
        return meta is null ? null : (meta.Target, meta.Name, meta.Size);
    }

    /// <summary>Moves the assembled file to its target, creating parent folders
    /// (folder uploads keep their hierarchy).</summary>
    /// <param name="id">Session id.</param>
    /// <param name="overwrite">Replaces an existing file.</param>
    /// <param name="renameOnConflict">Keeps both files by generating "name (1).ext".</param>
    /// <returns>Full physical path of the stored file.</returns>
    public static string Finish(string id, bool overwrite, bool renameOnConflict)
    {
        var meta = ReadMeta(id) ?? throw new FileNotFoundException("Upload session not found or expired.");
        var part = PartPath(id);
        if (!File.Exists(part)) throw new FileNotFoundException("Uploaded data not found.");

        var length = new FileInfo(part).Length;
        if (meta.Size >= 0 && length != meta.Size)
            throw new IOException($"Upload incomplete ({length} of {meta.Size} bytes).");

        var target = meta.Target;
        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        if (File.Exists(target) || Directory.Exists(target))
        {
            if (Directory.Exists(target))
                throw new IOException($"'{Path.GetFileName(target)}' is a folder.");
            if (renameOnConflict) target = UniquePath(target);
            else if (!overwrite)
                throw new UploadConflictException(Path.GetFileName(target));
        }

        MoveFile(part, target);
        TryDelete(MetaPath(id));
        return target;
    }

    /// <summary>Aborts a session and removes the partial file and its metadata.</summary>
    /// <param name="id">Session id.</param>
    public static void Discard(string id)
    {
        TryDelete(PartPath(id));
        TryDelete(MetaPath(id));
    }

    /// <summary>Computes the SHA-1 based session id for a scope and target.</summary>
    private static string Hash(string value) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>Reads the session metadata, or <c>null</c> when it is missing or broken.</summary>
    private static UploadSessionMetadata? ReadMeta(string id)
    {
        try
        {
            var json = File.ReadAllText(MetaPath(id));
            return JsonSerializer.Deserialize<UploadSessionMetadata>(json);
        }
        catch { return null; }
    }

    /// <summary>Finds the first free "name (1).ext" style path.</summary>
    private static string UniquePath(string target)
    {
        var dir = Path.GetDirectoryName(target)!;
        var stem = Path.GetFileNameWithoutExtension(target);
        var ext = Path.GetExtension(target);
        for (var i = 1; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        throw new IOException("Could not find a free name for the upload.");
    }

    /// <summary>Moves a file, falling back to copy + delete across volumes.</summary>
    private static void MoveFile(string from, string to)
    {
        var sameVolume = string.Equals(Path.GetPathRoot(from), Path.GetPathRoot(to),
            StringComparison.OrdinalIgnoreCase);
        if (sameVolume)
        {
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
            return;
        }
        File.Copy(from, to, true);
        TryDelete(from);
    }

    /// <summary>Deletes a file when it exists; never throws.</summary>
    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    /// <summary>Removes sessions abandoned more than 12 hours ago.</summary>
    private static void Cleanup()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-12);
            foreach (var f in Directory.EnumerateFiles(RootDir))
            {
                var info = new FileInfo(f);
                if (info.LastWriteTimeUtc >= cutoff) continue;
                var id = info.Name.Split('.')[0];
                if (IsValidId(id)) Discard(id);
            }
        }
        catch { /* cleanup must never fail the request */ }
    }
}
