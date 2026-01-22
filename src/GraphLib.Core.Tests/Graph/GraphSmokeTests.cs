using Xunit;
using FluentAssertions;
using GraphLib.Core.Models;

namespace GraphLib.Core.Tests.Graph;

/// <summary>
/// Integration tests for Graph service layer.
/// These are smoke tests to verify basic Graph API interactions work correctly.
/// Note: These tests do NOT require actual SharePoint/Azure AD connections.
/// </summary>
public class GraphSmokeTests
{
    [Fact]
    public void ConflictBehavior_Parse_recognizes_all_valid_values()
    {
        // Arrange & Act
        var fail = ConflictBehaviorExtensions.Parse("fail");
        var replace = ConflictBehaviorExtensions.Parse("replace");
        var rename = ConflictBehaviorExtensions.Parse("rename");

        // Assert
        fail.Should().Be(ConflictBehavior.Fail);
        replace.Should().Be(ConflictBehavior.Replace);
        rename.Should().Be(ConflictBehavior.Rename);
    }

    [Fact]
    public void ConflictBehavior_Parse_is_case_insensitive()
    {
        // Arrange & Act
        var fail = ConflictBehaviorExtensions.Parse("FAIL");
        var replace = ConflictBehaviorExtensions.Parse("Replace");
        var rename = ConflictBehaviorExtensions.Parse("rEnAmE");

        // Assert
        fail.Should().Be(ConflictBehavior.Fail);
        replace.Should().Be(ConflictBehavior.Replace);
        rename.Should().Be(ConflictBehavior.Rename);
    }

    [Fact]
    public void ConflictBehavior_Parse_uses_default_for_invalid_value()
    {
        // Arrange & Act
        var result = ConflictBehaviorExtensions.Parse("invalid", ConflictBehavior.Fail);

        // Assert
        result.Should().Be(ConflictBehavior.Fail);
    }

    [Fact]
    public void ConflictBehavior_ToGraphValue_produces_correct_api_strings()
    {
        // Arrange & Act
        var fail = ConflictBehavior.Fail.ToGraphValue();
        var replace = ConflictBehavior.Replace.ToGraphValue();
        var rename = ConflictBehavior.Rename.ToGraphValue();

        // Assert
        fail.Should().Be("fail");
        replace.Should().Be("replace");
        rename.Should().Be("rename");
    }

    [Fact]
    public void GraphLibSettings_DefaultForInit_provides_sensible_defaults()
    {
        // Arrange & Act
        var defaults = GraphLibSettings.DefaultForInit();

        // Assert
        defaults.Should().NotBeNull();
        defaults.SiteUrl.Should().Contain("sharepoint.com");
        defaults.LibraryName.Should().Be("Shared Documents");
        defaults.TempFolder.Should().Be("GraphLibTemp");
        defaults.PdfFolder.Should().Be("GraphLibPdf");
        defaults.CleanupTemp.Should().BeTrue();
        defaults.ConflictBehavior.Should().Be(ConflictBehavior.Replace);
        defaults.StorePdfInSharePoint.Should().BeTrue();
        defaults.ProcessFolderMode.Should().BeFalse();
        defaults.IgnoreFailuresWhenFolderMode.Should().BeTrue();
        defaults.TenantId.Should().Be("00000000-0000-0000-0000-000000000000");
        defaults.ClientId.Should().Be("00000000-0000-0000-0000-000000000000");
        defaults.ClientSecret.Should().Be("REPLACE_ME");
    }

    [Fact]
    public void GraphLibSettings_record_is_immutable()
    {
        // Arrange
        var original = GraphLibSettings.DefaultForInit();

        // Act - Create modified copy using "with" expression
        var modified = original with { SiteUrl = "https://newsiteurl.sharepoint.com/sites/NewSite" };

        // Assert - Original unchanged, modified is new instance
        original.SiteUrl.Should().Contain("SiteName");
        modified.SiteUrl.Should().Be("https://newsiteurl.sharepoint.com/sites/NewSite");
        original.Should().NotBeSameAs(modified);
    }

    [Fact]
    public void GraphStage_constants_provide_all_pipeline_stages()
    {
        // Arrange & Act & Assert
        GraphStage.ResolveSite.Should().Be("resolveSite");
        GraphStage.ResolveDrive.Should().Be("resolveDrive");
        GraphStage.EnsureFolder.Should().Be("ensureFolder");
        GraphStage.Upload.Should().Be("upload");
        GraphStage.Convert.Should().Be("convert");
        GraphStage.StorePdf.Should().Be("storePdf");
        GraphStage.Cleanup.Should().Be("cleanup");
    }

    [Fact]
    public void LogLevel_constants_match_expected_values()
    {
        // Arrange & Act & Assert
        LogLevel.Info.Should().Be("Info");
        LogLevel.Warn.Should().Be("Warn");
        LogLevel.Error.Should().Be("Error");
    }

    [Fact]
    public void GraphLibRunResult_can_be_created_with_success()
    {
        // Arrange & Act
        var result = new GraphLibRunResult
        {
            RunId = "test-run-123",
            Success = true,
            Summary = "OK file='document.pdf' pdfBytes=50000",
            InputBytes = 100000,
            PdfBytes = 50000,
            Elapsed = TimeSpan.FromSeconds(5)
        };

        // Assert
        result.RunId.Should().Be("test-run-123");
        result.Success.Should().BeTrue();
        result.Summary.Should().Contain("OK");
        result.InputBytes.Should().Be(100000);
        result.PdfBytes.Should().Be(50000);
        result.Elapsed.TotalSeconds.Should().Be(5);
    }

    [Fact]
    public void GraphLibRunResult_can_be_created_with_failure()
    {
        // Arrange & Act
        var result = new GraphLibRunResult
        {
            RunId = "test-run-456",
            Success = false,
            Summary = "FAIL file='document.docx' (HttpRequestException)",
            InputBytes = 200000,
            PdfBytes = 0,
            Elapsed = TimeSpan.FromSeconds(2)
        };

        // Assert
        result.Success.Should().BeFalse();
        result.Summary.Should().Contain("FAIL");
        result.PdfBytes.Should().Be(0);
    }
}
