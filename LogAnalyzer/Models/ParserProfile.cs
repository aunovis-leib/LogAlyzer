using System.ComponentModel;
using System.Collections.Generic;
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
