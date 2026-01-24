using Xunit;
using FluentAssertions;
using GraphLib.Core.Models;

namespace GraphLib.Core.Tests.Models;

/// <summary>
/// Tests for conflict behavior enum and parsing logic.
/// Ensures conflict resolution strategies work as expected.
/// </summary>
public class ConflictBehaviorTests
{
    [Theory]
    [InlineData("fail", ConflictBehavior.Fail)]
    [InlineData("replace", ConflictBehavior.Replace)]
    [InlineData("rename", ConflictBehavior.Rename)]
    [InlineData("FAIL", ConflictBehavior.Fail)]
    [InlineData("Replace", ConflictBehavior.Replace)]
    [InlineData("RENAME", ConflictBehavior.Rename)]
    public void Parse_recognizes_all_behavior_values(string input, ConflictBehavior expected)
    {
        // Act
        var result = ConflictBehaviorExtensions.Parse(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Parse_null_input_returns_default()
    {
        // Act
        var result = ConflictBehaviorExtensions.Parse(null);

        // Assert
        result.Should().Be(ConflictBehavior.Replace);
    }

    [Fact]
    public void Parse_empty_string_returns_default()
    {
        // Act
        var result = ConflictBehaviorExtensions.Parse(string.Empty);

        // Assert
        result.Should().Be(ConflictBehavior.Replace);
    }

    [Fact]
    public void Parse_whitespace_returns_default()
    {
        // Act
        var result = ConflictBehaviorExtensions.Parse("   ");

        // Assert
        result.Should().Be(ConflictBehavior.Replace);
    }

    [Fact]
    public void Parse_invalid_value_returns_default()
    {
        // Act
        var result = ConflictBehaviorExtensions.Parse("invalid-behavior");

        // Assert
        result.Should().Be(ConflictBehavior.Replace);
    }

    [Fact]
    public void Parse_with_custom_default()
    {
        // Act
        var result = ConflictBehaviorExtensions.Parse("invalid", ConflictBehavior.Fail);

        // Assert
        result.Should().Be(ConflictBehavior.Fail);
    }

    [Theory]
    [InlineData(ConflictBehavior.Fail, "fail")]
    [InlineData(ConflictBehavior.Replace, "replace")]
    [InlineData(ConflictBehavior.Rename, "rename")]
    public void ToGraphValue_produces_correct_api_string(ConflictBehavior behavior, string expected)
    {
        // Act
        var result = behavior.ToGraphValue();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Parse_and_ToGraphValue_roundtrip()
    {
        // Arrange
        var original = ConflictBehavior.Rename;

        // Act
        var graphValue = original.ToGraphValue();
        var parsed = ConflictBehaviorExtensions.Parse(graphValue);

        // Assert
        parsed.Should().Be(original);
    }
}
