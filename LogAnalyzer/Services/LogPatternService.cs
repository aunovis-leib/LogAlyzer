using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LogAnalyzer.Models;
using YamlDotNet.Serialization;

namespace LogAnalyzer.Services
{
    /// <summary>
    /// Service zur Verwaltung und Anwendung von Log-Pattern-Templates.
    /// </summary>
    public class LogPatternService
    {
        private readonly List<LogPattern> _patterns = [];
        private readonly Dictionary<string, Regex> _compiledRegexes = [];
        private readonly string _patternDirectory;

        public event EventHandler<PatternMatch>? PatternMatched;

        public LogPatternService(string patternDirectory = "LogPatterns")
        {
            _patternDirectory = patternDirectory;
        }

        /// <summary>
        /// Lädt alle Pattern-Templates aus dem Verzeichnis.
        /// </summary>
        public async Task LoadPatternsAsync()
        {
            if (!Directory.Exists(_patternDirectory))
            {
                Directory.CreateDirectory(_patternDirectory);
                return;
            }

            _patterns.Clear();
            _compiledRegexes.Clear();

            var yamlFiles = Directory.GetFiles(_patternDirectory, "*.yaml");
            var deserializer = new DeserializerBuilder().Build();

            foreach (var file in yamlFiles)
            {
                try
                {
                    var yaml = await File.ReadAllTextAsync(file);
                    var pattern = deserializer.Deserialize<LogPattern>(yaml);

                    if (pattern != null && !pattern.IsDisabled)
                    {
                        _patterns.Add(pattern);
                        CompileRegex(pattern);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fehler beim Laden von Pattern {file}: {ex.Message}");
                }
            }

            // Sortiere Patterns nach Priorität (höher = zuerst)
            _patterns.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Kompiliert das Regex-Pattern für schnellere Wiederverwendung.
        /// </summary>
        private void CompileRegex(LogPattern pattern)
        {
            try
            {
                var regex = new Regex(pattern.RegexPattern, RegexOptions.Compiled);
                _compiledRegexes[pattern.Id] = regex;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Regex-Fehler in Pattern {pattern.Id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Versucht, alle geladenen Patterns auf eine Log-Zeile anzuwenden.
        /// </summary>
        public List<PatternMatch> MatchLine(LogFileEntry logEntry)
        {
            var matches = new List<PatternMatch>();

            foreach (var pattern in _patterns)
            {
                if (!_compiledRegexes.TryGetValue(pattern.Id, out var regex))
                {
                    continue;
                }

                var match = regex.Match(logEntry.Text);
                if (match.Success)
                {
                    var patternMatch = new PatternMatch
                    {
                        Pattern = pattern,
                        LogEntry = logEntry,
                        ExtractedFields = ExtractFields(pattern, match)
                    };

                    matches.Add(patternMatch);
                    PatternMatched?.Invoke(this, patternMatch);
                }
            }

            return matches;
        }

        /// <summary>
        /// Extrahiert die definierten Felder aus einem Regex-Match.
        /// </summary>
        private Dictionary<string, string> ExtractFields(LogPattern pattern, Match match)
        {
            var fields = new Dictionary<string, string>();

            foreach (var fieldName in pattern.Fields)
            {
                try
                {
                    var group = match.Groups[fieldName];
                    if (group.Success)
                    {
                        fields[fieldName] = group.Value;
                    }
                }
                catch
                {
                    // Gruppe existiert nicht
                }
            }

            return fields;
        }

        /// <summary>
        /// Gibt alle geladenen Patterns zurück.
        /// </summary>
        public IReadOnlyList<LogPattern> GetPatterns() => _patterns.AsReadOnly();

        /// <summary>
        /// Speichert ein Pattern als YAML-Datei.
        /// </summary>
        public async Task SavePatternAsync(LogPattern pattern)
        {
            Directory.CreateDirectory(_patternDirectory);

            var serializer = new SerializerBuilder()
                .DisableAliases()
                .Build();

            var yaml = serializer.Serialize(pattern);
            var filePath = Path.Combine(_patternDirectory, $"{pattern.Id}.yaml");

            await File.WriteAllTextAsync(filePath, yaml);

            if (_patterns.FirstOrDefault(p => p.Id == pattern.Id) is null)
            {
                _patterns.Add(pattern);
                _patterns.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }

            CompileRegex(pattern);
        }

        /// <summary>
        /// Löscht ein Pattern.
        /// </summary>
        public async Task DeletePatternAsync(string patternId)
        {
            _patterns.RemoveAll(p => p.Id == patternId);
            _compiledRegexes.Remove(patternId);

            var filePath = Path.Combine(_patternDirectory, $"{patternId}.yaml");
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
            }
        }

        /// <summary>
        /// Filtert Patterns nach Tags.
        /// </summary>
        public IEnumerable<LogPattern> FilterByTags(params string[] tags)
        {
            var tagSet = new HashSet<string>(tags);
            return _patterns.Where(p => p.Tags.Any(t => tagSet.Contains(t)));
        }

        /// <summary>
        /// Filtert Patterns nach Schweregrad.
        /// </summary>
        public IEnumerable<LogPattern> FilterBySeverity(string severity)
        {
            return _patterns.Where(p => p.Severity == severity);
        }
    }
}
