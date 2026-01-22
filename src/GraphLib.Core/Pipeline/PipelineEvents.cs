using System.Text.Json;

namespace GraphLib.Core.Pipeline;

/// <summary>
/// Helper class for serializing pipeline events to JSON for storage in EventLogs table.
/// </summary>
public static class PipelineEvents
{
    /// <summary>
    /// Converts an object to a compact JSON string (no indentation).
    /// Used for payload serialization in EventLogRepository.Insert().
    /// </summary>
    public static string BuildPayloadJson(object o)
        => JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = false });
}
