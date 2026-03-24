using LogAnalyzer.Models;
using LogAnalyzer.ViewModels;
using NSubstitute;
using System;
using System.Collections.Generic;
using Xunit;

namespace LogAnalyzer.Tests
{
    public class LiveChartViewModelTests
    {
        [Fact]
        public void UpdateFromEntries_GroupsByHour_And_PopulatesSeries()
        {
            // Arrange
            var vm = new LiveChartViewModel
            {
                TypeToShow = null // show all types
            };

            var entriesList = new List<LogFileEntry>
            {
                new() { Date = new DateTime(2024,1,1,10,0,0, DateTimeKind.Utc), Type = LogType.Info },
                new() { Date = new DateTime(2024,1,1,10,30,0, DateTimeKind.Utc), Type = LogType.Error },
                new() { Date = new DateTime(2024,1,1,11,0,0, DateTimeKind.Utc), Type = LogType.Info }
            };

            var entries = Substitute.For<IEnumerable<LogFileEntry>>();
            entries.GetEnumerator().Returns(ci => entriesList.GetEnumerator());

            // Act
            StaTestHelper.Run(() => vm.UpdateFromEntries(entries, null, null));

            // Assert
            Assert.Equal(2, vm.XLabels.Count); // two hourly groups
            // ChartSeries should have one series per LogType except All
            Assert.Equal(Enum.GetValues<LogType>().Length - 1, vm.ChartSeries.Count);
        }
    }
}
