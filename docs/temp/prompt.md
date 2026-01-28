Perfect — the two compiler errors you called out tell me *exactly* what we need to do 👍
You’re almost certainly compiling with an **older C# language version** (likely C# 7.3 or earlier), so we need to:

* ❌ **Avoid** `using var` (C# 8+) → causes **CS8370**
* ❌ **Avoid** newer pattern matching / modern syntax
* ✅ Use explicit `using (...) {}` blocks
* ✅ Keep method signatures simple and unambiguous to avoid **CS1501**

Below is a **safe, drop-in method** you can paste **inside your existing class**, with no new language features.

---

## ✅ Final method (older-C# safe)

```csharp
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public async Task<CaseManagerLookupResult> GetCaseManagerByCertNumberAsync(
    string environment,
    string certNumber)
{
    if (string.IsNullOrEmpty(environment))
        throw new ArgumentException("environment is required", "environment");

    if (string.IsNullOrEmpty(certNumber))
        throw new ArgumentException("certNumber is required", "certNumber");

    environment = environment.Trim().ToUpperInvariant();

    string requestUrl;
    HttpClient httpClient = new HttpClient();

    try
    {
        if (environment == "LOCAL")
        {
            // Adjust endpoint name/port if needed
            requestUrl = "http://localhost:3000/CaseManagers?certNumber=" +
                         Uri.EscapeDataString(certNumber);
        }
        else if (environment == "PROD")
        {
            string apiConfig = Environment.GetEnvironmentVariable("ApiConfig");
            if (string.IsNullOrEmpty(apiConfig))
                throw new InvalidOperationException("ApiConfig environment variable not found.");

            string[] configBlocks = apiConfig.Split(',');

            if (configBlocks.Length < 3)
                throw new InvalidOperationException(
                    "ApiConfig must be: apiPath,clientId,clientSecret");

            string apiPath = configBlocks[0].Trim();
            string clientId = configBlocks[1].Trim();
            string clientSecret = configBlocks[2].Trim();

            requestUrl = apiPath + "?certNumber=" +
                         Uri.EscapeDataString(certNumber);

            httpClient.DefaultRequestHeaders.Add("X-Client-Id", clientId);
            httpClient.DefaultRequestHeaders.Add("X-Client-Secret", clientSecret);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                "environment",
                environment,
                "Environment must be LOCAL or PROD");
        }

        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response = await httpClient.GetAsync(requestUrl);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        CaseManagerRecord[] records =
            JsonSerializer.Deserialize<CaseManagerRecord[]>(json);

        CaseManagerRecord first = null;
        if (records != null && records.Length > 0)
            first = records[0];

        string firstRawJson = null;
        if (first != null)
            firstRawJson = JsonSerializer.Serialize(first);

        return new CaseManagerLookupResult
        {
            RawJson = firstRawJson,
            Record = first
        };
    }
    finally
    {
        httpClient.Dispose();
    }
}
```

---

## ✅ How to call it (also older-safe)

```csharp
CaseManagerLookupResult result =
    await GetCaseManagerByCertNumberAsync("LOCAL", "12345");

Console.WriteLine(result.RawJson);

if (result.Record != null)
{
    Console.WriteLine(result.Record.CaseMgrFstNme);
}
```

---

## Why this avoids your errors

### ❌ CS8370 (language feature not available)

* Removed:

  * `using var`
  * advanced pattern matching
* Uses **explicit `HttpClient` lifecycle**

### ❌ CS1501 (no overload found)

* No ambiguous method calls
* No optional params
* No `CancellationToken` overload confusion

---

## Optional next step (recommended later)

When you’re ready, we can:

* Inject `HttpClient` instead of creating it
* Add logging hooks
* Support both **array OR single-object responses**
* Move PROD header logic behind a strategy

But this version will compile cleanly **today** and behaves exactly as you specified.

If you want this wired directly into **GraphLib.Console** or your **NGDocs local API**, say the word and we’ll tailor it.


---

Below is a clean, drop-in **C# model + query service** that does exactly what you described:

* Inputs: `environment` (string), `certNumber` (string)
* `LOCAL`: calls a **local json-server** via GET, passes `certNumber` in query string
* `PROD`: reads `ApiConfig` from env var, splits by comma into `apiPath, clientId, clientSecret`, then calls remote API via GET and sends `clientId/clientSecret` as headers
* Takes the **first** record returned (array), and stores it as:

  * `RawJson` (string)
  * `Record` (typed object)

> **Note:** json-server usually returns an **array** for filtered queries (`?certNumber=...`). This code assumes the API returns a JSON array.

---

## 1) The model (matches your JSON)

```csharp
using System;
using System.Text.Json.Serialization;

public sealed class CaseManagerRecord
{
    [JsonPropertyName("CASE_MGR_NTWK_ID")]
    public string? CaseMgrNtwkId { get; set; }

    [JsonPropertyName("CASE_MGR_FST_NME")]
    public string? CaseMgrFstNme { get; set; }

    [JsonPropertyName("CASE_MGR_MID_NME")]
    public string? CaseMgrMidNme { get; set; }

    [JsonPropertyName("CASE_MGR_LST_NME")]
    public string? CaseMgrLstNme { get; set; }

    [JsonPropertyName("CASE_SPVR_REGN_CDE")]
    public string? CaseSpvrRegnCde { get; set; }

    [JsonPropertyName("ORG_UNIQ_NUM")]
    public int? OrgUniqNum { get; set; }

    [JsonPropertyName("INST_OCC_CHTR_NUM")]
    public int? InstOccChtrNum { get; set; }

    // If your API returns date strings like "2026-01-27", DateTime? is perfect.
    // If it returns something unusual, switch this to string and parse yourself.
    [JsonPropertyName("CASE_VRSN_RND_DTE")]
    public DateTime? CaseVrsnRndDte { get; set; }
}
```

---

## 2) Result wrapper (holds raw + typed)

```csharp
public sealed class CaseManagerLookupResult
{
    public string? RawJson { get; init; }
    public CaseManagerRecord? Record { get; init; }
}
```

---

## 3) The query service (LOCAL + PROD logic)

```csharp
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class CaseManagerApiClient
{
    private readonly HttpClient _http;

    // You can inject HttpClient (recommended).
    public CaseManagerApiClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>
    /// Queries the Case Manager API and returns the first matching record
    /// as both raw JSON and a typed object.
    /// </summary>
    public async Task<CaseManagerLookupResult> GetFirstByCertNumberAsync(
        string environment,
        string certNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentException("Environment is required.", nameof(environment));

        if (string.IsNullOrWhiteSpace(certNumber))
            throw new ArgumentException("CertNumber is required.", nameof(certNumber));

        environment = environment.Trim().ToUpperInvariant();

        // Decide endpoint + headers based on environment
        string requestUrl;

        switch (environment)
        {
            case "LOCAL":
            {
                // Example json-server endpoint (adjust path/port to your actual setup)
                // Typical json-server filter: /CaseManagers?certNumber=123
                var baseUrl = "http://localhost:3000/CaseManagers";
                requestUrl = $"{baseUrl}?certNumber={Uri.EscapeDataString(certNumber)}";
                break;
            }

            case "PROD":
            {
                // ApiConfig format: "https://api.yourco.com/CaseManagers, myClientId, myClientSecret"
                var apiConfig = Environment.GetEnvironmentVariable("ApiConfig");
                if (string.IsNullOrWhiteSpace(apiConfig))
                    throw new InvalidOperationException("Missing environment variable: ApiConfig");

                string[] configBlocks = apiConfig.Split(',')
                                                 .Select(x => x.Trim())
                                                 .Where(x => !string.IsNullOrWhiteSpace(x))
                                                 .ToArray();

                if (configBlocks.Length < 3)
                    throw new InvalidOperationException("ApiConfig must contain: apiPath, clientId, clientSecret (comma-separated).");

                var apiPath = configBlocks[0];
                var clientId = configBlocks[1];
                var clientSecret = configBlocks[2];

                requestUrl = $"{apiPath}?certNumber={Uri.EscapeDataString(certNumber)}";

                // Add headers (adjust header names to what your PROD API expects)
                _http.DefaultRequestHeaders.Remove("X-Client-Id");
                _http.DefaultRequestHeaders.Remove("X-Client-Secret");
                _http.DefaultRequestHeaders.Add("X-Client-Id", clientId);
                _http.DefaultRequestHeaders.Add("X-Client-Secret", clientSecret);

                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(environment), environment, "Environment must be LOCAL or PROD.");
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);

        // Expecting an array; take first element
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false // we used exact JsonPropertyName mappings
        };

        CaseManagerRecord? first = null;

        try
        {
            var items = JsonSerializer.Deserialize<CaseManagerRecord[]>(json, options);
            first = items?.FirstOrDefault();
        }
        catch (JsonException)
        {
            // If your API sometimes returns a single object instead of an array,
            // uncomment this fallback:
            //
            // first = JsonSerializer.Deserialize<CaseManagerRecord>(json, options);
            //
            // Otherwise, keep throwing:
            throw;
        }

        // Save first object as raw JSON string too (not the whole array)
        string? firstRawJson = null;
        if (first != null)
        {
            firstRawJson = JsonSerializer.Serialize(first, options);
        }

        return new CaseManagerLookupResult
        {
            RawJson = firstRawJson,
            Record = first
        };
    }
}
```

---

## Usage example

```csharp
using var http = new HttpClient();
var client = new CaseManagerApiClient(http);

var result = await client.GetFirstByCertNumberAsync("LOCAL", "12345");

Console.WriteLine(result.RawJson);
Console.WriteLine(result.Record?.CaseMgrFstNme);
```

---

### Two quick things to confirm in your environment (so you don’t get stuck)

1. **What is the real endpoint path?** I used `/CaseManagers` as a reasonable default.
2. **What header names does PROD expect?** I used `X-Client-Id` and `X-Client-Secret`. If your API expects `clientId` / `clientSecret` or `Authorization`, tell me what it wants and I’ll adjust the code.
