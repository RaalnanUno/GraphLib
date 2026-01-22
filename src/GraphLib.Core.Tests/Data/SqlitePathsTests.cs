using Xunit;
using FluentAssertions;
using GraphLib.Core.Data;

namespace GraphLib.Core.Tests.Data;

/// <summary>
/// Tests for database path resolution logic.
/// Ensures paths are correctly normalized and directories are created.
/// </summary>
public class SqlitePathsTests
{
    [Fact]
    public void ResolveDbPath_uses_default_when_null()
    {
        // Act
        var result = SqlitePaths.ResolveDbPath(null);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("GraphLib.db");
    }

    [Fact]
    public void ResolveDbPath_uses_default_when_empty()
    {
        // Act
        var result = SqlitePaths.ResolveDbPath(string.Empty);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("GraphLib.db");
    }

    [Fact]
    public void ResolveDbPath_uses_default_when_whitespace()
    {
        // Act
        var result = SqlitePaths.ResolveDbPath("   ");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("GraphLib.db");
    }

    [Fact]
    public void ResolveDbPath_converts_relative_path_to_absolute()
    {
        // Arrange
        var relativePath = "./Data/test.db";

        // Act
        var result = SqlitePaths.ResolveDbPath(relativePath);

        // Assert
        result.Should().NotBeNullOrEmpty();
        Path.IsPathRooted(result).Should().BeTrue();
        result.Should().EndWith("test.db");
    }

    [Fact]
    public void ResolveDbPath_preserves_absolute_path()
    {
        // Arrange
        var absolutePath = "C:\\Data\\GraphLib.db";

        // Act
        var result = SqlitePaths.ResolveDbPath(absolutePath);

        // Assert
        result.Should().Be(absolutePath);
    }

    [Fact]
    public void ResolveDbPath_trims_whitespace()
    {
        // Arrange
        var pathWithWhitespace = "  ./Data/test.db  ";

        // Act
        var result = SqlitePaths.ResolveDbPath(pathWithWhitespace);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("test.db");
    }

    [Fact]
    public void ResolveDbPath_creates_directory_if_not_exists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var dbPath = Path.Combine(tempDir, "subdir", "test.db");

        try
        {
            // Act
            var result = SqlitePaths.ResolveDbPath(dbPath);

            // Assert
            var directoryPath = Path.GetDirectoryName(result);
            Directory.Exists(directoryPath).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveDbPath_handles_custom_default()
    {
        // Arrange
        var customDefault = "./CustomDb/app.sqlite";

        // Act
        var result = SqlitePaths.ResolveDbPath(null, customDefault);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith("app.sqlite");
    }

    [Fact]
    public void ResolveDbPath_prefers_provided_path_over_default()
    {
        // Arrange
        var providedPath = "./MyDb/custom.db";
        var defaultPath = "./DefaultDb/default.db";

        // Act
        var result = SqlitePaths.ResolveDbPath(providedPath, defaultPath);

        // Assert
        result.Should().EndWith("custom.db");
        result.Should().NotContain("DefaultDb");
    }
}
