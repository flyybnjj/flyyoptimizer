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
            if (e.Args.Length > 0)
            {
                if (e.Args.Length != 2 || e.Args[0] is not ("--scan" or "--render" or "--smoke-test"))
                    throw new ArgumentException("Uso: FlyyOptimizer.exe --scan <informe.json> | --render <vista.png> | --smoke-test <directorio>");
                if (e.Args[0] == "--scan")
                {
                    var result = await new WindowsCollector().CollectAsync(null, CancellationToken.None);
                    await new ReportStore().SaveAsync(result);
                    await WriteJsonAsync(Path.GetFullPath(e.Args[1]), result);
                    Shutdown(0); return;
                }
            }

            var model = new MainViewModel(e.Args.FirstOrDefault() == "--smoke-test" ? Path.Combine(Path.GetFullPath(e.Args[1]), "history") : null);
            var window = new MainWindow(model);
            MainWindow = window;
            window.Closed += (_, _) => { if (e.Args.Length == 0) Shutdown(0); };
            window.Show();
            await model.InitializeAsync();
            if (e.Args.Length == 0) { ShutdownMode = ShutdownMode.OnMainWindowClose; return; }
            if (!await model.ScanAsync() || model.Snapshot is null) throw new InvalidOperationException("El escaneo de verificación no terminó: " + model.Error);
            await SettleLayoutAsync(window);
            if (e.Args[0] == "--render")
            {
                await RenderAsync(window, Path.GetFullPath(e.Args[1]));
            }
            else
            {
                var directory = Path.GetFullPath(e.Args[1]);
                Directory.CreateDirectory(directory);
                await WriteJsonAsync(Path.Combine(directory, "snapshot.json"), model.Snapshot);
                await File.WriteAllTextAsync(Path.Combine(directory, "report.md"), ReportFormatter.ToMarkdown(model.Snapshot, RecommendationEngine.Analyze(model.Snapshot)));
                var pages = new[] { "summary", "hardware", "drivers", "improvements", "gaming", "history" };
                var initialActual = new { Width = window.ActualWidth, Height = window.ActualHeight };
                for (var i = 0; i < pages.Length; i++)
                {
                    model.SelectedPage = i;
                    await SettleLayoutAsync(window);
                    await RenderAsync(window, Path.Combine(directory, pages[i] + ".png"));
                }
                window.Width = window.MinWidth; window.Height = window.MinHeight;
                for (var i = 0; i < pages.Length; i++)
                {
                    model.SelectedPage = i;
                    await SettleLayoutAsync(window);
                    await RenderAsync(window, Path.Combine(directory, pages[i] + "-minimum.png"));
                }
                await WriteJsonAsync(Path.Combine(directory, "status.json"), new
                {
                    Success = true, model.Snapshot.CapturedAt, model.Snapshot.Id,
                    Samples = model.Snapshot.Samples.Count, Drivers = model.Snapshot.Drivers.Count,
                    Findings = model.Findings.Count, Notes = model.Snapshot.Notes,
                    ViewsRendered = pages, InitialSize = new { Width = 1240, Height = 820 },
                    MinimumSize = new { Width = window.MinWidth, Height = window.MinHeight },
                    InitialActualSize = initialActual, MinimumActualSize = new { Width = window.ActualWidth, Height = window.ActualHeight },
                    CapturedGaming = false, AppliedSystemChanges = false
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
                if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
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
    private static async Task SettleLayoutAsync(MainWindow window)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        // A dispatcher idle callback alone can precede native WM_SIZE delivery on DPI-scaled desktops.
        // Await a presentation frame so the final size and selected page have reached layout.
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => rendered.TrySetResult();
        CompositionTarget.Rendering += handler;
        try { await rendered.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        finally { CompositionTarget.Rendering -= handler; }
        window.UpdateLayout();
    }
    private static async Task RenderAsync(MainWindow window, string path)
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
}
