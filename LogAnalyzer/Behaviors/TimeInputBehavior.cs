using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LogAnalyzer.Behaviors;

public static class TimeInputBehavior
{
    // Aktivieren: TimeInputBehavior.IsEnabled="True"
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(TimeInputBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(UIElement element, bool value) =>
        element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(UIElement element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox tb)
        {
            if ((bool)e.NewValue)
                Attach(tb);
            else
                Detach(tb);
        }
    }

    // Optional: Leeren bei ungültig beim LostFocus (Default true)
    public static readonly DependencyProperty ClearOnInvalidProperty =
        DependencyProperty.RegisterAttached(
            "ClearOnInvalid",
            typeof(bool),
            typeof(TimeInputBehavior),
            new PropertyMetadata(true));

    public static void SetClearOnInvalid(UIElement element, bool value) =>
        element.SetValue(ClearOnInvalidProperty, value);
    public static bool GetClearOnInvalid(UIElement element) =>
        (bool)element.GetValue(ClearOnInvalidProperty);

    // Regex nur erlaubte Zeichen
    private static readonly Regex AllowedInputRegex = new("^[0-9:]$");

    private static void Attach(TextBox tb)
    {
        tb.PreviewTextInput += Tb_PreviewTextInput;
        tb.PreviewKeyDown += Tb_PreviewKeyDown;
        DataObject.AddPastingHandler(tb, Tb_Pasting);
        tb.LostFocus += Tb_LostFocus;
        tb.MaxLength = 8; // "HH:mm:ss"
    }

    private static void Detach(TextBox tb)
    {
        tb.PreviewTextInput -= Tb_PreviewTextInput;
        tb.PreviewKeyDown -= Tb_PreviewKeyDown;
        DataObject.RemovePastingHandler(tb, Tb_Pasting);
        tb.LostFocus -= Tb_LostFocus;
        // MaxLength bleibt unverändert oder setze auf 0 falls gewünscht
    }

    private static void Tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!AllowedInputRegex.IsMatch(e.Text)) { e.Handled = true; return; }

        var tb = (TextBox)sender;

        if (TryAutoInsertSecondSeparator(tb, e.Text))
        {
            e.Handled = true;
            return;
        }

        string newText = GetTextAfterInput(tb, e.Text);
        if (newText.Length > 8 || !IsPlausiblePartial(newText)) e.Handled = true;
    }

    private static void Tb_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) e.Handled = true;
    }

    private static void Tb_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text)) { e.CancelCommand(); return; }
        string paste = e.SourceDataObject.GetData(DataFormats.Text) as string ?? "";
        var tb = (TextBox)sender;
        string newText = GetTextAfterInput(tb, paste);
        if (newText.Length > 8 || !IsValidCompleteOrPlausible(newText)) e.CancelCommand();
    }

    private static void Tb_LostFocus(object sender, RoutedEventArgs e)
    {
        var tb = (TextBox)sender;
        var txt = tb.Text?.Trim();
        if (string.IsNullOrEmpty(txt)) return;

        string? normalized = TryNormalizeToHMS(txt);
        if (normalized != null)
            tb.Text = normalized;
        else if (GetClearOnInvalid(tb))
            tb.Text = "";
    }

    // Helpers
    private static string GetTextAfterInput(TextBox tb, string input)
    {
        var text = tb.Text ?? "";
        int selStart = tb.SelectionStart;
        int selLen = tb.SelectionLength;
        return text.Substring(0, selStart) + input + text.Substring(selStart + selLen);
    }

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

    private static bool IsValidCompleteOrPlausible(string s)
    {
        if (s.Length == 0) return true;
        if (s.Length != 8) return IsPlausiblePartial(s);
        if (!TimeSpan.TryParseExact(s, @"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture, out _)) return false;
        var parts = s.Split(':');
        if (int.TryParse(parts[0], out int hh) && int.TryParse(parts[1], out int mm) && int.TryParse(parts[2], out int ss))
            return hh >= 0 && hh <= 23 && mm >= 0 && mm <= 59 && ss >= 0 && ss <= 59;
        return false;
    }

    private static bool TryAutoInsertSecondSeparator(TextBox tb, string input)
    {
        if (input.Length != 1 || !char.IsDigit(input[0])) return false;
        if (tb.SelectionLength != 0) return false;

        var text = tb.Text ?? string.Empty;
        if (tb.SelectionStart != text.Length) return false;
        string? newText = null;

        if (Regex.IsMatch(text, @"^\d{2}$"))
        {
            newText = text + ":" + input;
        }
        else if (Regex.IsMatch(text, @"^\d{2}:\d{2}$"))
        {
            newText = $"{text}:{input}";
        }

        if (newText is null) return false;
        if (!IsPlausiblePartial(newText)) return false;

        tb.Text = newText;
        tb.SelectionStart = newText.Length;
        return true;
    }

    private static string? TryNormalizeToHMS(string input)
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
}
