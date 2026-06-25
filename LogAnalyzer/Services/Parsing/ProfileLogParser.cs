using System;
using System.Globalization;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services.Parsing
{
    public sealed class ProfileLogParser : ILogParser
    {
        private readonly ParserProfile _profile;
        private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
        private DateTime? _contextDate;

        public ProfileLogParser(ParserProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public bool TryParse(string line, out LogFileEntry entry)
        {
            entry = new LogFileEntry();
            if (string.IsNullOrWhiteSpace(line)) return false;

            if (TryCaptureContextDate(line))
            {
                return false;
            }

            var firstSep = line.IndexOf(_profile.Splitter, StringComparison.Ordinal);
            if (firstSep < 0) return false;

            var secondSep = line.IndexOf(_profile.Splitter, firstSep + _profile.Splitter.Length, StringComparison.Ordinal);
            if (secondSep < 0) return false;

            var datePart = line.AsSpan(0, firstSep).Trim();
            var typePart = line.AsSpan(firstSep + _profile.Splitter.Length, secondSep - firstSep - _profile.Splitter.Length).Trim();
            var textPart = line.AsSpan(secondSep + _profile.Splitter.Length).Trim();

            if (!TryParseDate(datePart, _profile.DateFormat, out var dt, out var isTimeOnly))
                return false;

            entry.Date = dt;
            entry.IsTimeOnlyTimestamp = isTimeOnly;
            if (isTimeOnly && _contextDate.HasValue)
            {
                entry.Date = _contextDate.Value.Date.Add(dt.TimeOfDay);
                entry.IsTimeOnlyTimestamp = false;
            }
            entry.Type = TryParseLogType(typePart);
            entry.Text = textPart.ToString();
            entry.RawLine = line;
            return true;
        }

        private bool TryCaptureContextDate(string line)
        {
            if (string.IsNullOrWhiteSpace(_profile.ContextDatePrefix)
                || string.IsNullOrWhiteSpace(_profile.ContextDateFormat))
            {
                return false;
            }

            var prefix = _profile.ContextDatePrefix;
            var index = line.IndexOf(prefix, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var valueStart = index + prefix.Length;
            if (valueStart > line.Length)
            {
                return false;
            }

            var dateText = line.AsSpan(valueStart).Trim();
            if (DateTime.TryParseExact(dateText, _profile.ContextDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
                || DateTime.TryParseExact(dateText, _profile.ContextDateFormat, GermanCulture, DateTimeStyles.None, out parsedDate))
            {
                _contextDate = parsedDate.Date;
                return true;
            }

            return false;
        }

        private static bool TryParseDate(ReadOnlySpan<char> datePart, string dateFormat, out DateTime dt, out bool isTimeOnly)
        {
            if (DateTime.TryParseExact(datePart, dateFormat, GermanCulture, DateTimeStyles.None, out dt))
            {
                isTimeOnly = false;
                return true;
            }

            if (TryParseTimeOnly(datePart, out dt))
            {
                isTimeOnly = true;
                return true;
            }

            if (System.DateTimeOffset.TryParse(
                datePart,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dto))
            {
                dt = dto.LocalDateTime;
                isTimeOnly = false;
                return true;
            }

            isTimeOnly = false;
            return false;
        }

        private static bool TryParseTimeOnly(ReadOnlySpan<char> datePart, out DateTime dt)
        {
            if (TimeOnly.TryParseExact(datePart, "HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.None, out var utcTime)
                || TimeOnly.TryParseExact(datePart, "HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out utcTime)
                || TimeOnly.TryParseExact(datePart, "HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.None, out utcTime)
                || TimeOnly.TryParseExact(datePart, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out utcTime))
            {
                dt = DateTime.Today.Add(utcTime.ToTimeSpan());
                return true;
            }

            dt = default;
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
