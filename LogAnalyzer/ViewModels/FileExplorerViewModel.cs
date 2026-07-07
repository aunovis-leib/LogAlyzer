using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace LogAnalyzer.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
    public event EventHandler<IReadOnlyList<string>>? FilesSelected;
    public event EventHandler<string>? FileCleared;

    private HashSet<string> _loadedFiles = new(StringComparer.OrdinalIgnoreCase);
    private string _rootPath = string.Empty;
    private bool _syncingSelectedHistoryPath;

    public ObservableCollection<FileSystemItem> Items { get; } = new();

    [ObservableProperty]
    private ObservableCollection<string> _explorerRootFolderHistory = new();

    [ObservableProperty]
    private string? _selectedHistoryPath;

    [ObservableProperty]
    private FileSystemItem? _selectedItem;

    private string _currentPath = string.Empty;
    public string CurrentPath
    {
        get => _currentPath;
        set
        {
            if (SetProperty(ref _currentPath, value))
            {
                AddToHistory(value);
                _syncingSelectedHistoryPath = true;
                try
                {
                    SelectedHistoryPath = value;
                }
                finally
                {
                    _syncingSelectedHistoryPath = false;
                }
            }
        }
    }

    private void AddToHistory(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        if (!ExplorerRootFolderHistory.Any(existing => string.Equals(NormalizePath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            ExplorerRootFolderHistory.Add(normalizedPath);
            SaveHistoryToSettings();
        }
    }

    private void SaveHistoryToSettings()
    {
        var manager = AppSettingsManager.Instance;
        var settingsView = manager.Settings.SettingsView;
        if (settingsView != null)
        {
            settingsView.ExplorerRootFolderHistory = new List<string>(ExplorerRootFolderHistory);
            manager.Save();
        }
    }

    public FileExplorerViewModel()
    {
        var startDir = Directory.GetCurrentDirectory();
        CurrentPath = startDir;
        LoadItems(startDir);
    }

    public void SetExplorerRootFolderHistory(ObservableCollection<string> history)
    {
        DeduplicateHistory(history);
        ExplorerRootFolderHistory = history;
    }

    private static void DeduplicateHistory(ObservableCollection<string> history)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = history.Count - 1; i >= 0; i--)
        {
            var normalized = NormalizePath(history[i]);
            if (string.IsNullOrWhiteSpace(normalized) || !unique.Add(normalized))
            {
                history.RemoveAt(i);
                continue;
            }

            history[i] = normalized;
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var fullPath = Path.GetFullPath(path.Trim());
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim();
        }
    }

    partial void OnSelectedHistoryPathChanged(string? value)
    {
        if (_syncingSelectedHistoryPath)
        {
            return;
        }

        var path = value?.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        LoadItems(path);
    }

    public void SetRootFolder(string? rootFolder)
    {
        var candidate = (rootFolder ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(candidate))
        {
            _rootPath = string.Empty;
            LoadItems(Directory.GetCurrentDirectory());
            return;
        }

        if (!Directory.Exists(candidate))
        {
            return;
        }

        _rootPath = NormalizePath(candidate);
        LoadItems(_rootPath);
    }

    public void LoadItems(string path)
    {
        Items.Clear();
        CurrentPath = path;
        try
        {
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in Directory.GetDirectories(path))
            {
                var fullDirPath = Path.GetFullPath(dir);
                if (seenPaths.Add(fullDirPath))
                {
                    Items.Add(new FileSystemItem(fullDirPath, true, false));
                }
            }

            foreach (var file in Directory.GetFiles(path, "*.log"))
            {
                var fullFilePath = Path.GetFullPath(file);
                if (seenPaths.Add(fullFilePath))
                {
                    Items.Add(new FileSystemItem(fullFilePath, false, _loadedFiles.Contains(fullFilePath)));
                }
            }
        }
        catch { /* Fehler ignorieren, z.B. Zugriffsprobleme */ }
    }

    public void SetLoadedFiles(IEnumerable<string> filePaths)
    {
        _loadedFiles = new HashSet<string>(filePaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(CurrentPath) && Directory.Exists(CurrentPath))
        {
            LoadItems(CurrentPath);
        }
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedItem is { IsDirectory: true, Path: var dirPath })
        {
            LoadItems(dirPath);
            return;
        }

        if (SelectedItem is { IsDirectory: false, Path: var filePath })
        {
            FilesSelected?.Invoke(this, [filePath]);
        }
    }

    public void OpenSelection(IEnumerable<FileSystemItem>? selectedItems)
    {
        var items = selectedItems?.ToList() ?? [];

        if (items.Count == 0 && SelectedItem is not null)
        {
            items.Add(SelectedItem);
        }

        if (items.Count == 1 && items[0].IsDirectory)
        {
            LoadItems(items[0].Path);
            return;
        }

        var files = items
            .Where(x => !x.IsDirectory)
            .Select(x => x.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count > 0)
        {
            FilesSelected?.Invoke(this, files);
        }
    }

    [RelayCommand]
    private void GoUp()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
        {
            return;
        }

        DirectoryInfo? parent;
        try
        {
            parent = Directory.GetParent(CurrentPath);
        }
        catch
        {
            return;
        }

        if (parent is null || !IsWithinRoot(parent.FullName))
        {
            return;
        }

        LoadItems(parent.FullName);
    }

    private bool IsWithinRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(_rootPath))
        {
            return true;
        }

        var normalizedPath = NormalizePath(path);
        if (string.Equals(normalizedPath, _rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootPrefix = _rootPath + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void ClearFile(FileSystemItem? item)
    {
        if (item is null || item.IsDirectory)
            return;

        try
        {
            File.WriteAllText(item.Path, string.Empty);
            FileCleared?.Invoke(this, item.Path);
        }
        catch { /* Fehler ignorieren, z.B. Zugriffsprobleme */ }
    }

    [RelayCommand]
    private void OpenInExplorer(FileSystemItem? item)
    {
        if (item is null)
            return;

        try
        {
            var path = item.Path;
            if (item.IsDirectory)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
        }
        catch { /* Fehler ignorieren */ }
    }
}

public class FileSystemItem
{
    public string Name { get; }
    public string Path { get; }
    public bool IsDirectory { get; }
    public bool IsLoaded { get; }
    public DateTime LastModified { get; }
    public string LastModifiedDisplay => LastModified.ToString("dd.MM.yyyy HH:mm");
    public long? SizeBytes { get; }
    public string SizeDisplay => SizeBytes is null ? string.Empty : FormatSize(SizeBytes.Value);

    public FileSystemItem(string path, bool isDirectory, bool isLoaded)
    {
        Name = System.IO.Path.GetFileName(path);
        Path = path;
        IsDirectory = isDirectory;
        IsLoaded = isLoaded;

        if (isDirectory)
        {
            var info = new DirectoryInfo(path);
            LastModified = info.LastWriteTime;
            SizeBytes = null;
        }
        else
        {
            var info = new FileInfo(path);
            LastModified = info.LastWriteTime;
            SizeBytes = info.Length;
        }
    }

    private static string FormatSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = size;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{value:0} {units[unitIndex]}" : $"{value:0.##} {units[unitIndex]}";
    }
}
