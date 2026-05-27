using System;
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
        public DateTime Date { get; set; }
        public bool IsTimeOnlyTimestamp { get; set; }
        public string DateDisplay => IsTimeOnlyTimestamp
            ? Date.ToString("HH:mm:ss.fff")
            : Date.ToString("dd.MM.yyyy HH:mm:ss.fff");
        public LogType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public string[] Detail { get; set; } = [];

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
