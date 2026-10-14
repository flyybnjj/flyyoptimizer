using FlyyOptimizer.Core;
using FlyyOptimizer.Windows;
using FlyyOptimizer.Gaming;
using System.IO;
using System.Diagnostics;

namespace FlyyOptimizer.App;


internal sealed class ViewModelServices
{
    public IAppUpdateService AppUpdates { get; init; } = new AppUpdateService();
    public Version InstalledAppVersion { get; init; } = typeof(MainViewModel).Assembly.GetName().Version ?? new Version(1, 1, 0, 0);
    public Func<PreparedAppUpdate, ProcessStartInfo> PrepareAppUpdate { get; init; } = PortableAppUpdater.CreateStartInfo;
    public Action<ProcessStartInfo> LaunchAppUpdate { get; init; } = start =>
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Windows no pudo abrir el instalador de FlyyOptimizer.");
    };
    public Func<IProgress<string>?, CancellationToken, IProgress<ScanProgress>?, Task<SystemSnapshot>> Collect { get; init; } =
        (progress, token, detailed) => new WindowsCollector().CollectAsync(progress, token, detailed);
    public Func<CancellationToken, Task<DriverUpdateResult>> FindUpdates { get; init; } =
        token => new DriverUpdateService().FindUpdatesAsync(token);
    public Func<IReadOnlyList<DriverUpdateInfo>, CancellationToken, Task<DriverDownloadResult>> DownloadDriverUpdates { get; init; } =
        (updates, token) => new DriverDownloadService().DownloadAsync(updates, token);
    public Func<MachineInfo, bool, CancellationToken, Task<OemSupportResult>> FindOem { get; init; } =
        (machine, serial, token) => new OemDriverService().FindAsync(machine, serial, token);
    public Action<ActionKind, string?> Open { get; init; } = WindowsActions.Open;
    public Func<INativeOptimizationService> CreateNativeTools { get; init; } = () => new NativeOptimizationService();
    public Func<IFortniteProfileService> CreateFortniteProfile { get; init; } = () => new FortniteProfileService();
    public Func<bool> ConfirmOptimization { get; init; } = OptimizationConfirmation.Confirm;
    public Func<string?> ChooseFortniteFolder { get; init; } = () =>
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Seleccionar carpeta de instalación de Fortnite", Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    };
    public Func<string> ResolvePresentMon { get; init; } = () => BundledPresentMon.Resolve();
    public Func<string, int, int, string, CancellationToken, Task<GamingSession>> Capture { get; init; } =
        (executable, pid, duration, path, token) => new GamingCapture().CaptureAsync(executable, pid, duration, path, token);
    public string CaptureDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlyyOptimizer", "Captures");
    public Func<bool> IsElevated { get; init; } = Elevation.IsElevated;
    public Action OpenElevated { get; init; } = Elevation.OpenElevated;
    public Action OpenElevatedForUpdates { get; init; } = Elevation.OpenElevatedDrivers;
}
