# Log Pattern & Template System – Dokumentation

## 📋 Übersicht

Das Log Pattern System ermöglicht es, **wiederverwendbare Templates** für Log-Muster zu definieren, um automatisch spezifische Use-Cases oder Fehlerfälle in Log-Dateien zu erkennen und hervorzuheben.

**Komponenten:**
- **Pattern-Modelle** (`LogPattern.cs`): Datenstruktur für Pattern-Definitionen
- **Pattern-Service** (`LogPatternService.cs`): Verwaltung & Anwendung von Patterns
- **ViewModels** für UI-Interaktion
- **YAML-Templates** in `LogPatterns/`-Verzeichnis

---

## 🎯 Kernkonzepte

### 1. **Pattern-Struktur** (YAML)

```yaml
Id: unique_pattern_id
Name: "Anzeigename"
Description: "Was erkennt dieses Pattern?"
RegexPattern: '(?<field1>\d+) .* (?<field2>.*)'
Severity: "error|warning|info|debug|critical"
Tags: [tag1, tag2, ...]
Fields: [field1, field2, ...]
Priority: 100  # höher = zuerst geprüft
IsDisabled: false

Action:
  ShowInPanel: true        # In Pattern-Match-Panel zeigen
  Pin: false               # Automatisch pinnen?
  UITag: CustomTag         # Für Filterung
  NotificationText: "..."  # Benachrichtigung (null = keine)
```

### 2. **Regex mit Named Capture Groups**

Die Regex-Pattern verwenden **named capture groups**, um automatisch Felder zu extrahieren:

```regex
(?<timestamp>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})  # Zeitstempel
(?<exception>\w+Exception)                         # Exception-Typ
(?<message>.*)                                     # Fehlermeldung
```

### 3. **Severity-Level**

- `critical` 🔴 – System-Fehler, sofort überprüfen
- `error` 🟠 – Schwerer Fehler
- `warning` 🟡 – Warnung
- `info` 🔵 – Information
- `debug` ⚪ – Debug-Ausgabe

---

## 📂 YAML-Template-Beispiele

### Beispiel 1: NullReferenceException

```yaml
# LogPatterns/null_reference.yaml
Id: NullReferenceException
Name: Null Reference Exception
Description: Erkennt C# NullReferenceException-Fehler
RegexPattern: "(?<timestamp>\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}.*?) .* (System\\.)?NullReferenceException: (?<message>.*)"
Severity: error
Tags:
  - exception
  - null
  - csharp
Fields:
  - timestamp
  - message
Action:
  ShowInPanel: true
  Pin: true
  UITag: NullRef
  NotificationText: NullReferenceException erkannt
Priority: 100
IsDisabled: false
```

### Beispiel 2: HTTP 5xx Fehler

```yaml
# LogPatterns/http_error.yaml
Id: HttpErrorResponse
Name: HTTP Error Response (5xx)
Description: Erkennt HTTP 5xx Server-Fehler
RegexPattern: "(?<timestamp>\\d{2}:\\d{2}:\\d{2}) .* \"(?<method>GET|POST|PUT|DELETE|PATCH) (?<url>/[^\\s]*) (?<protocol>HTTP/[\\d.]+)\" (?<status>[5]\\d{2}) (?<size>\\d+)"
Severity: error
Tags:
  - http
  - error
  - server
Fields:
  - timestamp
  - method
  - url
  - status
  - size
Action:
  ShowInPanel: true
  Pin: false
  UITag: Http5xx
  NotificationText: HTTP 5xx Fehler erkannt
Priority: 90
IsDisabled: false
```

### Beispiel 3: Datenbank-Timeout

