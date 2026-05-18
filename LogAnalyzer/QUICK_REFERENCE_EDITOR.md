# 🎯 QUICK REFERENCE – Pattern Editor Aufruf (1-Seiter)

## 📍 LOCATION MAP

```
┌─────────────────────────────────────────────────────┐
│           LogAnalyzer Main Window                   │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [Toolbar]  [⚙️ SETTINGS] ←─────────────────┐      │
│                                             │      │
│  ┌─────────────────────┐                   │      │
│  │   Log Viewer Area   │                   │      │
│  │   (Log Entries)     │      ┌────────────┴──┐   │
│  │                     │      │ SETTINGS PANE │   │
│  └─────────────────────┘      ├───────────────┤   │
│                               │ List Settings │   │
│  ┌─────────────────────┐      │ Live Chart    │   │
│  │ Pattern Matches     │      │ ──────────────│   │
│  │ (NEW!)              │      │ 📋 PATTERN    │◄──┼─ KLICK HIER!
│  │ • Match 1           │      │    EDITOR     │   │
│  │ • Match 2           │      │ 📋 Open       │   │
│  └─────────────────────┘      │    Pattern    │   │
│                               │    Editor     │   │
│                               │ ──────────────│   │
│                               │ [Reset] [...]│   │
│                               └───────────────┘   │
│                                                     │
└─────────────────────────────────────────────────────┘
```

---

## ⚡ QUICK STEPS (Nur 4 Klicks!)

```
SCHRITT 1: Hauptfenster
┌─────────────────────────────┐
│ LogAnalyzer geöffnet        │
└─────────────────────────────┘
              ↓
SCHRITT 2: ⚙️ Klicken
┌─────────────────────────────┐
│ ⚙️ Settings Button           │
│ (rechts oben im Fenster)    │
└─────────────────────────────┘
              ↓
SCHRITT 3: Scrollen
┌─────────────────────────────┐
│ Settings Panel öffnet sich  │
│ Scroll nach unten           │
│ bis "Pattern Editor"        │
└─────────────────────────────┘
              ↓
SCHRITT 4: 📋 Klicken
┌─────────────────────────────┐
│ "📋 Open Pattern Editor"    │
│ Button klicken              │
└─────────────────────────────┘
              ↓
        ✅ EDITOR ÖFFNET SICH!
```

---

## 🖱️ MOUSE PATH (Mausweg zeigen)

```
Bildschirm oben rechts:
┌──────────────────────────────────────┐
│ [Datei] [Bearbeiten] ...   [⚙️] ← KLICK 1
└──────────────────────────────────────┘
           ↓ (Settings Panel öffnet sich)
┌──────────────────────────────────────┐
│ Settings                             │
│ ─────────────────────────────────    │
│ List Settings                        │
│ ─────────────────────────────────    │
│ Live Chart                           │
│ ─────────────────────────────────    │
│                                      │
│ [Scroll down ↓]                      │
│                                      │
│ ─────────────────────────────────    │
│ 📋 Log Pattern Editor               │
│ 📋 Open Pattern Editor ← KLICK 2    │
│ Create, edit, and test...            │
│ ─────────────────────────────────    │
│ [Reset defaults]                     │
└──────────────────────────────────────┘
           ↓
    Pattern Editor Fenster
    ┌──────────────────────┐
    │ Log Pattern Editor  │
    │ ──────────────────  │
    │ [Patterns] [Editor] │
    │ ──────────────────  │
    │ • null_reference    │
    │ • http_error        │
    │ • database_timeout  │
    │ • out_of_memory     │
    │ + Neues Pattern     │
    └──────────────────────┘
```

---

## 🎯 USE CASES – 3 Häufige Szenarien

### SCENARIO 1: Bestehendes Pattern bearbeiten (30 Sekunden)

```
1. [⚙️] Settings klicken
2. [📋 Open Pattern Editor] klicken
3. "null_reference" in der Liste klicken
4. Rechts: Name/Regex anpassen
5. [Speichern] klicken
6. ✅ Fertig!
```

### SCENARIO 2: Neues Pattern erstellen (2 Minuten)

```
1. [⚙️] Settings klicken
2. [📋 Open Pattern Editor] klicken
3. "+ Neues Pattern" klicken
4. Felder ausfüllen:
   - Name: "My Custom Error"
   - Regex: (?<error>ERROR:.*)
   - Severity: error
5. Testzeile eingeben
6. [Test ausführen] klicken
7. ✓ Match erfolgreich anzeigen
8. [Speichern] klicken
9. ✅ Pattern aktiv!
```

### SCENARIO 3: Pattern löschen (10 Sekunden)

```
1. [⚙️] Settings klicken
2. [📋 Open Pattern Editor] klicken
3. Pattern in Liste klicken
4. [Löschen] klicken
5. ✅ Pattern entfernt!
```

---

## 🔧 TECHNICAL FLOW (Für Developer)

```
USER ACTION                SYSTEM RESPONSE
─────────────────────────────────────────────────────
User klickt ⚙️         
    ↓
SettingsView.xaml
    ↓
Command="{Binding OpenPatternEditorCommand}"
    ↓
SettingsViewModel.OpenPatternEditor()
    ↓
App.PatternService wird geladen
    ↓
PatternEditorViewModel wird erstellt
    ↓
PatternEditorView wird in neuem Window geladen
    ↓
Window.ShowDialog() → MODAL BLOCKING
    ↓
User bearbeitet Patterns
    ↓
User klickt [Speichern]
    ↓
Pattern als YAML gespeichert
    ↓
User schließt Fenster
    ↓
ShowDialog() beendet
    ↓
Zurück zur Hauptanwendung
    ↓
Patterns sind sofort aktiv!
```

