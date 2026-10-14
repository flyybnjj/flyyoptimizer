using System.IO;
using System.Windows.Threading;
using FlyyOptimizer.Core;

namespace FlyyOptimizer.App;

/// <summary>Fixtures verify view-model behavior only. They never install, capture, or open system tools.</summary>
internal static class InteractionSmokeChecks
{
    public static async Task<IReadOnlyList<string>> RunAsync(string directory, Dispatcher dispatcher)
    {
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Fallo de interacción: " + name);
            checks.Add(name);
        }

        var sample = new MetricSample { CpuPercent = 32, MemoryPercent = 61, GpuPercent = null };
        var fixture = new SystemSnapshot
        {
            Machine = new() { Manufacturer = "Lenovo", Model = "FixtureModel", ProductName = "Fixture product", DeviceKind = DeviceKind.Laptop, SystemSku = "FixtureSku" },
            Samples = [sample], Drivers = [new() { DeviceName = "Fixture graphics", Provider = "Fixture" }],
            Processes = [new("Fixture game", 54321, 300)]
        };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serialRequests = new List<bool>();
        var opened = new List<ActionKind>();
        var offline = false;
        var cancelScan = true;
        var services = new ViewModelServices
        {
            Collect = async (_, token, detailed) =>
            {
                if (cancelScan)
                {
                    detailed?.Report(new("Fixture stage", "Fixture observation", 1, 4, sample));
                    started.TrySetResult();
                    await release.Task.WaitAsync(token);
                }
                token.ThrowIfCancellationRequested(); return fixture;
            },
            FindUpdates = _ => Task.FromResult(offline ? new DriverUpdateResult(false, [], "Fixture offline") : new DriverUpdateResult(true, [new("Fixture Windows offer", "Fixture", "Display", "2", "2026-10-14")], "Fixture offers")),
            FindOem = (_, serial, _) =>
            {
                serialRequests.Add(serial);
                return Task.FromResult(offline
                    ? new OemSupportResult(false, "Lenovo", "Fixture product", "https://support.lenovo.com/", [], "Fixture offline; reintenta o abre el portal.", false)
                    : new OemSupportResult(true, "Lenovo", "Fixture product", "https://support.lenovo.com/", [new("Fixture chipset", "1", "Chipset", "2026-10-14", "Windows 11", "https://download.lenovo.com/fixture.exe", "https://support.lenovo.com/")], "Fixture catalog", true, serial ? "••••1234" : ""));
            },
            Open = (kind, _) => opened.Add(kind)
        };
        using var model = new MainViewModel(Path.Combine(directory, "fixture-history"), services);
        Check(model.ScanCommand.CanExecute(null) && !model.ExportCommand.CanExecute(null) && !model.SearchOemCommand.CanExecute(null), "Empty-state commands explain missing observation");
        model.NavigateCommand.Execute("4");
        Check(model.SelectedPage == 4, "Navigation command selects Gaming");
        var cancelled = model.ScanAsync();
        await started.Task;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        Check(model.IsScanning && model.ScanPercent == 25 && model.CpuValue == "32%" && model.GpuValue == "No disponible" && model.CpuTrace.Count == 1, "Completed-step progress and actual live sample with unavailable GPU");
        Check(!model.ScanCommand.CanExecute(null) && model.CancelCommand.CanExecute(null), "Concurrent scan blocked and cancel available");
        model.CancelCommand.Execute(null);
        Check(!await cancelled && !model.IsBusy && !model.IsScanning && !model.HasSnapshot && serialRequests.Count == 0 && model.Status.Contains("parciales"), "Cancelled scan preserves empty state and saves no partial observation");
        cancelScan = false;
        Check(await model.ScanAsync() && serialRequests.SequenceEqual([false]) && model.HasOemPackages && model.CanSearchSerial, "Fresh scan automatically finds model without sending serial");
        Check(model.DeviceKindLabel == "Portátil" && model.DeviceIdentity.Contains("FixtureSku"), "Device type, model and SKU displayed");
        model.DriverSearch = "no such device";
        Check(!model.DriversView.Cast<object>().Any(), "Driver inventory filter");
        model.OemSearch = "chipset";
        Check(model.OemPackagesView.Cast<object>().Count() == 1, "Official catalog category filter");
        model.SelectedOemPackage = model.OemPackages.Single();
        await model.OpenOemAsync(true);
        Check(opened.SequenceEqual([ActionKind.OpenManufacturer]) && model.Status.Contains("compatibilidad"), "Official package command opens reviewed URL without installing");
        opened.Clear();
        model.OemSearch = "absent";
        Check(!model.OemPackagesView.Cast<object>().Any(), "Official catalog unmatched filter");
        model.SelectedProcess = model.CaptureProcesses.Single();
        Check(model.ProcessId == "54321", "Real process choice supplies PID");
        model.ProcessId = "42";
        Check(model.SelectedProcess is null && model.ProcessId == "42", "Manual PID clears old picker selection");
        model.ProcessId = "0";
        Check(!model.ValidateCaptureInputs(out _, out _) && model.HasCaptureError && !model.HasError, "Invalid capture input stays beside capture form");
        await model.SearchOemAsync(true);
        Check(serialRequests.SequenceEqual([false, true]) && model.SerialDisplay.Contains("1234"), "Only explicit serial action sends serial request");
        await model.SearchUpdatesAsync();
        Check(model.HasUpdates, "Windows Update result stays separate from model catalog");
        model.SelectedProcess = model.CaptureProcesses.Single();
        var archived = fixture with
        {
            CapturedAt = DateTimeOffset.Now.AddDays(-30),
            Processes = [new("Archived name with reused PID", 54321, 100), new("Another archived process", 999, 50)]
        };
        model.ApplySnapshot(archived);
        Check(model.CaptureProcesses.Count == 0 && !model.HasCaptureProcesses && model.SelectedProcess is null && model.ProcessId == "", "History clears capture candidates selected process and stale PID even when archived PID matches current selection");
        Check(model.Snapshot!.Processes.Count == 2, "Archived process inventory remains available for historical inspection");
        model.ProcessId = "42";
        Check(model.ProcessId == "42" && model.SelectedProcess is null && model.RefreshProcessesCommand.CanExecute(null), "Manual PID and live refresh remain available after history load");
        Check(!model.HasUpdates && !model.HasOemPackages && !model.HasOemPortal && !model.CanSearchSerial && model.SelectedOemPackage is null, "Loading observation resets stale updates catalog selection and serial permission");
        Check(!await model.SearchOemAsync(true) && serialRequests.Count == 2, "History cannot send current machine serial");
        offline = true;
        await model.SearchOemAsync(false);
        Check(!model.HasOemPackages && model.HasOemPortal && model.OemStatus.Contains("offline") && model.SearchOemCommand.CanExecute(null), "Offline OEM keeps official portal and retry available");
        await model.SearchUpdatesAsync();
        Check(!model.HasUpdates && model.UpdateStatus.Contains("no disponible") && model.HasError, "Unavailable update query reports useful result");
        model.ClearErrorCommand.Execute(null);
        Check(!model.HasError, "Dismiss error command");
        var guideKinds = Enum.GetValues<ActionKind>().Where(k => k is not (ActionKind.None or ActionKind.OpenManufacturer)).ToList();
        foreach (var kind in guideKinds)
            await model.OpenGuideAsync(kind.ToString());
        Check(opened.SequenceEqual(guideKinds) && model.Actions.Count == guideKinds.Count + 1 && model.Actions.All(a => a.Outcome.Contains("no se comprobó ni aplicó")), "All Windows guide commands dispatch and log opened-only outcome");
        await model.PrepareToCloseAsync();
        Check(model.IsClosing && !model.CanStartOperation && !model.ScanCommand.CanExecute(null), "Closing waits for cleanup and disables new work");
        return checks;
    }
}
