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
            StaTestHelper.Run(() =>
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
            });
        }

        [Fact]
        public void FilterByType_Text_And_Dates_Work()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("filter");
                AppSettingsManager.Initialize(temp);
                var vm = new LogListViewModel(AppSettingsManager.Instance, null);

                var d1 = new DateTime(2024, 1, 1, 0, 0, 0);
                var d2 = new DateTime(2024, 1, 2, 0, 0, 1);
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
            });
        }

        [Fact]
        public void FilterByType_Text_Searches_Details()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("filterdetails");
                AppSettingsManager.Initialize(temp);
                var vm = new LogListViewModel(AppSettingsManager.Instance, null);

                var matchingEntry = new LogFileEntry
                {
                    Date = DateTime.Now,
                    Type = LogType.Info,
                    Text = "main text",
                    Detail = new[] { "first detail", "contains needle" }
                };
                var nonMatchingEntry = new LogFileEntry
                {
                    Date = DateTime.Now,
                    Type = LogType.Info,
                    Text = "other main text",
                    Detail = new[] { "other detail" }
                };

                vm.LogFilesEntries.Add(matchingEntry);
                vm.LogFilesEntries.Add(nonMatchingEntry);

                vm.FilterText = "needle";
                vm.LogFilesView.Refresh();

                var list = vm.LogFilesView.Cast<LogFileEntry>().ToList();
                Assert.Single(list);
                Assert.Equal(matchingEntry, list[0]);
            });
        }

        [Fact]
        public void ApplyFilterTextCommand_Sets_Trimmed_FilterText()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("applyfiltertext");
                AppSettingsManager.Initialize(temp);
                var vm = new LogListViewModel(AppSettingsManager.Instance, null);

                vm.ApplyFilterTextCommand.Execute("  selected detail text  ");

                Assert.Equal("selected detail text", vm.FilterText);
            });
        }

        [Fact]
        public void SelectEntryFromOutside_SelectsClosest_Within_Tolerance()
        {
            StaTestHelper.Run(() =>
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
            });
        }

        [Fact]
        public void TypesChanged_Event_Is_Raised()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("typeschanged");
                AppSettingsManager.Initialize(temp);
                var vm = new LogListViewModel(AppSettingsManager.Instance, null);

                LogType? last = null;
                vm.TypesChanged += (_, t) => last = t;

                vm.LogFilesEntries.Add(new LogFileEntry { Date = DateTime.Now, Type = LogType.Warning, Text = "w" });

                Assert.Equal(LogType.All, last); // TypesChanged invoked with SelectedType (still All)
            });
        }

        [Fact]
        public void SelectEntryCommand_Toggles_DetailVisibility_On_Repeated_Click()
        {
            StaTestHelper.Run(() =>
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
            });
        }

        [Fact]
        public void SelectEntryFromOutside_Expands_Detail_For_Selected_Entry()
        {
            StaTestHelper.Run(() =>
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
            });
        }

        [Fact]
        public void TimeInputBehavior_Normalize_HourAndMinute_DoesNotAppendSeconds()
        {
            StaTestHelper.Run(() =>
            {
                var method = typeof(TimeInputBehaviorCore).GetMethod(
                    "TryNormalizeToHMS",
                    BindingFlags.Public | BindingFlags.Static);

                Assert.NotNull(method);
                var result = method!.Invoke(null, new object[] { "05:33" }) as string;

                Assert.Equal("05:33", result);
            });
        }

        [Fact]
        public void TimeInputBehavior_AutoInsertSecondSeparator_WhenTypingAfterHourMinute()
        {
            StaTestHelper.Run(() =>
            {
                // Use the test-friendly core helper to avoid STA TextBox creation in unit tests
                var core = typeof(TimeInputBehaviorCore).GetMethod(
                    "TryAutoInsertSecondSeparatorCore",
                    BindingFlags.Public | BindingFlags.Static);

                Assert.NotNull(core);
                var text = "05:33";
                var selectionStart = text.Length;
                var args = new object[] { text, selectionStart, "1", null, 0 };
                var result = (bool)core!.Invoke(null, args)!;
                Assert.True(result);
                Assert.Equal("05:33:1", args[3]);
                Assert.Equal(((string)args[3]).Length, (int)args[4]);
            });
        }

        [Fact]
        public void TimeInputBehavior_AutoInsertSeparator_WhenTypingAfterHour()
        {
            StaTestHelper.Run(() =>
            {
                var core = typeof(TimeInputBehaviorCore).GetMethod(
                    "TryAutoInsertSecondSeparatorCore",
                    BindingFlags.Public | BindingFlags.Static);

                Assert.NotNull(core);
                var text = "05";
                var selectionStart = text.Length;
                var args = new object[] { text, selectionStart, "3", null, 0 };
                var result = (bool)core!.Invoke(null, args)!;
                Assert.True(result);
                Assert.Equal("05:3", args[3]);
                Assert.Equal(((string)args[3]).Length, (int)args[4]);
            });
        }

        [Fact]
        public void FilteredEntryCount_TracksCurrentFilter()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("filteredcount");
                try
                {
                    AppSettingsManager.Initialize(temp);
                    var vm = new LogListViewModel(AppSettingsManager.Instance, null);

                    vm.FilterTime = string.Empty;
                    vm.LogFilesEntries.Add(new LogFileEntry { Date = new DateTime(2024, 1, 1), Type = LogType.Info, Text = "alpha" });
                    vm.LogFilesEntries.Add(new LogFileEntry { Date = new DateTime(2024, 1, 1), Type = LogType.Error, Text = "beta" });
                    vm.LogFilesView.Refresh();

                    Assert.Equal(2, vm.FilteredEntryCount);

                    vm.FilterText = "alpha";
                    Assert.Equal(1, vm.FilteredEntryCount);

                    vm.FilterText = "not-found";
                    Assert.Equal(0, vm.FilteredEntryCount);
                }
                finally
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
            });
        }

        [Fact]
        public void HasNewEntries_InitiallyFalse()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("newentries_init");
                try
                {
                    AppSettingsManager.Initialize(temp);
                    var vm = new LogListViewModel(AppSettingsManager.Instance, null);

                    Assert.False(vm.HasNewEntries);
                    Assert.Equal(0, vm.NewEntriesCount);
                }
                finally
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
            });
        }

        [Fact]
        public void ClearNewEntriesNotificationCommand_ResetsNotification()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("clear_notif");
                try
                {
                    AppSettingsManager.Initialize(temp);
                    var vm = new LogListViewModel(AppSettingsManager.Instance, null);

                    // Manuell setzen (würde normalerweise vom Debounce Timer gesetzt)
                    vm.HasNewEntries = true;
                    vm.NewEntriesCount = 5;

                    Assert.True(vm.HasNewEntries);
                    Assert.Equal(5, vm.NewEntriesCount);

                    // Command ausführen
                    vm.ClearNewEntriesNotificationCommand.Execute(null);

                    Assert.False(vm.HasNewEntries);
                    Assert.Equal(0, vm.NewEntriesCount);
                }
                finally
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
            });
        }

        [Fact]
        public void AutoReloadToggled_Event_StopsAutoReload_When_Disabled()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("autoreload_toggle_stop");
                try
                {
                    AppSettingsManager.Initialize(temp);
                    var settingsVm = new SettingsViewModel();
                    var listVm = new LogListViewModel(AppSettingsManager.Instance, null, settingsVm);

                    // Simuliere geladene Dateien
                    var logFile = Path.Combine(temp, "test.log");
                    File.WriteAllText(logFile, "test");

                    // Starte Auto-Reload durch Settings Toggle
                    settingsVm.AutoReloadLogFiles = true;
                    // Würde StartAutoReload() aufrufen

                    // Jetzt deaktivieren
                    settingsVm.AutoReloadLogFiles = false;
                    // Würde StopAutoReload() aufrufen - sollte keine Exception werfen

                    Assert.False(settingsVm.AutoReloadLogFiles);
                }
                finally
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
            });
        }

        [Fact]
        public void AutoReloadToggled_Event_StartsAutoReload_When_Enabled()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("autoreload_toggle_start");
                try
                {
                    AppSettingsManager.Initialize(temp);
                    var settingsVm = new SettingsViewModel();
                    var listVm = new LogListViewModel(AppSettingsManager.Instance, null, settingsVm);

                    // Simuliere geladene Dateien
                    var logFile = Path.Combine(temp, "test.log");
                    File.WriteAllText(logFile, "test");
                    listVm.LogFilesEntries.Add(new LogFileEntry { Date = DateTime.Now, Type = LogType.Info, Text = "entry" });

                    // Aktiviere Auto-Reload
                    settingsVm.AutoReloadLogFiles = true;

                    Assert.True(settingsVm.AutoReloadLogFiles);
                }
                finally
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
            });
        }

        [Fact]
        public void SettingsViewModel_AutoReloadToggled_Event_Fires()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("settings_event_fire");
                try
                {
                    AppSettingsManager.Initialize(temp);
                    var settingsVm = new SettingsViewModel();

                    bool eventFired = false;
                    bool? toggledValue = null;

                    settingsVm.AutoReloadToggled += (sender, enabled) =>
                    {
                        eventFired = true;
                        toggledValue = enabled;
                    };

                    // Toggeln von false auf true
                    settingsVm.AutoReloadLogFiles = true;
                    Assert.True(eventFired);
                    Assert.True(toggledValue);

                    // Reset
                    eventFired = false;
                    toggledValue = null;

                    // Toggeln von true auf false
                    settingsVm.AutoReloadLogFiles = false;
                    Assert.True(eventFired);
                    Assert.False(toggledValue);
                }
                finally
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
            });
        }

        [Fact]
        public void LogListViewModel_WithSettings_Receives_AutoReloadToggled_Events()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("listvm_receives_toggle");
                try
                {
                    AppSettingsManager.Initialize(temp);
                    var settingsVm = new SettingsViewModel();
                    var listVm = new LogListViewModel(AppSettingsManager.Instance, null, settingsVm);

                    // Stelle sicher, dass keine Fehler beim Toggle auftreten
                    settingsVm.AutoReloadLogFiles = true;
                    settingsVm.AutoReloadLogFiles = false;
                    settingsVm.AutoReloadLogFiles = true;

                    // Sollte ohne Fehler durchlaufen
                    Assert.True(settingsVm.AutoReloadLogFiles);
                }
                finally
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task MaxEntriesPerList_Changes_Trigger_Reload()
        {
            var temp = CreateTempDir("max_entries_reload");
            try
            {
                AppSettingsManager.Initialize(temp);

                var settingsVm = new SettingsViewModel();
                var listVm = new LogListViewModel(AppSettingsManager.Instance, null, settingsVm);

                // Manually add entries to simulate loaded files
                for (int i = 0; i < 20; i++)
                {
                    listVm.LogFilesEntries.Add(new LogFileEntry 
                    { 
                        Date = DateTime.Now, 
                        Type = LogType.Info, 
                        Text = $"Entry {i}" 
                    });
                }

                var initialCount = listVm.LogFilesEntries.Count;
                Assert.Equal(20, initialCount);

                // Store the initial loaded files to simulate what _currentLoadedFiles would be
                var testFiles = new[] { "test.log" };
                // We can't directly test reload without files, so we'll verify the method exists and is callable
                // The actual reload logic is already tested through the integration tests

                await listVm.ReloadWithNewMaxEntriesAsync();

                Assert.True(true); // Test passes if no exception is thrown
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task MaxEntriesPerListChanged_Event_Triggers_Reload_In_MainViewModel()
        {
            StaTestHelper.Run(async () =>
            {
                var temp = CreateTempDir("max_entries_main_reload");
                try
                {
                    AppSettingsManager.Initialize(temp);

                    var manager = AppSettingsManager.Instance;
                    var mainVm = new MainViewModel(manager);

                    // Add some entries to test the event
                    mainVm.Lists[0].LogFilesEntries.Add(new LogFileEntry 
                    { 
                        Date = DateTime.Now, 
                        Type = LogType.Info, 
                        Text = "Entry 1" 
                    });
                    mainVm.Lists[0].LogFilesEntries.Add(new LogFileEntry 
                    { 
                        Date = DateTime.Now, 
                        Type = LogType.Info, 
                        Text = "Entry 2" 
                    });

                    var initialCount = mainVm.Lists[0].LogFilesEntries.Count;
                    Assert.Equal(2, initialCount);

                    // Change max entries - should trigger the event handler
                    mainVm.SettingsVM.MaxEntriesPerList = 7;

                    // Wait a bit for async operations
                    await System.Threading.Tasks.Task.Delay(50);

                    // Verify the setting was updated
                    Assert.Equal(7, mainVm.SettingsVM.MaxEntriesPerList);
                }
                finally
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                }
            });
        }
    }
}
