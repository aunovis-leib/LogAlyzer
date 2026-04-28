using System;
using System.Linq;
using System.IO;
using System.Reflection;
using LogAnalyzer.Behaviors;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using LogAnalyzer.ViewModels;
using Xunit;

namespace LogAnalyzer.Tests
{
    [Collection("AppSettingsManagerSerial")]
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

            // Filter by time (prefix match on HH:mm:ss)
            vm.FilterFromDate = null;
            vm.FilterToDate = null;
            vm.FilterTime = "00:00:00";
            vm.LogFilesView.Refresh();
            list = vm.LogFilesView.Cast<LogFileEntry>().ToList();
            Assert.Single(list);
            Assert.Equal(e1, list[0]);

            vm.FilterTime = "00:00:01";
            vm.LogFilesView.Refresh();
            list = vm.LogFilesView.Cast<LogFileEntry>().ToList();
            Assert.Single(list);
            Assert.Equal(e2, list[0]);
        }

        [Fact]
        public void FilterByType_Text_Searches_Details()
        {
            var temp = CreateTempDir("filterdetails");
            AppSettingsManager.Initialize(temp);
            var vm = new LogListViewModel(AppSettingsManager.Instance, null);

            var matchingEntry = new LogFileEntry
            {
                Date = DateTime.Now,
                Type = LogType.Info,
                Text = "main text",
                Detail = ["first detail", "contains needle"]
            };
            var nonMatchingEntry = new LogFileEntry
            {
                Date = DateTime.Now,
                Type = LogType.Info,
                Text = "other main text",
                Detail = ["other detail"]
            };

            vm.LogFilesEntries.Add(matchingEntry);
            vm.LogFilesEntries.Add(nonMatchingEntry);

            vm.FilterText = "needle";
            vm.LogFilesView.Refresh();

            var list = vm.LogFilesView.Cast<LogFileEntry>().ToList();
            Assert.Single(list);
            Assert.Equal(matchingEntry, list[0]);
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

        [Fact]
        public void SelectEntryCommand_Toggles_DetailVisibility_On_Repeated_Click()
        {
            var temp = CreateTempDir("detailtoggle");
            AppSettingsManager.Initialize(temp);
            var vm = new LogListViewModel(AppSettingsManager.Instance, null);

            var entry = new LogFileEntry { Date = DateTime.Now, Type = LogType.Info, Text = "entry" };
            vm.LogFilesEntries.Add(entry);

            Assert.False(entry.IsDetailVisible);

            vm.SelectEntryCommand.Execute(entry);
            Assert.True(entry.IsDetailVisible);
            Assert.Equal(entry, vm.SelectedEntry);

            vm.SelectEntryCommand.Execute(entry);
            Assert.False(entry.IsDetailVisible);
            Assert.Equal(entry, vm.SelectedEntry);
        }

        [Fact]
        public void SelectEntryFromOutside_Expands_Detail_For_Selected_Entry()
        {
            var temp = CreateTempDir("detailfromoutside");
            AppSettingsManager.Initialize(temp);
            var vm = new LogListViewModel(AppSettingsManager.Instance, null);

            var now = DateTime.Now;
            var entry = new LogFileEntry { Date = now, Type = LogType.Info, Text = "entry" };
            vm.LogFilesEntries.Add(entry);

            Assert.False(entry.IsDetailVisible);

            vm.SelectEntryFromOutside(new LogFileEntry { Date = now }, TimeSpan.Zero);

            Assert.Equal(entry, vm.SelectedEntry);
            Assert.True(entry.IsDetailVisible);
        }

        [Fact]
        public void TimeInputBehavior_Normalize_HourAndMinute_DoesNotAppendSeconds()
        {
            var method = typeof(TimeInputBehavior).GetMethod(
                "TryNormalizeToHMS",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            var result = method!.Invoke(null, ["05:33"]) as string;

            Assert.Equal("05:33", result);
        }

        [Fact]
        public void TimeInputBehavior_AutoInsertSecondSeparator_WhenTypingAfterHourMinute()
        {
            var method = typeof(TimeInputBehavior).GetMethod(
                "TryAutoInsertSecondSeparator",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            var tb = new System.Windows.Controls.TextBox
            {
                Text = "05:33"
            };
            tb.SelectionStart = tb.Text.Length;
            tb.SelectionLength = 0;

            var handled = (bool?)method!.Invoke(null, [tb, "1"]);

            Assert.True(handled);
            Assert.Equal("05:33:1", tb.Text);
            Assert.Equal(tb.Text.Length, tb.SelectionStart);
        }

        [Fact]
        public void TimeInputBehavior_AutoInsertSeparator_WhenTypingAfterHour()
        {
            var method = typeof(TimeInputBehavior).GetMethod(
                "TryAutoInsertSecondSeparator",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            var tb = new System.Windows.Controls.TextBox
            {
                Text = "05"
            };
            tb.SelectionStart = tb.Text.Length;
            tb.SelectionLength = 0;

            var handled = (bool?)method!.Invoke(null, [tb, "3"]);

            Assert.True(handled);
            Assert.Equal("05:3", tb.Text);
            Assert.Equal(tb.Text.Length, tb.SelectionStart);
        }
    }
}
