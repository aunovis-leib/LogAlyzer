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
    }
}
