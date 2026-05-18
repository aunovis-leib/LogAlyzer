# 🎯 Pattern Editor – Aufruf & Bedienung

## 📍 WO wird der Editor aufgerufen?

### 1. **Im Settings-Panel** (Hauptzugang)

```
┌─────────────────────────────────────────┐
│   LogAlyzer Main Window                 │
├─────────────────────────────────────────┤
│                                         │ ⚙️ Settings-Button
│   [Log Lists hier]                      │   (rechts oben)
│                                         │
│                                         │
│     ┌──────────────────────────────┐   │
│     │  Settings Panel (ausfahr)    │   │
│     │  ⚙️ Settings                 │   │
│     │  ───────────────────────────  │   │
│     │  List Settings               │   │
│     │  Live chart: ☐              │   │
│     │  ───────────────────────────  │   │
│     │  Sync Tolerance              │   │
│     │  ───────────────────────────  │   │
│     │  📋 Log Pattern Editor       │◄──┼─ HIER!
│     │  📋 Open Pattern Editor      │   │
│     │  Create, edit, and test...   │   │
│     │  ───────────────────────────  │   │
│     │  [Reset defaults]            │   │
│     └──────────────────────────────┘   │
│                                         │
└─────────────────────────────────────────┘
```

### 2. **Technischer Ablauf**

```
User klickt auf Button
   ↓
"📋 Open Pattern Editor" Button
   ↓
SettingsViewModel.OpenPatternEditorCommand
   ↓
App.PatternService wird geladen
   ↓
PatternEditorViewModel wird erstellt
   ↓
PatternEditorView wird in neuem Window angezeigt
   ↓
Modales Dialog-Fenster (ShowDialog())
```

---

## 🚀 HOW – Schritt-für-Schritt Bedienung

### Schritt 1: Öffnen Sie den Editor

```
1. LogAnalyzer Hauptfenster öffnen
2. Klick auf ⚙️ SETTINGS (rechts oben)
3. Scrollen Sie nach unten zum Abschnitt:
   "📋 Log Pattern Editor"
4. Klick auf Button: "📋 Open Pattern Editor"
```

**Result:** Ein neues Fenster öffnet sich:
```
╔════════════════════════════════════════════════════════╗
║ Log Pattern Editor                                     ║
╠════════════════════════════════════════════════════════╣
║                                                        ║
║  [Patterns] (links)         [Editor] (rechts)         ║
║  ─────────────────         ─────────────────────      ║
║  • null_reference            ID: ___________          ║
║  • http_error                Name: _________         ║
║  • database_timeout          Beschreibung: ___       ║
║  • out_of_memory             Regex: [Monospace]      ║
║  + Neues Pattern             Severity: [error ▼]    ║
║                              Priority: [===50===]    ║
║                              Tags: ___________       ║
║                              ───────────────────     ║
║                              Test-Panel:             ║
║                              Testzeile: __________   ║
║                              [Test ausführen]        ║
║                              ───────────────────     ║
║                              [Speichern] [Löschen]   ║
║                                                       ║
╚════════════════════════════════════════════════════════╝
```

### Schritt 2: Pattern auswählen oder neu erstellen

**Bestehendes Pattern bearbeiten:**
```
1. Links in der Liste: Pattern klicken (z.B. "null_reference")
2. Rechts erscheinen die Details
3. Felder anpassen
4. [Speichern]
```

**Neues Pattern erstellen:**
```
1. Links: Klick auf "+ Neues Pattern"
2. Neuer leerer Editor rechts
3. Fülle alle Felder aus
4. Testzeile eingeben & testen
5. [Speichern]
```

### Schritt 3: Pattern-Felder ausfüllen

| Feld | Beispiel | Beschreibung |
|------|----------|-------------|
| **ID** | `null_reference` | Eindeutige ID (Auto-generiert wenn neu) |
| **Name** | `Null Reference Exception` | Anzeigename |
| **Beschreibung** | `Erkennt C# NullRef...` | Wofür das Pattern? |
| **Regex** | `(?<ex>\w+Exception): (?<msg>.*)` | Mit Named Groups! |
| **Severity** | `error` | critical, error, warning, info, debug |
| **Priority** | `100` | Höher = zuerst getestet (0-200) |
| **Tags** | `exception, null` | Komma-getrennt |

