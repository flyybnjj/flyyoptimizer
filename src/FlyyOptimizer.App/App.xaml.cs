using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlyyOptimizer.Core;
using FlyyOptimizer.Windows;

namespace FlyyOptimizer.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var driverMode = e.Args.Length == 1 && e.Args[0] == "--drivers";
            if (e.Args.Length > 0 && !driverMode)
            {
                if (e.Args.Length != 2 || e.Args[0] is not ("--scan" or "--render" or "--smoke-test" or "--interaction-test"))
                    throw new ArgumentException("Uso: FlyyOptimizer.exe --scan <informe.json> | --render <vista.png> | --smoke-test <directorio> | --interaction-test <directorio>");
                if (e.Args[0] == "--scan")
                {
                    var result = await new WindowsCollector().CollectAsync(null, CancellationToken.None);
                    await new ReportStore().SaveAsync(result);
                    await WriteJsonAsync(Path.GetFullPath(e.Args[1]), result);
                    Shutdown(0); return;
                }
                if (e.Args[0] == "--interaction-test")
                {
                    var directory = Path.GetFullPath(e.Args[1]);
                    var checks = await InteractionSmokeChecks.RunAsync(Path.Combine(directory, "interaction-fixtures"), Dispatcher);
                    await WriteJsonAsync(Path.Combine(directory, "interaction-checks.json"), new { Success = true, FixtureOnly = true, Checks = checks, ExternalToolsOpened = false, SystemSettingsApplied = false, CapturedGaming = false, ViewsRendered = false });
                    Shutdown(0); return;
                }
            }

            var model = new MainViewModel(e.Args.FirstOrDefault() == "--smoke-test" ? Path.Combine(Path.GetFullPath(e.Args[1]), "history") : null);
            var window = new MainWindow(model);
            MainWindow = window;
            window.Closed += (_, _) => { if (e.Args.Length == 0 || driverMode) Shutdown(0); };
            window.Show();
            await model.InitializeAsync();
            if (e.Args.Length == 0 || driverMode)
            { if (driverMode) model.SelectedPage = 2; ShutdownMode = ShutdownMode.OnMainWindowClose; return; }
            Task? liveRender = null;
            System.ComponentModel.PropertyChangedEventHandler? captureLive = null;
            if (e.Args[0] == "--smoke-test")
            {
                var renderDirectory = Path.GetFullPath(e.Args[1]);
                await RenderAsync(window, Path.Combine(renderDirectory, "empty.png"));
                model.SelectedPage = 2;
                await RenderAsync(window, Path.Combine(renderDirectory, "drivers-empty.png"));
                model.SelectedPage = 0;
                window.Width = window.MinWidth; window.Height = window.MinHeight;
                await RenderAsync(window, Path.Combine(renderDirectory, "empty-minimum.png"));
                window.Width = 1240; window.Height = 820;
                await SettleLayoutAsync(window);
                captureLive = (_, _) =>
                {
                    if (liveRender is null && model.IsScanning && model.CpuTrace.Count >= 2)
                        liveRender = RenderAsync(window, Path.Combine(renderDirectory, "scan-live.png"));
                };
                model.PropertyChanged += captureLive;
            }
            try
            {
                if (!await model.ScanAsync() || model.Snapshot is null) throw new InvalidOperationException("El escaneo de verificación no terminó: " + model.Error);
                if (liveRender is not null) await liveRender;
            }
            finally { if (captureLive is not null) model.PropertyChanged -= captureLive; }
            await SettleLayoutAsync(window);
            if (e.Args[0] == "--render")
            {
                await RenderAsync(window, Path.GetFullPath(e.Args[1]));
            }
            else
            {
                var directory = Path.GetFullPath(e.Args[1]);
                Directory.CreateDirectory(directory);
                var interactionChecks = await InteractionSmokeChecks.RunAsync(Path.Combine(directory, "interaction-fixtures"), window.Dispatcher);
                await WriteJsonAsync(Path.Combine(directory, "interaction-checks.json"), new { Success = true, FixtureOnly = true, Checks = interactionChecks, ExternalToolsOpened = false, SystemSettingsApplied = false, CapturedGaming = false });
                await WriteJsonAsync(Path.Combine(directory, "snapshot.json"), model.Snapshot);
                await File.WriteAllTextAsync(Path.Combine(directory, "report.md"), ReportFormatter.ToMarkdown(model.Snapshot, RecommendationEngine.Analyze(model.Snapshot)));
                var pages = new[] { "summary", "hardware", "drivers", "improvements", "gaming", "history" };
                var initialActual = new { Width = window.ActualWidth, Height = window.ActualHeight };
                for (var i = 0; i < pages.Length; i++)
                {
                    model.SelectedPage = i;
                    if (i == 4)
                    {
                        await model.WaitForFortniteDetectionAsync();
                        if (!model.Fortnite.HasPreview) throw new InvalidOperationException("El perfil integrado de Fortnite no pudo cargar: " + model.Fortnite.Status);
                    }
                    await SettleLayoutAsync(window);
                    await RenderAsync(window, Path.Combine(directory, pages[i] + ".png"));
                }
                await RenderDeepViewsAsync(window, model, directory, "");
                window.Width = window.MinWidth; window.Height = window.MinHeight;
                for (var i = 0; i < pages.Length; i++)
                {
                    model.SelectedPage = i;
                    await SettleLayoutAsync(window);
                    await RenderAsync(window, Path.Combine(directory, pages[i] + "-minimum.png"));
                }
                await RenderDeepViewsAsync(window, model, directory, "-minimum");
                var confirmation = new OptimizationConfirmation { Owner = window, ShowActivated = false };
                confirmation.Show();
                await RenderAsync(confirmation, Path.Combine(directory, "optimization-confirmation.png"));
                confirmation.Close();
                await WriteJsonAsync(Path.Combine(directory, "status.json"), new
                {
                    Success = true, model.Snapshot.CapturedAt, model.Snapshot.Id,
                    Samples = model.Snapshot.Samples.Count, Drivers = model.Snapshot.Drivers.Count,
                    Findings = model.Findings.Count, Notes = model.Snapshot.Notes,
                    ViewsRendered = pages, InitialSize = new { Width = 1240, Height = 820 },
                    MinimumSize = new { Width = window.MinWidth, Height = window.MinHeight },
                    InitialActualSize = initialActual, MinimumActualSize = new { Width = window.ActualWidth, Height = window.ActualHeight },
                    CapturedGaming = false, AppliedSystemChanges = false,
                    InteractionChecks = interactionChecks.Count, ModelLookup = model.OemStatus, ModelPackages = model.OemPackages.Count, CaptureProcesses = model.CaptureProcesses.Count,
                    SerialLookupRequested = false, LiveScanRendered = liveRender is not null,
                    FortnitePreviewReady = model.Fortnite.HasPreview, FortniteInstalled = model.Fortnite.IsInstalled,
                    FortniteDetectedDirectory = model.Fortnite.IsInstalled ? model.Fortnite.InstallPath : null,
                    FortniteAvailableOptions = model.Fortnite.Options.Count(option => option.Option.Available)
                });
            }
            model.Dispose(); window.Close(); Shutdown(0);
        }
        catch (Exception ex)
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlyyOptimizer");
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "last-error.txt"), $"{DateTimeOffset.Now:O}\n{ex}");
                if (e.Args.Length == 2 && e.Args[0] is "--smoke-test" or "--interaction-test")
                {
                    Directory.CreateDirectory(Path.GetFullPath(e.Args[1]));
                    await WriteJsonAsync(Path.Combine(Path.GetFullPath(e.Args[1]), "status.json"), new { Success = false, Error = ex.Message });
                }
            }
            catch { /* The exit code remains authoritative if the diagnostics folder is unavailable. */ }
            if (e.Args.Length == 0) MessageBox.Show($"FlyyOptimizer no pudo iniciar. {ex.Message}", "FlyyOptimizer", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(e.Args.Length > 0 && ex is ArgumentException ? 2 : 1);
        }
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, ReportStore.JsonOptions));
    }
    private static async Task SettleLayoutAsync(Window window)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Task.Delay(Motion.SettleMilliseconds);
        // A dispatcher idle callback alone can precede native WM_SIZE delivery on DPI-scaled desktops.
        // Await a presentation frame so the final size and selected page have reached layout.
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => rendered.TrySetResult();
        CompositionTarget.Rendering += handler;
        try { await rendered.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        finally { CompositionTarget.Rendering -= handler; }
        window.UpdateLayout();
    }
    private static async Task RenderAsync(Window window, string path)
    {
        await SettleLayoutAsync(window);
        var visual = (FrameworkElement)window.Content;
        var width = Math.Max(1, (int)Math.Ceiling(visual.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(visual.ActualHeight));
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        image.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static async Task RenderDeepViewsAsync(MainWindow window, MainViewModel model, string directory, string suffix)
    {
        foreach (var (page, offset, name) in new[] { (2, 350d, "drivers-package"), (3, 380d, "improvements-startup"), (3, 770d, "improvements-cleanup"), (3, 1100d, "improvements-undo"), (4, 420d, "gaming-options") })
        {
            model.SelectedPage = page;
            if (page == 2) model.SelectedOemPackage = model.OemPackages.FirstOrDefault();
            await SettleLayoutAsync(window);
            if (window.Pages.SelectedContent is System.Windows.Controls.ScrollViewer viewer)
            {
                viewer.ScrollToVerticalOffset(offset);
                await RenderAsync(window, Path.Combine(directory, name + suffix + ".png"));
                viewer.ScrollToTop();
                await SettleLayoutAsync(window);
            }
        }
        model.SelectedOemPackage = null;
    }
}
