using System.Text.Json;

namespace GraphLib.Core.Pipeline;

public static class PipelineEvents
{
    public static string BuildPayloadJson(object o)
        => JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = false });
}
