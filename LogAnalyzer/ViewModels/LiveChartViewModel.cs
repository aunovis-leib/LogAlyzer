using LiveCharts;
using LiveCharts.Wpf;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LogAnalyzer.Models;

namespace LogAnalyzer.ViewModels
{
    public class LiveChartViewModel : INotifyPropertyChanged
    {
        private SeriesCollection _chartSeries = [];
        public SeriesCollection ChartSeries
        {
            get => _chartSeries;
            set { _chartSeries = value; OnPropertyChanged(); }
        }

        private IList<string> _xLabels = [];
        public IList<string> XLabels
        {
            get => _xLabels;
            set { _xLabels = value; OnPropertyChanged(); }
        }

        public LiveChartViewModel()
        {
            ChartSeries = [];
            XLabels = [];
        }

        public void UpdateFromEntries(IEnumerable<LogFileEntry> entries, DateTime? fromDate, DateTime? toDate)
        {
            var filtered = entries.Where(e =>
                (fromDate is null || e.Date.Date >= fromDate.Value.Date) &&
                (toDate is null || e.Date.Date <= toDate.Value.Date));

            // group by hour
            var byHour = filtered
                .GroupBy(e => new DateTime(e.Date.Year, e.Date.Month, e.Date.Day, e.Date.Hour, 0, 0, e.Date.Kind))
                .OrderBy(g => g.Key)
                .ToList();

            // build x labels
            XLabels = [.. byHour.Select(g => g.Key.ToString("dd.MM.yyyy HH:mm"))];
            OnPropertyChanged(nameof(XLabels));

            // get all types present
            var allTypes = Enum.GetValues<LogType>().ToList();

            var series = new SeriesCollection();
            foreach (var t in allTypes)
            {
                if (t is LogType.Error)
                {
                    var values = new ChartValues<double>(byHour.Select(hour => (double)hour.Count(e => e.Type == t)));
                    series.Add(new LineSeries
                    {
                        Title = t.ToString(),
                        Values = values,
                        Stroke = GetColor(t),
                    }); 
                }
            }

            ChartSeries = series;
        }

        private static System.Windows.Media.SolidColorBrush GetColor(LogType logType)
        {
            return logType switch
            {
                LogType.Info => System.Windows.Media.Brushes.Green,
                LogType.Debug => System.Windows.Media.Brushes.Orange,
                LogType.Error => System.Windows.Media.Brushes.Red,
                _ => System.Windows.Media.Brushes.Gray
            };

        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}