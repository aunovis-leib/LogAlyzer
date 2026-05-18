# 🔍 LogAnalyzer – Log Pattern System

Vollständiges System zur **automatischen Erkennung und Kategorisierung** von Log-Patterns und Fehlerfällen.

## 🎯 Features

### ✨ Pattern-Erkennung
- **YAML-basierte Patterns** mit Regex + Named Capture Groups
- **Automatische Feldextraktion** (Zeitstempel, Exception-Typ, Fehlercode, etc.)
- **Severity-Level** (Critical → Debug)
- **Priority-System** für effiziente Verarbeitung
- **Tag-basierte Kategorisierung**

### 📊 Pattern-Match-Panel (UI)
- **Live-Übersicht** erkannter Matches
- **Filter & Suche** nach Pattern, Severity, Tags
- **Pin-Funktionalität** für wichtige Events
- **CSV-Export** für Auswertung
- **Extrahierte Felder** pro Match anzeigen

### ✏️ Pattern-Editor
- **Visuelle Bearbeitung** mit Testpanel
- **Live-Regex-Test** mit Beispiel-Zeilen
- **CRUD-Operationen** (Create, Read, Update, Delete)
- **Validierung** vor Speicherung

---

## 📁 Projektstruktur

```
LogAnalyzer/
├── Models/
│   ├── LogPattern.cs                 # Pattern-Modelle
│   └── LogFileEntry.cs               # (existierend)
├── Services/
│   ├── LogPatternService.cs          # Pattern-Verwaltung & -Anwendung
│   └── (weitere Services)
├── ViewModels/
│   ├── PatternMatchPanelViewModel.cs # Match-Panel-ViewModel
│   ├── PatternEditorViewModel.cs     # Editor-ViewModel
│   └── (weitere ViewModels)
├── Views/
│   ├── PatternMatchPanelView.xaml    # Match-Panel-UI
│   ├── PatternEditorView.xaml        # Editor-UI
│   └── (weitere Views)
├── LogPatterns/                       # YAML-Template-Verzeichnis
│   ├── null_reference.yaml
│   ├── http_error.yaml
│   ├── database_timeout.yaml
│   ├── out_of_memory.yaml
│   └── (weitere Custom-Patterns)
├── LOG_PATTERN_DOCUMENTATION.md      # Ausführliche Doku
└── PATTERN_QUICK_START.md            # Quick-Start-Guide

LogAnalyzer.Tests/
└── Services/
    └── LogPatternServiceTests.cs     # Unit-Tests
```

---

## 🚀 Getting Started

### 1. Patterns laden (im App-Startup)

```csharp
// App.xaml.cs oder MainViewModel
var patternService = new LogPatternService("LogPatterns");
await patternService.LoadPatternsAsync();
```

### 2. Pattern-Match-Panel anzeigen

```csharp
var matchPanelVM = new PatternMatchPanelViewModel(patternService);
// DataContext = matchPanelVM;
```

### 3. Log-Zeilen verarbeiten

```csharp
var logEntry = new LogFileEntry
{
    Date = DateTime.Now,
    Type = LogType.Error,
    Text = "2024-01-15T14:35:22.123Z System.NullReferenceException: Object reference..."
};

// Patterns automatisch anwenden
var matches = patternService.MatchLine(logEntry);
// → Matches erscheinen im UI Panel
```

---

## 📋 Vordefinierte Patterns

| Pattern | Severity | Erkennt |
|---------|----------|---------|
| **null_reference** | error | C# NullReferenceException |
| **http_error** | error | HTTP 5xx Responses |
| **database_timeout** | warning | DB CommandTimeout |
| **out_of_memory** | critical | OutOfMemoryException |

---

## 🎨 Pattern Erstellen

### Schnell-Vorlage

```yaml
# LogPatterns/my_pattern.yaml
Id: my_unique_id
Name: "Descriptive Name"
Description: "Was erkennt dieses Pattern?"
RegexPattern: '(?<field1>.*) (?<field2>.*)'
Severity: "error"
Tags: [custom, important]
Fields: [field1, field2]
Priority: 75
IsDisabled: false

Action:
  ShowInPanel: true
  Pin: false
  UITag: CustomTag
  NotificationText: null
```

### Regex-Tipps

```regex
(?<timestamp>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})    # ISO DateTime
(?<exception>\w+Exception): (?<message>.*)            # Exception + Message
"(?<method>GET|POST)" (?<url>/\S+) (?<status>\d{3})   # HTTP Request
CommandTimeout=(?<timeout>\d+)                        # Key=Value
```

---

## 🧪 Tests

Alle Tests grün ✅:

