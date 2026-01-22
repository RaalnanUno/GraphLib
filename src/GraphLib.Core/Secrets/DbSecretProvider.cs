namespace GraphLib.Core.Secrets;

/// <summary>
/// Default ISecretProvider implementation (v1).
/// Returns secrets as-is from the database (no decryption).
/// This is a seam point: can be replaced with encryption/vault providers in the future.
/// </summary>
public sealed class DbSecretProvider : ISecretProvider
{
    /// <summary>
    /// Returns the raw value from database unchanged.
    /// In a future implementation, this could decrypt the value or fetch from Key Vault.
    /// </summary>
    public string GetSecret(string key, string rawValueFromDb) => rawValueFromDb;
}
