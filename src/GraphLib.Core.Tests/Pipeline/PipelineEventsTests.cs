using Xunit;
using FluentAssertions;
using System.Text.Json;
using GraphLib.Core.Pipeline;

namespace GraphLib.Core.Tests.Pipeline;

/// <summary>
/// Tests for pipeline event serialization.
/// Ensures JSON payloads are correctly serialized for storage.
/// </summary>
public class PipelineEventsTests
{
    [Fact]
    public void BuildPayloadJson_serializes_simple_object()
    {
        // Arrange
        var payload = new { message = "test", value = 123 };

        // Act
        var result = PipelineEvents.BuildPayloadJson(payload);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("message");
        result.Should().Contain("test");
        result.Should().Contain("value");
        result.Should().Contain("123");
    }

    [Fact]
    public void BuildPayloadJson_produces_valid_json()
    {
        // Arrange
        var payload = new { stage = "upload", success = true, itemId = "abc123" };

        // Act
        var result = PipelineEvents.BuildPayloadJson(payload);

        // Assert
        // Should not throw when parsing
        Action parse = () => JsonDocument.Parse(result);
        parse.Should().NotThrow();
    }

    [Fact]
    public void BuildPayloadJson_uses_compact_format_no_indentation()
    {
        // Arrange
        var payload = new { nested = new { value = "test" } };

        // Act
        var result = PipelineEvents.BuildPayloadJson(payload);

        // Assert
        result.Should().NotContain("\n");
        result.Should().NotContain("\r");
        // Should be compact single line
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildPayloadJson_handles_null_values()
    {
        // Arrange
        var payload = new { message = (string?)null, value = 123 };

        // Act
        var result = PipelineEvents.BuildPayloadJson(payload);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("null");
    }

    [Fact]
    public void BuildPayloadJson_handles_complex_nested_objects()
    {
        // Arrange
        var payload = new
        {
            runId = "run-123",
            stage = "convert",
            success = true,
            graph = new { statusCode = 200, requestId = "req-456" },
            file = new { name = "document.docx", sizeBytes = 50000 }
        };

        // Act
        var result = PipelineEvents.BuildPayloadJson(payload);

        // Assert
        result.Should().Contain("run-123");
        result.Should().Contain("convert");
        result.Should().Contain("200");
        result.Should().Contain("document.docx");
        
        // Verify it's valid JSON
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("runId").GetString().Should().Be("run-123");
    }

    [Fact]
    public void BuildPayloadJson_handles_arrays()
    {
        // Arrange
        var payload = new { items = new[] { "a", "b", "c" } };

        // Act
        var result = PipelineEvents.BuildPayloadJson(payload);

        // Assert
        result.Should().Contain("a");
        result.Should().Contain("b");
        result.Should().Contain("c");
    }

    [Fact]
    public void BuildPayloadJson_handles_datetime()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var payload = new { timestamp = now };

        // Act
        var result = PipelineEvents.BuildPayloadJson(payload);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("timestamp");
    }

    [Fact]
    public void BuildPayloadJson_preserves_booleans()
    {
        // Arrange
        var payload = new { success = true, failed = false };

        // Act
        var result = PipelineEvents.BuildPayloadJson(payload);

        // Assert
        result.Should().Contain("true");
        result.Should().Contain("false");
    }
}
