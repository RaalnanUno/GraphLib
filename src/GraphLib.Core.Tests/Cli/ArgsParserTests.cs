using Xunit;
using FluentAssertions;
using GraphLib.Core.Models;
using GraphLib.ConsoleApp.Cli;

namespace GraphLib.Core.Tests.Cli;

/// <summary>
/// Tests for command-line argument parsing.
/// Verifies that CLI arguments are correctly parsed and default values are applied.
/// </summary>
public class ArgsParserTests
{
    [Fact]
    public void Parse_empty_argv_defaults_to_run_command()
    {
        // Arrange
        var argv = Array.Empty<string>();

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.Command.Should().Be("run");
    }

    [Fact]
    public void Parse_recognizes_init_command()
    {
        // Arrange
        var argv = new[] { "init", "--db", "./Data/GraphLib.db" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.Command.Should().Be("init");
        result.Db.Should().Be("./Data/GraphLib.db");
    }

    [Fact]
    public void Parse_recognizes_help_command()
    {
        // Arrange
        var argv = new[] { "help" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.Command.Should().Be("help");
    }

    [Fact]
    public void Parse_extracts_file_argument()
    {
        // Arrange
        var argv = new[] { "run", "--file", "C:\\path\\to\\document.docx" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.File.Should().Be("C:\\path\\to\\document.docx");
    }

    [Fact]
    public void Parse_extracts_multiple_settings()
    {
        // Arrange
        var argv = new[]
        {
            "run",
            "--file", "document.docx",
            "--siteUrl", "https://tenant.sharepoint.com/sites/MySite",
            "--libraryName", "Documents",
            "--tempFolder", "Temp"
        };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.File.Should().Be("document.docx");
        result.SiteUrl.Should().Be("https://tenant.sharepoint.com/sites/MySite");
        result.LibraryName.Should().Be("Documents");
        result.TempFolder.Should().Be("Temp");
    }

    [Fact]
    public void Parse_handles_boolean_flags()
    {
        // Arrange
        var argv = new[] { "run", "--cleanupTemp", "true", "--logFailuresOnly", "false" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.CleanupTemp.Should().Be(true);
        result.LogFailuresOnly.Should().Be(false);
    }

    [Fact]
    public void Parse_treats_flag_without_value_as_true()
    {
        // Arrange
        var argv = new[] { "run", "--cleanupTemp" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.CleanupTemp.Should().Be(true);
    }

    [Fact]
    public void Parse_is_case_insensitive_for_command()
    {
        // Arrange
        var argv = new[] { "INIT", "--db", "test.db" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.Command.Should().Be("init");
    }

    [Fact]
    public void Parse_is_case_insensitive_for_options()
    {
        // Arrange
        var argv = new[] { "run", "--FILE", "document.docx" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.File.Should().Be("document.docx");
    }

    [Fact]
    public void Parse_extracts_auth_arguments()
    {
        // Arrange
        var argv = new[]
        {
            "run",
            "--tenantId", "12345678-1234-1234-1234-123456789012",
            "--clientId", "87654321-4321-4321-4321-210987654321",
            "--clientSecret", "my-secret-value"
        };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.TenantId.Should().Be("12345678-1234-1234-1234-123456789012");
        result.ClientId.Should().Be("87654321-4321-4321-4321-210987654321");
        result.ClientSecret.Should().Be("my-secret-value");
    }

    [Fact]
    public void Parse_skips_unrecognized_arguments()
    {
        // Arrange
        var argv = new[] { "run", "--file", "doc.docx", "--unknown-arg", "value" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.File.Should().Be("doc.docx");
        // Unknown argument should be silently ignored
    }

    [Fact]
    public void Parse_conflictBehavior_option_is_captured()
    {
        // Arrange
        var argv = new[] { "run", "--conflictBehavior", "replace" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.ConflictBehavior.Should().Be("replace");
    }

    [Fact]
    public void Parse_runId_option_is_captured()
    {
        // Arrange
        var argv = new[] { "run", "--runId", "my-custom-run-id" };

        // Act
        var result = ArgsParser.Parse(argv);

        // Assert
        result.RunId.Should().Be("my-custom-run-id");
    }
}
