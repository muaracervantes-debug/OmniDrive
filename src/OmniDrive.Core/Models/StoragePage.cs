namespace OmniDrive.Core.Models;

public sealed record StoragePage(
    IReadOnlyList<StorageItem> Items,
    string? ContinuationToken = null);
