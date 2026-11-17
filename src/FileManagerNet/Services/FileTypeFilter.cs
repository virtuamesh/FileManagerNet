using System.IO.Enumeration;

namespace FileManagerNet.Services;

/// <summary>
/// Matches file names against <c>*.jpg|*.png</c> style patterns. Simple
/// extension patterns are kept in a hash set so filtering a large listing stays
/// a lookup per file; anything richer falls back to a glob match.
/// </summary>
public sealed class FileTypeFilter
{
    /// <summary>A filter that matches nothing because it has no patterns.</summary>
    public static readonly FileTypeFilter Empty = new(Array.Empty<string>());

    private readonly HashSet<string> _extensions;
    private readonly string[] _globs;

    /// <summary>Creates a filter from patterns such as "*.jpg" or "photo-*.png".</summary>
    /// <param name="patterns">Patterns to match; empty entries are ignored.</param>
    public FileTypeFilter(IEnumerable<string> patterns)
    {
        _extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var globs = new List<string>();
        foreach (var raw in patterns)
        {
            var p = raw.Trim();
            if (p.Length == 0) continue;

            /* the common case is "*.jpg": keep it in a set so filtering a 10k
             * entry listing stays a hash lookup per file */
            if (p.StartsWith("*.", StringComparison.Ordinal) && p.IndexOfAny(new[] { '*', '?' }, 2) < 0)
                _extensions.Add(p[1..]);            // ".jpg"
            else
                globs.Add(p);
        }
        _globs = globs.ToArray();
    }

    /// <summary>True when the filter has no patterns and therefore matches nothing.</summary>
    public bool IsEmpty => _extensions.Count == 0 && _globs.Length == 0;

    /// <summary>True when <paramref name="fileName"/> matches one of the patterns.</summary>
    /// <param name="fileName">File name to test, without any directory part.</param>
    public bool Matches(string fileName)
    {
        if (_extensions.Count > 0)
        {
            var dot = fileName.LastIndexOf('.');
            if (dot >= 0 && _extensions.Contains(fileName[dot..])) return true;
        }
        foreach (var g in _globs)
            if (FileSystemName.MatchesSimpleExpression(g, fileName, ignoreCase: true)) return true;
        return false;
    }

    /// <summary>Parses a <c>|</c>, <c>;</c> or <c>,</c> separated pattern list.</summary>
    /// <param name="text">Pattern text; blank input yields <see cref="Empty"/>.</param>
    internal static FileTypeFilter Parse(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? Empty
            : new FileTypeFilter(text.Split(new[] { '|', ';', ',' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
