# GraphLib Unit Testing Summary

## Overview
Successfully added comprehensive unit tests to the GraphLib project using:
- **Framework**: xUnit
- **Mocking**: Moq
- **Assertions**: FluentAssertions
- **Test Count**: 61 tests, all passing

## Test Files Created

### 1. Graph/GraphSmokeTests.cs (11 tests)
Tests for core model classes and enums:
- `ConflictBehavior_Parse_*` - Conflict resolution parsing (3 tests)
  - Case insensitivity
  - Default value handling
  - Invalid value handling
- `ConflictBehavior_ToGraphValue_*` - Graph API conversion (1 test)
- `GraphLibSettings_*` - Settings configuration (2 tests)
  - Default factory method
  - Record immutability
- `GraphStage_*` - Pipeline stage constants (1 test)
- `LogLevel_*` - Log level constants (1 test)
- `GraphLibRunResult_*` - Pipeline results (2 tests)
  - Success scenario
  - Failure scenario

### 2. Cli/ArgsParserTests.cs (13 tests)
Tests for command-line argument parsing:
- Command detection (init, help, run)
- File and path argument extraction
- Multiple settings parsing
- Boolean flag handling
- Case insensitivity
- Flag without value defaults to true
- Auth arguments (tenantId, clientId, clientSecret)
- Unrecognized arguments handling
- ConflictBehavior and RunId options

### 3. Models/ConflictBehaviorTests.cs (12 tests)
Deep testing of conflict behavior enum:
- All three enum values (Fail, Replace, Rename)
- Case-insensitive parsing
- Null/empty/whitespace handling
- Invalid value with custom default
- Graph API string conversion
- Roundtrip conversion (parse → toGraphValue → parse)

### 4. Data/SqlitePathsTests.cs (10 tests)
Database path resolution logic:
- Default path usage (null, empty, whitespace)
- Relative to absolute path conversion
- Absolute path preservation
- Whitespace trimming
- Directory creation
- Custom defaults
- Provided path preference over default

### 5. Secrets/DbSecretProviderTests.cs (6 tests)
Secret resolution testing:
- Unchanged value return
- Special character preservation
- Empty string handling
- Null value handling
- Different keys support
- ISecretProvider interface implementation

### 6. Pipeline/PipelineEventsTests.cs (9 tests)
JSON event serialization:
- Simple object serialization
- Valid JSON output
- Compact format (no indentation)
- Null value handling
- Complex nested objects
- Array handling
- DateTime preservation
- Boolean preservation

## Test Patterns Used

### Arrange-Act-Assert (AAA)
All tests follow the standard AAA pattern for clarity:
```csharp
// Arrange - Setup test data
var input = "value";

// Act - Execute the method under test
var result = Method(input);

// Assert - Verify the result
result.Should().Be(expected);
```

### Theory-Based Tests
Used `[Theory]` with `[InlineData]` for parameterized testing:
```csharp
[Theory]
[InlineData("fail", ConflictBehavior.Fail)]
[InlineData("replace", ConflictBehavior.Replace)]
public void Parse_recognizes_all_behavior_values(string input, ConflictBehavior expected)
```

### FluentAssertions
All tests use fluent syntax for readable assertions:
```csharp
result.Should().NotBeNull();
result.Should().BeTrue();
result.Should().Contain("expected");
result.Should().Be(expected);
```

## Running the Tests

### Run all tests:
```powershell
cd src\GraphLib.Core.Tests
dotnet test
```

### Run with detailed output:
```powershell
dotnet test --verbosity detailed
```

### Run specific test file:
```powershell
dotnet test --filter ClassName=GraphLib.Core.Tests.Cli.ArgsParserTests
```

### Run with code coverage:
```powershell
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Coverage

The tests focus on:
1. **Model validation** - Enums, records, POCOs
2. **Utility functions** - Parsing, conversion, path resolution
3. **Configuration** - Settings defaults, CLI argument parsing
4. **Serialization** - JSON event payload creation
5. **Security** - Secret provider behavior

## Next Steps for Expanded Testing

To further increase test coverage:

1. **Graph Services** - Mock HttpClient to test GraphAuth, GraphClient, and resolver services
2. **Repositories** - Use SQLite in-memory database for data layer testing
3. **Pipeline** - Mock all dependencies to test SingleFilePipeline orchestration
4. **Integration** - End-to-end tests with test SharePoint instance
5. **Error Handling** - Test exception scenarios and error recovery

Example approach for Graph service testing:
```csharp
[Fact]
public async Task GraphSiteResolver_ResolveSiteAsync_throws_on_invalid_url()
{
    // Arrange
    var httpClient = new Mock<HttpClient>();
    var auth = new Mock<GraphAuth>();
    var resolver = new GraphSiteResolver(new GraphClient(httpClient.Object, auth.Object));

    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(() => 
        resolver.ResolveSiteAsync("https://sharepoint.com/", "client-id", CancellationToken.None)
    );
}
```

## Package Versions

- **xunit**: 2.9.3
- **xunit.runner.visualstudio**: 3.1.4
- **FluentAssertions**: 8.8.0
- **Microsoft.NET.Test.Sdk**: 17.14.1
- **coverlet.collector**: 6.0.4 (for code coverage)

## Notes

- Tests target net10.0 (.NET 10) while main code targets net8.0
- All tests are independent and can run in any order
- No external dependencies or database required for these unit tests
- Tests are fast (< 3 seconds for all 61 tests)
