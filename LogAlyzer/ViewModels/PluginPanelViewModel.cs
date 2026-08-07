using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAlyzer.PluginContracts;
using LogAlyzer.PluginHost;

namespace LogAlyzer.ViewModels;

public sealed class PluginPanelViewModel : ObservableObject, IDisposable
{
    private readonly PluginManager? _pluginManager;

    public PluginPanelViewModel(PluginManager? pluginManager)
    {
        _pluginManager = pluginManager;
        if (_pluginManager is null)
        {
            return;
        }

        _pluginManager.PluginsChanged += PluginManager_PluginsChanged;
        RebuildPanels();
    }

    public ObservableCollection<PluginPanelContributionViewModel> Panels { get; } = [];

    public bool HasPanels => Panels.Count > 0;

    public void Dispose()
    {
        if (_pluginManager is not null)
        {
            _pluginManager.PluginsChanged -= PluginManager_PluginsChanged;
        }
    }

    private void PluginManager_PluginsChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(RebuildPanels);
            return;
        }

        RebuildPanels();
    }

    private void RebuildPanels()
    {
        Panels.Clear();

        if (_pluginManager is not null)
        {
            foreach (var loadedPlugin in _pluginManager.LoadedPlugins)
            {
                if (loadedPlugin.Instance is not IPluginUiContribution contribution
                    || contribution.Panel is null)
                {
                    continue;
                }

                Panels.Add(new PluginPanelContributionViewModel(contribution.Panel));
            }
        }

        OnPropertyChanged(nameof(HasPanels));
    }
}

public sealed class PluginPanelContributionViewModel
{
    public PluginPanelContributionViewModel(IPluginPanelContribution panel)
    {
        DisplayName = panel.DisplayName;
        Actions = [.. panel.Actions.Select(action => new PluginActionViewModel(action))];
    }

    public string DisplayName { get; }

    public ObservableCollection<PluginActionViewModel> Actions { get; }
}

public sealed partial class PluginActionViewModel : ObservableObject
{
    private readonly IPluginAction _action;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public PluginActionViewModel(IPluginAction action)
    {
        _action = action;
        Descriptor = action.Descriptor;
        ExecuteCommand = new AsyncRelayCommand(ExecuteAsync);
    }

    public PluginActionDescriptor Descriptor { get; }

    public IAsyncRelayCommand ExecuteCommand { get; }

    private async Task ExecuteAsync()
    {
        IsExecuting = true;
        StatusMessage = "Wird ausgeführt...";

        try
        {
            var result = await _action.ExecuteAsync();
            StatusMessage = string.IsNullOrWhiteSpace(result.Message)
                ? (result.Succeeded ? "Erfolgreich." : "Fehlgeschlagen.")
                : result.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsExecuting = false;
        }
    }
}