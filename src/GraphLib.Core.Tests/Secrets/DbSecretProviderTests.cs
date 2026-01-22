using Xunit;
using FluentAssertions;
using GraphLib.Core.Secrets;

namespace GraphLib.Core.Tests.Secrets;

/// <summary>
/// Tests for secret provider implementations.
/// Ensures secrets are correctly resolved and returned.
/// </summary>
public class DbSecretProviderTests
{
    [Fact]
    public void GetSecret_returns_value_unchanged()
    {
        // Arrange
        var provider = new DbSecretProvider();
        var secret = "my-secret-value";

        // Act
        var result = provider.GetSecret("ClientSecret", secret);

        // Assert
        result.Should().Be(secret);
    }

    [Fact]
    public void GetSecret_preserves_special_characters()
    {
        // Arrange
        var provider = new DbSecretProvider();
        var secret = "p@ssw0rd!#$%^&*()";

        // Act
        var result = provider.GetSecret("ClientSecret", secret);

        // Assert
        result.Should().Be(secret);
    }

    [Fact]
    public void GetSecret_handles_empty_string()
    {
        // Arrange
        var provider = new DbSecretProvider();

        // Act
        var result = provider.GetSecret("key", string.Empty);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void GetSecret_handles_null_value()
    {
        // Arrange
        var provider = new DbSecretProvider();

        // Act
        var result = provider.GetSecret("key", null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSecret_works_with_different_keys()
    {
        // Arrange
        var provider = new DbSecretProvider();

        // Act
        var clientSecret = provider.GetSecret("ClientSecret", "secret1");
        var apiKey = provider.GetSecret("ApiKey", "secret2");

        // Assert
        clientSecret.Should().Be("secret1");
        apiKey.Should().Be("secret2");
    }

    [Fact]
    public void DbSecretProvider_implements_ISecretProvider()
    {
        // Arrange & Act
        var provider = new DbSecretProvider();

        // Assert
        provider.Should().BeAssignableTo<ISecretProvider>();
    }
}