---

## 📝 CODE SNIPPETS – Copy-Paste Ready

### Snippet 1: Wie der Command funktioniert

```csharp
// SettingsViewModel.cs
[RelayCommand]
private void OpenPatternEditor()
{
    var patternService = App.PatternService;
    var editorVM = new PatternEditorViewModel(patternService);

    var editorWindow = new Window
    {
        Title = "Log Pattern Editor",
        Width = 1000,
        Height = 700,
        Content = new PatternEditorView { DataContext = editorVM }
    };

    editorWindow.ShowDialog();  // Modal (blockiert bis close)
}
```

### Snippet 2: PatternService Initialisierung

```csharp
// App.xaml.cs
private static LogPatternService? _patternService;

protected override void OnStartup(StartupEventArgs e)
{
    _patternService = new LogPatternService("LogPatterns");
    _patternService.LoadPatternsAsync();
}

public static LogPatternService? PatternService => _patternService;
```

### Snippet 3: XAML Button

```xaml
<!-- SettingsView.xaml -->
<Button Content="📋 Open Pattern Editor" 
        Command="{Binding OpenPatternEditorCommand}"
        Padding="10,8"
        Background="#007ACC"
        Foreground="White"/>
```

---

## 🐛 TROUBLESHOOTING – Häufige Probleme

| Problem | Lösung |
|---------|--------|
| Button nicht sichtbar | Scrollen Sie in Settings Panel nach unten |
| Editor-Fenster nicht öffnet | App neu starten, Build überprüfen |
| Patterns nicht gespeichert | Regex-Syntax überprüfen (Named Groups?) |
| Pattern wird nicht angewendet | Priority überprüfen (höher = zuerst) |
| Fenster hängt | (Normal) ShowDialog() blockt – OK |

---

## 🎓 KEYBOARD SHORTCUTS

| Taste | Aktion |
|-------|--------|
| `ESC` | Fenster schließen |
| `Ctrl+S` | Speichern (optional, nur Button) |
| `Ctrl+T` | Test (optional, nur Button) |

---

## 📊 FEATURE MATRIX – Was kann wo?

```
                    Main Window | Settings Panel | Editor Window
────────────────────────────────────────────────────────────────
View Log Entries        ✅            ✗                ✗
Access Settings         ✅            ✅               ✗
Open Editor             ✗             ✅               ✗
Create Pattern          ✗             ✗                ✅
Edit Pattern            ✗             ✗                ✅
Delete Pattern          ✗             ✗                ✅
Test Regex              ✗             ✗                ✅
See Matches             ✅            ✗                ✗
────────────────────────────────────────────────────────────────
```

---

## 🚀 LAUNCH CHECKLIST

- [ ] LogAnalyzer gebaut (Ctrl+Shift+B)
- [ ] Keine Build-Fehler
- [ ] App gestartet (Ctrl+F5)
- [ ] Hauptfenster sichtbar
- [ ] ⚙️ Button sichtbar (rechts oben)
- [ ] ⚙️ klicken → Settings öffnet sich
- [ ] Scrollen → "📋 Pattern Editor" sichtbar
- [ ] "📋 Open Pattern Editor" Klicken
- [ ] Neues Fenster öffnet sich ✅
- [ ] Pattern auswählen & bearbeiten
- [ ] [Speichern] klicken
- [ ] ✅ Fertig!

---

## 💡 TIPS & TRICKS

**Tipp 1:** Benutze `regex101.com` zum Testen deiner Regex vor dem Speichern

**Tipp 2:** Testzeile muss exakt gleich sein wie echte Log-Zeile

**Tipp 3:** Named groups sind wichtig: `(?<name>...)`

**Tipp 4:** Priority höher = Pattern wird zuerst geprüft

**Tipp 5:** Tags helfen später beim Filtern

---

## 📚 VOLLSTÄNDIGE DOKUMENTATION

```
HOW_TO_CALL_EDITOR.md          ← DIESE DATEI (1-Seiter)
  └─ Quick Reference

LOG_PATTERN_DOCUMENTATION.md   ← Technische Referenz
  └─ API, Best Practices

PATTERN_QUICK_START.md         ← Schnell Patterns erstellen
  └─ Copy-paste Vorlagen

INTEGRATION_GUIDE.md           ← Integration in App
  └─ Schritt-für-Schritt

EDITOR_CALL_SUMMARY.md         ← Großer Überblick
  └─ Alles detailliert
```

---

## 🎉 SUMMARY

**Question:** "Wie und wo wird der Editor aufgerufen?"

**Answer:**

```
WHERE:  Settings Panel (⚙️ rechts oben)
        └─ "📋 Open Pattern Editor" Button

HOW:    1. ⚙️ klicken
        2. Scrollen (Pattern Editor)
        3. "📋 Open Pattern Editor" klicken
        4. ✅ Editor öffnet sich!
```

**Status:** ✅ Ready to Use

**Next:** Build → Run → ⚙️ → Klick → Enjoy! 🚀

---

**Fragen?** → Siehe `LOG_PATTERN_DOCUMENTATION.md`