```yaml
# LogPatterns/database_timeout.yaml
Id: DatabaseTimeout
Name: Database Command Timeout
Description: Erkennt Datenbank-Timeout-Fehler
RegexPattern: "(?<timestamp>\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}.*?) .* Timeout .* CommandTimeout=(?<timeout>\\d+) ms"
Severity: warning
Tags:
  - database
  - timeout
  - performance
Fields:
  - timestamp
  - timeout
Action:
  ShowInPanel: true
  Pin: false
  UITag: DbTimeout
  NotificationText: null
Priority: 80
IsDisabled: false
```

---

## 🔧 Verwendung im Code

### Pattern-Service initialisieren

```csharp
var patternService = new LogPatternService("LogPatterns");
await patternService.LoadPatternsAsync();
```

### Patterns auf Log-Zeile anwenden

```csharp
var logEntry = new LogFileEntry
{
    Date = DateTime.Now,
    Type = LogType.Error,
    Text = "2024-01-15T14:35:22.123Z ... System.NullReferenceException: Object reference not set"
};

var matches = patternService.MatchLine(logEntry);

foreach (var match in matches)
{
    Console.WriteLine($"Pattern: {match.Pattern.Name}");
    Console.WriteLine($"Severity: {match.Pattern.Severity}");

    foreach (var field in match.ExtractedFields)
    {
        Console.WriteLine($"  {field.Key}: {field.Value}");
    }
}
```

### Patterns filtern

```csharp
// Nach Tags filtern
var exceptionPatterns = patternService.FilterByTags("exception");

// Nach Severity filtern
var errorPatterns = patternService.FilterBySeverity("error");

// Nach kritischen Patterns
var criticalPatterns = patternService.GetPatterns()
    .Where(p => p.Severity == "critical");
```

### Pattern erstellen & speichern

```csharp
var newPattern = new LogPattern
{
    Id = "custom_pattern",
    Name = "Custom Error Pattern",
    Description = "Erkennt spezifische Fehlermeldungen",
    RegexPattern = @"(?<timestamp>\d{2}:\d{2}:\d{2}) ERROR: (?<code>\d+) - (?<message>.*)",
    Severity = "error",
    Priority = 75
};

newPattern.Tags.Add("custom");
newPattern.Tags.Add("business-logic");
newPattern.Fields.Add("timestamp");
newPattern.Fields.Add("code");
newPattern.Fields.Add("message");

await patternService.SavePatternAsync(newPattern);
```

### Pattern löschen

```csharp
await patternService.DeletePatternAsync("pattern_id");
```

---

## 🎨 UI – Pattern-Match-Panel (Point 5)

### Features

1. **Live-Anzeige erkannter Matches**
   - Neue Matches erscheinen oben in der Liste
   - Automatisches Highlighting nach Severity-Level
   - Pin-Funktionalität für wichtige Matches

2. **Filter & Suche**
   - Textsuche in Log-Zeilen und Pattern-Namen
   - Severity-Filter (All, Debug, Info, Warning, Error, Critical)
   - Tag-basierte Filterung

3. **Aktionen**
   - **Pin/Unpin** – Wichtige Matches markieren
   - **Unpin All** – Alle Pins entfernen
   - **Export CSV** – Matches exportieren
   - **Clear All** – List leeren

4. **Extrahierte Felder anzeigen**
   - Pro Match: alle aus Regex extrahierten Feldwerte
   - Farblich gekennzeichnet
   - Klickbar für Drilldown (optional)

### Integration im MainViewModel

```csharp
// Im MainViewModel oder App.xaml.cs:
var patternService = new LogPatternService("LogPatterns");
await patternService.LoadPatternsAsync();

var matchPanelVM = new PatternMatchPanelViewModel(patternService);

// Bei jedem neuen Log-Entry:
foreach (var logEntry in newLogEntries)
{
    patternService.MatchLine(logEntry);
}
```

---

## 📝 Pattern-Editor-View

### Features

- **Pattern-Liste** (links): Alle definierten Patterns
- **Editor** (rechts): Felder bearbeiten
  - ID, Name, Beschreibung
  - Regex Pattern (mit Syntax-Highlighting optional)
  - Severity, Priority
  - Tags & Felder

