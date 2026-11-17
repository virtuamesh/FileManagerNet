namespace FileManagerNet.Services;

/// <summary>
/// Cached folder size together with the time it was measured.
/// </summary>
/// <param name="Bytes">Total bytes below the folder.</param>
/// <param name="MeasuredAt">Tick count when the size was measured.</param>
internal sealed record DirectoryUsageSlot(long Bytes, long MeasuredAt);
