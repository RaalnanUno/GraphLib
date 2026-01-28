using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class CaseManagerApiClient
{
    /// <summary>
    /// Queries the Case Manager API and returns the first matching record
    /// as both raw JSON (for the first record only) and a typed object.
    ///
    /// environment:
    ///  - "LOCAL": calls local json-server, certNumber in querystring
    ///  - "PROD" : reads ApiConfig env var (apiPath, clientId, clientSecret),
    ///             passes certNumber in querystring and clientId/clientSecret in headers
    /// </summary>
    public async Task<CaseManagerLookupResult> GetFirstByCertNumberAsync(string environment, string certNumber)
    {
        if (string.IsNullOrEmpty(environment))
            throw new ArgumentException("environment is required", "environment");

        if (string.IsNullOrEmpty(certNumber))
            throw new ArgumentException("certNumber is required", "certNumber");

        string env = environment.Trim().ToUpperInvariant();

        string requestUrl = null;
        HttpClient httpClient = new HttpClient();

        try
        {
            if (env == "LOCAL")
            {
                // Adjust these to match your json-server setup
                // Example: http://localhost:3000/CaseManagers?certNumber=12345
                string baseUrl = "http://localhost:3000/CaseManagers";
                requestUrl = baseUrl + "?certNumber=" + Uri.EscapeDataString(certNumber);
            }
            else if (env == "PROD")
            {
                string apiConfig = Environment.GetEnvironmentVariable("ApiConfig");
                if (string.IsNullOrEmpty(apiConfig))
                    throw new InvalidOperationException("Missing environment variable: ApiConfig");

                string[] configBlocks = apiConfig.Split(',');
                if (configBlocks.Length < 3)
                    throw new InvalidOperationException("ApiConfig must contain: apiPath, clientId, clientSecret (comma-separated).");

                string apiPath = configBlocks[0].Trim();
                string clientId = configBlocks[1].Trim();
                string clientSecret = configBlocks[2].Trim();

                requestUrl = apiPath + "?certNumber=" + Uri.EscapeDataString(certNumber);

                // Use whatever header names your PROD API expects
                httpClient.DefaultRequestHeaders.Add("X-Client-Id", clientId);
                httpClient.DefaultRequestHeaders.Add("X-Client-Secret", clientSecret);
            }
            else
            {
                throw new ArgumentOutOfRangeException("environment", environment, "Environment must be LOCAL or PROD.");
            }

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            // Expecting an array response (json-server style)
            CaseManagerRecord[] records = JsonSerializer.Deserialize<CaseManagerRecord[]>(json);

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
}
