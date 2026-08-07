namespace LogAlyzer.PluginContracts;

public interface IPluginUiContribution
{
    IPluginPanelContribution? Panel { get; }
}

public interface IPluginPanelContribution
{
    string PanelId { get; }

    string DisplayName { get; }

    IReadOnlyList<IPluginAction> Actions { get; }
}

public interface IPluginAction
{
    PluginActionDescriptor Descriptor { get; }

    ValueTask<PluginActionResult> ExecuteAsync(
        CancellationToken cancellationToken = default);
}

public sealed record PluginActionDescriptor(
    string Id,
    string DisplayName,
    bool IsPrimary = false);

public sealed record PluginActionResult(
    bool Succeeded,
    string? Message = null);