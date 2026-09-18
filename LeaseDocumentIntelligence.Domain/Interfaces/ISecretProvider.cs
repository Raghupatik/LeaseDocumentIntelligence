namespace LeaseDocumentIntelligence.Domain.Interfaces;

/// <summary>
/// Abstraction for retrieving secrets from a secure store (Key Vault, etc.)
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Retrieves a secret value by name
    /// </summary>
    /// <param name="secretName">The name of the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The secret value</returns>
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves multiple secrets by names
    /// </summary>
    /// <param name="secretNames">The names of the secrets</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of secret name to value</returns>
    Task<IDictionary<string, string>> GetSecretsAsync(
        IEnumerable<string> secretNames,
        CancellationToken cancellationToken = default);
}