### Schritt 4: Regex testen

```
1. Testzeile eingeben (copy-paste aus realem Log):
   "14:35:22 System.NullReferenceException: Object reference..."

2. Klick "[Test ausführen]"

3. Ergebnis unten:
   ✓ Match erfolgreich!

   Extrahierte Felder:
     ex: NullReferenceException
     msg: Object reference not set...
```

### Schritt 5: Speichern oder Löschen

```
[Speichern] → Pattern als YAML gespeichert ✓
             Ist sofort aktiv für neue Logs

[Löschen]  → Pattern entfernt
             YAML-Datei gelöscht
```

### Schritt 6: Fenster schließen

```
✕ Fenster schließen (oben rechts)
oder
[ESC] Taste

→ Zurück zur Hauptanwendung
→ Patterns sind geladen & aktiv
```

---

## 🎯 Praktisches Beispiel – Schritt für Schritt

### Szenario: Neues Custom Pattern erstellen

```
USER-AKTION                    | SYSTEM-RESPONSE
───────────────────────────────┼──────────────────────────────
1. Settings-Button klicken     | Settings-Panel öffnet sich
   (⚙️ rechts oben)            |
───────────────────────────────┼──────────────────────────────
2. Scroll zu "Pattern Editor"  | Fenster scroll runter
───────────────────────────────┼──────────────────────────────
3. "Open Pattern Editor"       | Neues Fenster öffnet sich
   Button klicken              |
───────────────────────────────┼──────────────────────────────
4. "+ Neues Pattern" klicken   | Leerer Editor rechts
   (links unten)               | neue ID: pattern_xyz123
───────────────────────────────┼──────────────────────────────
5. Name eingeben:              | Name Field: "Payment Failed"
   "Payment Failed"            |
───────────────────────────────┼──────────────────────────────
6. Regex eingeben:             | Regex Field gefüllt
   (?<id>\d+) failed:          |
   (?<reason>.*)               |
───────────────────────────────┼──────────────────────────────
7. Severity: "error"           | Dropdown auf "error" gesetzt
───────────────────────────────┼──────────────────────────────
8. Testzeile:                  | Testzeile Field gefüllt
   "Payment 12345 failed:      |
    Network timeout"           |
───────────────────────────────┼──────────────────────────────
9. [Test ausführen]            | ✓ Match erfolgreich!
                               | Fields:
                               |   id: 12345
                               |   reason: Network timeout
───────────────────────────────┼──────────────────────────────
10. [Speichern]                | Datei gespeichert:
                               | LogPatterns/pattern_xyz123.yaml
───────────────────────────────┼──────────────────────────────
11. ✕ Fenster schließen        | Zurück zur Hauptanwendung
                               | Pattern sofort aktiv!
```

---

## 🔧 Technische Integration – Was passiert im Hintergrund?

### Code-Aufruf-Kette

```csharp
// 1. User klickt Button → Settings ViewModel Command
SettingsViewModel.OpenPatternEditorCommand()
{
    // 2. Pattern Service vom App abrufen
    var patternService = App.PatternService;

    // 3. Editor ViewModel erstellen
    var editorVM = new PatternEditorViewModel(patternService);

    // 4. Neues Window mit View erstellen
    var editorWindow = new Window
    {
        Title = "Log Pattern Editor",
        Width = 1000,
        Height = 700,
        Content = new PatternEditorView { DataContext = editorVM }
    };

    // 5. Modal anzeigen (blockiert bis User schließt)
    editorWindow.ShowDialog();
}
```

### Initialisierung beim App-Start

```csharp
// App.xaml.cs
public partial class App : Application
{
    private static LogPatternService? _patternService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Pattern Service initialisieren
        _patternService = new LogPatternService("LogPatterns");
        _patternService.LoadPatternsAsync();  // Async!
    }

    public static LogPatternService? PatternService => _patternService;
}
```

---

## 📊 UI-Komponenten im Detail

### Pattern Editor View (XAML Struktur)

