using System;
using System.Linq;
using System.IO;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using LogAnalyzer.ViewModels;
using Xunit;

namespace LogAnalyzer.Tests
{
    public class LogListViewModelTests
    {
        private static string CreateTempDir(string name)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", name + "_" + Guid.NewGuid().ToString());
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        [Fact]
        public void AvailableTypes_Updates_WhenEntriesAdded()
        {
            var temp = CreateTempDir("types");
            AppSettingsManager.Initialize(temp);
            var vm = new LogListViewModel(AppSettingsManager.Instance, null);

            Assert.Single(vm.AvailableTypes);
            Assert.Equal(LogType.All, vm.AvailableTypes.First());

            vm.LogFilesEntries.Add(new LogFileEntry { Date = DateTime.Now, Type = LogType.Error, Text = "err" });
            vm.LogFilesEntries.Add(new LogFileEntry { Date = DateTime.Now, Type = LogType.Info, Text = "info" });

            // After adding, AvailableTypes should contain All, Error, Info
            Assert.Equal(3, vm.AvailableTypes.Count);
            Assert.Contains(LogType.Error, vm.AvailableTypes);
            Assert.Contains(LogType.Info, vm.AvailableTypes);
        }

        [Fact]
        public void FilterByType_Text_And_Dates_Work()
        {
            var temp = CreateTempDir("filter");
            AppSettingsManager.Initialize(temp);
            var vm = new LogListViewModel(AppSettingsManager.Instance, null);

            var d1 = new DateTime(2024, 1, 1);
            var d2 = new DateTime(2024, 1, 2);
            var e1 = new LogFileEntry { Date = d1, Type = LogType.Error, Text = "hello error" };
            var e2 = new LogFileEntry { Date = d2, Type = LogType.Info, Text = "info message" };

            vm.LogFilesEntries.Add(e1);
            vm.LogFilesEntries.Add(e2);

            // Filter by text
            vm.FilterText = "hello";
            vm.LogFilesView.Refresh();
            var list = vm.LogFilesView.Cast<LogFileEntry>().ToList();
            Assert.Single(list);
            Assert.Equal(e1, list[0]);

            // Filter by type
            vm.FilterText = string.Empty;
            vm.SelectedType = LogType.Info;
            vm.LogFilesView.Refresh();
            list = vm.LogFilesView.Cast<LogFileEntry>().ToList();
            Assert.Single(list);
            Assert.Equal(e2, list[0]);

            // Filter by date range
            vm.SelectedType = LogType.All;
            vm.FilterFromDate = d2;
            vm.FilterToDate = d2;
            vm.LogFilesView.Refresh();
            list = vm.LogFilesView.Cast<LogFileEntry>().ToList();
            Assert.Single(list);
            Assert.Equal(e2, list[0]);
        }

        [Fact]
        public void SelectEntryFromOutside_SelectsClosest_Within_Tolerance()
        {
            var temp = CreateTempDir("select");
            AppSettingsManager.Initialize(temp);
            var vm = new LogListViewModel(AppSettingsManager.Instance, null);

            var now = DateTime.Now;
            var e1 = new LogFileEntry { Date = now, Type = LogType.Info, Text = "one" };
            var e2 = new LogFileEntry { Date = now.AddSeconds(2), Type = LogType.Info, Text = "two" };
            vm.LogFilesEntries.Add(e1);
            vm.LogFilesEntries.Add(e2);

            var external = new LogFileEntry { Date = now.AddSeconds(1) };
            vm.SelectEntryFromOutside(external, TimeSpan.FromSeconds(1.5));

            Assert.NotNull(vm.SelectedEntry);
            // should select the closest (either e1 or e2) - expect e1 or e2; ensure selection is one of them
            Assert.True(vm.SelectedEntry == e1 || vm.SelectedEntry == e2);

            // zero tolerance matches by exact string representation
            var exact = new LogFileEntry { Date = e2.Date };
            vm.SelectEntryFromOutside(exact, TimeSpan.Zero);
            Assert.Equal(e2, vm.SelectedEntry);
        }

        [Fact]
        public void TypesChanged_Event_Is_Raised()
        {
            var temp = CreateTempDir("typeschanged");
            AppSettingsManager.Initialize(temp);
            var vm = new LogListViewModel(AppSettingsManager.Instance, null);

            LogType? last = null;
            vm.TypesChanged += (_, t) => last = t;

            vm.LogFilesEntries.Add(new LogFileEntry { Date = DateTime.Now, Type = LogType.Warning, Text = "w" });

            Assert.Equal(LogType.All, last); // TypesChanged invoked with SelectedType (still All)
        }
    }
}
