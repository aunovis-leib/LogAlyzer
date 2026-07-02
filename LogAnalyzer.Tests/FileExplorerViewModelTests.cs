using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogAnalyzer.ViewModels;
using Xunit;

namespace LogAnalyzer.Tests;

public class FileExplorerViewModelTests
{
    private static string CreateTempDir(string name)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LogAnalyzerTests", name + "_" + Guid.NewGuid());
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }

        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    [Fact]
    public void LoadItems_ShowsDirectoriesAndOnlyLogFiles()
    {
        var dir = CreateTempDir("explorer_load_items");
        try
        {
            var subDir = Path.Combine(dir, "sub");
            Directory.CreateDirectory(subDir);
            var logFile = Path.Combine(dir, "a.log");
            var txtFile = Path.Combine(dir, "a.txt");
            File.WriteAllText(logFile, "log");
            File.WriteAllText(txtFile, "txt");

            var vm = new FileExplorerViewModel();
            vm.LoadItems(dir);

            Assert.Contains(vm.Items, i => i.IsDirectory && i.Path == subDir);
            Assert.Contains(vm.Items, i => !i.IsDirectory && i.Path == logFile);
            Assert.DoesNotContain(vm.Items, i => i.Path == txtFile);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SetLoadedFiles_MarksLoadedItems()
    {
        var dir = CreateTempDir("explorer_loaded_files");
        try
        {
            var log1 = Path.Combine(dir, "a.log");
            var log2 = Path.Combine(dir, "b.log");
            File.WriteAllText(log1, "a");
            File.WriteAllText(log2, "b");

            var vm = new FileExplorerViewModel();
            vm.LoadItems(dir);
            vm.SetLoadedFiles([log2]);

            var item1 = vm.Items.Single(i => i.Path == log1);
            var item2 = vm.Items.Single(i => i.Path == log2);

            Assert.False(item1.IsLoaded);
            Assert.True(item2.IsLoaded);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void OpenSelection_WithDirectory_NavigatesIntoDirectory()
    {
        var root = CreateTempDir("explorer_open_dir");
        try
        {
            var child = Path.Combine(root, "child");
            Directory.CreateDirectory(child);

            var vm = new FileExplorerViewModel();
            vm.LoadItems(root);

            var selected = vm.Items.Single(i => i.IsDirectory && i.Path == child);
            vm.OpenSelection([selected]);

            Assert.Equal(child, vm.CurrentPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SelectedHistoryPath_NavigatesToExistingFolder()
    {
        var root = CreateTempDir("explorer_history_select");
        var child = Path.Combine(root, "child");
        Directory.CreateDirectory(child);

        try
        {
            var vm = new FileExplorerViewModel();
            vm.SetExplorerRootFolderHistory(new System.Collections.ObjectModel.ObservableCollection<string> { root, child });

            vm.SelectedHistoryPath = child;

            Assert.Equal(child, vm.CurrentPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void OpenSelection_WithMultipleFiles_RaisesFilesSelectedDistinct()
    {
        var dir = CreateTempDir("explorer_multi_select");
        try
        {
            var log1 = Path.Combine(dir, "a.log");
            var log2 = Path.Combine(dir, "b.log");
            File.WriteAllText(log1, "a");
            File.WriteAllText(log2, "b");

            var vm = new FileExplorerViewModel();
            vm.LoadItems(dir);

            IReadOnlyList<string>? raised = null;
            vm.FilesSelected += (_, files) => raised = files;

            var item1 = vm.Items.Single(i => i.Path == log1);
            var item2 = vm.Items.Single(i => i.Path == log2);
            vm.OpenSelection([item1, item2, item1]);

            Assert.NotNull(raised);
            Assert.Equal(2, raised!.Count);
            Assert.Contains(log1, raised);
            Assert.Contains(log2, raised);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SetRootFolder_RestrictsGoUpNavigation()
    {
        var root = CreateTempDir("explorer_root");
        var child = Path.Combine(root, "child");
        Directory.CreateDirectory(child);

        try
        {
            var vm = new FileExplorerViewModel();
            vm.SetRootFolder(root);
            vm.LoadItems(child);

            vm.GoUpCommand.Execute(null);
            Assert.Equal(root, vm.CurrentPath);

            vm.GoUpCommand.Execute(null);
            Assert.Equal(root, vm.CurrentPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ClearFileCommand_ClearsFileContent()
    {
        var dir = CreateTempDir("explorer_clear_file");
        try
        {
            var logFile = Path.Combine(dir, "test.log");
            File.WriteAllText(logFile, "initial content");

            var vm = new FileExplorerViewModel();
            vm.LoadItems(dir);

            var fileItem = vm.Items.Single(i => i.Path == logFile);
            vm.ClearFileCommand.Execute(fileItem);

            var fileContent = File.ReadAllText(logFile);
            Assert.Equal(string.Empty, fileContent);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ClearFileCommand_RaisesFileClearedEvent()
    {
        var dir = CreateTempDir("explorer_clear_event");
        try
        {
            var logFile = Path.Combine(dir, "test.log");
            File.WriteAllText(logFile, "content");

            var vm = new FileExplorerViewModel();
            vm.LoadItems(dir);

            string? clearedFilePath = null;
            vm.FileCleared += (_, filePath) => clearedFilePath = filePath;

            var fileItem = vm.Items.Single(i => i.Path == logFile);
            vm.ClearFileCommand.Execute(fileItem);

            Assert.NotNull(clearedFilePath);
            Assert.Equal(logFile, clearedFilePath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ClearFileCommand_IgnoresDirectories()
    {
        var dir = CreateTempDir("explorer_clear_dir");
        try
        {
            var subDir = Path.Combine(dir, "subdir");
            Directory.CreateDirectory(subDir);

            var vm = new FileExplorerViewModel();
            vm.LoadItems(dir);

            bool eventRaised = false;
            vm.FileCleared += (_, _) => eventRaised = true;

            var dirItem = vm.Items.Single(i => i.IsDirectory && i.Path == subDir);
            vm.ClearFileCommand.Execute(dirItem);

            Assert.False(eventRaised);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ClearFileCommand_IgnoresNullItem()
    {
        var vm = new FileExplorerViewModel();

        bool eventRaised = false;
        vm.FileCleared += (_, _) => eventRaised = true;

        vm.ClearFileCommand.Execute(null);

        Assert.False(eventRaised);
    }

    [Fact]
    public void OpenInExplorerCommand_AcceptsFile()
    {
        var dir = CreateTempDir("explorer_open_file");
        try
        {
            var logFile = Path.Combine(dir, "test.log");
            File.WriteAllText(logFile, "content");

            var vm = new FileExplorerViewModel();
            vm.LoadItems(dir);

            var fileItem = vm.Items.Single(i => !i.IsDirectory && i.Path == logFile);

            // The command should not throw
            vm.OpenInExplorerCommand.Execute(fileItem);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void OpenInExplorerCommand_AcceptsDirectory()
    {
        var root = CreateTempDir("explorer_open_dir");
        try
        {
            var subDir = Path.Combine(root, "subdir");
            Directory.CreateDirectory(subDir);

            var vm = new FileExplorerViewModel();
            vm.LoadItems(root);

            var dirItem = vm.Items.Single(i => i.IsDirectory && i.Path == subDir);

            // The command should not throw
            vm.OpenInExplorerCommand.Execute(dirItem);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void OpenInExplorerCommand_IgnoresNullItem()
    {
        var vm = new FileExplorerViewModel();

        // The command should not throw when given null
        vm.OpenInExplorerCommand.Execute(null);
    }
}
