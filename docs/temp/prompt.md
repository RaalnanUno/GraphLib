Yep — this is a perfect fit for **.NET Minimal API**. You can replace Express + json-server with:

* **Minimal API** hosting your static UI (your `index.html` + `app.js`)
* **Minimal API endpoints** that read/write the same `db.json` file
* Same routes your front-end already expects:
  `GET /api/supervisory-info-by-casemgr`
  `POST /api/supervisory-info-by-casemgr`
  `PATCH /api/supervisory-info-by-casemgr/{id}`

Below is a drop-in “json-server-like” implementation that keeps your current front-end almost unchanged.

---

## 1) Create the .NET project

From a new folder:

```powershell
dotnet new web -n SupervisoryInfoEditor
cd SupervisoryInfoEditor
```

---

## 2) Move your static files to wwwroot

In ASP.NET Core, static files live in `wwwroot`.

Create this structure:

```
SupervisoryInfoEditor/
  Program.cs
  SupervisoryInfoEditor.csproj
  db.json
  wwwroot/
    index.html
    app.js
```

Copy:

* `public/index.html` → `wwwroot/index.html`
* `public/app.js` → `wwwroot/app.js`
* `db.json` (your data file) → project root (same level as Program.cs)

---

## 3) Update your front-end to use same-origin API (recommended)

In `wwwroot/app.js`, replace:

```js
const API_BASE = "http://127.0.0.1:60375";
const API_PREFIX = "/api";
```

with:

```js
const API_BASE = "";     // same origin
const API_PREFIX = "/api";
```

Everything else can stay the same.

*(This avoids CORS entirely and works cleanly on corp servers.)*

---

## 4) Replace Program.cs with this Minimal API

**File:** `Program.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

// Optional: make JSON pretty in responses (doesn't affect db.json storage)
builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = null; // keep your CASE_MGR_... keys as-is
    o.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

// ---- Static files (serves wwwroot/index.html and wwwroot/app.js) ----
app.UseDefaultFiles(); // serves index.html by default
app.UseStaticFiles();

// ---- "json-server-like" storage ----
var dbPath = Path.Combine(app.Environment.ContentRootPath, "db.json");
var collectionName = "supervisory-info-by-casemgr";
var gate = new SemaphoreSlim(1, 1);

JsonSerializerOptions dbJsonOptions = new()
{
    PropertyNamingPolicy = null,
    WriteIndented = true
};

async Task<JsonObject> LoadDbAsync()
{
    if (!File.Exists(dbPath))
    {
        // Create a default db.json if missing
        var root = new JsonObject
        {
            [collectionName] = new JsonArray()
        };
        await File.WriteAllTextAsync(dbPath, root.ToJsonString(dbJsonOptions));
        return root;
    }

    var text = await File.ReadAllTextAsync(dbPath);
    var node = JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text) as JsonObject;
    node ??= new JsonObject();

    if (node[collectionName] is null)
        node[collectionName] = new JsonArray();

    return node;
}

async Task SaveDbAsync(JsonObject root)
{
    var text = root.ToJsonString(dbJsonOptions);
    await File.WriteAllTextAsync(dbPath, text);
}

static int NextId(JsonArray arr)
{
    // Find max existing "id" and +1
    var max = 0;
    foreach (var item in arr)
    {
        if (item is JsonObject obj &&
            obj.TryGetPropertyValue("id", out var idNode) &&
            idNode is JsonValue v &&
            v.TryGetValue<int>(out var id))
        {
            if (id > max) max = id;
        }
    }
    return max + 1;
}

// GET all
app.MapGet("/api/{collection}", async (string collection) =>
{
    if (!string.Equals(collection, collectionName, StringComparison.OrdinalIgnoreCase))
        return Results.NotFound(new { error = "Unknown collection" });

    await gate.WaitAsync();
    try
    {
        var db = await LoadDbAsync();
        var arr = (JsonArray)db[collectionName]!;
        return Results.Ok(arr);
    }
    finally
    {
        gate.Release();
    }
});

// POST create
app.MapPost("/api/{collection}", async (string collection, JsonObject body) =>
{
    if (!string.Equals(collection, collectionName, StringComparison.OrdinalIgnoreCase))
        return Results.NotFound(new { error = "Unknown collection" });

    await gate.WaitAsync();
    try
    {
        var db = await LoadDbAsync();
        var arr = (JsonArray)db[collectionName]!;

        // json-server behavior: assign id if omitted
        if (body["id"] is null)
            body["id"] = NextId(arr);

        arr.Add(body);

        await SaveDbAsync(db);

        return Results.Created($"/api/{collectionName}/{body["id"]}", body);
    }
    finally
    {
        gate.Release();
    }
});

// PATCH partial update
app.MapMethods("/api/{collection}/{id:int}", new[] { "PATCH" }, async (string collection, int id, JsonObject patch) =>
{
    if (!string.Equals(collection, collectionName, StringComparison.OrdinalIgnoreCase))
        return Results.NotFound(new { error = "Unknown collection" });

    await gate.WaitAsync();
    try
    {
        var db = await LoadDbAsync();
        var arr = (JsonArray)db[collectionName]!;

        JsonObject? target = null;

        foreach (var item in arr)
        {
            if (item is JsonObject obj &&
                obj.TryGetPropertyValue("id", out var idNode) &&
                idNode is JsonValue v &&
                v.TryGetValue<int>(out var currentId) &&
                currentId == id)
            {
                target = obj;
                break;
            }
        }

        if (target is null)
            return Results.NotFound(new { error = $"id={id} not found" });

        // Apply patch fields (simple merge)
        foreach (var kvp in patch)
        {
            if (string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase))
                continue; // don't allow id changes

            target[kvp.Key] = kvp.Value?.DeepClone();
        }

        await SaveDbAsync(db);
        return Results.Ok(target);
    }
    finally
    {
        gate.Release();
    }
});

app.Run();
```

---

## 5) Run it

```powershell
dotnet run
```

By default it will bind to a random port (like `http://localhost:5xxx`).

If you want it to match your old port, set `Properties/launchSettings.json`, or run:

```powershell
dotnet run --urls "http://127.0.0.1:60375"
```

Then open:

* `http://127.0.0.1:60375/` (serves `wwwroot/index.html`)
* API: `http://127.0.0.1:60375/api/supervisory-info-by-casemgr`

---

## 6) Notes (so this behaves like your current setup)

* Your UI already does **GET/POST/PATCH** exactly how we implemented.
* `db.json` format remains:

```json
{
  "supervisory-info-by-casemgr": [ ... ]
}
```

* This is **single-file persistence**, just like json-server.
* The `SemaphoreSlim` prevents two writes from stomping each other.

---

## Optional next steps (if you want to harden it for corp)

* Add **validation** (required fields, date format, etc.)
* Add **paging / search** (DataTables can hit server-side endpoints)
* Swap file-based db.json for **SQLite** (still lightweight, no “install express” drama)

If you want, paste your *real* `db.json` (or just 2–3 sample rows) and I’ll add:

* server-side sorting/search/paging shaped specifically for DataTables, **without changing your UI much**.
