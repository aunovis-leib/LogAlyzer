using System;
using System.Text.RegularExpressions;

namespace LogAnalyzer.Behaviors
{
    // Testable core logic extracted from TimeInputBehavior that does not reference WPF types.
    public static class TimeInputBehaviorCore
    {
        public static bool TryAutoInsertSecondSeparatorCore(string text, int selectionStart, string input, out string newText, out int newSelectionStart)
        {
            newText = text;
            newSelectionStart = selectionStart;

            if (input.Length != 1 || !char.IsDigit(input[0])) return false;
            if (selectionStart < 0) return false;

            if (selectionStart != text.Length) return false;

            string? candidate = null;

            if (Regex.IsMatch(text, @"^\d{2}$"))
            {
                candidate = text + ":" + input;
            }
            else if (Regex.IsMatch(text, @"^\d{2}:\d{2}$"))
            {
                candidate = $"{text}:{input}";
            }

            if (candidate is null) return false;
            if (!IsPlausiblePartial(candidate)) return false;

            newText = candidate;
            newSelectionStart = candidate.Length;
            return true;
        }

        public static string? TryNormalizeToHMS(string input)
        {
            var parts = input.Split(':');
            if (parts.Length == 0 || parts.Length > 3) return null;

            if (!int.TryParse(parts[0], out int hh)) return null;

            int mm = 0;
            int ss = 0;

            if (parts.Length >= 2 && !int.TryParse(parts[1], out mm)) return null;
            if (parts.Length == 3 && !int.TryParse(parts[2], out ss)) return null;

            if (hh < 0 || hh > 23 || mm < 0 || mm > 59 || ss < 0 || ss > 59) return null;

            return parts.Length switch
            {
                1 => hh.ToString("00"),
                2 => $"{hh:00}:{mm:00}",
                _ => $"{hh:00}:{mm:00}:{ss:00}"
            };
        }

        // Helpers copied from TimeInputBehavior
        private static bool IsPlausiblePartial(string s)
        {
            if (!Regex.IsMatch(s, @"^[0-9:]*$")) return false;
            if (s.Split(':').Length - 1 > 2) return false;
            if (s.Length == 0) return true;

            var parts = s.Split(':');
            if (!ArePartLengthsValid(parts)) return false;
            if (s.StartsWith(':')) return false;

            if (!IsHourPlausible(parts)) return false;
            if (!IsMinutePlausible(parts)) return false;
            if (!IsSecondPlausible(parts)) return false;

            return true;
        }

        private static bool ArePartLengthsValid(string[] parts)
        {
            if (parts[0].Length > 2) return false;
            if (parts.Length > 1 && parts[1].Length > 2) return false;
            if (parts.Length > 2 && parts[2].Length > 2) return false;
            return true;
        }

        private static bool IsHourPlausible(string[] parts)
        {
            if (parts[0].Length == 2 && int.TryParse(parts[0], out int h))
                return h >= 0 && h <= 23;
            return true;
        }

        private static bool IsMinutePlausible(string[] parts)
        {
            if (parts.Length >= 2 && parts[1].Length == 2 && int.TryParse(parts[1], out int m))
                return m >= 0 && m <= 59;
            return true;
        }

        private static bool IsSecondPlausible(string[] parts)
        {
            if (parts.Length == 3 && parts[2].Length == 2 && int.TryParse(parts[2], out int sec))
                return sec >= 0 && sec <= 59;
            return true;
        }
    }
}
