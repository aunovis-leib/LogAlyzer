using System;

namespace LogAnalyzer.Models
{
    public enum LogType
    {
        All = -1,
        Error,
        Info,
        Warning,
        Debug
    }

    public class LogFileEntry
    {
        public DateTime Date { get; set; }
        public LogType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public string[] Detail { get; set; } = [];
    }
}
