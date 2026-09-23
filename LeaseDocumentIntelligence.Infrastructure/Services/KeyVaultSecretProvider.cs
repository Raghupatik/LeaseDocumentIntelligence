using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LeaseDocumentIntelligence.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeaseDocumentIntelligence.Infrastructure.Services;

/// <summary>
/// Configuration options for Key Vault integration
/// </summary>
public class KeyVaultOptions
{
    public const string SectionName = "KeyVault";
    public string VaultUri { get; set; } = string.Empty;
    public int CacheDurationMinutes { get; set; } = 60;
}

/// <summary>
/// Provides secrets from Azure Key Vault with in-memory caching
/// </summary>
public class KeyVaultSecretProvider : ISecretProvider, IDisposable
{
    private readonly SecretClient _secretClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KeyVaultSecretProvider> _logger;
    private readonly TimeSpan _cacheDuration;
    private bool _disposed;

    public KeyVaultSecretProvider(
        IOptions<KeyVaultOptions> options,
        IMemoryCache cache,
        ILogger<KeyVaultSecretProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options?.Value?.VaultUri);

        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheDuration = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes > 0
            ? options.Value.CacheDurationMinutes
            : 60);

        var credential = new DefaultAzureCredential();
        _secretClient = new SecretClient(new Uri(options.Value.VaultUri), credential);
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        var cacheKey = $"kv-secret-{secretName}";

        if (_cache.TryGetValue(cacheKey, out string? cachedValue) && !string.IsNullOrEmpty(cachedValue))
        {
            return cachedValue;
        }

        try
        {
            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            var secretValue = response.Value.Value;

            _cache.Set(cacheKey, secretValue, _cacheDuration);
            return secretValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretName} from Key Vault", secretName);
            throw;
        }
    }

    public async Task<IDictionary<string, string>> GetSecretsAsync(
        IEnumerable<string> secretNames,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string>();

        foreach (var secretName in secretNames)
        {
            try
            {
                var value = await GetSecretAsync(secretName, cancellationToken);
                results[secretName] = value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve secret {SecretName}", secretName);
            }
        }

        return results;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
