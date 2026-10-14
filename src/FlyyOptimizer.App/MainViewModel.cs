using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using FlyyOptimizer.Core;
using FlyyOptimizer.Gaming;
using FlyyOptimizer.Windows;
using Microsoft.Win32;

namespace FlyyOptimizer.App;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ReportStore store;
    private readonly OperationLifetime lifetime = new();
    private bool busy;
    private string status = "Listo para analizar. El diagnóstico no modifica tu equipo.";
    private string error = "";
    private string driverSearch = "";
    private string updateStatus = "Consulta las ofertas de Windows Update cuando lo necesites.";
    private string presentMonPath = "";
    private string processId = "";
    private string captureDuration = "30";
    private SystemSnapshot? snapshot;
    private SessionDisplay? sessionA;
    private SessionDisplay? sessionB;
    private SavedReport? selectedReport;
    private int selectedPage;

    public MainViewModel(string? reportDirectory = null)
    {
        store = new ReportStore(reportDirectory);
        DriversView = CollectionViewSource.GetDefaultView(Drivers);
        DriversView.Filter = item => item is DriverInfo driver && (string.IsNullOrWhiteSpace(DriverSearch) || $"{driver.DeviceName} {driver.Provider} {driver.Manufacturer} {driver.DeviceClass} {driver.Version}".Contains(DriverSearch.Trim(), StringComparison.OrdinalIgnoreCase));
        ScanCommand = Command(_ => ScanAsync(), _ => CanStartOperation);
        CancelCommand = new RelayCommand(_ => lifetime.Cancel(), _ => IsBusy && lifetime.CanCancel);
        ExportCommand = Command(ExportAsync, _ => CanStartOperation && HasSnapshot);
        SearchUpdatesCommand = Command(_ => SearchUpdatesAsync(), _ => CanStartOperation);
        OpenFindingCommand = Command(OpenFindingAsync, parameter => parameter is FindingRow { HasAction: true } && CanStartOperation);
        OpenGuideCommand = Command(OpenGuideAsync, _ => CanStartOperation);
        OpenManufacturerCommand = Command(OpenManufacturerAsync, parameter => CanStartOperation && parameter is DriverInfo d && ManufacturerUrl(d) is not null);
        RefreshHistoryCommand = Command(async _ => { await RunBusyAsync("Leyendo historial…", async _ => { await RefreshHistoryAsync(); Status = "Historial actualizado."; }); }, _ => CanStartOperation);
        LoadReportCommand = Command(_ => LoadReportAsync(), _ => CanStartOperation && SelectedReport is not null);
        ImportSessionCommand = Command(ImportSessionAsync, parameter => CanStartOperation && (parameter?.ToString() != "B" || HasSessionA));
        ChoosePresentMonCommand = new RelayCommand(_ => ChoosePresentMon(), _ => CanStartOperation);
        CaptureCommand = Command(_ => CaptureAsync(), _ => CanStartOperation);
        OpenPresentMonCommand = Command(_ => OpenPresentMonAsync(), _ => CanStartOperation);
        NavigateCommand = new RelayCommand(parameter => { if (int.TryParse(parameter?.ToString(), out var index) && index is >= 0 and <= 5) SelectedPage = index; });
        ClearErrorCommand = new RelayCommand(_ => Error = "");
    }

    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand SearchUpdatesCommand { get; }
    public ICommand OpenFindingCommand { get; }
    public ICommand OpenGuideCommand { get; }
    public ICommand OpenManufacturerCommand { get; }
    public ICommand RefreshHistoryCommand { get; }
    public ICommand LoadReportCommand { get; }
    public ICommand ImportSessionCommand { get; }
    public ICommand ChoosePresentMonCommand { get; }
    public ICommand CaptureCommand { get; }
    public ICommand OpenPresentMonCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand ClearErrorCommand { get; }
    public ObservableCollection<DriverInfo> Drivers { get; } = [];
    public System.ComponentModel.ICollectionView DriversView { get; }
    public ObservableCollection<DriverUpdateInfo> Updates { get; } = [];
    public ObservableCollection<SavedReport> Reports { get; } = [];
    public ObservableCollection<ActionRecord> Actions { get; } = [];
    public SystemSnapshot? Snapshot => snapshot;
    public bool IsBusy { get => busy; private set { if (Set(ref busy, value)) { Changed(nameof(IsIdle)); CommandManager.InvalidateRequerySuggested(); } } }
    public bool IsIdle => !IsBusy;
    public bool IsClosing => lifetime.IsStopping;
    public bool CanStartOperation => !IsBusy && !IsClosing;
    public bool HasSnapshot => snapshot is not null;
    public bool HasNotes => snapshot?.Notes.Count > 0;
    public bool HasFindings => Findings.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasReports => Reports.Count > 0;
    public bool HasActions => Actions.Count > 0;
    public bool HasUpdates => Updates.Count > 0;
    public bool HasSessionA => SessionA is not null;
    public bool HasSessionB => SessionB is not null;
    public bool HasComparison => HasSessionA && HasSessionB;
    public bool EquivalentTiming => HasComparison && string.Equals(SessionA!.Session.TimingColumn, SessionB!.Session.TimingColumn, StringComparison.OrdinalIgnoreCase);
    public string ComparisonNotice => HasComparison && !EquivalentTiming ? "Las sesiones usan columnas de tiempo distintas. Sus valores se muestran, pero no se calcula un cambio; exporta ambas con la misma columna." : "Compara el mismo juego, escena y ajustes. El cambio medido no demuestra qué lo causó.";
    public bool HasProcesses => snapshot?.Processes.Count > 0;
    public bool HasStartup => snapshot?.Startup.Count > 0;
    public bool HasNetworks => snapshot?.Networks.Count > 0;
    public string Status { get => status; private set => Set(ref status, value); }
    public string Error { get => error; private set { if (Set(ref error, value)) Changed(nameof(HasError)); } }
    public string DriverSearch { get => driverSearch; set { if (Set(ref driverSearch, value)) DriversView.Refresh(); } }
    public string UpdateStatus { get => updateStatus; private set => Set(ref updateStatus, value); }
    public string PresentMonPath { get => presentMonPath; set => Set(ref presentMonPath, value); }
    public string ProcessId { get => processId; set => Set(ref processId, value); }
    public string CaptureDuration { get => captureDuration; set => Set(ref captureDuration, value); }
    public int SelectedPage { get => selectedPage; set { if (Set(ref selectedPage, value)) { Changed(nameof(PageTitle)); Changed(nameof(PageDescription)); } } }
    public SavedReport? SelectedReport { get => selectedReport; set { Set(ref selectedReport, value); CommandManager.InvalidateRequerySuggested(); } }
    public SessionDisplay? SessionA { get => sessionA; private set { Set(ref sessionA, value); Changed(null); } }
    public SessionDisplay? SessionB { get => sessionB; private set { Set(ref sessionB, value); Changed(null); } }
    public IReadOnlyList<FindingRow> Findings { get; private set; } = [];
    public IReadOnlyList<FindingRow> HighlightFindings => Findings.Take(3).ToList();
    public string PageTitle => SelectedPage switch { 1 => "Hardware", 2 => "Drivers", 3 => "Mejoras guiadas", 4 => "Gaming", 5 => "Historial", _ => "Tu equipo, con evidencia" };
    public string PageDescription => SelectedPage switch
    {
        1 => "Inventario y recursos observados durante el análisis.",
        2 => "Revisa versiones, firmas e incidencias de tus dispositivos.",
        3 => "Recomendaciones que puedes revisar en las herramientas oficiales.",
        4 => "Mide la fluidez de un juego y compara el mismo escenario.",
        5 => "Tus observaciones y las guías abiertas, guardadas en este equipo.",
        _ => "Entiende qué está pasando antes de cambiar algo."
    };
    public string Observation => snapshot is null ? "Sin observación todavía" : $"Observación del {snapshot.CapturedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm", Labels.Culture)}";
    public string MachineTitle => snapshot is null ? "Diagnóstico del equipo" : Labels.Known(string.Join(" ", new[] { snapshot.Machine.Manufacturer, snapshot.Machine.Model }.Where(s => !string.IsNullOrWhiteSpace(s))));
    public string MachineSubtitle => snapshot is null ? "Windows · análisis local" : $"{Labels.Known(snapshot.Machine.OsName)} · {Labels.Known(snapshot.Machine.OsVersion)}";
    public string CpuName => Labels.Known(snapshot?.Machine.CpuName);
    public string GpuName => Labels.Known(snapshot?.Machine.GpuName);
    public string CpuValue => Labels.Number(Mean(s => s.CpuPercent), "%");
    public string MemoryValue => Labels.Number(Mean(s => s.MemoryPercent), "%");
    public string GpuValue => Labels.Number(Mean(s => s.GpuPercent), "%");
    public string CpuTemperature => Labels.Number(Mean(s => s.CpuTemperature), " °C");
    public string GpuTemperature => Labels.Number(Mean(s => s.GpuTemperature), " °C");
    public string MemoryDetail => snapshot?.Machine.TotalMemoryGb > 0 ? $"{Labels.Number(snapshot.Machine.TotalMemoryGb, " GB", 1)} instalados" : "Capacidad no disponible";
    public string SampleDetail => snapshot is null ? "Se tomarán muestras durante unos segundos." : $"{snapshot.Samples.Count} muestras · promedio de esta observación";
    public IReadOnlyList<double?> CpuTrace => snapshot?.Samples.Select(s => s.CpuPercent).ToList() ?? [];
    public IReadOnlyList<double?> MemoryTrace => snapshot?.Samples.Select(s => s.MemoryPercent).ToList() ?? [];
    public IReadOnlyList<double?> GpuTrace => snapshot?.Samples.Select(s => s.GpuPercent).ToList() ?? [];
    public string FindingsSummary => !HasSnapshot ? "Sin análisis" : Findings.Count == 0 ? "Sin alertas en las métricas disponibles" : $"{Findings.Count} puntos para revisar";
    public string DriverCount => HasSnapshot ? $"{Drivers.Count} dispositivos" : "Sin inventario";
    public string NotesSummary => $"{snapshot?.Notes.Count ?? 0} notas de adquisición";
    public IReadOnlyList<HardwareRow> Hardware => snapshot is null ? [] :
    [
        new("Procesador", Labels.Known(snapshot.Machine.CpuName)),
        new("Procesadores lógicos", snapshot.Machine.LogicalCores > 0 ? snapshot.Machine.LogicalCores.ToString() : "No disponible"),
        new("Gráficos", Labels.Known(snapshot.Machine.GpuName)),
        new("Memoria instalada", snapshot.Machine.TotalMemoryGb > 0 ? Labels.Number(snapshot.Machine.TotalMemoryGb, " GB", 1) : "No disponible"),
        new("Sistema operativo", $"{Labels.Known(snapshot.Machine.OsName)} {snapshot.Machine.OsVersion}"),
        new("Equipo", MachineTitle),
        new("Plan de energía", Labels.Known(snapshot.Machine.PowerPlanName)),
        new("Tiempo encendido", Labels.Number(snapshot.Machine.UptimeHours, " h", 1)),
        new("Permisos actuales", snapshot.Machine.IsAdministrator ? "Administrador" : "Usuario estándar")
    ];
    public IReadOnlyList<GameMetric> GamingMetrics
    {
        get
        {
            if (SessionA is null) return [];
            var a = SessionA.Session;
            var b = SessionB?.Session;
            return
            [
                Metric("FPS promedio", a.AverageFps, b?.AverageFps, " FPS", EquivalentTiming),
                Metric("1% low", a.OnePercentLowFps, b?.OnePercentLowFps, " FPS", EquivalentTiming),
                Metric("Frame time promedio", a.AverageFrameTimeMs, b?.AverageFrameTimeMs, " ms", EquivalentTiming),
                Metric("Frame time p95", a.P95FrameTimeMs, b?.P95FrameTimeMs, " ms", EquivalentTiming),
                Metric("Frame time p99", a.P99FrameTimeMs, b?.P99FrameTimeMs, " ms", EquivalentTiming)
            ];
        }
    }
    public IReadOnlyList<double?> SessionTrace => SessionA is null ? [] : Trace(SessionA.Session.FrameTimesMs);
    public double SessionTraceLimit => SessionA?.Session.FrameTimesMs.DefaultIfEmpty(1).Max() is { } max ? Math.Max(1, max) : 1;

    private AsyncCommand Command(Func<object?, Task> execute, Func<object?, bool> canExecute) => new(execute, canExecute, ex => Error = $"No se pudo completar la operación: {ex.Message}");
    private double? Mean(Func<MetricSample, double?> selector)
    {
        var values = snapshot?.Samples.Select(selector).Where(x => x.HasValue && double.IsFinite(x.Value)).Select(x => x!.Value).ToList();
        return values is { Count: > 0 } ? values.Average() : null;
    }
    private static GameMetric Metric(string name, double a, double? b, string unit, bool equivalent) => new(name, Labels.Number(a, unit, 1), Labels.Number(b, unit, 1), b is { } v ? equivalent ? $"{(v - a >= 0 ? "+" : "")}{Labels.Number(v - a, unit, 1)}" : "No comparable" : "—");
    private static IReadOnlyList<double?> Trace(List<double> values)
    {
        if (values.Count <= 180) return values.Select(x => (double?)x).ToList();
        // Draw real evenly selected frames, with no averaging or invented interpolation.
        return Enumerable.Range(0, 180).Select(i => (double?)values[(int)((long)i * (values.Count - 1) / 179)]).ToList();
    }

    public async Task InitializeAsync()
    {
        await RunBusyAsync("Leyendo historial local…", async _ => { await RefreshHistoryAsync(); Status = "Listo para analizar. El diagnóstico no modifica tu equipo."; });
    }

    public async Task<bool> ScanAsync()
    {
        return await RunBusyAsync("Preparando análisis…", async token =>
        {
            var progress = new Progress<string>(text => Status = text);
            var result = await new WindowsCollector().CollectAsync(progress, token);
            token.ThrowIfCancellationRequested();
            ApplySnapshot(result);
            try { await store.SaveAsync(result); await RefreshHistoryAsync(); Status = "Análisis guardado. Revisa los resultados y las notas de adquisición."; }
            catch (Exception ex) { Status = "Análisis completado; no se pudo guardar el historial."; Error = $"Puedes exportar el resultado. Error al guardar: {ex.Message}"; }
        });
    }

    public void ApplySnapshot(SystemSnapshot result)
    {
        snapshot = result;
        Findings = RecommendationEngine.Analyze(result).OrderByDescending(f => f.Severity).Select(f => new FindingRow(f)).ToList();
        Drivers.Clear();
        foreach (var driver in result.Drivers.OrderBy(d => d.DeviceName)) Drivers.Add(driver);
        DriversView.Refresh();
        Updates.Clear();
        UpdateStatus = "Consulta las ofertas de Windows Update para el equipo actual. El inventario guardado no incluye ofertas de actualizaciones.";
        Changed(null);
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task<bool> RunBusyAsync(string message, Func<CancellationToken, Task> work)
    {
        if (!CanStartOperation) return false;
        IsBusy = true; Error = ""; Status = message;
        try { await lifetime.RunAsync(work); return true; }
        catch (OperationCanceledException) { Status = "Operación cancelada. Puedes volver a intentarlo."; return false; }
        catch (Exception ex) { Error = ex.Message; Status = "La operación no se completó. Revisa el detalle."; return false; }
        finally { IsBusy = false; }
    }

    private async Task ExportAsync(object? parameter)
    {
        if (snapshot is null) return;
        var markdown = parameter?.ToString() == "markdown";
        var dialog = new SaveFileDialog
        {
            Title = "Exportar informe local", Filter = markdown ? "Informe Markdown (*.md)|*.md" : "Informe JSON (*.json)|*.json",
            FileName = $"FlyyOptimizer-{snapshot.CapturedAt:yyyyMMdd-HHmm}.{(markdown ? "md" : "json")}", AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;
        var current = snapshot;
        await RunBusyAsync("Exportando informe…", async _ =>
        {
            var text = markdown ? ReportFormatter.ToMarkdown(current, Findings.Select(f => f.Finding).ToList()) : System.Text.Json.JsonSerializer.Serialize(current, ReportStore.JsonOptions);
            await File.WriteAllTextAsync(dialog.FileName, text);
            Status = $"Informe exportado: {Path.GetFileName(dialog.FileName)}";
        });
    }

    private async Task SearchUpdatesAsync()
    {
        UpdateStatus = "Buscando ofertas de drivers en Windows Update…";
        Updates.Clear(); Changed(nameof(HasUpdates));
        var completed = await RunBusyAsync(UpdateStatus, async token =>
        {
            var result = await new DriverUpdateService().FindUpdatesAsync(token);
            token.ThrowIfCancellationRequested();
            foreach (var update in result.Updates) Updates.Add(update);
            UpdateStatus = result.Success ? result.Updates.Count == 0 ? "Windows Update no ofrece drivers en esta consulta. El fabricante puede tener otras versiones." : $"{result.Updates.Count} ofertas encontradas. Revisa su instalación en Windows Update." : $"Consulta no disponible: {result.Message}";
            if (!result.Success) Error = result.Message;
            Status = result.Success ? "Consulta de Windows Update terminada." : "No se pudo consultar Windows Update.";
            Changed(nameof(HasUpdates));
        });
        if (!completed) UpdateStatus = "La consulta no se completó. Puedes volver a buscar.";
    }

    private async Task OpenFindingAsync(object? parameter)
    {
        if (parameter is not FindingRow row) return;
        await RunBusyAsync("Abriendo guía…", async _ => { WindowsActions.Open(row.Finding.Action, row.Finding.ActionUrl); await RecordGuideAsync(row.Finding.Id, row.Title); });
    }
    private async Task OpenGuideAsync(object? parameter)
    {
        if (!Enum.TryParse<ActionKind>(parameter?.ToString(), out var action) || action is ActionKind.None or ActionKind.OpenManufacturer) return;
        await RunBusyAsync("Abriendo herramienta oficial…", async _ => { WindowsActions.Open(action); await RecordGuideAsync(action.ToString(), Labels.Action(action)); });
    }
    private static string? ManufacturerUrl(DriverInfo driver) => WindowsActions.ManufacturerUrl(driver.Manufacturer) ?? WindowsActions.ManufacturerUrl(driver.Provider);
    private async Task OpenManufacturerAsync(object? parameter)
    {
        if (parameter is not DriverInfo driver || ManufacturerUrl(driver) is not { } url) return;
        await RunBusyAsync("Abriendo fuente oficial…", async _ => { WindowsActions.Open(ActionKind.OpenManufacturer, url); await RecordGuideAsync("manufacturer", $"Fabricante: {Labels.Known(driver.Manufacturer)} / {driver.DeviceName}"); });
    }
    private async Task OpenPresentMonAsync()
    {
        await RunBusyAsync("Abriendo PresentMon oficial…", async _ => { Process.Start(new ProcessStartInfo("https://github.com/GameTechDev/PresentMon/releases") { UseShellExecute = true }); await RecordGuideAsync("presentmon", "Fuente oficial de PresentMon"); });
    }
    private async Task RecordGuideAsync(string id, string title)
    {
        Status = "Guía abierta. Los cambios se realizan en la herramienta oficial.";
        try { await store.RecordActionAsync(new(DateTimeOffset.Now, id, title, "Guía abierta; no se comprobó ni aplicó un cambio.")); await RefreshHistoryAsync(); }
        catch (Exception ex) { Error = $"La guía se abrió, pero no se pudo guardar el registro: {ex.Message}"; }
    }

    private async Task RefreshHistoryAsync()
    {
        var reports = await store.ListAsync();
        Reports.Clear(); foreach (var report in reports.OrderByDescending(r => r.CapturedAt)) Reports.Add(report);
        Changed(nameof(HasReports));
        try
        {
            var actions = await store.GetActionsAsync();
            Actions.Clear(); foreach (var action in actions.OrderByDescending(a => a.At)) Actions.Add(action);
        }
        catch (Exception ex) { Actions.Clear(); Error = $"Las observaciones están disponibles, pero no se pudo leer el registro de guías: {ex.Message}"; }
        Changed(nameof(HasActions));
    }
    private async Task LoadReportAsync()
    {
        if (SelectedReport is not { } report) return;
        await RunBusyAsync("Abriendo observación guardada…", async token =>
        {
            var result = await store.LoadAsync(report.Path);
            token.ThrowIfCancellationRequested();
            ApplySnapshot(result); SelectedPage = 0;
            Status = "Observación guardada abierta. La fecha indica cuándo se midió el equipo.";
        });
    }
    private async Task ImportSessionAsync(object? parameter)
    {
        var targetB = parameter?.ToString() == "B";
        var dialog = new OpenFileDialog { Title = targetB ? "Importar sesión B de PresentMon" : "Importar sesión A de PresentMon", Filter = "PresentMon CSV (*.csv)|*.csv", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        await RunBusyAsync("Leyendo frame times…", async token =>
        {
            var session = await Task.Run(() => PresentMonCsv.Parse(dialog.FileName), token);
            token.ThrowIfCancellationRequested();
            if (targetB) SessionB = new(session); else SessionA = new(session);
            Status = $"Sesión {(targetB ? "B" : "A")} importada: {session.FrameCount:N0} frames válidos.";
        });
    }
    private void ChoosePresentMon()
    {
        var dialog = new OpenFileDialog { Title = "Seleccionar PresentMon de consola", Filter = "Ejecutable de PresentMon (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog() == true) PresentMonPath = dialog.FileName;
    }
    private async Task CaptureAsync()
    {
        if (!int.TryParse(ProcessId, out var pid) || pid <= 0) { Error = "Escribe un PID válido. Puedes encontrarlo en Detalles del Administrador de tareas."; return; }
        if (!int.TryParse(CaptureDuration, out var duration) || duration is < 15 or > 300) { Error = "Elige una duración de 15 a 300 segundos."; return; }
        if (string.IsNullOrWhiteSpace(PresentMonPath) || !File.Exists(PresentMonPath) || !string.Equals(Path.GetExtension(PresentMonPath), ".exe", StringComparison.OrdinalIgnoreCase)) { Error = "Selecciona el ejecutable de consola de PresentMon obtenido de su fuente oficial."; return; }
        var executable = PresentMonPath;
        await RunBusyAsync($"Capturando {duration} segundos del proceso {pid}…", async token =>
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlyyOptimizer", "Captures");
            Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, $"PresentMon-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv");
            var session = await new GamingCapture().CaptureAsync(executable, pid, duration, output, token);
            token.ThrowIfCancellationRequested();
            SessionA = new(session);
            Status = $"Captura terminada. CSV conservado: {Path.GetFileName(output)}";
        });
    }
    public async Task PrepareToCloseAsync()
    {
        var cleanup = lifetime.StopAsync();
        Changed(nameof(IsClosing)); Changed(nameof(CanStartOperation));
        Status = IsBusy ? "Cerrando: esperando que la operación y su limpieza terminen…" : "Cerrando…";
        CommandManager.InvalidateRequerySuggested();
        await cleanup;
    }
    public void Dispose() => lifetime.Cancel();
}
