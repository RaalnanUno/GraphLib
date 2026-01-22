namespace GraphLib.Core.Secrets;

/// <summary>
/// Default v1 provider: all settings live in SQLite, including clientSecret.
/// This seam exists so you can later plug in KeyFolio, env overrides, etc.
/// </summary>
public sealed class DbSecretProvider : ISecretProvider
{
    public string GetSecret(string key, string rawValueFromDb) => rawValueFromDb;
}
