app.MapGet("/api/{collection}", async (string collection, string? caseManagerId) =>
{
    if (!string.Equals(collection, collectionName, StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound(new { error = "Unknown collection" });
    }

    await gate.WaitAsync();
    try
    {
        var db = await LoadDbAsync();
        var arr = (JsonArray)db[collectionName]!;

        if (string.IsNullOrWhiteSpace(caseManagerId))
        {
            return Results.Ok(arr);
        }

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

            return Results.Ok(obj);
        }

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
