namespace OmniDrive.Core.Models;

public sealed record StorageAccount(
    string Id,
    string ProviderId,
    string DisplayName,
    bool IsConnected);
