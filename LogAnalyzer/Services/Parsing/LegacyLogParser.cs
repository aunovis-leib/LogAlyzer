using System;
using System.Globalization;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services.Parsing
{
    public sealed class LegacyLogParser : ILogParser
    {
        private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

        public bool TryParse(string line, out LogFileEntry entry)
        {
            entry = new LogFileEntry();
            if (string.IsNullOrWhiteSpace(line)) return false;

            var firstSep = line.IndexOf('|');
            if (firstSep < 0) return false;

            var secondSep = line.IndexOf('|', firstSep + 1);
            if (secondSep < 0) return false;

            var datePart = line.AsSpan(0, firstSep).Trim();
            var typePart = line.AsSpan(firstSep + 1, secondSep - firstSep - 1).Trim();
            var textPart = line.AsSpan(secondSep + 1).Trim();

            if (!TryParseDate(datePart, "dd.MM.yyyy HH:mm:ss.fff", out var dt))
                return false;

            entry.Date = dt;
            entry.Type = TryParseLogType(typePart);
            entry.Text = textPart.ToString();
            entry.RawLine = line;
            return true;
        }

        private static bool TryParseDate(ReadOnlySpan<char> datePart, string dateFormat, out DateTime dt)
        {
            if (DateTime.TryParseExact(datePart, dateFormat, GermanCulture, DateTimeStyles.None, out dt))
            {
                return true;
            }
            if (System.DateTimeOffset.TryParse(
                datePart,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dto))
            {
                dt = dto.LocalDateTime;
                return true;
            }
            return false;
        }

        private static LogType TryParseLogType(ReadOnlySpan<char> typePart)
        {
            var sanitizedType = typePart.ToString().Replace("\t", string.Empty).Trim();
            if (int.TryParse(sanitizedType, out var numeric))
            {
                return (LogType)numeric;
            }

            if (!Enum.TryParse<LogType>(sanitizedType, true, out var type))
            {
                type = LogType.Info;
            }
            return type;
        }
    }
}
