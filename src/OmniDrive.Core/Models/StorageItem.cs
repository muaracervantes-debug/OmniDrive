namespace OmniDrive.Core.Models;

/// <summary>A provider-independent item exposed by a storage service.</summary>
public sealed record StorageItem(
    string Id,
    string Name,
    StorageItemKind Kind,
    string ProviderId,
    string AccountId,
    StorageCapabilities Capabilities,
    long? SizeBytes = null,
    DateTimeOffset? ModifiedAt = null,
    string? ContentType = null,
    string? ParentId = null);
