using FileManagerNet.Models;

namespace FileManagerNet.Services;

/// <summary>
/// One cached directory listing together with the timestamps used to collapse
/// concurrent callers and to expire the reuse window.
/// </summary>
/// <param name="Value">The listing.</param>
/// <param name="StartedAt">When the enumeration started.</param>
/// <param name="CompletedAt">When it finished.</param>
/// <param name="ExpiresAt">End of the optional reuse window.</param>
internal sealed record DirectoryListingSlot(ListResult Value, long StartedAt, long CompletedAt, long ExpiresAt);
