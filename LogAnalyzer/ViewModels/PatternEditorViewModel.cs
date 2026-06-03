using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels
{
    /// <summary>
    /// ViewModel für den Pattern-Editor.
    /// </summary>
    public class PatternEditorViewModel : INotifyPropertyChanged
    {
        private readonly LogPatternService _patternService;
        private LogPattern _currentPattern;
        private string _testLine = string.Empty;
        private string _testResult = string.Empty;

        public ObservableCollection<LogPattern> Patterns { get; } = [];

        private RelayCommand? _addPatternCommand;
        private RelayCommand? _deletePatternCommand;
        private RelayCommand? _savePatternCommand;
        private RelayCommand? _testPatternCommand;

        public PatternEditorViewModel(LogPatternService patternService)
        {
            _patternService = patternService;
            _currentPattern = new LogPattern();
            LoadPatterns();
        }

        public LogPattern CurrentPattern
        {
            get => _currentPattern;
            set
            {
                if (_currentPattern == value)
                    return;
                _currentPattern = value;
                OnPropertyChanged();
            }
        }

        public string TestLine
        {
            get => _testLine;
            set
            {
                if (_testLine == value)
                    return;
                _testLine = value;
                OnPropertyChanged();
            }
        }

        public string TestResult
        {
            get => _testResult;
            set
            {
                if (_testResult == value)
                    return;
                _testResult = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddPatternCommand => _addPatternCommand ??= new RelayCommand(_ =>
        {
            CurrentPattern = new LogPattern
            {
                Id = $"pattern_{System.Guid.NewGuid().ToString().Substring(0, 8)}",
                Priority = 50
            };
        });

        public ICommand DeletePatternCommand => _deletePatternCommand ??= new RelayCommand(async _ =>
        {
            if (CurrentPattern?.Id != null)
            {
                await _patternService.DeletePatternAsync(CurrentPattern.Id);
                LoadPatterns();
                CurrentPattern = new LogPattern();
            }
        });

        public ICommand SavePatternCommand => _savePatternCommand ??= new RelayCommand(async _ =>
        {
            if (CurrentPattern?.Id != null)
            {
                CurrentPattern.RegexPattern = NormalizePatternInput(CurrentPattern.RegexPattern);
                await _patternService.SavePatternAsync(CurrentPattern);
                LoadPatterns();
                TestResult = "Pattern gespeichert. Bereits geladene Log-Einträge werden neu geprüft.";
            }
        });

        public ICommand TestPatternCommand => _testPatternCommand ??= new RelayCommand(_ =>
        {
            if (string.IsNullOrEmpty(TestLine))
            {
                TestResult = "Bitte Testzeile eingeben.";
                return;
            }

            try
            {
                var normalizedPattern = NormalizePatternInput(CurrentPattern.RegexPattern);
                var regex = new Regex(normalizedPattern);
                var match = regex.Match(TestLine);

                if (match.Success)
                {
                    var fields = new System.Text.StringBuilder();
                    foreach (var groupName in regex.GetGroupNames())
                    {
                        if (groupName != "0")
                        {
                            var value = match.Groups[groupName].Value;
                            fields.AppendLine($"  {groupName}: {value}");
                        }
                    }
                    TestResult = $"✓ Match erfolgreich!\nRegex: {normalizedPattern}\n\nExtrahierte Felder:\n{fields}";
                }
                else
                {
                    TestResult = $"✗ Keine Übereinstimmung gefunden.\nRegex: {normalizedPattern}";
                }
            }
            catch (System.Exception ex)
            {
                TestResult = $"✗ Regex-Fehler: {ex.Message}";
            }
        });

        private static string NormalizePatternInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            // Bei klaren Regex-Indikatoren Eingabe unverändert lassen.
            if (input.Contains("(?<", StringComparison.Ordinal)
                || input.Contains("\\d", StringComparison.Ordinal)
                || input.Contains("\\s", StringComparison.Ordinal)
                || input.Contains("\\w", StringComparison.Ordinal)
                || input.Contains("[", StringComparison.Ordinal)
                || input.Contains("]", StringComparison.Ordinal)
                || input.Contains("|", StringComparison.Ordinal)
                || input.Contains("^", StringComparison.Ordinal)
                || input.Contains("$", StringComparison.Ordinal)
                || input.Contains("\\", StringComparison.Ordinal))
            {
                return input;
            }

            // Wildcard-Text in Regex umwandeln: '*' => '.*', Rest escapen.
            var escaped = Regex.Escape(input);
            return escaped.Replace("\\*", ".*");
        }

        private void LoadPatterns()
        {
            Patterns.Clear();
            foreach (var pattern in _patternService.GetPatterns())
            {
                Patterns.Add(pattern);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