```xaml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="250"/>        <!-- Pattern-Liste -->
    <ColumnDefinition Width="*"/>          <!-- Editor -->
  </Grid.ColumnDefinitions>

  <!-- Links: Pattern-Liste -->
  <Border Grid.Column="0" Background="#F5F5F5">
    <ListBox ItemsSource="{Binding Patterns}"
             SelectedItem="{Binding CurrentPattern}">
      <ListBox.ItemTemplate>
        <DataTemplate>
          <StackPanel>
            <TextBlock Text="{Binding Name}"/>
            <TextBlock Text="{Binding Severity}"/>
          </StackPanel>
        </DataTemplate>
      </ListBox.ItemTemplate>
    </ListBox>
  </Border>

  <!-- Rechts: Editor-Formular -->
  <ScrollViewer Grid.Column="1">
    <StackPanel>
      <TextBox Text="{Binding CurrentPattern.Name}"/>
      <TextBox Text="{Binding CurrentPattern.RegexPattern}"
               FontFamily="Courier New"/>
      <ComboBox SelectedItem="{Binding CurrentPattern.Severity}">
        critical, error, warning, info, debug
      </ComboBox>
      <!-- Test-Panel -->
      <Button Content="Test ausführen"
              Command="{Binding TestPatternCommand}"/>
      <TextBlock Text="{Binding TestResult}"/>
      <!-- Buttons -->
      <Button Content="Speichern"
              Command="{Binding SavePatternCommand}"/>
      <Button Content="Löschen"
              Command="{Binding DeletePatternCommand}"/>
    </StackPanel>
  </ScrollViewer>
</Grid>
```

---

## ⚡ Keyboard-Shortcuts (Optional, kann erweitert werden)

```
Ctrl+N   = Neues Pattern
Ctrl+S   = Speichern
Ctrl+D   = Löschen
Ctrl+T   = Test ausführen
Ctrl+W   = Window schließen (ESC auch)
```

---

## 🔍 Fehlerbehebung

### Problem: Editor-Button ist grayed out

```
✗ Pattern Service nicht initialisiert
✓ Lösung: 
  - App neu starten
  - Logs überprüfen (Debug-Output)
  - Eventuell YamlDotNet nicht installiert?
```

### Problem: Pattern wird nicht gespeichert

```
✗ Regex-Fehler oder Validierung fehlgeschlagen
✓ Lösung:
  - Regex-Syntax überprüfen (regex101.com)
  - Named Groups? (?<name>...)
  - Test-Panel nutzen
```

### Problem: Fenster öffnet nicht

```
✗ Exception beim Erstellen der PatternEditorView
✓ Lösung:
  - Visual Studio Output-Fenster checken
  - Exception-Details ansehen
  - Neubau (Clean Build)
```

---

## 📋 Zusammenfassung – Kurz-Referenz

| Frage | Antwort |
|-------|---------|
| **Wo öffne ich den Editor?** | Settings Panel → "📋 Open Pattern Editor" |
| **Wie erstelle ich ein Pattern?** | "+ Neues Pattern" klicken |
| **Wo werden Patterns gespeichert?** | `LogPatterns/` Verzeichnis (YAML) |
| **Wann sind Patterns aktiv?** | Sofort nach dem Speichern |
| **Kann ich Patterns löschen?** | Ja, [Löschen]-Button |
| **Wie teste ich ein Pattern?** | Testzeile eingeben → [Test ausführen] |
| **Wo finde ich Error-Infos?** | Visual Studio Output-Fenster (Debug) |

---

## 🎓 Checkliste – Pattern Editor verwenden

- ✅ LogAnalyzer öffnen
- ✅ Settings-Button klicken
- ✅ Zum "Pattern Editor" Abschnitt scrollen
- ✅ "📋 Open Pattern Editor" Button klicken
- ✅ Fenster öffnet sich
- ✅ Pattern wählen oder neu erstellen
- ✅ Felder ausfüllen
- ✅ Testzeile testen
- ✅ Speichern
- ✅ Fenster schließen
- ✅ Neues Pattern ist aktiv! 🎉

---

## 📞 Support

Alle Dokumentation:
- `INTEGRATION_GUIDE.md` – Integration in Hauptanwendung
- `LOG_PATTERN_DOCUMENTATION.md` – Technische Details
- `PATTERN_QUICK_START.md` – Schnelle Pattern-Erstellung
- `HOW_TO_CALL_EDITOR.md` – Diese Datei
