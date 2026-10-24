using OmniDrive.Application;
using OmniDrive.Core.Abstractions;
using OmniDrive.Core.Models;

namespace OmniDrive.Application.Tests;

public sealed class ProviderRegistryTests
{
    [Fact]
    public void Registry_ResolvesProviderWithoutKnowingItsConcreteType()
    {
        var provider = new FakeProvider();
        var registry = new ProviderRegistry([provider]);

        Assert.True(registry.TryGet("fake", out var resolved));
        Assert.Same(provider, resolved);
    }

    [Fact]
    public void Registry_RejectsDuplicateProviderIds()
    {
        Assert.Throws<ArgumentException>(() =>
            new ProviderRegistry([new FakeProvider(), new FakeProvider()]));
    }

    private sealed class FakeProvider : IStorageProvider
    {
        public string ProviderId => "fake";
        public string DisplayName => "Fake";
        public StorageCapabilities AccountCapabilities => StorageCapabilities.List;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<IReadOnlyList<StorageAccount>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StorageAccount>>([]);

        public Task<StoragePage> ListAsync(StorageLocation location, string? continuationToken = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoragePage([]));

        public Task<IReadOnlyList<StorageItem>> SearchAsync(string accountId, string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StorageItem>>([]);

        public Task DownloadAsync(StorageItem item, Stream destination,
            IProgress<TransferProgress>? progress = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UploadAsync(StorageLocation destination, string fileName, Stream source,
            IProgress<TransferProgress>? progress = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
