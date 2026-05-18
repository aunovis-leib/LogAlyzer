# 🔌 Integration Guide – Pattern System in bestehende App

Schritt-für-Schritt Anleitung zur Integration des Pattern-Systems in die LogAnalyzer-App.

---

## 1️⃣ App-Startup (App.xaml.cs)

```csharp
using LogAnalyzer.Services;

public partial class App : Application
{
    private LogPatternService? _patternService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Pattern Service initialisieren
        _patternService = new LogPatternService("LogPatterns");
        InitializePatternServiceAsync();
    }

    private async void InitializePatternServiceAsync()
    {
        try
        {
            await _patternService.LoadPatternsAsync();
            Debug.WriteLine("✓ Patterns loaded successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"✗ Error loading patterns: {ex.Message}");
        }
    }

    // In der App-Klasse bereitstellen
    public static LogPatternService? PatternService 
        => ((App)Current)._patternService;
}
```

---

## 2️⃣ MainViewModel anpassen

```csharp
using LogAnalyzer.ViewModels;
using LogAnalyzer.Services;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly LogPatternService _patternService;
    private PatternMatchPanelViewModel? _matchPanelVM;

    public MainViewModel()
    {
        _patternService = App.PatternService 
            ?? throw new InvalidOperationException("Pattern Service not initialized");

        // Match-Panel ViewModel erstellen
        _matchPanelVM = new PatternMatchPanelViewModel(_patternService);
    }

    public PatternMatchPanelViewModel? MatchPanelVM
    {
        get => _matchPanelVM;
        set
        {
            if (_matchPanelVM == value) return;
            _matchPanelVM = value;
            OnPropertyChanged();
        }
    }

    // ... weitere bestehende Code ...
}
```

---

## 3️⃣ MainWindow.xaml anpassen

Füge ein Tab oder ein Panel für die Pattern-Matches hinzu:

```xaml
<!-- In MainWindow.xaml -->
<Window ... xmlns:local="clr-namespace:LogAnalyzer.Views">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="300" MinHeight="100"/>
        </Grid.RowDefinitions>

        <!-- Bestehende Log-View (oben) -->
        <local:LogListView Grid.Row="0"/>

        <!-- Pattern-Match-Panel (unten) -->
        <local:PatternMatchPanelView 
            Grid.Row="1"
            DataContext="{Binding MatchPanelVM}"/>
    </Grid>
</Window>
```

Oder als **Tab-Element**:

```xaml
<TabControl>
    <TabItem Header="Log Entries">
        <local:LogListView/>
    </TabItem>

    <TabItem Header="Pattern Matches">
        <local:PatternMatchPanelView 
            DataContext="{Binding MatchPanelVM}"/>
    </TabItem>

    <TabItem Header="Pattern Editor">
        <local:PatternEditorView 
            DataContext="{Binding PatternEditorVM}"/>
    </TabItem>
</TabControl>
```

---

## 4️⃣ LogListViewModel anpassen

Beim Laden von Log-Entries automatisch Patterns anwenden:

```csharp
public class LogListViewModel : INotifyPropertyChanged
{
    private readonly LogPatternService? _patternService;

    public LogListViewModel()
    {
        _patternService = App.PatternService;
    }

    // Beim Hinzufügen von Log-Entries
    public void AddLogEntries(IEnumerable<LogFileEntry> entries)
    {
        foreach (var entry in entries)
        {
            LogEntries.Add(entry);

            // Patterns anwenden
            if (_patternService != null)
            {
                var matches = _patternService.MatchLine(entry);
                // Matches sind automatisch im MatchPanelViewModel sichtbar
                // weil wir das Event `PatternMatched` abonnieren
            }
        }
    }

    // ... weiterer Code ...
}
```

---

## 5️⃣ Settings/Menü anpassen

Füge einen Menü-Eintrag für den Pattern-Editor hinzu:

```xaml
<!-- In MainWindow.xaml -->
<Menu>
    <MenuItem Header="Tools">
        <MenuItem Header="Pattern Editor" 
                  Command="{Binding OpenPatternEditorCommand}"/>
        <Separator/>
        <MenuItem Header="Reload Patterns" 
                  Command="{Binding ReloadPatternsCommand}"/>
    </MenuItem>
</Menu>
```

---

## 6️⃣ Commands in MainViewModel

