using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LogAnalyzer.Models
{
    public class ParserProfile : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _dateFormat = "dd.MM.yyyy HH:mm:ss.fff";
        private string _splitter = "|";
        private string? _contextDatePrefix;
        private string? _contextDateFormat;
        private int _timeOffsetHours;
        private int _timeOffsetMinutes;
        private bool _isCsv;
        private string _csvDelimiter = ",";
        private string? _csvDateColumn;
        private List<string> _csvColumns = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public string DateFormat
        {
            get => _dateFormat;
            set => SetField(ref _dateFormat, value);
        }

        public string Splitter
        {
            get => _splitter;
            set => SetField(ref _splitter, value);
        }

        public string? ContextDatePrefix
        {
            get => _contextDatePrefix;
            set => SetField(ref _contextDatePrefix, value);
        }

        public string? ContextDateFormat
        {
            get => _contextDateFormat;
            set => SetField(ref _contextDateFormat, value);
        }

        public int TimeOffsetHours
        {
            get => _timeOffsetHours;
            set => SetField(ref _timeOffsetHours, value);
        }

        public int TimeOffsetMinutes
        {
            get => _timeOffsetMinutes;
            set => SetField(ref _timeOffsetMinutes, value);
        }

        /// <summary>When true, files are parsed as CSV (also auto-enabled for *.csv files).</summary>
        public bool IsCsv
        {
            get => _isCsv;
            set => SetField(ref _isCsv, value);
        }

        /// <summary>Column separator used for CSV parsing.</summary>
        public string CsvDelimiter
        {
            get => _csvDelimiter;
            set => SetField(ref _csvDelimiter, string.IsNullOrEmpty(value) ? "," : value);
        }

        /// <summary>Header name of the column that provides the timestamp (parsed via <see cref="DateFormat"/>).</summary>
        public string? CsvDateColumn
        {
            get => _csvDateColumn;
            set => SetField(ref _csvDateColumn, value);
        }

        /// <summary>
        /// Header names of the columns to display (must match the CSV header row exactly).
        /// If empty, all columns from the header row are shown.
        /// </summary>
        public List<string> CsvColumns
        {
            get => _csvColumns;
            set => SetField(ref _csvColumns, value ?? new List<string>());
        }

        /// <summary>
        /// Newline-/semicolon-separated view of <see cref="CsvColumns"/> for editing in the UI.
        /// </summary>
        public string CsvColumnsText
        {
            get => string.Join(Environment.NewLine, _csvColumns);
            set
            {
                var list = (value ?? string.Empty)
                    .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();

                CsvColumns = list;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CsvColumnsText)));
            }
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
