using LogAnalyzer.Models;
using LogAnalyzer.Services;
using LogAnalyzer.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace LogAnalyzer.Tests
{
    [Collection("AppSettingsManagerSerial")]
    public class MainViewModelTests
    {
        private static string CreateTempDir(string name)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", name + "_" + Guid.NewGuid().ToString());
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        [Fact]
        public void ShowSearchResultsTab_Is_True_Only_With_SearchText()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("main_search_visibility");
                AppSettingsManager.Initialize(temp);
                var vm = new MainViewModel(AppSettingsManager.Instance);

                Assert.False(vm.ShowSearchResultsTab);

                vm.GlobalSearchText = "error";
                Assert.True(vm.ShowSearchResultsTab);

                vm.GlobalSearchText = "   ";
                Assert.False(vm.ShowSearchResultsTab);
            });
        }

        [Fact]
        public void Search_Finds_Matches_In_Text_RawLine_And_Detail_Across_Lists()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("main_search_results");
                AppSettingsManager.Initialize(temp);
                var vm = new MainViewModel(AppSettingsManager.Instance);

                var list1 = vm.Lists[0];
                var entryByText = new LogFileEntry
                {
                    Date = new DateTime(2024, 1, 1, 10, 0, 0),
                    Type = LogType.Info,
                    Text = "contains needle",
                    RawLine = "raw 1"
                };
                list1.LogFilesEntries.Add(entryByText);

                vm.AddListCommand.Execute(null);
                var list2 = vm.Lists[1];
                var entryByRawLine = new LogFileEntry
                {
                    Date = new DateTime(2024, 1, 1, 10, 1, 0),
                    Type = LogType.Warning,
                    Text = "other",
                    RawLine = "RAW Needle line"
                };
                var entryByDetail = new LogFileEntry
                {
                    Date = new DateTime(2024, 1, 1, 10, 2, 0),
                    Type = LogType.Error,
                    Text = "different",
                    RawLine = "raw 3",
                    Detail = new[] { "first", "detail has needle value" }
                };
                list2.LogFilesEntries.Add(entryByRawLine);
                list2.LogFilesEntries.Add(entryByDetail);

                vm.GlobalSearchText = "needle";

                Assert.Equal(3, vm.SearchResults.Count);
                Assert.Contains(entryByText, vm.SearchResults);
                Assert.Contains(entryByRawLine, vm.SearchResults);
                Assert.Contains(entryByDetail, vm.SearchResults);
            });
        }

        [Fact]
        public void Selecting_Search_Result_Synchronizes_Global_SelectedEntry()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("main_search_selection");
                AppSettingsManager.Initialize(temp);
                var vm = new MainViewModel(AppSettingsManager.Instance);

                var entry = new LogFileEntry
                {
                    Date = new DateTime(2024, 1, 1, 9, 0, 0),
                    Type = LogType.Info,
                    Text = "needle"
                };
                vm.Lists[0].LogFilesEntries.Add(entry);

                vm.GlobalSearchText = "needle";
                var result = vm.SearchResults.Single();

                vm.SelectedSearchResult = result;

                Assert.Same(result, vm.SelectedEntryGlobal);
            });
        }

        [Fact]
        public void List_Context_Action_Can_Set_GlobalSearchText()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("main_search_context_action");
                AppSettingsManager.Initialize(temp);
                var vm = new MainViewModel(AppSettingsManager.Instance);

                vm.Lists[0].ApplyGlobalSearchTextCommand.Execute("timeout");

                Assert.Equal("timeout", vm.GlobalSearchText);
                Assert.True(vm.ShowSearchResultsTab);
            });
        }

        [Fact]
        public void List_Context_Action_Can_Set_GlobalSearchText_From_Entry()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("main_search_context_action_entry");
                AppSettingsManager.Initialize(temp);
                var vm = new MainViewModel(AppSettingsManager.Instance);

                var entry = new LogFileEntry
                {
                    Date = new DateTime(2024, 1, 1, 9, 0, 0),
                    Type = LogType.Info,
                    Text = "timeout from entry"
                };

                vm.Lists[0].ApplyGlobalSearchTextCommand.Execute(entry);

                Assert.Equal("timeout from entry", vm.GlobalSearchText);
                Assert.True(vm.ShowSearchResultsTab);
            });
        }

        [Fact]
        public void NavigateToSearchResult_Selects_Entry_In_Owning_List()
        {
            StaTestHelper.Run(() =>
            {
                var temp = CreateTempDir("main_search_navigate_to_result");
                AppSettingsManager.Initialize(temp);
                var vm = new MainViewModel(AppSettingsManager.Instance);

                vm.SettingsVM!.SyncSelectionAcrossLists = false;

                vm.AddListCommand.Execute(null);
                var firstList = vm.Lists[0];
                var secondList = vm.Lists[1];

                var firstEntry = new LogFileEntry
                {
                    Date = new DateTime(2024, 1, 1, 9, 0, 0),
                    Type = LogType.Info,
                    Text = "first"
                };

                var secondEntry = new LogFileEntry
                {
                    Date = new DateTime(2024, 1, 1, 9, 1, 0),
                    Type = LogType.Warning,
                    Text = "second"
                };

                firstList.LogFilesEntries.Add(firstEntry);
                secondList.LogFilesEntries.Add(secondEntry);

                var navigated = vm.NavigateToSearchResult(secondEntry);

                Assert.True(navigated);
                Assert.Same(secondEntry, vm.SelectedEntryGlobal);
                Assert.Same(secondEntry, secondList.SelectedEntry);
                Assert.NotSame(secondEntry, firstList.SelectedEntry);
                Assert.True(secondEntry.IsDetailVisible);
            });
        }
    }
}
