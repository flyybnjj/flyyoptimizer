using System.IO;
using System.Windows.Threading;
using FlyyOptimizer.Core;
using FlyyOptimizer.Windows;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlyyOptimizer.App;


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
        var native = new FixtureOptimizationService();
        var fortnite = new FixtureFortniteProfileService(directory);
        var approveOptimization = false;
        var optimizationPrompts = 0;
        string? capturedExecutable = null;
        var denyCapture = false;
        var elevationRequests = 0;
        var elevationCancelled = true;
        var elevatedWindowsOpened = 0;
        var services = new ViewModelServices
        {
            CreateNativeTools = () => native,
            CreateFortniteProfile = () => fortnite,
            ConfirmOptimization = () => { optimizationPrompts++; return approveOptimization; },
            ChooseFortniteFolder = () => Path.Combine(directory, "Manually selected Fortnite fixture"),
            ResolvePresentMon = () => Environment.ProcessPath!,
            CaptureDirectory = Path.Combine(directory, "fixture-captures"),
            IsElevated = () => false,
            OpenElevated = () =>
            {
                elevationRequests++;
                if (elevationCancelled) throw new System.ComponentModel.Win32Exception(1223, "Fixture UAC cancelled");
                elevatedWindowsOpened++;
            },
            Capture = (executable, _, _, output, _) =>
            {
                if (denyCapture) throw new InvalidOperationException("Fixture trace session: access denied; administrative privileges required.");
                capturedExecutable = executable;
                return Task.FromResult(new GamingSession { ProcessName = "Fixture game", SourceFile = output, FrameCount = 30, TimingColumn = "FrameTime", AverageFps = 60, AverageFrameTimeMs = 16.667, P95FrameTimeMs = 17, P99FrameTimeMs = 18, OnePercentLowFps = 55, FrameTimesMs = [16, 17, 18] });
            },
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
        var tools = model.Tools;
        var simpleFortnite = new FixtureFortniteProfileService(Path.Combine(directory, "simple"));
        using var simpleModel = new MainViewModel(Path.Combine(directory, "simple-reports"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => simpleFortnite,
            ConfirmOptimization = () => { optimizationPrompts++; return approveOptimization; },
            ResolvePresentMon = () => Environment.ProcessPath!, IsElevated = () => false,
            Open = (_, _) => throw new InvalidOperationException("Simple optimization must stay in Flyy")
        });
        var optimizeCommand = simpleModel.Fortnite.GetType().GetProperty("OptimizeCommand")?.GetValue(simpleModel.Fortnite) as ICommand;
        Check(optimizeCommand is not null && optimizeCommand.CanExecute(null), "Simple optimization button is available without exposing a preview or selection form");
        await ExecuteAsync(optimizeCommand!);
        Check(optimizationPrompts == 1 && simpleFortnite.ApplyCalls == 0 && !simpleModel.Fortnite.HasPreview, "Declining optimization makes no changes and does not prepare a hidden profile");
        approveOptimization = true;
        await ExecuteAsync(optimizeCommand!);
        Check(optimizationPrompts == 2 && simpleFortnite.AppliedIds.SequenceEqual(["winutil-game-mode", "fortnite-settings"]) && simpleModel.Fortnite.Status == "PC listo." && !simpleModel.Fortnite.HasActionError, "Confirmed optimization prepares applicable defaults automatically and reports ready after verified completion");
        simpleFortnite.PartialResultNext = true;
        await ExecuteAsync(optimizeCommand!);
        Check(simpleModel.Fortnite.HasActionError && simpleModel.Fortnite.Status != "PC listo.", "A partial failure never reports PC ready in the simple flow");
        Check(simpleModel.Fortnite.HasFailedSteps && simpleModel.Fortnite.FailedSteps.All(step => !step.Success && !string.IsNullOrWhiteSpace(step.Message)), "Partial optimization exposes failed step messages for the error disclosure");
        var selectionCalls = simpleFortnite.ApplyCalls;
        approveOptimization = false;
        await ExecuteAsync(simpleModel.Fortnite.ApplySelectionCommand);
        Check(simpleFortnite.ApplyCalls == selectionCalls, "Declining manual selection makes no system changes");
        approveOptimization = true;
        simpleModel.Fortnite.Options.Single(option => option.Option.Id == "winutil-game-mode").Selected = false;
        await ExecuteAsync(simpleModel.Fortnite.ApplySelectionCommand);
        Check(simpleFortnite.AppliedIds.SequenceEqual(["fortnite-settings"]) && !simpleModel.Fortnite.HasActionError, "Confirmed manual selection applies only the chosen available controls");
        var updateMode = 0;
        var cancelDownload = false;
        var offeredDriver = new DriverUpdateInfo("Driver disponible", "Fixture", "Display", "", "2026-10-14", "12345678-1234-1234-1234-123456789abc", 1);
        using var updateModel = new MainViewModel(Path.Combine(directory, "updates-flow"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => simpleFortnite,
            ResolvePresentMon = () => Environment.ProcessPath!,
            IsElevated = () => true,
            FindUpdates = _ => Task.FromResult(updateMode == 0 ? new DriverUpdateResult(true, [], "Fixture empty") : updateMode == 1 ? new DriverUpdateResult(true, [offeredDriver], "Fixture offers") : new DriverUpdateResult(false, [offeredDriver], "Fixture incomplete")),
            DownloadDriverUpdates = async (updates, token) =>
            {
                if (cancelDownload) { await Task.Delay(Timeout.Infinite, token); }
                return new(true, updates.Select(update => update.UpdateId).ToList(), "Descarga lista; no se instalaron drivers.");
            }
        });
        var updateWindow = new MainWindow(updateModel) { Width = 1240, Height = 820, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        updateModel.SelectedPage = 2; updateWindow.Show();
        await updateModel.SearchUpdatesAsync();
        Check(!updateModel.HasUpdates && !updateModel.DownloadUpdatesCommand.CanExecute(null) && updateModel.UpdateStatus.Contains("No hay actualizaciones"), "No-update search shows a clear empty result and no downloadable actions");
        await RenderFixtureWindowAsync(updateWindow, Path.Combine(directory, "updates-none.png"));
        updateMode = 1;
        await updateModel.SearchUpdatesAsync();
        Check(updateModel.HasUpdates && updateModel.HasDownloadableUpdates && updateModel.DownloadUpdatesCommand.CanExecute(null), "Verified offers expose download and available-update list");
        ((Expander)updateWindow.FindName("AvailableUpdates")).IsExpanded = true;
        await RenderFixtureWindowAsync(updateWindow, Path.Combine(directory, "updates-available.png"));
        await updateModel.DownloadUpdatesAsync();
        Check(updateModel.Updates.Single().Downloaded && !updateModel.HasDownloadableUpdates && !updateModel.DownloadUpdatesCommand.CanExecute(null) && updateModel.DownloadUpdatesLabel == "Descargadas", "Download completion marks only confirmed payloads downloaded and disables repeat download");
        await RenderFixtureWindowAsync(updateWindow, Path.Combine(directory, "updates-downloaded.png"));
        await updateModel.SearchUpdatesAsync();
        cancelDownload = true;
        var pendingDownload = updateModel.DownloadUpdatesAsync();
        Check(updateModel.IsBusy && !updateModel.ScanCommand.CanExecute(null) && updateModel.CancelCommand.CanExecute(null), "Driver download shares operation gating and cancellation");
        updateModel.CancelCommand.Execute(null);
        await pendingDownload;
        Check(!updateModel.Updates.Single().Downloaded && updateModel.UpdateStatus.Contains("cancelada") && updateModel.DownloadUpdatesCommand.CanExecute(null), "Cancelled download keeps offers retryable without claiming downloaded");
        updateMode = 2;
        await updateModel.SearchUpdatesAsync();
        Check(!updateModel.HasUpdates && !updateModel.HasDownloadableUpdates && updateModel.HasError && updateModel.UpdateStatus.Contains("no disponible"), "Incomplete query does not expose unverified downloads or claim there are no updates");
        updateWindow.Hide(); updateWindow.Close();
        var permitElevation = false;
        using var permissionModel = new MainViewModel(Path.Combine(directory, "update-permission"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => simpleFortnite,
            ResolvePresentMon = () => Environment.ProcessPath!, IsElevated = () => false,
            FindUpdates = _ => Task.FromResult(new DriverUpdateResult(true, [offeredDriver], "Fixture offer")),
            DownloadDriverUpdates = (_, _) => throw new InvalidOperationException("Downloads must wait for elevation"),
            OpenElevatedForUpdates = () => { if (!permitElevation) throw new System.ComponentModel.Win32Exception(1223); }
        });
        await permissionModel.SearchUpdatesAsync();
        await permissionModel.DownloadUpdatesAsync();
        Check(permissionModel.DownloadNeedsPermission && permissionModel.UpdateStatus.Contains("Permiso cancelado") && permissionModel.DownloadUpdatesCommand.CanExecute(null), "Cancelled download permission keeps a clear recovery and does not start a download");
        permitElevation = true;
        await permissionModel.DownloadUpdatesAsync();
        var driverStart = Elevation.CreateDriverStartInfo();
        Check(permissionModel.UpdateStatus.Contains("Flyy se abrió") && driverStart.ArgumentList.SequenceEqual(["--drivers"]) && driverStart.Verb == "runas" && driverStart.FileName == Path.Combine(AppContext.BaseDirectory, "FlyyOptimizer.exe"), "Download elevation targets only Flyy and opens the Drivers view without starting a hidden installation");
        Check(!model.Fortnite.HasPreview && !model.Fortnite.ApplyCommand.CanExecute(null) && fortnite.ApplyCalls == 0, "Fortnite optimization requires reviewing detected installation and selected changes before any mutation");
        await model.Fortnite.DetectAsync();
        Check(model.Fortnite.HasPreview && model.Fortnite.IsInstalled && fortnite.ApplyCalls == 0 && model.Fortnite.Options.Count == 4 && !model.Fortnite.Options.Single(o => o.Option.Id == "gpu-preference").Selected && !model.Fortnite.Options.Single(o => o.Option.Id == "animations").Selected, "Fortnite preview detects paths without mutating and omits unavailable or optional changes from selection");
        model.Fortnite.Options.Single(o => o.Option.Id == "winutil-game-mode").Selected = false;
        await model.Fortnite.ApplyAsync();
        Check(fortnite.AppliedIds.SequenceEqual(["fortnite-settings"]) && model.Fortnite.Steps.Single().Changed && model.Fortnite.HasChanges && opened.Count == 0, "Optimize PC dispatches only selected available Fortnite steps and presents actual results with restore history");
        model.Fortnite.SelectedChange = model.Fortnite.Changes.Single();
        await model.Fortnite.UndoAsync();
        Check(fortnite.RestoredIds.SequenceEqual(["fixture-fortnite-change"]) && !model.Fortnite.HasChanges, "Fortnite restore dispatches selected saved change and refreshes history");
        fortnite.PartialCancelNext = true;
        await model.Fortnite.ApplyAsync();
        Check(model.Fortnite.HasChanges && model.Fortnite.HasActionError && model.Fortnite.Status.Contains("cancelado") && fortnite.PreviewTokens[^1] == CancellationToken.None, "A partially cancelled Fortnite profile reloads saved backups without the cancelled token");
        fortnite.FailPreviewNext = true;
        await model.Fortnite.DetectAsync();
        Check(!model.Fortnite.HasPreview && model.Fortnite.Options.Count == 0 && model.Fortnite.HasActionError && !model.Fortnite.ApplyCommand.CanExecute(null), "A failed replacement Fortnite preview disables stale selections and requires detecting again");
        await ExecuteAsync(model.Fortnite.ChooseFolderCommand);
        Check(model.Fortnite.IsInstalled && model.Fortnite.InstallPath.EndsWith("Manually selected Fortnite fixture") && fortnite.LastRequestedDirectory == model.Fortnite.InstallPath, "Manual folder choice is validated through profile preview and displayed in the app");
        model.Fortnite.Options.Single(o => o.Option.Id == "gpu-preference").Selected = true;
        Check(!model.Fortnite.Options.Single(o => o.Option.Id == "gpu-preference").Selected, "Unavailable Fortnite option cannot be selected through programmatic or keyboard input");
        fortnite.PartialResultNext = true;
        fortnite.CancelResultNext = true;
        await model.Fortnite.ApplyAsync();
        Check(model.Fortnite.Steps.Count == 2 && model.Fortnite.Steps.Count(s => s.Changed) == 1 && model.Fortnite.Steps.Any(s => !s.Success) && model.Fortnite.HasActionError && fortnite.LastRequestedDirectory == model.Fortnite.InstallPath, "Mixed profile results preserve successful and skipped steps instead of claiming complete optimization");
        fortnite.MissingInstallation = true;
        var appliedBeforeMissing = fortnite.ApplyCalls;
        await model.Fortnite.DetectAsync();
        Check(!model.Fortnite.IsInstalled && !model.Fortnite.Options.Single(o => o.Option.Id == "fortnite-settings").Option.Available && fortnite.ApplyCalls == appliedBeforeMissing, "Missing Fortnite disables game configuration and keeps preview read-only");
        fortnite.MissingInstallation = false;
        await model.Fortnite.DetectAsync();
        Check(tools.RefreshCommand.CanExecute(null) && !tools.ApplyPowerCommand.CanExecute(null) && !tools.UndoCommand.CanExecute(null), "Native improvement inspection is available before a full scan");
        await tools.RefreshAsync();
        Check(native.Mutations == 0 && !model.HasSnapshot && tools.HasInspection && tools.GamingToggles.Count == 2 && tools.ImprovementToggles.Single().State == "Activado", "Inspecting concrete settings needs no scan and makes no mutations");
        async Task ExecuteAsync(ICommand command, object? parameter = null)
        {
            Check(command.CanExecute(parameter), "Tool command is available for selected target");
            command.Execute(parameter);
            while (model.IsBusy) await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        await ExecuteAsync(tools.DisableToggleCommand, tools.ImprovementToggles.Single());
        Check(tools.ImprovementToggles.Single().State == "Desactivado" && tools.HasChanges, "Applying animations refreshes actual state and exposes reversible change");
        tools.SelectedChange = tools.Changes.Single();
        await ExecuteAsync(tools.UndoCommand);
        Check(tools.ImprovementToggles.Single().State == "Activado" && !tools.HasChanges, "Undo restores previous state and refreshes change list");
        await ExecuteAsync(tools.EnableToggleCommand, tools.GamingToggles.Single(t => t.Setting.Id == "game-mode"));
        await ExecuteAsync(tools.DisableToggleCommand, tools.GamingToggles.Single(t => t.Setting.Id == "xbox-captures"));
        Check(tools.GamingToggles.Single(t => t.Setting.Id == "game-mode").Setting.Enabled == true && tools.GamingToggles.Single(t => t.Setting.Id == "xbox-captures").Setting.Enabled == false, "Gaming controls dispatch separate Game Mode and Xbox capture changes");
        tools.SelectedPowerScheme = tools.PowerSchemes.Single(p => p.Id == "performance");
        await ExecuteAsync(tools.ApplyPowerCommand);
        Check(tools.PowerSchemes.Single(p => p.Id == "performance").Active && !tools.ApplyPowerCommand.CanExecute(null), "Applying selected existing power plan refreshes active state");
        var powerControl = new ContentControl { Content = tools, ContentTemplate = (DataTemplate)Application.Current.FindResource("PowerSchemeTemplate") };
        powerControl.Measure(new Size(700, 240)); powerControl.Arrange(new Rect(0, 0, 700, 240)); powerControl.UpdateLayout();
        Check(VisibleText(powerControl).Contains("Rendimiento fixture"), "Power selector displays selected plan name rather than object or GUID");
        tools.SelectedStartup = tools.StartupEntries.Single();
        await ExecuteAsync(tools.ToggleStartupCommand);
        Check(!tools.StartupEntries.Single().Entry.Enabled && tools.StartupEntries.Single().State == "Retirada de Run" && tools.StartupActionLabel == "Restaurar entrada", "Startup action removes only selected Run entry and offers restoring it");
        await ExecuteAsync(tools.PreviewTempCommand);
        Check(tools.TemporaryFiles.Count == 2 && !tools.CleanTempCommand.CanExecute(null) && tools.TemporaryFiles.All(f => !f.Selected), "Cleanup previews files without deleting or selecting them automatically");
        tools.TemporaryFiles[0].Selected = true;
        native.CancelPreviewNext = true;
        await ExecuteAsync(tools.PreviewTempCommand);
        Check(!tools.HasPreview && !tools.HasTemporaryFiles && !tools.CleanTempCommand.CanExecute(null) && tools.CleanupStatus.Contains("cancelada"), "Cancelled replacement preview clears stale file IDs selection and cleanup command");
        await ExecuteAsync(tools.PreviewTempCommand);
        tools.TemporaryFiles[1].Selected = true;
        await ExecuteAsync(tools.CleanTempCommand);
        Check(native.CleanedIds.SequenceEqual(["second"]) && !tools.HasTemporaryFiles && tools.CleanupStatus.Contains("2,0 KiB"), "Cleanup sends only selected file IDs and shows recovered bytes with a fresh preview required");
        var countBeforeFailure = tools.Changes.Count;
        native.FailNext = true;
        await ExecuteAsync(tools.EnableToggleCommand, tools.ImprovementToggles.Single() with { Setting = tools.ImprovementToggles.Single().Setting with { Enabled = null } });
        Check(tools.Status.Contains("Fixture permission denied") && tools.HasActionError && tools.Changes.Count == countBeforeFailure && model.Status.Contains("Fixture permission denied"), "Rejected change reports backend reason distinctly and footer finishes without a successful change");
        native.PartialCancelNext = true;
        await ExecuteAsync(tools.EnableToggleCommand, tools.GamingToggles.Single(t => t.Setting.Id == "xbox-captures"));
        Check(tools.GamingToggles.Single(t => t.Setting.Id == "xbox-captures").Setting.Enabled == true && tools.Changes.Count == countBeforeFailure + 1 && tools.Status.Contains("cancelada"), "Cancelled partial change refreshes current state and saved undo record before releasing operation");
        Check(model.HasBundledCollector && model.CaptureCommand.CanExecute(null), "Included collector enables capture without executable selection");
        model.ProcessId = Environment.ProcessId.ToString();
        await model.CaptureAsync();
        Check(model.HasSessionA && capturedExecutable == Environment.ProcessPath && model.SessionA!.Session.FrameCount == 30 && model.SessionA.Source.EndsWith(".csv"), "Capture uses resolved included collector and presents measured result");
        denyCapture = true;
        await model.CaptureAsync();
        Check(model.OpenElevatedCommand.CanExecute(null) && model.CanRequestCaptureElevation && elevationRequests == 0 && model.HasCaptureError && !model.HasError, "ETW permission failure offers explicit elevation without launching it automatically");
        await ExecuteAsync(model.OpenElevatedCommand);
        Check(elevationRequests == 1 && elevatedWindowsOpened == 0 && model.CaptureError.Contains("Cancelaste") && model.OpenElevatedCommand.CanExecute(null) && model.Status.Contains("cancelada"), "Cancelled UAC request gives inline recovery and allows an explicit retry");
        elevationCancelled = false;
        await ExecuteAsync(model.OpenElevatedCommand);
        Check(elevationRequests == 2 && elevatedWindowsOpened == 1 && !model.CanRequestCaptureElevation && model.HasSessionA && !model.IsClosing && model.Status.Contains("Esta sesión sigue disponible"), "Explicit elevation launches a new instance and keeps original observation session available");
        var elevatedStart = Elevation.CreateStartInfo();
        Check(elevatedStart.FileName == Path.Combine(AppContext.BaseDirectory, "FlyyOptimizer.exe") && elevatedStart.WorkingDirectory == AppContext.BaseDirectory && elevatedStart.UseShellExecute && elevatedStart.Verb == "runas" && elevatedStart.ArgumentList.Count == 0 && elevatedStart.Arguments == "" && elevatedStart.WindowStyle == System.Diagnostics.ProcessWindowStyle.Normal, "Elevation launch targets only this apphost with native UAC and no smoke or external-tool arguments");
        denyCapture = false;
        using (var elevatedModel = new MainViewModel(Path.Combine(directory, "already-elevated"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), ResolvePresentMon = () => Environment.ProcessPath!,
            IsElevated = () => true, OpenElevated = () => throw new InvalidOperationException("Fixture must never elevate an elevated process"),
            CaptureDirectory = Path.Combine(directory, "elevated-fixture-captures"),
            Capture = (_, _, _, _, _) => throw new InvalidOperationException("Fixture access denied; administrative privileges required")
        }))
        {
            elevatedModel.ProcessId = Environment.ProcessId.ToString();
            await elevatedModel.CaptureAsync();
            Check(elevatedModel.HasCaptureError && !elevatedModel.CanRequestCaptureElevation && !elevatedModel.OpenElevatedCommand.CanExecute(null), "An already elevated process keeps permission error without offering another elevation");
        }
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
        Check(!model.ValidateCaptureInputs(out _, out _) && model.HasCaptureError && !model.HasError && !model.CanRequestCaptureElevation && !model.OpenElevatedCommand.CanExecute(null), "Invalid capture input stays beside capture form without offering elevation");
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
        var guidesBeforeStartup = model.Actions.Count;
        var integratedSections = new List<string>();
        model.SectionNavigationRequested += integratedSections.Add;
        await model.OpenGuideAsync("OpenStartup");
        Check(model.SelectedPage == 3 && opened.Count == 0 && model.Actions.Count == guidesBeforeStartup, "Review startup stays inside Improvements without opening Windows or logging an external guide");
        Check(integratedSections.SequenceEqual(["StartupSection"]) && tools.HasStartup, "Review startup requests the editable startup section with current local entries");
        var startupFinding = new FindingRow(new("fixture-startup", "Revisar inicio", "Fixture", "Fixture", Severity.Info, "Inicio", ActionKind.OpenStartup));
        model.SelectedPage = 0;
        await ExecuteAsync(model.OpenFindingCommand, startupFinding);
        Check(model.SelectedPage == 3 && integratedSections.Count == 2 && integratedSections[^1] == "StartupSection" && opened.Count == 0 && model.Actions.Count == guidesBeforeStartup, "Summary recommendation opens the same integrated startup control without an external action");
        var guideKinds = Enum.GetValues<ActionKind>().Where(k => k is not (ActionKind.None or ActionKind.OpenManufacturer)).ToList();
        foreach (var kind in guideKinds)
            await model.OpenGuideAsync(kind.ToString());
        var externalGuides = new[] { ActionKind.OpenWindowsUpdate, ActionKind.OpenOptionalDrivers, ActionKind.OpenGraphics, ActionKind.OpenTaskManager };
        Check(opened.SequenceEqual(externalGuides) && model.Actions.Count == externalGuides.Length + 1 && model.Actions.All(a => a.Outcome.Contains("no se comprobó ni aplicó")), "Only explicit Windows capabilities without integrated controls dispatch and log external opened-only outcome");
        model.ProcessId = Environment.ProcessId.ToString(); denyCapture = true;
        await model.CaptureAsync(); denyCapture = false;
        native.BlockInspection = true;
        var pendingTools = tools.RefreshAsync();
        await native.InspectionStarted.Task;
        Check(model.IsBusy && !model.ScanCommand.CanExecute(null) && !model.CaptureCommand.CanExecute(null) && !tools.PreviewTempCommand.CanExecute(null) && !model.OpenElevatedCommand.CanExecute(null) && model.CancelCommand.CanExecute(null), "Native operation shares scan capture cleanup elevation and cancel gating");
        var closing = model.PrepareToCloseAsync();
        await native.InspectionCancelled.Task;
        model.OpenElevatedCommand.Execute(null);
        Check(!closing.IsCompleted && !tools.RefreshCommand.CanExecute(null) && !model.OpenElevatedCommand.CanExecute(null) && elevationRequests == 2, "Closing awaits native cleanup and blocks new tool and elevation launches");
        native.ReleaseInspection.TrySetResult();
        await closing; await pendingTools;
        Check(model.IsClosing && !model.CanStartOperation && !model.ScanCommand.CanExecute(null), "Closing waits for cleanup and disables new work");
        using var unavailable = new MainViewModel(Path.Combine(directory, "missing-collector"), new ViewModelServices { CreateNativeTools = () => new FixtureOptimizationService(), ResolvePresentMon = () => throw new InvalidOperationException("Fixture bundled collector missing; extract FlyyOptimizer again.") });
        Check(!unavailable.HasBundledCollector && !unavailable.CaptureCommand.CanExecute(null) && unavailable.CollectorStatus.Contains("extract"), "Missing bundled collector blocks capture with package recovery instruction");
        using var navigationModel = new MainViewModel(Path.Combine(directory, "navigation-visual"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), ResolvePresentMon = () => Environment.ProcessPath!,
            Open = (_, _) => throw new InvalidOperationException("An integrated action must never launch another tool")
        });
        var navigationWindow = new MainWindow(navigationModel) { Width = 1240, Height = 820, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        navigationWindow.Show();
        await dispatcher.InvokeAsync(navigationWindow.UpdateLayout, DispatcherPriority.Loaded);
        Check(navigationWindow.Icon is not null, "Packaged window has the Flyy icon");
        navigationModel.SelectedPage = 4;
        await navigationModel.WaitForFortniteDetectionAsync();
        await dispatcher.InvokeAsync(navigationWindow.UpdateLayout, DispatcherPriority.Loaded);
        Check(!VisibleText(navigationWindow, true).Any(text => text.Contains("Medir la fluidez") || text.Contains("Cómo leer las métricas") || text.Contains("PID manual")), "Gaming exposes performance choices without the removed capture and metric flow");
        navigationModel.SelectedPage = 2;
        await dispatcher.InvokeAsync(navigationWindow.UpdateLayout, DispatcherPriority.Loaded);
        Check(VisibleText(navigationWindow, true).Contains("Drivers para tu equipo") && !VisibleText(navigationWindow, true).Contains("Inventario de drivers"), "Unscanned driver page shows discovery instead of empty inventory");
        await navigationModel.OpenGuideAsync("OpenStartup");
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        navigationWindow.UpdateLayout();
        Check(navigationWindow.Pages.SelectedContent is ScrollViewer { VerticalOffset: > 0 } && navigationWindow.FindName("StartupSection") is FrameworkElement { ActualHeight: > 0 }, "Review startup scrolls the actual Improvements page to its editable section");
        navigationWindow.Hide();
        var closingProfile = new FixtureFortniteProfileService(directory) { BlockApply = true, BlockRecovery = true };
        using var closingProfileModel = new MainViewModel(Path.Combine(directory, "profile-closing"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => closingProfile,
            ResolvePresentMon = () => Environment.ProcessPath!, Open = (_, _) => throw new InvalidOperationException("Profile cannot open external tools")
        });
        await closingProfileModel.Fortnite.DetectAsync();
        var applyingProfile = closingProfileModel.Fortnite.ApplyAsync();
        await closingProfile.ApplyStarted.Task;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        Check(closingProfileModel.IsBusy && closingProfileModel.Fortnite.IsApplying && closingProfileModel.Fortnite.ProgressPercent == 50 && !closingProfileModel.ScanCommand.CanExecute(null) && !closingProfileModel.Tools.RefreshCommand.CanExecute(null) && !closingProfileModel.Fortnite.ApplyCommand.CanExecute(null), "Fortnite application reports real completed steps while sharing scan and native-tool busy gating");
        var profileClose = closingProfileModel.PrepareToCloseAsync();
        await closingProfile.ApplyCancelled.Task; await closingProfile.RecoveryStarted.Task;
        Check(!profileClose.IsCompleted && !closingProfileModel.Fortnite.DetectCommand.CanExecute(null) && !closingProfileModel.OpenFortniteCommand.CanExecute(null), "Closing cancels Fortnite work and waits for partial-change recovery before releasing the app");
        closingProfile.ReleaseRecovery.TrySetResult();
        await applyingProfile; await profileClose;
        Check(closingProfileModel.IsClosing && closingProfileModel.Fortnite.HasChanges && closingProfile.PreviewTokens[^1] == CancellationToken.None && !closingProfileModel.Fortnite.UndoCommand.CanExecute(null), "Closing finishes profile recovery with backups visible and all new profile actions blocked");
        return checks;
    }

    private static IEnumerable<string> VisibleText(DependencyObject root, bool visibleOnly = false)
    {
        if (visibleOnly && root is UIElement { IsVisible: false }) yield break;
        if (root is TextBlock text) yield return text.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in VisibleText(VisualTreeHelper.GetChild(root, i), visibleOnly)) yield return child;
    }

    private static async Task RenderFixtureWindowAsync(Window window, string path)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => rendered.TrySetResult();
        CompositionTarget.Rendering += handler;
        try { await rendered.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        finally { CompositionTarget.Rendering -= handler; }
        window.UpdateLayout();
        var visual = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private sealed class FixtureFortniteProfileService(string directory) : IFortniteProfileService
    {
        private readonly List<AppliedOptimization> changes = [];
        public int ApplyCalls { get; private set; }
        public bool PartialCancelNext { get; set; }
        public bool PartialResultNext { get; set; }
        public bool CancelResultNext { get; set; }
        public bool FailPreviewNext { get; set; }
        public bool MissingInstallation { get; set; }
        public bool BlockApply { get; set; }
        public bool BlockRecovery { get; set; }
        public string? LastRequestedDirectory { get; private set; }
        public TaskCompletionSource ApplyStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ApplyCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource RecoveryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRecovery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyCollection<string> AppliedIds { get; private set; } = [];
        public List<string> RestoredIds { get; } = [];
        public List<CancellationToken> PreviewTokens { get; } = [];
        public async Task<FortniteProfilePreview> PreviewAsync(string? installDirectory = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); PreviewTokens.Add(cancellationToken); LastRequestedDirectory = installDirectory;
            if (FailPreviewNext) { FailPreviewNext = false; throw new InvalidOperationException("Fixture detection permission denied"); }
            if (BlockRecovery && ApplyCalls > 0) { RecoveryStarted.TrySetResult(); await ReleaseRecovery.Task; }
            return new FortniteProfilePreview(new(!MissingInstallation, MissingInstallation ? null : installDirectory ?? Path.Combine(directory, "Fortnite fixture"), MissingInstallation ? null : Path.Combine(directory, "FortniteClient-Win64-Shipping.exe"), MissingInstallation ? null : Path.Combine(directory, "GameUserSettings.ini"), false, MissingInstallation ? "Fixture not installed" : "Fixture installation detected"),
                [new("winutil-game-mode", "Modo Juego", "Fixture system setting", true, true, "Disponible"), new("fortnite-settings", "Calidad competitiva", "Fixture lower visual quality", !MissingInstallation, true, "Juego cerrado"), new("gpu-preference", "GPU", "Fixture unavailable GPU", false, true, "No disponible"), new("animations", "Animaciones", "Fixture optional visuals", true, false, "Opcional")], changes.ToList(), "Fixture preview");
        }
        public async Task<FortniteProfileResult> ApplyAsync(IReadOnlyCollection<string> optionIds, IProgress<FortniteProfileProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); ApplyCalls++; AppliedIds = optionIds.ToArray();
            changes.Add(new(ApplyCalls == 1 ? "fixture-fortnite-change" : $"fixture-fortnite-change-{ApplyCalls}", "Fortnite fixture", DateTimeOffset.Now, true, "Fixture backup"));
            if (BlockApply)
            {
                progress?.Report(new(1, 2, "Fixture first change complete")); ApplyStarted.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { ApplyCancelled.TrySetResult(); throw; }
            }
            if (PartialCancelNext) { PartialCancelNext = false; throw new OperationCanceledException("Fixture parcialmente cancelado", cancellationToken); }
            if (PartialResultNext)
            {
                PartialResultNext = false;
                var cancelled = CancelResultNext; CancelResultNext = false;
                return new([new("winutil-game-mode", "Modo Juego", true, true, "Fixture applied"), new("fortnite-settings", "Calidad competitiva", false, false, "Fixture game opened; skipped")], cancelled, "Fixture partially applied");
            }
            progress?.Report(new(1, 1, "Fixture step"));
            return new(optionIds.Select(id => new FortniteProfileStepResult(id, "Fixture ajuste", true, true, "Fixture actual change")).ToList(), false, "Fixture profile applied");
        }
        public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); RestoredIds.Add(changeId); changes.Clear();
            return Task.FromResult(new OptimizationOutcome(true, "Fixture restored", true));
        }
    }

    private sealed class FixtureOptimizationService : INativeOptimizationService
    {
        private readonly List<ToggleSetting> toggles = [new("animations", "Animaciones", "Fixture animations", true, "Fixture"), new("game-mode", "Modo Juego", "Fixture Game Mode", false, "Fixture"), new("xbox-captures", "Capturas Xbox", "Fixture Xbox recording", true, "Fixture")];
        private readonly List<PowerScheme> power = [new("balanced", "Equilibrado", true), new("performance", "Rendimiento fixture", false)];
        private readonly List<FlyyOptimizer.Windows.StartupEntry> startup = [new("user-run", "Fixture launcher", "fixture.exe", true, "Usuario actual")];
        private readonly List<AppliedOptimization> changes = [];
        private readonly Dictionary<string, Action> restores = [];
        public int Mutations { get; private set; }
        public bool FailNext { get; set; }
        public bool PartialCancelNext { get; set; }
        public bool CancelPreviewNext { get; set; }
        public bool BlockInspection { get; set; }
        public TaskCompletionSource InspectionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource InspectionCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseInspection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyCollection<string> CleanedIds { get; private set; } = [];
        public async Task<OptimizationOverview> InspectAsync(CancellationToken cancellationToken = default)
        {
            if (BlockInspection)
            {
                InspectionStarted.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { InspectionCancelled.TrySetResult(); await ReleaseInspection.Task; throw; }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(toggles.ToList(), power.ToList(), startup.ToList(), changes.ToList());
        }
        private Task<OptimizationOutcome> Change(string title, Action apply, Action restore, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (FailNext) { FailNext = false; return Task.FromResult(new OptimizationOutcome(false, "Fixture permission denied")); }
            apply(); Mutations++;
            var id = Guid.NewGuid().ToString("N");
            changes.Add(new(id, title, DateTimeOffset.Now, true, "Fixture saved previous state")); restores.Add(id, restore);
            if (PartialCancelNext) { PartialCancelNext = false; throw new OperationCanceledException(token); }
            return Task.FromResult(new OptimizationOutcome(true, "Fixture change applied and checked", true));
        }
        public Task<OptimizationOutcome> SetToggleAsync(string id, bool enabled, CancellationToken cancellationToken = default)
        {
            var index = toggles.FindIndex(t => t.Id == id); var previous = toggles[index];
            return Change(previous.Name, () => toggles[index] = previous with { Enabled = enabled }, () => toggles[index] = previous, cancellationToken);
        }
        public Task<OptimizationOutcome> SetPowerSchemeAsync(string id, CancellationToken cancellationToken = default)
        {
            var previous = power.ToList();
            return Change("Fixture power", () => { for (var i = 0; i < power.Count; i++) power[i] = power[i] with { Active = power[i].Id == id }; }, () => { power.Clear(); power.AddRange(previous); }, cancellationToken);
        }
        public Task<OptimizationOutcome> SetStartupEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default)
        {
            var index = startup.FindIndex(s => s.Id == id); var previous = startup[index];
            return Change(previous.Name, () => startup[index] = previous with { Enabled = enabled }, () => startup[index] = previous, cancellationToken);
        }
        public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); restores[changeId](); restores.Remove(changeId); changes.RemoveAll(c => c.Id == changeId); Mutations++;
            return Task.FromResult(new OptimizationOutcome(true, "Fixture previous state restored", true));
        }
        public Task<TemporaryCleanupPreview> PreviewTempAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancelPreviewNext) { CancelPreviewNext = false; throw new OperationCanceledException(cancellationToken); }
            return Task.FromResult(new TemporaryCleanupPreview([new("first", "fixture-first.tmp", 1024), new("second", "fixture-second.tmp", 2048)], 3072, false, "Fixture preview"));
        }
        public Task<TemporaryCleanupResult> CleanTempAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); CleanedIds = ids.ToArray(); Mutations++;
            return Task.FromResult(new TemporaryCleanupResult(1, 0, 2048, "Fixture selected file removed"));
        }
    }
}
