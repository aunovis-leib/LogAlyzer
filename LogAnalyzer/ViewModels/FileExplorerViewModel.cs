using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;

namespace LogAnalyzer.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
    public event EventHandler<IReadOnlyList<string>>? FilesSelected;

    private HashSet<string> _loadedFiles = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<FileSystemItem> Items { get; } = new();

    [ObservableProperty]
    private FileSystemItem? _selectedItem;

    private string _currentPath;
    public string CurrentPath
    {
        get => _currentPath;
        set => SetProperty(ref _currentPath, value);
    }

    public FileExplorerViewModel()
    {
        // Setze Startverzeichnis auf Projektverzeichnis
        var startDir = Directory.GetCurrentDirectory();
        CurrentPath = startDir;
        LoadItems(startDir);
    }

    public void LoadItems(string path)
    {
        Items.Clear();
        CurrentPath = path;
        try
        {
            foreach (var dir in Directory.GetDirectories(path))
                Items.Add(new FileSystemItem(dir, true, false));
            foreach (var file in Directory.GetFiles(path, "*.log"))
                Items.Add(new FileSystemItem(file, false, _loadedFiles.Contains(file)));
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
        var parent = Directory.GetParent(CurrentPath);
        if (parent != null)
            LoadItems(parent.FullName);
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
