using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Data;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels
{
    /// <summary>
    /// ViewModel für die Anzeige von erkannten Pattern-Matches.
    /// </summary>
    public class PatternMatchViewModel : INotifyPropertyChanged
    {
        private PatternMatch _match = null!;
        private bool _isSelected;
        private bool _isPinned;

        public PatternMatch Match
        {
            get => _match;
            set
            {
                if (_match == value)
                    return;
                _match = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned == value)
                    return;
                _isPinned = value;
                OnPropertyChanged();
            }
        }

        public string SeverityColor => Match?.Pattern?.Severity switch
        {
            "critical" => "#FF0000",
            "error" => "#FF4444",
            "warning" => "#FFAA00",
            "info" => "#0080FF",
            _ => "#808080"
        };

        public string DisplayText => $"[{Match?.Pattern?.Name}] {Match?.LogEntry?.Text}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel zur Verwaltung des Pattern-Match-Panels.
    /// </summary>
    public class PatternMatchPanelViewModel : INotifyPropertyChanged
    {
        private readonly LogPatternService _patternService;
        private PatternMatchViewModel? _selectedMatch;
        private string _filterText = string.Empty;
        private string _selectedSeverity = "All";
        private readonly ICollectionView _matchesView;

        public ObservableCollection<PatternMatchViewModel> Matches { get; } = [];
        public ICollectionView MatchesView => _matchesView;
        public ObservableCollection<string> Severities { get; } = ["All", "debug", "info", "warning", "error", "critical"];
        public ObservableCollection<string> AvailableTags { get; } = [];
        public int PinnedCount => Matches.Count(x => x.IsPinned);

        public event EventHandler<LogFileEntry?>? MatchSelected;

        private RelayCommand? _clearAllCommand;
        private RelayCommand? _pinSelectedCommand;
        private RelayCommand? _unpinAllCommand;
        private RelayCommand? _exportCommand;

        public PatternMatchPanelViewModel(LogPatternService patternService)
        {
            _patternService = patternService;
            _matchesView = CollectionViewSource.GetDefaultView(Matches);
            _matchesView.Filter = FilterMatch;
            _patternService.PatternMatched += OnPatternMatched;
            InitializeTags();
        }

        public PatternMatchViewModel? SelectedMatch
        {
            get => _selectedMatch;
            set
            {
                if (_selectedMatch == value)
                    return;
                _selectedMatch = value;
                OnPropertyChanged();
                MatchSelected?.Invoke(this, _selectedMatch?.Match?.LogEntry);
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText == value)
                    return;
                _filterText = value;
                OnPropertyChanged();
                RefreshFilter();
            }
        }

        public string SelectedSeverity
        {
            get => _selectedSeverity;
            set
            {
                if (_selectedSeverity == value)
                    return;
                _selectedSeverity = value;
                OnPropertyChanged();
                RefreshFilter();
            }
        }

        public ICommand ClearAllCommand => _clearAllCommand ??= new RelayCommand(_ =>
        {
            Matches.Clear();
            OnPropertyChanged(nameof(PinnedCount));
        });

        public ICommand PinSelectedCommand => _pinSelectedCommand ??= new RelayCommand(_ =>
        {
            if (SelectedMatch != null)
            {
                SelectedMatch.IsPinned = !SelectedMatch.IsPinned;
                OnPropertyChanged(nameof(PinnedCount));
            }
        });

        public ICommand UnpinAllCommand => _unpinAllCommand ??= new RelayCommand(_ =>
        {
            foreach (var match in Matches)
            {
                match.IsPinned = false;
            }

            OnPropertyChanged(nameof(PinnedCount));
        });

        public ICommand ExportCommand => _exportCommand ??= new RelayCommand(_ => ExportMatches());

        private void OnPatternMatched(object? sender, PatternMatch match)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                var viewModel = new PatternMatchViewModel
                {
                    Match = match,
                    IsPinned = match.Pattern.Action.Pin
                };

                viewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(PatternMatchViewModel.IsPinned))
                    {
                        OnPropertyChanged(nameof(PinnedCount));
                    }
                };

                Matches.Insert(0, viewModel);
                OnPropertyChanged(nameof(PinnedCount));

                // Begrenze auf letzten 1000 Matches
                while (Matches.Count > 1000)
                {
                    Matches.RemoveAt(Matches.Count - 1);
                }

                OnPropertyChanged(nameof(PinnedCount));

                // Bei kritischem Severity automatisch zeigen
                if (match.Pattern.Severity == "critical")
                {
                    SelectedMatch = viewModel;
                }
            });
        }

        private void InitializeTags()
        {
            AvailableTags.Clear();
            foreach (var tag in _patternService.GetPatterns()
                .SelectMany(p => p.Tags)
                .Distinct()
                .OrderBy(t => t))
            {
                AvailableTags.Add(tag);
            }
        }

        private void RefreshFilter()
        {
            _matchesView.Refresh();
        }

        private bool FilterMatch(object obj)
        {
            if (obj is not PatternMatchViewModel m)
            {
                return false;
            }

            if (_selectedSeverity != "All" && !string.Equals(m.Match.Pattern.Severity, _selectedSeverity, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(FilterText))
            {
                return true;
            }

            return (m.Match.LogEntry.Text?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (m.Match.Pattern.Name?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void ExportMatches()
        {
            var csv = "Timestamp,Pattern,Severity,Text,Fields\n";

            foreach (var match in Matches)
            {
                var fields = string.Join("; ",
                    match.Match.ExtractedFields.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                var text = match.Match.LogEntry.Text
                    .Replace("\"", "\"\"")
                    .Replace("\n", " ");

                csv += $"\"{match.Match.LogEntry.Date:O}\"," +
                       $"\"{match.Match.Pattern.Name}\"," +
                       $"\"{match.Match.Pattern.Severity}\"," +
                       $"\"{text}\"," +
                       $"\"{fields}\"\n";
            }

            var fileName = $"pattern_matches_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = fileName,
                DefaultExt = ".csv",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(dialog.FileName, csv);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Einfacher RelayCommand für WPF-Commands.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}
