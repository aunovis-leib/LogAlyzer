# ⚡ Quick Start – Log Patterns erstellen

## 1️⃣ Pattern-YAML anlegen

Neue Datei in `LogPatterns/` erstellen:

```yaml
# LogPatterns/my_custom_pattern.yaml
Id: my_pattern_id
Name: "My Custom Pattern"
Description: "Beschreibung, was erkannt wird"
RegexPattern: '(?<timestamp>\d{2}:\d{2}:\d{2}) ERROR: (?<code>\d+)'
Severity: "error"
Tags: [custom, important]
Fields: [timestamp, code]
Priority: 50
IsDisabled: false

Action:
  ShowInPanel: true
  Pin: false
  UITag: MyTag
  NotificationText: null
```

## 2️⃣ Regex Pattern testen

**Im Pattern Editor (UI):**
1. Pattern öffnen
2. Testzeile einfügen
3. "Test ausführen" klicken
4. Erfolg? → "Speichern"

**Beispiel-Testzeile:**
```
14:35:22 ERROR: 500 - Database connection failed
```

## 3️⃣ Häufige Regex-Patterns

### Zeit extrahieren
```regex
(?<timestamp>\d{2}:\d{2}:\d{2})
```

### HTTP-Status
```regex
HTTP/[\d.]+" (?<status>\d{3})
```

### Exception + Message
```regex
(?<exception>\w+Exception): (?<message>.*)
```

### Key=Value Pairs
```regex
CommandTimeout=(?<timeout>\d+)
```

### JSON im Log
```regex
(?<json>\{.*\})
```

## 4️⃣ Severity-Guide

| Level | Farbe | Wann? |
|-------|-------|-------|
| `critical` | 🔴 | OutOfMemory, Security-Breach |
| `error` | 🟠 | Exception, HTTP 5xx |
| `warning` | 🟡 | Timeout, Retry, Low Memory |
| `info` | 🔵 | State Change, Success |
| `debug` | ⚪ | Detailed Trace |

## 5️⃣ Tags verwenden

**Technisch:**
```
- exception, database, http, performance, cache
```

**Geschäftlich:**
```
- payment, auth, integration, user-data, compliance
```

## 6️⃣ Pattern-Beispiele

### Exception-Pattern
```yaml
Id: generic_exception
RegexPattern: '(?<time>\d{2}:\d{2}:\d{2}) .* (?<ex>\w+Exception): (?<msg>.*)'
Severity: "error"
Tags: [exception]
```

### Performance-Pattern
```yaml
Id: slow_query
RegexPattern: 'Query executed in (?<duration>\d+)ms'
Severity: "warning"
Tags: [performance, database]
```

### Business-Logic-Pattern
```yaml
Id: order_failed
RegexPattern: 'Order (?<order_id>\d+) failed: (?<reason>.*)'
Severity: "error"
Tags: [business, order]
```

## 7️⃣ Tipps & Tricks

✅ **DO:**
- Named groups für alle relevanten Daten
- Priority nach Kritikalität setzen
- Konkrete Patterns vor generischen

❌ **DON'T:**
- Zu breite Regex (`.*` überall)
- Timestamp aus Dateinamen nehmen (aus Log-Content!)
- Zu viele Fields pro Pattern

## 8️⃣ Debugging

**Pattern findet nichts?**
1. Regex-Syntax überprüfen
2. Named groups korrekt? `(?<name>...)`
3. Escaping? `\d`, `\.`, `\s`
4. Testzeile exakt gleich wie Log-Content?

**Performance-Problem?**
- Vereinfache Regex
- Erhöhe Priority konkreterer Patterns
- Deaktiviere ungenutzte Patterns

## 9️⃣ CSV Export

Klick "📥 Export" im Pattern-Match-Panel:
```csv
Timestamp,Pattern,Severity,Text,Fields
"2024-01-15T14:35:22.123Z","NullRef","error","System.NullReferenceException...","timestamp=14:35:22; message=Object reference"
```

## 🔟 Weitere Ressourcen

- **Regex Tester:** https://regex101.com/
- **Grok Patterns:** https://github.com/elastic/beats/tree/main/libbeat/processors/grok/patterns
- **Dokumentation:** `LOG_PATTERN_DOCUMENTATION.md`

---

**Schnelle Frage beantwortung:**
- Wo speichern? → `LogPatterns/` Verzeichnis
- Format? → YAML
- Datum aus? → Log-Zeile selbst, nicht Filename!
- Wie testen? → Pattern Editor im UI
- Performance ok? → Ja, .Compiled Regex
