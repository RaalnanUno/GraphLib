namespace GraphLib.Core.Secrets;

public interface ISecretProvider
{
    string GetSecret(string key, string rawValueFromDb);
}
