using OmniDrive.Core.Abstractions;

namespace OmniDrive.Application;

/// <summary>Holds the providers registered by the application composition root.</summary>
public sealed class ProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IStorageProvider> _providers;

    public ProviderRegistry(IEnumerable<IStorageProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var map = new Dictionary<string, IStorageProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (string.IsNullOrWhiteSpace(provider.ProviderId))
                throw new ArgumentException("Provider IDs cannot be empty.", nameof(providers));
            if (!map.TryAdd(provider.ProviderId, provider))
                throw new ArgumentException($"Duplicate provider ID: '{provider.ProviderId}'.", nameof(providers));
        }

        _providers = map;
    }

    public IReadOnlyCollection<IStorageProvider> Providers => _providers.Values.ToArray();

    public bool TryGet(string providerId, out IStorageProvider? provider) =>
        _providers.TryGetValue(providerId, out provider);
}
