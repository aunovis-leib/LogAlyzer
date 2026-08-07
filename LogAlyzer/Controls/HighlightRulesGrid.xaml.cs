using System.Windows.Controls;

namespace LogAlyzer.Controls;

/// <summary>
/// Reusable DataGrid showing the active highlight rules. Bound to a
/// <see cref="LogAlyzer.ViewModels.SettingsViewModel"/> via the inherited DataContext.
/// </summary>
public partial class HighlightRulesGrid : UserControl
{
    public HighlightRulesGrid()
    {
        InitializeComponent();
    }
}
