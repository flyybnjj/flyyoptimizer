using FlyyOptimizer.Core;
using FlyyOptimizer.Windows;
using FlyyOptimizer.Gaming;
using System.IO;

namespace FlyyOptimizer.App;

/// <summary>Native boundaries can be replaced by fixtures; smoke tests never change Windows settings.</summary>
internal sealed class ViewModelServices
{
    public Func<IProgress<string>?, CancellationToken, IProgress<ScanProgress>?, Task<SystemSnapshot>> Collect { get; init; } =
        (progress, token, detailed) => new WindowsCollector().CollectAsync(progress, token, detailed);
    public Func<CancellationToken, Task<DriverUpdateResult>> FindUpdates { get; init; } =
        token => new DriverUpdateService().FindUpdatesAsync(token);
    public Func<MachineInfo, bool, CancellationToken, Task<OemSupportResult>> FindOem { get; init; } =
        (machine, serial, token) => new OemDriverService().FindAsync(machine, serial, token);
    public Action<ActionKind, string?> Open { get; init; } = WindowsActions.Open;
    public Func<INativeOptimizationService> CreateNativeTools { get; init; } = () => new NativeOptimizationService();
    public Func<string> ResolvePresentMon { get; init; } = () => BundledPresentMon.Resolve();
    public Func<string, int, int, string, CancellationToken, Task<GamingSession>> Capture { get; init; } =
        (executable, pid, duration, path, token) => new GamingCapture().CaptureAsync(executable, pid, duration, path, token);
    public string CaptureDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlyyOptimizer", "Captures");
    public Func<bool> IsElevated { get; init; } = Elevation.IsElevated;
    public Action OpenElevated { get; init; } = Elevation.OpenElevated;
}
