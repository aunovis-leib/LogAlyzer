using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LogAnalyzer.Models;
using Microsoft.Extensions.Logging;

namespace LogAnalyzer.Services.Parsing
{
    /// <summary>
    /// Parses CSV files into <see cref="LogFileEntry"/> instances.
    /// The first non-empty line is treated as the header row. Column values are stored
    /// in <see cref="LogFileEntry.Fields"/> keyed by their header name. The timestamp is
    /// taken from the configured date column (or auto-detected), never from the file name.
    /// </summary>
    public sealed class CsvLogParser : ILogParser
    {
        private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

        private readonly ParserProfile? _profile;
        private readonly char _delimiter;
        private readonly string _dateFormat;
        private readonly TimeSpan _timeOffset;
        private readonly ILogger<CsvLogParser> _logger = AppServices.CreateLogger<CsvLogParser>();

        private string[]? _headers;
        private int _dateColumnIndex = -1;
        private List<string> _displayColumns = new();

        public CsvLogParser(ParserProfile? profile)
        {
            _profile = profile;
            var delimiter = string.IsNullOrEmpty(profile?.CsvDelimiter) ? "," : profile!.CsvDelimiter;
            _delimiter = delimiter[0];
            _dateFormat = string.IsNullOrWhiteSpace(profile?.DateFormat)
                ? "dd.MM.yyyy HH:mm:ss.fff"
                : profile!.DateFormat;
            _timeOffset = TimeSpan.FromHours(profile?.TimeOffsetHours ?? 0)
                + TimeSpan.FromMinutes(profile?.TimeOffsetMinutes ?? 0);
            _logger.LogInformation(
                "CSV-Parser initialisiert: Profil {ProfileName}, Trennzeichen {Delimiter}, Datumsformat {DateFormat}, Zeitoffset {TimeOffset}",
                profile?.Name ?? "Standard",
                _delimiter,
                _dateFormat,
                _timeOffset);
        }

        /// <summary>
        /// The columns that should be displayed. Known only after the header row has been read.
        /// If the profile specifies <see cref="ParserProfile.CsvColumns"/>, those are used;
        /// otherwise all header columns are shown.
        /// </summary>
        public IReadOnlyList<string> DisplayColumns => _displayColumns;

        public bool HeaderParsed => _headers is not null;

        public bool TryParse(string line, out LogFileEntry entry)
        {
            entry = new LogFileEntry();

            if (line is null || line.Length == 0)
            {
                return false;
            }

            var values = SplitCsvLine(line);

            if (_headers is null)
            {
                if (values.All(string.IsNullOrWhiteSpace))
                {
                    return false;
                }

                InitializeHeaders(values);
                return false;
            }

            entry.RawLine = line;

            for (var i = 0; i < _headers.Length; i++)
            {
                var value = i < values.Length ? values[i] : string.Empty;
                entry.Fields[_headers[i]] = value;
            }

            if (_dateColumnIndex >= 0 && _dateColumnIndex < values.Length
                && TryParseDate(values[_dateColumnIndex], out var date, out var isTimeOnly))
            {
                entry.Date = _timeOffset == TimeSpan.Zero ? date : date.Add(_timeOffset);
                entry.IsTimeOnlyTimestamp = isTimeOnly;
            }

            entry.Text = line;
            return true;
        }

        private void InitializeHeaders(string[] headerValues)
        {
            _headers = headerValues.Select(h => h.Trim()).ToArray();

            _dateColumnIndex = ResolveDateColumnIndex(_headers, _profile?.CsvDateColumn);
            var dateColumnName = _dateColumnIndex >= 0 ? _headers[_dateColumnIndex] : null;

            var configured = _profile?.CsvColumns;
            if (configured is { Count: > 0 })
            {
                _displayColumns = configured
                    .Where(c => _headers.Contains(c, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (_displayColumns.Count == 0)
                {
                    _displayColumns = _headers.ToList();
                }
            }
            else
            {
                _displayColumns = _headers.ToList();
            }

            // The timestamp is shown in the fixed "Date" column, so exclude it from the
            // dynamic columns to avoid displaying it twice.
            if (dateColumnName is not null)
            {
                _displayColumns.RemoveAll(c => string.Equals(c, dateColumnName, StringComparison.OrdinalIgnoreCase));
            }

            _logger.LogInformation(
                "CSV-Header erkannt: {ColumnCount} Spalten, Datumsspalte {DateColumn}, Anzeigespalten {DisplayColumnCount}",
                _headers.Length,
                dateColumnName ?? "nicht erkannt",
                _displayColumns.Count);
        }

        private static int ResolveDateColumnIndex(string[] headers, string? configuredDateColumn)
        {
            if (!string.IsNullOrWhiteSpace(configuredDateColumn))
            {
                for (var i = 0; i < headers.Length; i++)
                {
                    if (string.Equals(headers[i], configuredDateColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            for (var i = 0; i < headers.Length; i++)
            {
                var name = headers[i];
                if (name.Contains("date", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("time", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("datum", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("zeit", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("timestamp", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool TryParseDate(string value, out DateTime dt, out bool isTimeOnly)
        {
            isTimeOnly = false;
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                dt = default;
                return false;
            }

            if (DateTime.TryParseExact(trimmed, _dateFormat, GermanCulture, DateTimeStyles.None, out dt)
                || DateTime.TryParseExact(trimmed, _dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                return true;
            }

            if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)
                || DateTimeOffset.TryParse(trimmed, GermanCulture, DateTimeStyles.None, out dto))
            {
                dt = dto.LocalDateTime;
                return true;
            }

            dt = default;
            return false;
        }

        private string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == _delimiter)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            result.Add(sb.ToString());
            return result.ToArray();
        }
    }
}