```csharp
private RelayCommand? _openPatternEditorCommand;
private RelayCommand? _reloadPatternsCommand;

public ICommand OpenPatternEditorCommand 
    => _openPatternEditorCommand ??= new RelayCommand(_ => 
    {
        // Öffne Pattern-Editor-Fenster
        var editorWindow = new PatternEditorWindow
        {
            DataContext = new PatternEditorViewModel(_patternService)
        };
        editorWindow.ShowDialog();
    });

public ICommand ReloadPatternsCommand 
    => _reloadPatternsCommand ??= new RelayCommand(async _ =>
    {
        await _patternService.LoadPatternsAsync();
        Debug.WriteLine("✓ Patterns reloaded");
    });
```

---

## 7️⃣ Dependency Injection (Optional, Advanced)

Wenn du bereits MVVM-Toolkit oder DI verwendest:

```csharp
// In App.xaml.cs oder Startup-Code
var services = new ServiceCollection();

// Register Pattern Service als Singleton
services.AddSingleton<LogPatternService>(sp => 
{
    var service = new LogPatternService("LogPatterns");
    _ = service.LoadPatternsAsync(); // Fire & Forget für Startup
    return service;
});

// Register ViewModels
services.AddSingleton<PatternMatchPanelViewModel>();
services.AddSingleton<PatternEditorViewModel>();

var provider = services.BuildServiceProvider();

// Später in MainViewModel:
public MainViewModel(LogPatternService patternService)
{
    _patternService = patternService;
    _matchPanelVM = new PatternMatchPanelViewModel(_patternService);
}
```

---

## 8️⃣ Fehlerbehandlung

Robuste Error-Handling:

```csharp
public class MainViewModel : INotifyPropertyChanged
{
    private string _statusMessage = string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private async void InitializePatterns()
    {
        try
        {
            StatusMessage = "Loading patterns...";
            await _patternService.LoadPatternsAsync();
            StatusMessage = $"✓ {_patternService.GetPatterns().Count} patterns loaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Error: {ex.Message}";
            Debug.WriteLine($"Pattern Error: {ex}");
        }
    }
}
```

---

## 9️⃣ Performance-Tipps

### Lazy Loading für Log-Dateien

```csharp
// Laden großer Dateien in Chunks
var chunkLoader = new LogFileChunkLoader(_patternService);

foreach (var chunk in await chunkLoader.LoadChunksAsync(filePath))
{
    foreach (var entry in chunk)
    {
        _patternService.MatchLine(entry);  // Pattern pro Entry
    }
}
```

### Batch-Processing

```csharp
// Mehrere Entries auf einmal verarbeiten
var matches = entries
    .AsParallel()  // Parallel für große Mengen
    .SelectMany(e => _patternService.MatchLine(e))
    .ToList();
```

---

## 🔟 Troubleshooting

| Problem | Lösung |
|---------|--------|
| Patterns nicht gefunden | Überprüfe `LogPatterns/`-Verzeichnis |
| Keine Matches im UI | Prüfe Pattern-Regex mit Test-Panel |
| Performance-Probleme | Reduziere Pattern-Count oder erhöhe Priority |
| YAML parse-Fehler | Überprüfe YAML-Syntax (Indentation!) |
| DLL nicht gefunden | Stelle sicher `YamlDotNet` NuGet installiert |

---

## ✅ Checkliste Integration

- [ ] `LogPatternService` in App-Startup initalisieren
- [ ] `PatternMatchPanelViewModel` in MainViewModel
- [ ] `PatternMatchPanelView` in UI hinzufügen
- [ ] Menu-Eintrag für Pattern-Editor
- [ ] Pattern-Ordner erstellen & Beispiel-Patterns kopieren
- [ ] Tests ausführen
- [ ] UI-Layout testen
- [ ] Error-Handling überprüfen
- [ ] Performance mit großen Log-Dateien testen

---

## 🎯 Nächste Schritte

1. **Customize Patterns**: Eigene Business-Patterns erstellen
2. **Extend UI**: Zusätzliche Filter/Ansichten
3. **Integrate with Notifications**: Alerts bei kritischen Patterns
4. **Export Features**: Weitere Export-Formate (JSON, XML)
5. **Analytics**: Häufigste Patterns dieser Woche/Monat

---

## 📖 Referenzen

- `LOG_PATTERN_DOCUMENTATION.md` – Ausführliche Dokumentation
- `PATTERN_QUICK_START.md` – Schnell-Anleitung Patterns
- `PATTERN_SYSTEM_README.md` – System-Überblick
- Code-Kommentare in `LogPatternService.cs`

