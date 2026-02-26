using LiveCharts;
using LiveCharts.Wpf;
using LogAnalyzer.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace LogAnalyzer.ViewModels
{
    public class LiveChartViewModel : INotifyPropertyChanged
    {
        public LogType? TypeToShow { get; set; }

        private SeriesCollection _chartSeries = new SeriesCollection();
        public SeriesCollection ChartSeries
        {
            get => _chartSeries;
            set { _chartSeries = value; OnPropertyChanged(); }
        }
        private SectionsCollection _sections = new SectionsCollection();
        public SectionsCollection Sections
        {
            get => _sections;
            set { _sections = value; OnPropertyChanged(); }
        }

        private IList<string> _xLabels = new List<string>();
        public IList<string> XLabels
        {
            get => _xLabels;
            set { _xLabels = value; OnPropertyChanged(); }
        }


        public LiveChartViewModel()
        {
            ChartSeries = new SeriesCollection();
            XLabels = new List<string>();
            Sections = new SectionsCollection();
        }

        public void UpdateFromEntries(IEnumerable<LogFileEntry> entries, DateTime? fromDate, DateTime? toDate)
        {
            // Avoid running chart-building logic in the Visual Studio XAML designer
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                // ensure collections exist for the designer
                ChartSeries = new SeriesCollection();
                XLabels = new List<string>();
                Sections = new SectionsCollection();
                return;
            }

            var filtered = entries.Where(e =>
                (fromDate is null || e.Date.Date >= fromDate.Value.Date) &&
                (toDate is null || e.Date.Date <= toDate.Value.Date));

            // group by hour
            var byHour = filtered
                .GroupBy(e => new DateTime(e.Date.Year, e.Date.Month, e.Date.Day, e.Date.Hour, 0, 0, e.Date.Kind))
                .OrderBy(g => g.Key)
                .ToList();

            // build x labels
            XLabels = byHour.Select(g => g.Key.ToString("dd.MM.yyyy HH:mm")).ToList();
            OnPropertyChanged(nameof(XLabels));

            // determine which types to render (fallback to all except All)
            LogType typeToRender;
            if (TypeToShow.HasValue)
            {
                typeToRender = TypeToShow.Value;
            }
            else
            {
                typeToRender = LogType.All;
            }

            var series = new SeriesCollection();

            if (typeToRender != LogType.All)
            {
                var values = new ChartValues<double>(byHour.Select(hour => (double)hour.Count(e => e.Type == typeToRender)));
                series.Add(new LineSeries
                {
                    Title = typeToRender.ToString(),
                    Values = values,
                    Stroke = GetColor(typeToRender),
                });
            }
            else
            {
                // Show all types except All
                foreach (LogType t in Enum.GetValues(typeof(LogType)))
                {
                    if (t == LogType.All) continue;
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
            // Build a single section (width = 1 hour): highlight the last hour that contains an Error
            var sections = new SectionsCollection();
            int lastErrorIndex = -1;
            for (int i = 0; i < byHour.Count; i++)
            {
                if (byHour[i].Any(e => e.Type == LogType.Error))
                    lastErrorIndex = i;
            }

            if (lastErrorIndex >= 0)
            {
                sections.Add(new AxisSection
                {
                    Value = lastErrorIndex,
                    SectionWidth = 2.0,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0)),
                    Stroke = Brushes.Transparent
                });
            }

            Sections = sections;
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