```bash
dotnet test LogAnalyzer.Tests
# → 34 Tests Passed
```

Test-Beispiele:
- Pattern speichern & laden
- Regex-Matching
- Feldextraktion
- Filterung nach Tags/Severity
- Pattern löschen

---

## 📊 Architektur

```
LogPatternService (Singleton)
  ├── LoadPatternsAsync()        → YAML einladen
  ├── MatchLine(logEntry)        → Regex testen
  ├── SavePatternAsync(pattern)  → Neue Pattern speichern
  ├── DeletePatternAsync(id)     → Pattern löschen
  ├── FilterByTags(...)          → Tag-basiert filtern
  └── FilterBySeverity(...)      → Severity filtern

PatternMatchPanelViewModel (View-Logic)
  ├── Matches (ObservableCollection)
  ├── SelectedMatch
  ├── FilterText / SelectedSeverity
  └── Commands: Pin, Unpin, Export, Clear

PatternEditorViewModel (Bearbeitungs-Logic)
  ├── CurrentPattern
  ├── TestLine / TestResult
  └── Commands: Add, Save, Delete, Test
```

---

## 🔄 Workflow

### Entwickler: Neues Pattern erstellen

1. Öffne Pattern Editor (UI)
2. Klick "+ Neues Pattern"
3. Trage Daten ein
4. Gib Testzeile ein
5. Klick "Test ausführen"
6. Bei Erfolg: "Speichern"
7. Pattern ist sofort aktiv

### User: Fehlerfälle finden

1. Lade Log-Datei
2. Pattern-Panel zeigt Matches automatisch
3. Filter nach Severity oder Tag
4. Klick auf Match für Details
5. Export via CSV bei Bedarf

---

## 🔧 Konfiguration

### AppSettings (optional)

```json
{
  "PatternDirectory": "LogPatterns",
  "EnablePatternMatching": true,
  "MaxMatchesInPanel": 1000,
  "AutoNotifyOnCritical": true
}
```

### Pattern-Priorität

- **Priority 100+**: Konkrete, spezifische Patterns
- **Priority 50-99**: Standard-Patterns
- **Priority < 50**: Fallback/Generic Patterns

---

## 📈 Performance

- **Regex Compiled**: Pre-compiled für Geschwindigkeit
- **Pattern-Priorität**: Konkrete Patterns zuerst → weniger Tests
- **Event-Based**: `PatternMatched` Event statt Polling
- **Async/Await**: Nicht-blockierendes Laden

---

## 🚦 Best Practices

### ✅ DO
- Datum/Zeit **aus Log-Content** extrahieren (nicht Dateiname!)
- Konkrete Patterns vor generischen
- Aussagekräftige Tags verwenden
- Severity korrekt setzen
- Tests für neue Patterns

### ❌ DON'T
- Zu breite Regex (`.*` überall)
- Falsche Severity-Level
- Duplizierte Patterns
- Performance-intensive Regex ohne Grund

---

## 📚 Dokumentation

- **Ausführlich**: `LOG_PATTERN_DOCUMENTATION.md`
- **Quick-Start**: `PATTERN_QUICK_START.md`
- **API**: Code-Kommentare in `LogPatternService.cs`

---

## 🎓 Beispiel-Use-Cases

### Exception Tracking
```yaml
# Erkenne alle .NET Exceptions
RegexPattern: '(?<time>\d{2}:\d{2}:\d{2}) .* (?<ex>\w+Exception): (?<msg>.*)'
```

### Performance Monitoring
```yaml
# Erkenne langsame Queries
RegexPattern: 'Query executed in (?<duration>\d+)ms'
```

### Security Alerts
```yaml
# Erkenne Failed Logins
RegexPattern: 'Login failed for user (?<user>\S+) from (?<ip>\d+\.\d+\.\d+\.\d+)'
```

### Business Events
```yaml
# Erkenne Payment Failures
RegexPattern: 'Payment (?<id>\d+) failed: (?<reason>.*)'
```

---

## 🤝 Erweiterungen

Geplante Features:
- ✨ **Grok-Pattern-Support** (Logstash-kompatibel)
- 🔔 **Alert-Integration** (Teams, Slack)
- 📊 **Aggregation & Statistik**
- 🌍 **Pattern-Marketplace**
- 🤖 **ML-basiertes Fuzzy Matching**

---

## 📞 Kontakt & Support

- **Bug-Report**: GitHub Issue
- **Feature-Request**: GitHub Discussion
- **Fragen**: Siehe `LOG_PATTERN_DOCUMENTATION.md` FAQ

---

**Status**: ✅ Production-Ready | **Last Updated**: 2024 | **License**: [Your License]
