using System;
using System.Globalization;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services.Parsing
{
    public sealed class ProfileLogParser : ILogParser
    {
        private readonly ParserProfile _profile;
        private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

        public ProfileLogParser(ParserProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public bool TryParse(string line, out LogFileEntry entry)
        {
            entry = new LogFileEntry();
            if (string.IsNullOrWhiteSpace(line)) return false;

            var firstSep = line.IndexOf(_profile.Splitter, StringComparison.Ordinal);
            if (firstSep < 0) return false;

            var secondSep = line.IndexOf(_profile.Splitter, firstSep + _profile.Splitter.Length, StringComparison.Ordinal);
            if (secondSep < 0) return false;

            var datePart = line.AsSpan(0, firstSep).Trim();
            var typePart = line.AsSpan(firstSep + _profile.Splitter.Length, secondSep - firstSep - _profile.Splitter.Length).Trim();
            var textPart = line.AsSpan(secondSep + _profile.Splitter.Length).Trim();

            if (!TryParseDate(datePart, _profile.DateFormat, out var dt))
                return false;

            entry.Date = dt;
            entry.Type = TryParseLogType(typePart);
            entry.Text = textPart.ToString();
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
            if (!Enum.TryParse<LogType>(sanitizedType, true, out var type))
            {
                type = LogType.Info;
            }
            return type;
        }
    }
}
