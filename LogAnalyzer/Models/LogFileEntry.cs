using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

    public class LogFileEntry : INotifyPropertyChanged
    {
        public int LineNumber { get; set; }
        public DateTime Date { get; set; }
        public bool IsTimeOnlyTimestamp { get; set; }
        public string DateDisplay => IsTimeOnlyTimestamp
            ? Date.ToString("HH:mm:ss.fff")
            : Date.ToString("dd.MM.yyyy HH:mm:ss.fff");
        public LogType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty;
        public string[] Detail { get; set; } = [];

        /// <summary>Dynamic column values (used for CSV parsing). Key = header/column name.</summary>
        public Dictionary<string, string> Fields { get; set; } = new();

        /// <summary>Indexer for XAML binding to dynamic CSV columns, e.g. {Binding [ColumnName]}.</summary>
        public string this[string key] =>
            Fields.TryGetValue(key, out var value) ? value : string.Empty;

        private string? _highlightColor;
        public string? HighlightColor
        {
            get => _highlightColor;
            set
            {
                if (_highlightColor == value)
                {
                    return;
                }

                _highlightColor = value;
                OnPropertyChanged();
            }
        }

        private string? _matchedHighlightRule;
        public string? MatchedHighlightRule
        {
            get => _matchedHighlightRule;
            set
            {
                if (_matchedHighlightRule == value)
                {
                    return;
                }

                _matchedHighlightRule = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MatchedHighlightRuleDisplay));
            }
        }

        public string MatchedHighlightRuleDisplay => string.IsNullOrWhiteSpace(MatchedHighlightRule)
            ? "Ohne Regel"
            : MatchedHighlightRule;

        private bool _isDetailVisible;
        public bool IsDetailVisible
        {
            get => _isDetailVisible;
            set
            {
                if (_isDetailVisible == value)
                {
                    return;
                }

                _isDetailVisible = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
