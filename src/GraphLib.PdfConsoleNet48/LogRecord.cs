using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public sealed class LogRecord
{
    private readonly List<LogRecordData> _records = new();

    public void Add(
        int lineId,
        string message,
        string @class,
        string method)
    {
        _records.Add(new LogRecordData
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            LineId = lineId,
            Message = message,
            Class = @class,
            Method = method
        });
    }

    public string PrintRecords(LogGroupBy groupBy = LogGroupBy.None)
    {
        object output;

        switch (groupBy)
        {
            case LogGroupBy.Class:
                output = _records
                    .GroupBy(r => r.Class)
                    .ToDictionary(g => g.Key, g => g.ToList());
                break;

            case LogGroupBy.Method:
                output = _records
                    .GroupBy(r => r.Method)
                    .ToDictionary(g => g.Key, g => g.ToList());
                break;

            default:
                output = _records;
                break;
        }

        return JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private sealed class LogRecordData
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public int LineId { get; set; }
        public string Message { get; set; }
        public string Class { get; set; }
        public string Method { get; set; }
    }
}


// var log = new LogRecord();

// log.Add(10, "Starting process", "ProcessFiles", "Process");
// log.Add(20, "Calling GraphLib", "ProcessFiles", "Convert");
// log.Add(30, "Saving output", "FileWriter", "WritePdf");
// string result = log.PrintRecords(LogGroupBy.Class);
// Console.WriteLine(result);