namespace OmniDrive.Core.Models;

/// <summary>A provider-specific location represented without leaking provider SDK types.</summary>
public sealed record StorageLocation(
    string ProviderId,
    string AccountId,
    string? ContainerId = null,
    string? ParentId = null,
    string? DisplayName = null);
