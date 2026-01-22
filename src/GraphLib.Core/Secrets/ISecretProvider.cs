namespace GraphLib.Core.Secrets;

/// <summary>
/// Abstraction layer for secret retrieval and resolution.
/// Allows pluggable implementations: plaintext DB, decryption, Azure Key Vault, etc.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Resolves a secret by key.
    /// The raw value from the database is passed; implementation decides how to handle it.
    /// </summary>
    /// <param name="key">Secret name/identifier (e.g., "ClientSecret")</param>
    /// <param name="rawValueFromDb">The raw value stored in the database</param>
    /// <returns>The resolved/decrypted secret value</returns>
    string GetSecret(string key, string rawValueFromDb);
}
