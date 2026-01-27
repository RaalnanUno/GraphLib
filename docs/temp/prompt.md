app.MapGet("/api/{collection}", async (HttpRequest req, string collection) =>
{
    if (!string.Equals(collection, collectionName, StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound(new { error = "Unknown collection" });
    }

    var caseManagerId = req.Query["caseManagerId"].ToString();

    await gate.WaitAsync();
    try
    {
        var db = await LoadDbAsync();
        var arr = (JsonArray)db[collectionName]!;

        // No filter → return all records
        if (string.IsNullOrWhiteSpace(caseManagerId))
        {
            return Results.Ok(arr);
        }

        // Filter by CASE_MGR_NTWK_ID, return first match
        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                continue;
            }

            if (!obj.TryGetPropertyValue("CASE_MGR_NTWK_ID", out var value))
            {
                continue;
            }

            if (value is not JsonValue jsonValue)
            {
                continue;
            }

            if (!jsonValue.TryGetValue<string>(out var current))
            {
                continue;
            }

            if (!string.Equals(current, caseManagerId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // First match found
            return Results.Ok(obj);
        }

        // No match found
        return Results.NotFound(new
        {
            error = $"No record found for caseManagerId='{caseManagerId}'"
        });
    }
    finally
    {
        gate.Release();
    }
});
