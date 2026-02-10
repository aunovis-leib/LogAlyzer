using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using LogAnalyzer.Models;

namespace LogAnalyzer.ViewModels
{
    public class LiveChartViewModel : INotifyPropertyChanged
    {
        private SeriesCollection _chartSeries;
        public SeriesCollection ChartSeries
        {
            get => _chartSeries;
            set { _chartSeries = value; OnPropertyChanged(); }
        }

        private IList<string> _xLabels;
        public IList<string> XLabels
        {
            get => _xLabels;
            set { _xLabels = value; OnPropertyChanged(); }
        }

        public LiveChartViewModel()
        {
            ChartSeries = new SeriesCollection();
            XLabels = new List<string>();
        }

        public void UpdateFromEntries(IEnumerable<LogFileEntry> entries, DateTime? fromDate, DateTime? toDate)
        {
            var filtered = entries.Where(e =>
                (fromDate is null || e.Date.Date >= fromDate.Value.Date) &&
                (toDate is null || e.Date.Date <= toDate.Value.Date));

            // group by day
            var byDay = filtered
                .GroupBy(e => e.Date.Date)
                .OrderBy(g => g.Key)
                .ToList();

            // build x labels
            XLabels = byDay.Select(g => g.Key.ToString("dd.MM.yyyy")).ToList();
            OnPropertyChanged(nameof(XLabels));

            // get all types present
            var allTypes = Enum.GetValues(typeof(LogType)).Cast<LogType>().ToList();

            var series = new SeriesCollection();
            foreach (var t in allTypes)
            {
                var values = new ChartValues<double>();
                foreach (var day in byDay)
                {
                    var count = day.Count(e => e.Type == t);
                    values.Add(count);
                }
                series.Add(new LineSeries
                {
                    Title = t.ToString(),
                    Values = values
                });
            }

            ChartSeries = series;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}