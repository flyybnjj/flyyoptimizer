using FlyyOptimizer.Core;
using FlyyOptimizer.Windows;

namespace FlyyOptimizer.App;

/// <summary>Read-only operations can be replaced by deterministic fixtures for the smoke runner.</summary>
internal sealed class ViewModelServices
{
    public Func<IProgress<string>?, CancellationToken, IProgress<ScanProgress>?, Task<SystemSnapshot>> Collect { get; init; } =
        (progress, token, detailed) => new WindowsCollector().CollectAsync(progress, token, detailed);
    public Func<CancellationToken, Task<DriverUpdateResult>> FindUpdates { get; init; } =
        token => new DriverUpdateService().FindUpdatesAsync(token);
    public Func<MachineInfo, bool, CancellationToken, Task<OemSupportResult>> FindOem { get; init; } =
        (machine, serial, token) => new OemDriverService().FindAsync(machine, serial, token);
    public Action<ActionKind, string?> Open { get; init; } = WindowsActions.Open;
}
