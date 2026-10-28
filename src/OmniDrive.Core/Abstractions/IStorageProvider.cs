using OmniDrive.Core.Models;

namespace OmniDrive.Core.Abstractions;

/// <summary>
/// Common contract implemented by each storage integration. Provider-specific SDK
/// types must remain inside the provider project.
/// </summary>
public interface IStorageProvider : IAsyncDisposable
{
    string ProviderId { get; }
    string DisplayName { get; }
    StorageCapabilities AccountCapabilities { get; }

    Task<IReadOnlyList<StorageAccount>> GetAccountsAsync(
        CancellationToken cancellationToken = default);

    Task<StoragePage> ListAsync(
        StorageLocation location,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorageItem>> SearchAsync(
        string accountId,
        string query,
        CancellationToken cancellationToken = default);

    Task DownloadAsync(
        StorageItem item,
        Stream destination,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task UploadAsync(
        StorageLocation destination,
        string fileName,
        Stream source,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
