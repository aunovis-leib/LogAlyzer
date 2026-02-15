using System;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services.Parsing
{
    public sealed class ProfileLogParser : ILogParser
    {
        private readonly ParserProfile _profile;

        public ProfileLogParser(ParserProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public bool TryParse(string line, out LogFileEntry entry)
        {
            entry = new LogFileEntry();
            if (string.IsNullOrWhiteSpace(line)) return false;

            var parts = line.Split([_profile.Splitter], StringSplitOptions.None);
            if (parts.Length < 3) return false;

            var datePart = parts[0].Trim();
            var typePart = parts[1].Replace("\t", string.Empty).Trim();
            var textPart = string.Join(_profile.Splitter, parts[2..]).Trim();

            if (!TryParseDate(datePart, _profile.DateFormat, out var dt))
                return false;

            entry.Date = dt;
            entry.Type = TryParseLogType(typePart);
            entry.Text = textPart;
            return true;
        }

        private static bool TryParseDate(string datePart, string dateFormat, out DateTime dt)
        {
            if (DateTime.TryParseExact(
                datePart,
                dateFormat,
                System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
                System.Globalization.DateTimeStyles.None,
                out dt))
            {
                return true;
            }
            if (System.DateTimeOffset.TryParse(
                datePart,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var dto))
            {
                dt = dto.LocalDateTime;
                return true;
            }
            return false;
        }

        private static LogType TryParseLogType(string typePart)
        {
            if (!Enum.TryParse<LogType>(typePart, true, out var type))
            {
                type = LogType.Info;
            }
            return type;
        }
    }
}
