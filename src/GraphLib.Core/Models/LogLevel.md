namespace GraphLib.Core.Models;

/// <summary>
/// Log level constants for EventLogs table.
/// Flexible string-based levels allow easy extension (no enum constraint).
/// </summary>
public static class LogLevel
{
    /// <summary>Informational log entries (success messages)</summary>
    public const string Info = "Info";
    
    /// <summary>Warning log entries (unexpected but recoverable conditions)</summary>
    public const string Warn = "Warn";
    
    /// <summary>Error log entries (failures and exceptions)</summary>
    public const string Error = "Error";
}
