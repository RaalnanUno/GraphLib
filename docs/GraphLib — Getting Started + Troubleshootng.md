# GraphLib — Getting Started + Troubleshooting (Combined)

This single document combines:

- **Getting Started** (the “happy path”): build, initialize the SQLite DB, set `AppSettings`, and run a file through the pipeline.
- **Troubleshooting**: the most common setup/runtime problems (especially app-only Microsoft Graph → SharePoint issues like `401` on `resolveSite` when admin consent wasn’t granted).

---

## What GraphLib Does

GraphLib is a **.NET 8** document automation/conversion tool that:

- Uses **Microsoft Graph (app-only / client credentials)** to resolve a SharePoint site + library (drive)
- Uploads/processes content and runs conversion stages (e.g., produce PDF)
- Stores configuration and runtime state in a **SQLite** database (`GraphLib.db`)

---

## Recommended Workflow

Build → init DB → update `AppSettings` → (optional) PowerShell site test → run GraphLib.

---

## Contents

- [Azure App Registration — Exact Locations for IDs & Secrets](#azure-app-registration--exact-locations-for-ids--secrets)
- [Prerequisites](#prerequisites)
- [Build](#build)
- [Initialize DB](#initialize-db)
- [Update AppSettings](#update-appsettings)
- [Run](#run)
- [Run with explicit DB path](#run-with-explicit-db-path)
- [Optional: run scripts (bat/sql)](#optional-run-scripts-batsql)
- [Troubleshooting: 401 Unauthorized on resolveSite](#troubleshooting-401-unauthorized-on-resolvesite)
- [Troubleshooting: MSAL token/ClientId/secret errors](#troubleshooting-msal-tokenclientidsecret-errors)
- [Troubleshooting: SiteUrl points to a library/folder](#troubleshooting-siteurl-points-to-a-libraryfolder)
- [Troubleshooting: LibraryName / resolveDrive failures](#troubleshooting-libraryname--resolvedrive-failures)
- [Troubleshooting: SQLite updates don’t “stick”](#troubleshooting-sqlite-updates-dont-stick)
- [Troubleshooting: Confirm which DB GraphLib uses](#troubleshooting-confirm-which-db-graphlib-uses)
- [Minimum “success” checklist](#minimum-success-checklist)

---

## Azure App Registration — Exact Locations for IDs & Secrets

This section shows the **exact navigation paths in Azure** to locate the Tenant ID, Client ID, and Client Secret required by GraphLib.

### Step 1 — Open Azure App Registration

1. Sign in to `https://portal.azure.com`
2. Open the left menu
3. Select **Microsoft Entra ID**
4. Click **App registrations**
5. Select your application from the list

### Tenant ID & Client ID

**Location**  
`Microsoft Entra ID → App registrations → Your App → Overview`

- **Directory (tenant) ID** → use this as `TenantId`
- **Application (client) ID** → use this as `ClientId`

### Client Secret

**Location**  
`Microsoft Entra ID → App registrations → Your App → Certificates & secrets`

1. Under **Client secrets**, click **New client secret**
2. Enter a description
3. Select an expiration period
4. Click **Add**

> **IMPORTANT:** Copy the **Value** column immediately.  
> The **Secret ID** will NOT work, and the value cannot be retrieved later.

### Final Mapping

- `TenantId` → Directory (tenant) ID
- `ClientId` → Application (client) ID
- `ClientSecret` → Client secret **Value**

---

## Prerequisites

### Tools & Access

- **.NET SDK** installed (project targets **.NET 8**)
- A working clone of the repo
- A SharePoint/M365 tenant + **App Registration configured for app-only access**

### Application scope (GraphLib expectations)

GraphLib assumes:

- **App-only** (client credentials) authentication against Microsoft Graph
- Uploading/working with SharePoint content requires **Graph Application permissions**
- Your SharePoint targets are identified by:
  - `SiteUrl` (SharePoint site root)
  - `LibraryName` (document library display name)
- The app reads these values from **SQLite `AppSettings`** (typically row `Id = 1`)

> **Important:** App-only uploads require Graph **Application permissions** and **Admin consent granted**.  
> If you hit a `401` at `resolveSite`, jump to [401 Unauthorized on resolveSite](#troubleshooting-401-unauthorized-on-resolvesite).

---

## Build

Run from repo root (or adjust paths accordingly):

```powershell
cd src
dotnet restore
dotnet build -c Release
````

---

## Initialize DB

The `init` command creates the SQLite DB and schema (or ensures it exists).

```powershell
cd src
dotnet run -c Release --project .\GraphLib.Console\GraphLib.Console.csproj -- init
```

Tip: If you want a specific DB file, add `--db` (see [Run with explicit DB path](#run-with-explicit-db-path)).

---

## Update AppSettings

Update row `Id = 1` in `AppSettings`. `ClientSecret` must be the secret **Value** (not secret id).

```sql
UPDATE AppSettings SET
  SiteUrl      = 'https://tenant.sharepoint.com/sites/SiteName',
  LibraryName  = 'Shared Documents',
  TenantId     = <TENANT_ID>,
  ClientId     = <CLIENT_ID>,
  ClientSecret = <CLIENT_SECRET_VALUE>
WHERE Id = 1;
```

### SiteUrl rule

`SiteUrl` must be the site root (not a library/folder URL).

✅ Example: `https://tenant.sharepoint.com/sites/SiteName`

---

## Run

Basic sample:

```powershell
cd src
dotnet run -c Release --project .\GraphLib.Console\GraphLib.Console.csproj -- run `
  --file "C:\path\to\file.docx"
```

If you see `401` at `resolveSite`, it’s almost always **admin consent** or permissions.
See [401 Unauthorized on resolveSite](#troubleshooting-401-unauthorized-on-resolvesite).

---

## Run with explicit DB path

Recommended during setup/troubleshooting to avoid editing one DB while running another.

```powershell
cd src
dotnet run -c Release --project .\GraphLib.Console\GraphLib.Console.csproj -- run `
  --db ".\GraphLib.Console\bin\Release\net8.0\Data\GraphLib.db" `
  --file "C:\path\input.docx"
```

---

## Optional: run scripts (bat/sql)

If your repo includes a batch SQL runner (like the one referenced in troubleshooting), typical patterns:

### Apply an update script

```powershell
.\RunBat\run-sql.bat ".\RunBat\update-settings.sql"
```

### Quick verify (select)

```powershell
.\RunBat\run-sql.bat "SELECT SiteUrl, LibraryName, TenantId, ClientId FROM AppSettings WHERE Id=1;"
```

> If your repo doesn’t have `RunBat` yet: keep this section as a placeholder or delete it.
> It’s a team-friendly workflow because it’s repeatable and produces a clean success/failure signal.

---

# Troubleshooting

## Troubleshooting: 401 Unauthorized on resolveSite

**Most common root cause:**
API permissions exist but are **Not granted**. You must click **Grant admin consent** for the tenant.

### Symptoms

* GraphLib fails at stage `resolveSite`
* Event log shows `statusCode: 401`
* PowerShell app-only test also returns 401 for a SharePoint Graph endpoint

### Fix

1. Azure Portal → **Microsoft Entra ID** → **App registrations** → your app
2. Go to **API permissions**
3. Ensure you have **Application** permissions like:

   * `Sites.Read.All` (minimum)
   * or `Sites.ReadWrite.All` (upload/delete)
4. Click **Grant admin consent** (top of the permissions page)
5. Wait ~30–60 seconds and retry

### Verification (PowerShell)

```powershell
$tenantId = "<TENANT_ID>"
$clientId = "<CLIENT_ID>"
$clientSecret = "<CLIENT_SECRET_VALUE>" # value, not secret id

$body = @{
  client_id     = $clientId
  scope         = "https://graph.microsoft.com/.default"
  client_secret = $clientSecret
  grant_type    = "client_credentials"
}

$tokenResp = Invoke-RestMethod -Method Post `
  -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body $body

$accessToken = $tokenResp.access_token
$headers = @{ Authorization = "Bearer $accessToken" }

Invoke-RestMethod -Method Get `
  -Uri "https://graph.microsoft.com/v1.0/sites/<tenant>.sharepoint.com:/sites/<SiteName>" `
  -Headers $headers
```

Expected: **200 OK** and a JSON site payload.

---

## Troubleshooting: MSAL token/ClientId/secret errors

### What to store in SQLite

`ClientSecret` must be the **Value** shown when you create the secret — not the **Secret ID**.

### Symptoms

* `MsalServiceException`
* Messages like: “not a valid application identifier”

### Fix

* `TenantId` = Directory (tenant) ID
* `ClientId` = Application (client) ID
* `ClientSecret` = Secret **Value**

### SQLite update example

```sql
UPDATE AppSettings SET
  TenantId = '<TENANT_ID>',
  ClientId = '<CLIENT_ID>',
  ClientSecret = '<CLIENT_SECRET_VALUE>'
WHERE Id = 1;
```

---

## Troubleshooting: SiteUrl points to a library/folder

### Rule

`SiteUrl` must point to the **SharePoint site**, not the document library or a folder.

❌ Incorrect
`https://tenant.sharepoint.com/sites/SiteName/GraphLib`

✅ Correct
`https://tenant.sharepoint.com/sites/SiteName`

### Why

GraphLib resolves the site via Graph using:

`/sites/{hostname}:{server-relative-path}`

…which expects the site path (e.g., `/sites/AlphaOmega`), not a deeper library/folder URL.

---

## Troubleshooting: LibraryName / resolveDrive failures

### Symptoms

* `resolveSite` succeeds
* Fails at `resolveDrive`
* Error mentions drive/library not found

### Fix

`LibraryName` must match the document library’s **display name** in SharePoint.

Common names include:

* `Documents`
* `Shared Documents`
* a custom library name (e.g., `GraphLib`)

Tip: Open the site in the browser, click the library in the left navigation, and use that exact title for `LibraryName`.

---

## Troubleshooting: SQLite updates don’t “stick”

Some SQLite viewers don’t commit edits, or the grid doesn’t refresh after running SQL.

Recommended: Use the repo’s batch-based SQL runner to apply scripts and get a clean success/failure signal.

### Run an update script

```powershell
.\RunBat\run-sql.bat ".\RunBat\update-settings.sql"
```

### Verify values

```powershell
.\RunBat\run-sql.bat "SELECT SiteUrl, LibraryName, TenantId, ClientId FROM AppSettings WHERE Id=1;"
```

---

## Troubleshooting: Confirm which DB GraphLib uses

Relative DB paths are resolved relative to the executable folder. To avoid confusion, always run with an explicit DB path during troubleshooting.

### Recommended run command

```powershell
dotnet run -c Release --project .\src\GraphLib.Console\GraphLib.Console.csproj -- run `
  --db ".\src\GraphLib.Console\bin\Release\net8.0\Data\GraphLib.db" `
  --file "C:\path\input.docx"
```

Tip: Keep your SQL runner pointed at the same DB file you pass to `--db`.

---

## Minimum “success” checklist

* ✅ App Registration exists in tenant
* ✅ Graph **Application** permissions added (`Sites.Read.*` / `Sites.ReadWrite.*`)
* ✅ **Admin consent granted** for tenant
* ✅ `TenantId` / `ClientId` / `ClientSecret (Value)` stored in SQLite
* ✅ `SiteUrl` points to the SharePoint **site** (not library/folder)
* ✅ `LibraryName` matches SharePoint library **display name**
* ✅ You run against the **same SQLite DB** you update

```
