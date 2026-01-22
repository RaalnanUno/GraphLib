# GraphLib...

.NET 8 solution that:
- uploads a file to SharePoint doc library via Microsoft Graph (app-only)
- converts to PDF via `content?format=pdf`
- optionally stores the PDF back into SharePoint
- logs runs + events into a local SQLite DB (JSON payloads)

## Projects
- GraphLib.Core: Graph + pipeline + SQLite repositories
- GraphLib.Console: CLI (`init`, `run`)

---

## Graph permissions (app-only)
Your Azure AD App Registration must have **Application** permissions:
- Sites.ReadWrite.All  (or Sites.Selected with explicit site grants)
- Files.ReadWrite.All  (often covered by Sites.* depending on tenant policy)

Admin consent required.

---

## Initialize DB (settings live in SQLite)

Creates DB, schema, reference tables, and a default `AppSettings` row (Id=1):

```powershell
dotnet run --project .\GraphLib.Console\GraphLib.Console.csproj -- init --db ".\Data\GraphLib.db"

## Basic Run Sample
dotnet run -c Release --project .\GraphLib.Console\GraphLib.Console.csproj -- run ` --file "C:\Path\To\File.docx"