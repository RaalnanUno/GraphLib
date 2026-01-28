# GraphLib API Documentation

Auto-generated from code comments. Last updated: January 27, 2026.

## Overview

GraphLib is a .NET 8 CLI tool that uploads files to SharePoint, converts them to PDF via Microsoft Graph API, and optionally stores PDFs back into SharePoint. It logs all runs and events into a local SQLite database.

---

## Table of Contents

1. [Command-Line Interface](#command-line-interface)
2. [Application Settings](#application-settings)
3. [Pipeline Architecture](#pipeline-architecture)
4. [Graph API Services](#graph-api-services)
5. [Data Access Layer](#data-access-layer)
6. [Authentication & Secrets](#authentication--secrets)
7. [Conflict Resolution](#conflict-resolution)

---

## Command-Line Interface

### Args Class
`namespace: GraphLib.ConsoleApp.Cli`

Container for all command-line arguments parsed from `argv`. These properties can override database settings. All properties are nullable: `null` means the argument was not provided.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Command` | `string` | The command to execute: `"init"` or `"run"`. Defaults to `"run"`. |
| `File` | `string?` | Input file path (required for `"run"` command). When `ProcessFolderMode=true`, this becomes a folder path instead. |
| `Db` | `string?` | Database path override. If null, uses `./Data/GraphLib.db` relative to exe. |
| `SiteUrl` | `string?` | SharePoint site URL (e.g., `https://tenant.sharepoint.com/sites/SiteName`). |
| `LibraryName` | `string?` | Name of the document library in SharePoint (e.g., `"Shared Documents"`). |
| `TempFolder` | `string?` | Name of the temporary folder for uploads before PDF conversion. |
| `PdfFolder` | `string?` | Name of the folder where PDFs are stored (empty string to disable). |
| `CleanupTemp` | `bool?` | Whether to delete temporary files after successful conversion. |

### Program Entry Point
`file: src/GraphLib.Console/Program.cs`

The main entry point handles:

1. **Command parsing**: Parses command-line arguments using `ArgsParser.Parse(args)`
2. **Help command**: Displays help via `Commands.PrintHelp()`
3. **Database path resolution**: Uses `--db` override or defaults to `./Data/GraphLib.db`
4. **Init command**: Creates database schema and seeds default `AppSettings` via `DbInitializer`
5. **Run command**: 
   - Validates required `--file` argument
   - Loads settings from SQLite database (`AppSettings` table, `Id=1`)
   - Applies command-line overrides (CLI args take precedence)
   - Builds all Graph service instances (dependency injection)
   - Assembles the pipeline orchestrator
   - Generates unique run ID for tracking
   - Executes the pipeline: upload → convert PDF → optionally store PDF → cleanup
   - Returns exit code (0 = success, 1 = failure)

---

## Application Settings

### GraphLibSettings Record
`namespace: GraphLib.Core.Models`

Application settings for GraphLib. Stored in SQLite `AppSettings` table (`Id=1`). All properties are required (sealed record with `required` keyword). Used immutably throughout the application.

#### Target Properties

| Property | Type | Description |
|----------|------|-------------|
| `SiteUrl` | `string` | Full SharePoint site URL (e.g., `https://tenant.sharepoint.com/sites/SiteName`) |
| `LibraryName` | `string` | Document library name (e.g., `"Shared Documents"`) |
| `TempFolder` | `string` | Folder name for temporary files before conversion |
| `PdfFolder` | `string` | Folder name for storing converted PDFs (empty string disables) |

#### Behavior Properties

| Property | Type | Description |
|----------|------|-------------|
| `CleanupTemp` | `bool` | Whether to delete temporary files after successful conversion |
| `ConflictBehavior` | `ConflictBehavior` | How to handle filename conflicts: `Fail`, `Replace`, or `Rename` |

#### Feature Toggles

| Property | Type | Description |
|----------|------|-------------|
| `StorePdfInSharePoint` | `bool` | Whether to store the PDF back into SharePoint |
| `ProcessFolderMode` | `bool` | Process all files in a folder instead of single file |
| `IgnoreFailuresWhenFolderMode` | `bool` | In folder mode, continue processing even if individual file fails |

#### Authentication Properties

| Property | Type | Description |
|----------|------|-------------|
| `TenantId` | `string` | Azure AD tenant GUID |
| `ClientId` | `string` | Azure AD app registration GUID |
| `ClientSecret` | `string` | App registration client secret |

---

## Pipeline Architecture

### SingleFilePipeline
`namespace: GraphLib.Core.Pipeline`

Orchestrates the complete file conversion workflow. This is the "heartbeat" of the application.

#### Workflow Sequence

1. Resolve SharePoint site URL → site ID
2. Resolve document library name → drive ID
3. Create/ensure temp and PDF folders exist
4. Upload file to temp folder
5. Convert file to PDF via Graph API
6. (Optional) Store PDF back in SharePoint
7. (Optional) Delete temporary file

All operations are logged to `EventLogs` table (JSON payloads). Runs are tracked in `Runs` and `FileEvents` tables.

#### Constructor

```csharp
public SingleFilePipeline(
    GraphSiteResolver siteResolver,
    GraphDriveResolver driveResolver,
    GraphFolderService folders,
    GraphUploadService upload,
    GraphPdfConversionService convert,
    GraphUploadService storePdf,
    GraphCleanupService cleanup,
    RunRepository runs,
    FileEventRepository files,
    EventLogRepository logs)
```

Initializes pipeline with all required dependencies using constructor injection pattern (no runtime service discovery).

#### RunAsync Method

```csharp
public async Task<GraphLibRunResult> RunAsync(
    string runId,
    string filePath,
    GraphLibSettings settings,
    bool logFailuresOnly,
    CancellationToken ct)
```

Executes the complete pipeline for a single file.

| Parameter | Type | Description |
|-----------|------|-------------|
| `runId` | `string` | Unique ID for tracking this execution |
| `filePath` | `string` | Path to input file |
| `settings` | `GraphLibSettings` | SharePoint and auth configuration |
| `logFailuresOnly` | `bool` | If true, only log failures (not successes) |
| `ct` | `CancellationToken` | Cancellation token for async operations |

**Returns:** `GraphLibRunResult` with success status and metrics

**Behavior:** Returns immediately with result; all operations are async. Logs all stages to database even on failure.

---

## Graph API Services

### GraphClient
`namespace: GraphLib.Core.Graph`

HTTP client wrapper for Microsoft Graph API v1.0 endpoint. Automatically injects Bearer token and request tracking headers. All other `Graph*Service` classes use this client.

#### Constructor

```csharp
public GraphClient(HttpClient http, GraphAuth auth)
```

Initializes the Graph client with authentication. Sets base address to `https://graph.microsoft.com/v1.0/`

#### SendAsync Method

```csharp
public async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage req,
    string? clientRequestId,
    CancellationToken ct)
```

Sends an HTTP request to Graph API with authorization token and client request ID.

| Parameter | Type | Description |
|-----------|------|-------------|
| `req` | `HttpRequestMessage` | The HTTP request to send |
| `clientRequestId` | `string?` | Optional request ID for tracking (used in request headers) |
| `ct` | `CancellationToken` | Cancellation token for async operation |

**Returns:** HTTP response from Graph API

---

### GraphAuth
`namespace: GraphLib.Core.Graph`

Handles Azure AD authentication via Microsoft Identity Client library. Uses app-only authentication (no user interaction), suitable for background services. Obtains access tokens for Microsoft Graph API v1.0 calls.

#### Constructor

```csharp
public GraphAuth(string tenantId, string clientId, string clientSecret)
```

Initializes auth with Azure AD credentials.

| Parameter | Type | Description |
|-----------|------|-------------|
| `tenantId` | `string` | Azure AD tenant GUID |
| `clientId` | `string` | Azure AD app registration ID (GUID) |
| `clientSecret` | `string` | App registration client secret |

#### GetAccessTokenAsync Method

```csharp
public async Task<string> GetAccessTokenAsync(CancellationToken ct)
```

Acquires an access token for Microsoft Graph API calls. The token is valid for ~1 hour and is cached by MSAL.

---

### GraphUploadService
`namespace: GraphLib.Core.Graph`

Uploads files to SharePoint via Microsoft Graph. Uses PUT request with conflict resolution. Used twice in pipeline: once for temp folder, once for PDF folder.

#### UploadToFolderAsync Method

```csharp
public async Task<(string itemId, string rawJson)> UploadToFolderAsync(
    string driveId,
    string folderName,
    string fileName,
    byte[] bytes,
    ConflictBehavior conflictBehavior,
    string clientRequestId,
    CancellationToken ct)
```

Uploads a file to a specific folder in a SharePoint drive.

| Parameter | Type | Description |
|-----------|------|-------------|
| `driveId` | `string` | Graph drive ID |
| `folderName` | `string` | Target folder name (empty string = root) |
| `fileName` | `string` | Name for the uploaded file |
| `bytes` | `byte[]` | File content as byte array |
| `conflictBehavior` | `ConflictBehavior` | How to handle if file already exists |
| `clientRequestId` | `string` | Request ID for tracking |
| `ct` | `CancellationToken` | Cancellation token |

**Returns:** Tuple of `(itemId, rawJsonResponse)` where `itemId` is Graph's unique ID for the uploaded file

**Details:**
- Builds path: `/drives/{driveId}/root:/{folderName}/{fileName}:/content`
- Applies conflict resolution: `?@microsoft.graph.conflictBehavior=replace|fail|rename`
- Graph returns 201 Created or 200 OK depending on outcome
- Extracts the new item's ID from response JSON

---

### GraphPdfConversionService
`namespace: GraphLib.Core.Graph`

Downloads files from SharePoint as PDF via Microsoft Graph conversion. The conversion is done server-side by Graph; no external PDF service is used. Uses the pattern: `GET /drives/{driveId}/items/{itemId}/content?format=pdf`

#### DownloadPdfAsync Method

```csharp
public async Task<byte[]> DownloadPdfAsync(
    string driveId,
    string itemId,
    string clientRequestId,
    CancellationToken ct)
```

Downloads a SharePoint file as a PDF-converted version. Works with Office documents (Word, Excel, PowerPoint) and other formats.

| Parameter | Type | Description |
|-----------|------|-------------|
| `driveId` | `string` | Graph drive ID |
| `itemId` | `string` | Graph item ID (from upload response) |
| `clientRequestId` | `string` | Request ID for tracking |
| `ct` | `CancellationToken` | Cancellation token |

**Returns:** Raw PDF file bytes

**Details:**
- Uses `?format=pdf` query parameter to tell Graph to convert on-the-fly
- Returns binary PDF content directly (not JSON)

---

## Data Access Layer

### DbInitializer
`namespace: GraphLib.Core.Data`

Initializes the SQLite database with schema and default settings. Run once during `"graphlib init"` command.

#### EnsureCreatedAndSeedDefaults Method

```csharp
public void EnsureCreatedAndSeedDefaults()
```

Creates the database schema and seeds default `AppSettings` if they don't exist. Loads schema from embedded `Sql/schema.sql` file.

**Details:**
- Loads schema from `AppContext.BaseDirectory/Sql/schema.sql`
- Executes all `CREATE TABLE` statements
- Ensures default `AppSettings` row exists (`Id=1`)

#### EnsureSettingsRow Method

```csharp
private static void EnsureSettingsRow(SqliteConnection conn)
```

Ensures `AppSettings` row with `Id=1` exists, inserting defaults if missing.

---

## Authentication & Secrets

### ISecretProvider Interface
`namespace: GraphLib.Core.Secrets`

Abstraction for secret retrieval and decryption. Allows pluggable implementations (database, Key Vault, etc.).

---

### DbSecretProvider
`namespace: GraphLib.Core.Secrets`

Default `ISecretProvider` implementation (v1). Returns secrets as-is from the database (no decryption). This is a seam point: can be replaced with encryption/vault providers in the future.

#### GetSecret Method

```csharp
public string GetSecret(string key, string rawValueFromDb)
```

Returns the raw value from database unchanged. In a future implementation, this could decrypt the value or fetch from Key Vault.

| Parameter | Type | Description |
|-----------|------|-------------|
| `key` | `string` | Secret key identifier |
| `rawValueFromDb` | `string` | Raw value from database |

**Returns:** Secret value (currently as-is, can be overridden for encryption)

---

## Conflict Resolution

### ConflictBehavior Enum
`namespace: GraphLib.Core.Models`

Specifies how to handle filename conflicts when uploading files to SharePoint.

**Values:**
- `Fail` - Fail the operation if file exists
- `Replace` - Overwrite existing file
- `Rename` - Rename uploaded file to avoid conflict (Graph appends number)

---

## Design Patterns & Best Practices

### Graph Service Architecture

All Graph interactions follow a common pattern:

1. **GraphClient**: Wraps `HttpClient` with auth token injection and request-ID tracking
2. **Individual Graph*Service classes**: Encapsulate specific API calls
3. **Async/Await Pattern**: All async methods accept `CancellationToken ct` parameter (always pass through)
4. **Safe Response Parsing**: Use `GraphClient.ReadStringSafeAsync()` for safe response handling

### Database Access Pattern

- All data access goes through repositories in `Data/Repositories/`
- `DbConnectionFactory.Open()` returns fresh `SqliteConnection` (no connection pooling)
- Repositories use command text directly (no ORM)
- Integer bools in SQLite: use `GetInt32(i) != 0` to convert
- Schema defined in `Sql/schema.sql` — keep in sync

### Settings & Secrets

- Settings live in SQLite (`AppSettings` table, `Id=1`)
- Loaded once per run via `SettingsRepository`
- CLI arguments override database settings via `ApplyOverrides()`
- Always use `ISecretProvider` interface for secrets, not raw DB reads

### Pipeline Design

- Maintains strict sequencing of operations
- Logs each stage to three repositories: `RunRepository`, `FileEventRepository`, `EventLogRepository`
- Returns `GraphLibRunResult` with success status and timing information
- Executes cleanup even on errors (finally block pattern)

---

## Integration Points

- **Microsoft Graph API**: v1.0 endpoint; requires app-only auth (no user interaction)
- **Azure AD**: TenantId/ClientId/ClientSecret; stored encrypted in SQLite
- **SharePoint**: Site URL + library name; resolved dynamically via `GraphSiteResolver` + `GraphDriveResolver`
- **SQLite**: Single-threaded; no async support in library (sync queries only)

---

## Common Gotchas

1. **Cancellation tokens**: Always thread them through async calls; many Graph operations are long-running
2. **File cleanup**: Must run even on errors (use finally block in pipeline)
3. **PDF format parameter**: Use `?format=pdf` on Graph endpoint, not an external service
4. **Folder mode**: Setting `ProcessFolderMode=true` changes behavior to batch process all files; test both paths
5. **Nullable enabled globally**: Project-wide, use `required` keyword for record properties and validate nulls early

---

## Running the Application

### Initialize Database

```powershell
dotnet run --project .\GraphLib.Console\GraphLib.Console.csproj -- init --db ".\Data\GraphLib.db"
```

This creates the SQLite database with default settings.

### Configure Settings

After initialization, update `AppSettings` table with:
- `TenantId` (Azure AD tenant GUID)
- `ClientId` (Azure AD app registration GUID)
- `ClientSecret` (app registration secret)
- `SiteUrl` (SharePoint site URL)
- `LibraryName` (document library name)

Use SQLite client or the SQL script in `_bat/update-settings.sql`

### Run Single File Conversion

```powershell
dotnet run -c Release --project .\GraphLib.Console\GraphLib.Console.csproj -- run --file "C:\path\to\file.docx"
```

### Run with CLI Overrides

```powershell
dotnet run --project .\GraphLib.Console\GraphLib.Console.csproj -- run `
    --file "C:\document.docx" `
    --site-url "https://tenant.sharepoint.com/sites/MySite" `
    --library-name "Documents"
```

---

*Documentation generated from code comments in GraphLib project.*
