using System;
using System.Collections.Generic;

namespace LogAnalyzer.Models
{
    /// <summary>
    /// Definiert ein wiederverwendbares Pattern zur Erkennung spezifischer Log-Muster oder Fehlerfälle.
    /// </summary>
    public class LogPattern
    {
        /// <summary>
        /// Eindeutiger Bezeichner des Patterns.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Anzeigename des Patterns.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Beschreibung des Patterns.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Regulärer Ausdruck mit named capture groups.
        /// </summary>
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// Schweregrad: "debug", "info", "warning", "error", "critical".
        /// </summary>
        public string Severity { get; set; } = "info";

        /// <summary>
        /// Tags zur Kategorisierung (z.B. ["exception", "null", "database"]).
        /// </summary>
        public List<string> Tags { get; set; } = [];

        /// <summary>
        /// Feldnamen, die aus den Regex-Gruppen extrahiert werden sollen.
        /// </summary>
        public List<string> Fields { get; set; } = [];

        /// <summary>
        /// Aktionen bei erfolgreicher Erkennung.
        /// </summary>
        public PatternAction Action { get; set; } = new();

        /// <summary>
        /// Priorisierung: höhere Werte werden zuerst geprüft.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Wenn true, wird dieses Pattern nicht angewendet.
        /// </summary>
        public bool IsDisabled { get; set; } = false;
    }

    /// <summary>
    /// Definiert Aktionen, die beim Erkennen eines Patterns ausgeführt werden.
    /// </summary>
    public class PatternAction
    {
        /// <summary>
        /// Soll das Match in einem speziellen Panel angezeigt werden?
        /// </summary>
        public bool ShowInPanel { get; set; } = true;

        /// <summary>
        /// Soll das Match gepinnt/markiert werden?
        /// </summary>
        public bool Pin { get; set; } = false;

        /// <summary>
        /// Zugewiesener Tag für UI-Filterung.
        /// </summary>
        public string? UITag { get; set; }

        /// <summary>
        /// Benachrichtigungstext (wenn nicht leer, wird eine Benachrichtigung ausgelöst).
        /// </summary>
        public string? NotificationText { get; set; }
    }

    /// <summary>
    /// Ergebnis der Pattern-Erkennung.
    /// </summary>
    public class PatternMatch
    {
        /// <summary>
        /// Das erkannte Pattern.
        /// </summary>
        public LogPattern Pattern { get; set; } = null!;

        /// <summary>
        /// Log-Eintrag, der das Pattern erfüllt.
        /// </summary>
        public LogFileEntry LogEntry { get; set; } = null!;

        /// <summary>
        /// Extrahierte Feldwerte aus den Regex-Gruppen.
        /// </summary>
        public Dictionary<string, string> ExtractedFields { get; set; } = [];

        /// <summary>
        /// Zeitstempel der Erkennung.
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }
}
