# GraphLib Coding Guidelines for AI Agents

## Architecture Overview

GraphLib is a .NET 8 CLI tool that uploads files to SharePoint, converts them to PDF via Microsoft Graph API, and optionally stores PDFs back into SharePoint. It logs all runs and events into a local SQLite database.

**Key Layers:**
- **GraphLib.Console**: CLI entry point with command parsing (init, run); orchestrates dependency injection
- **GraphLib.Core**: Business logic split into three subsystems:
  - **Graph/**: Microsoft Graph API interactions (auth, site/drive resolution, file upload, PDF conversion, cleanup)
  - **Pipeline/**: `SingleFilePipeline` orchestrates the multi-stage workflow
  - **Data/**: SQLite repositories for runs, file events, and event logs; schema-driven via `Sql/schema.sql`

## Critical Design Patterns

### 1. Graph Service Architecture
All Graph interactions inherit from a common pattern:
- `GraphClient`: Wraps HttpClient with auth token injection and request-ID tracking
- Individual `Graph*Service` classes (e.g., `GraphUploadService`, `GraphPdfConversionService`) encapsulate specific API calls
- All async methods accept `CancellationToken ct` parameter (always pass it through)
- Use `GraphClient.ReadStringSafeAsync()` for safe response parsing

Example pattern from [GraphUploadService](src/GraphLib.Core/Graph/GraphUploadService.cs):
```csharp
public async Task<string> UploadAsync(string driveId, string folderId, FileInfo file, CancellationToken ct)
{
    var req = new HttpRequestMessage(HttpMethod.Post, $"drives/{driveId}/items/{folderId}:/{Uri.EscapeDataString(file.Name)}:/content");
    // ...
}
```

### 2. Pipeline Orchestration
[SingleFilePipeline](src/GraphLib.Core/Pipeline/SingleFilePipeline.cs) is the heartbeat of the application. It:
- Receives injected service dependencies in constructor (not discovered at runtime)
- Maintains strict sequencing: resolve site → resolve drive → check/create folder → upload → convert → optionally store PDF → cleanup
- Logs each stage to three repositories: `RunRepository`, `FileEventRepository`, `EventLogRepository`
- Returns `GraphLibRunResult` with success status and timing

When adding workflow changes, always update pipeline and corresponding database event logging.

### 3. Settings & Secrets Pattern
Settings live in SQLite (`AppSettings` table, Id=1) and are loaded once per run:
- [GraphLibSettings](src/GraphLib.Core/Models/GraphLibSettings.cs) is a sealed record with required properties
- [DbSecretProvider](src/GraphLib.Core/Secrets/DbSecretProvider.cs) decrypts `ClientSecret` field (default: returns plaintext; override for encryption)
- CLI args override DB settings: `ApplyOverrides(s, argsParsed)` in [Program.cs](src/GraphLib.Console/Program.cs#L52)

**Important:** Always use the `ISecretProvider` interface for secrets, not raw DB reads.

### 4. Database Access Pattern
All data access goes through repositories in [Data/Repositories](src/GraphLib.Core/Data/Repositories/):
- `DbConnectionFactory.Open()` returns fresh `SqliteConnection` (no connection pooling yet)
- Repositories use command text directly (no ORM)
- All integer bools in SQLite: use `GetInt32(i) != 0` to convert
- Schema defined in [Sql/schema.sql](src/GraphLib.Core/Sql/schema.sql) — keep in sync

## Developer Workflows

### Build & Test
```powershell
# Console project only (for now; no unit tests yet)
cd src
dotnet build GraphLib.Console\GraphLib.Console.csproj

# Initialize DB with defaults
dotnet run --project .\GraphLib.Console\GraphLib.Console.csproj -- init --db ".\Data\GraphLib.db"

# Run conversion on a file
dotnet run -c Release --project .\GraphLib.Console\GraphLib.Console.csproj -- run --file "C:\path\to\file.docx"
```

### Adding a New Graph Operation
1. Create service in [Graph/](src/GraphLib.Core/Graph/) following `Graph*Service` naming
2. Inject into `SingleFilePipeline` constructor
3. Add call in pipeline's `RunAsync()` method with event logging
4. Add DB logging step via `EventLogRepository` and/or `FileEventRepository`

### Adding CLI Arguments
1. Update [Args.cs](src/GraphLib.Console/Cli/Args.cs) with new properties
2. Extend [ArgsParser.cs](src/GraphLib.Console/Cli/ArgsParser.cs) parsing logic
3. Pass to `ApplyOverrides()` in [Program.cs](src/GraphLib.Console/Program.cs)

## Project-Specific Conventions

- **Nullable enabled globally** (in .csproj): use `required` keyword for record properties, validate nulls early
- **Sealed classes only**: `sealed class` for all services prevents accidental inheritance
- **Stopwatch timing**: [SingleFilePipeline](src/GraphLib.Core/Pipeline/SingleFilePipeline.cs#L51) logs elapsed milliseconds; preserve this pattern for observability
- **Run IDs**: Generated at CLI entry, passed through entire pipeline for correlation; critical for audit trail
- **Conflict behavior**: Enum in [Models/ConflictBehavior.cs](src/GraphLib.Core/Models/ConflictBehavior.cs); extend here, not inline

## Integration Points

- **Microsoft Graph API**: v1.0 endpoint; requires app-only auth (no user interaction)
- **Azure AD**: TenantId/ClientId/ClientSecret; stored encrypted in SQLite
- **SharePoint**: Site URL + library name; resolved dynamically via `GraphSiteResolver` + `GraphDriveResolver`
- **SQLite**: Single-threaded; no async support in library (sync queries only)

## Common Gotchas

1. **Cancellation tokens**: Always thread them through async calls; many Graph operations are long-running
2. **File cleanup**: [GraphCleanupService](src/GraphLib.Core/Graph/GraphCleanupService.cs) must run even on errors (finally block in pipeline)
3. **PDF format parameter**: Use `?format=pdf` on Graph endpoint, not an external service
4. **Folder mode**: Setting `ProcessFolderMode=true` changes behavior to batch process all files in a directory; test both paths