- **Test-Panel**
  - Testzeile eingeben
  - Pattern testen
  - Extrahierte Felder anzeigen

- **Buttons**
  - Neues Pattern
  - Speichern
  - Löschen

### Workflow

1. Öffne "Pattern Editor" aus Settings/Tools-Menü
2. Klick "+ Neues Pattern"
3. Fülle Felder aus
4. Gib Testzeile ein
5. Klick "Test ausführen"
6. Bei Erfolg: "Speichern"

---

## 🧪 Unit-Tests

Tests befinden sich in `LogAnalyzer.Tests\Services\LogPatternServiceTests.cs`:

```csharp
// Pattern speichern
await service.SavePatternAsync(pattern);

// Matches finden
var matches = service.MatchLine(logEntry);
Assert.NotEmpty(matches);

// Filtern nach Tags
var filtered = service.FilterByTags("exception");

// Filtern nach Severity
var errors = service.FilterBySeverity("error");

// Pattern löschen
await service.DeletePatternAsync(patternId);
```

---

## 🚀 Best Practices

### 1. **Regex-Optimierung**
- Nutze konkrete Patterns statt `.*` am Anfang
- Non-capturing groups `(?:...)` für Gruppen, die nicht extrahiert werden
- Escap special characters: `\.` statt `.`

### 2. **Priorisierung**
- Konkrete Patterns (Priority 100+) vor generischen (Priority < 50)
- `OutOfMemoryException` Priority 200 (kritisch)
- Standard-Exceptions Priority 80-100

### 3. **Tags verwenden**
- Kategorisiere nach: `exception`, `http`, `database`, `performance`
- Verwende Domain-Tags: `auth`, `payment`, `integration`
- Nutze für UI-Filterung

### 4. **Felder extrahieren**
- Definiere nur relevante Felder
- Timestamp immer extrahieren (für Log-Correlations-Tools)
- Error-Code/Message für Troubleshooting

### 5. **Datumsformate**
- **Wichtig:** Datum/Zeit aus **Log-Inhalt** extrahieren, nicht aus Dateiname
- Häufige Formate:
  - ISO 8601: `2024-01-15T14:35:22.123Z`
  - Zeit-nur: `14:35:22.123`
  - Deutsche: `15.01.2024 14:35:22`

---

## 📊 Erweiterungsmöglichkeiten

### Geplant

- **Grok-Pattern-Support**: Logstash-kompatible Patterns
- **Pattern-Marketplace**: Vordefinierte Templates teilen
- **Fuzzy Matching**: Variierende Log-Formate erkennen
- **Aggregation**: Häufigste Matches dieser Stunde
- **Alert-Integration**: Teams/Slack-Benachrichtigungen
- **Dark Mode** für Pattern-Editor

---

## ❓ FAQ

**F: Kann ich mein eigenes Pattern-Format verwenden?**
A: Ja, aber derzeit sind YAML + Regex standard. Für JSON würde man nur `YamlDotNet` durch `System.Text.Json` ersetzen.

**F: Wie handle ich mehrzeilige Log-Einträge?**
A: Log-Entries werden hier zeilenweise verarbeitet. Für mehrzeilige Entries (Stacktraces): nutze `LogFileEntry.Detail[]` um Stack-Frames zu speichern.

**F: Kann ich Pattern-Matches in CSV exportieren?**
A: Ja! Der Export-Button im Match-Panel erzeugt eine CSV mit Timestamp, Pattern, Severity, Text und extrahierten Feldern.

**F: Laufen die Regex-Tests bei jedem Log-Entry?**
A: Ja, alle aktivierten Pattern werden geprüft (sortiert nach Priority). Performance ist optimiert durch `.Compiled` Regex.

---

## 📞 Support

Bei Fragen oder Bugs:
- Öffne ein GitHub Issue
- Teile dein Pattern & Testzeile

