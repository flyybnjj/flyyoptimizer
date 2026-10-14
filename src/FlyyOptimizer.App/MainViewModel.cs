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
    private readonly ViewModelServices services;
    private readonly bool processElevated;
    private readonly OperationLifetime lifetime = new();
    private readonly List<MetricSample> liveSamples = [];
    private bool busy;
    private bool scanning;
    private bool snapshotIsCurrent;
    private int scanGeneration;
    private ScanProgress scanProgress = new("Preparando análisis", "", 0, 1);
    private string status = "Listo para analizar. El diagnóstico no modifica tu equipo.";
    private string error = "";
    private string driverSearch = "";
    private string updateStatus = "Consulta las ofertas de Windows Update cuando lo necesites.";
    private string collectorStatus = "El colector gaming viene incluido con FlyyOptimizer.";
    private bool hasBundledCollector;
    private string processId = "";
    private string captureDuration = "30";
    private string captureError = "";
    private string oemSearch = "";
    private string oemStatus = "Analiza tu equipo para buscar su modelo en la fuente oficial.";
    private OemSupportResult? oemResult;
    private OemDriverPackage? selectedOemPackage;
    private ProcessChoice? selectedProcess;
    private SystemSnapshot? snapshot;
    private SessionDisplay? sessionA;
    private SessionDisplay? sessionB;
    private SavedReport? selectedReport;
    private int selectedPage;
    private Task<bool>? fortniteDetection;

    public MainViewModel(string? reportDirectory = null) : this(reportDirectory, new ViewModelServices()) { }

    internal MainViewModel(string? reportDirectory, ViewModelServices services)
    {
        store = new ReportStore(reportDirectory);
        this.services = services;
        processElevated = services.IsElevated();
        Tools = new(services.CreateNativeTools(), RunBusyAsync, () => CanStartOperation);
        Fortnite = new(services.CreateFortniteProfile(), RunBusyAsync, () => CanStartOperation, Tools.RefreshStateAsync, services.ChooseFortniteFolder, services.ConfirmOptimization);
        Fortnite.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(Fortnite.Status)) Status = string.IsNullOrEmpty(Fortnite.Status) ? "Listo para optimizar." : Fortnite.Status; };
        Tools.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(Tools.Status)) Status = Tools.Status;
            else if (change.PropertyName == nameof(Tools.CleanupStatus)) Status = Tools.CleanupStatus;
        };
        RefreshCollectorState();
        DriversView = CollectionViewSource.GetDefaultView(Drivers);
        DriversView.Filter = item => item is DriverInfo driver && (string.IsNullOrWhiteSpace(DriverSearch) || $"{driver.DeviceName} {driver.Provider} {driver.Manufacturer} {driver.DeviceClass} {driver.Version}".Contains(DriverSearch.Trim(), StringComparison.OrdinalIgnoreCase));
        ScanCommand = Command(_ => ScanAsync(), _ => CanStartOperation);
        OemPackagesView = CollectionViewSource.GetDefaultView(OemPackages);
        OemPackagesView.Filter = item => item is OemDriverPackage package && (string.IsNullOrWhiteSpace(OemSearch) || $"{package.Name} {package.Category} {package.Version} {package.OperatingSystem}".Contains(OemSearch.Trim(), StringComparison.OrdinalIgnoreCase));
        CancelCommand = new RelayCommand(_ => { Status = "Cancelando: esperando que termine la operación…"; lifetime.Cancel(); CommandManager.InvalidateRequerySuggested(); }, _ => IsBusy && lifetime.CanCancel);
        ExportCommand = Command(ExportAsync, _ => CanStartOperation && HasSnapshot);
        SearchUpdatesCommand = Command(_ => SearchUpdatesAsync(), _ => CanStartOperation);
        OpenFindingCommand = Command(OpenFindingAsync, parameter => parameter is FindingRow { HasAction: true } && CanStartOperation);
        OpenGuideCommand = Command(OpenGuideAsync, _ => CanStartOperation);
        OpenManufacturerCommand = Command(OpenManufacturerAsync, parameter => CanStartOperation && parameter is DriverInfo d && ManufacturerUrl(d) is not null);
        SearchOemCommand = Command(_ => SearchOemAsync(false), _ => CanStartOperation && HasSnapshot);
        SearchOemSerialCommand = Command(_ => SearchOemAsync(true), _ => CanStartOperation && CanSearchSerial);
        OpenOemPortalCommand = Command(_ => OpenOemAsync(false), _ => CanStartOperation && HasOemPortal);
        OpenOemPackageCommand = Command(_ => OpenOemAsync(true), _ => CanStartOperation && CanOpenOemPackage);
        RefreshHistoryCommand = Command(async _ => { await RunBusyAsync("Leyendo historial…", async _ => { await RefreshHistoryAsync(); Status = "Historial actualizado."; }); }, _ => CanStartOperation);
        LoadReportCommand = Command(_ => LoadReportAsync(), _ => CanStartOperation && SelectedReport is not null);
        ImportSessionCommand = Command(ImportSessionAsync, parameter => CanStartOperation && (parameter?.ToString() != "B" || HasSessionA));
        CaptureCommand = Command(_ => CaptureAsync(), _ => CanStartOperation && HasBundledCollector);
        OpenElevatedCommand = Command(_ => OpenElevatedAsync(), _ => CanStartOperation && CanRequestCaptureElevation);
        RefreshProcessesCommand = Command(_ => RefreshProcessesAsync(), _ => CanStartOperation);
        NavigateCommand = new RelayCommand(parameter => { if (int.TryParse(parameter?.ToString(), out var index) && index is >= 0 and <= 5) SelectedPage = index; });
        OpenFortniteCommand = Command(_ => OpenFortniteAsync(), _ => CanStartOperation);
        ClearErrorCommand = new RelayCommand(_ => Error = "");
    }

    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand SearchUpdatesCommand { get; }
    public ICommand OpenFindingCommand { get; }
    public ICommand OpenGuideCommand { get; }
    public ICommand OpenManufacturerCommand { get; }
    public ICommand SearchOemCommand { get; }
    public ICommand SearchOemSerialCommand { get; }
    public ICommand OpenOemPortalCommand { get; }
    public ICommand OpenOemPackageCommand { get; }
    public ICommand RefreshHistoryCommand { get; }
    public ICommand LoadReportCommand { get; }
    public ICommand ImportSessionCommand { get; }
    public ICommand CaptureCommand { get; }
    public ICommand OpenElevatedCommand { get; }
    public ICommand RefreshProcessesCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand OpenFortniteCommand { get; }
    public ICommand ClearErrorCommand { get; }
    public event Action<string>? SectionNavigationRequested;
    public ToolsViewModel Tools { get; }
    public FortniteViewModel Fortnite { get; }
    public ObservableCollection<DriverInfo> Drivers { get; } = [];
    public System.ComponentModel.ICollectionView DriversView { get; }
    public ObservableCollection<DriverUpdateInfo> Updates { get; } = [];
    public ObservableCollection<OemDriverPackage> OemPackages { get; } = [];
    public System.ComponentModel.ICollectionView OemPackagesView { get; }
    public ObservableCollection<ProcessChoice> CaptureProcesses { get; } = [];
    public ObservableCollection<SavedReport> Reports { get; } = [];
    public ObservableCollection<ActionRecord> Actions { get; } = [];
    public SystemSnapshot? Snapshot => snapshot;
    public bool IsBusy { get => busy; private set { if (Set(ref busy, value)) { Changed(nameof(IsIdle)); Changed(nameof(IsBackgroundBusy)); CommandManager.InvalidateRequerySuggested(); } } }
    public bool IsIdle => !IsBusy;
    public bool IsBackgroundBusy => IsBusy && !IsScanning;
    public bool IsScanning { get => scanning; private set { if (Set(ref scanning, value)) Changed(null); } }
    public bool ShowStart => !HasSnapshot && !IsScanning;
    public bool HasResourceData => HasSnapshot || IsScanning;
    public bool HasCompletedResult => HasSnapshot && !IsScanning;
    public string ScanStage => scanProgress.Stage;
    public string ScanDetail => scanProgress.Detail;
    public double ScanPercent => scanProgress.TotalSteps > 0 ? Math.Clamp(100d * scanProgress.CompletedSteps / scanProgress.TotalSteps, 0, 100) : 0;
    public string ScanPercentLabel => $"{ScanPercent:N0}% · {scanProgress.CompletedSteps}/{scanProgress.TotalSteps} pasos completados";
    public bool IsClosing => lifetime.IsStopping;
    public bool CanStartOperation => !IsBusy && !IsClosing;
    public bool HasSnapshot => snapshot is not null;
    public bool HasNotes => snapshot?.Notes.Count > 0;
    public bool HasFindings => Findings.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasReports => Reports.Count > 0;
    public bool HasActions => Actions.Count > 0;
    public bool HasUpdates => Updates.Count > 0;
    public bool HasOemPackages => OemPackages.Count > 0;
    public bool HasOemPortal => !string.IsNullOrWhiteSpace(oemResult?.SupportUrl);
    public bool CanSearchSerial => HasSnapshot && snapshotIsCurrent && !string.IsNullOrWhiteSpace(snapshot?.Machine.Manufacturer);
    public bool HasSelectedOemPackage => SelectedOemPackage is not null;
    public bool CanOpenOemPackage => !string.IsNullOrWhiteSpace(SelectedOemPackage?.DownloadUrl ?? SelectedOemPackage?.DetailUrl);
    public string OemPackageActionLabel => !string.IsNullOrWhiteSpace(SelectedOemPackage?.DownloadUrl) ? "Abrir descarga oficial" : "Ver paquete oficial";
    public string OemSelectedSourceUrl => Labels.Known(SelectedOemPackage?.DetailUrl ?? SelectedOemPackage?.DownloadUrl);
    public bool HasCaptureProcesses => CaptureProcesses.Count > 0;
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
    public string OemStatus { get => oemStatus; private set => Set(ref oemStatus, value); }
    public string OemSearch { get => oemSearch; set { if (Set(ref oemSearch, value)) { OemPackagesView.Refresh(); Changed(nameof(OemFilterSummary)); } } }
    public string OemFilterSummary => $"{OemPackagesView.Cast<object>().Count()} de {OemPackages.Count} paquetes del catálogo";
    public string OemDeviceName => oemResult?.DeviceName ?? MachineTitle;
    public string OemSource => oemResult is null ? "Fuente pendiente" : $"{oemResult.Provider} · {(oemResult.ModelMatched ? "modelo identificado" : "portal oficial")}";
    public string OemPackageNotice => "El catálogo es para este modelo. No confirma qué paquete está instalado ni que sea una actualización.";
    public string SerialNotice => snapshotIsCurrent ? "«Buscar por serie» envía la serie solo al fabricante oficial; se muestra enmascarada y no se guarda ni se exporta." : "La serie requiere analizar este equipo. Desde el historial puedes buscar por modelo.";
    public string SerialDisplay => string.IsNullOrWhiteSpace(oemResult?.SerialDisplay) ? "" : $"Serie consultada: {oemResult.SerialDisplay}";
    public bool HasSerialDisplay => !string.IsNullOrWhiteSpace(oemResult?.SerialDisplay);
    public OemDriverPackage? SelectedOemPackage { get => selectedOemPackage; set { if (Set(ref selectedOemPackage, value)) { Changed(nameof(HasSelectedOemPackage)); Changed(nameof(CanOpenOemPackage)); Changed(nameof(OemPackageActionLabel)); Changed(nameof(OemSelectedSourceUrl)); CommandManager.InvalidateRequerySuggested(); } } }
    public string CollectorStatus => collectorStatus;
    public bool HasBundledCollector => hasBundledCollector;
    public string ProcessId { get => processId; set { if (Set(ref processId, value)) { CaptureError = ""; if (selectedProcess is not null && selectedProcess.Id.ToString() != value) { selectedProcess = null; Changed(nameof(SelectedProcess)); } } } }
    public string CaptureDuration { get => captureDuration; set { if (Set(ref captureDuration, value)) CaptureError = ""; } }
    public string CaptureError { get => captureError; private set { if (Set(ref captureError, value)) { Changed(nameof(HasCaptureError)); Changed(nameof(CanRequestCaptureElevation)); CommandManager.InvalidateRequerySuggested(); } } }
    public bool HasCaptureError => !string.IsNullOrWhiteSpace(CaptureError);
    public bool CanRequestCaptureElevation => !processElevated && HasCaptureError &&
        (CaptureError.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
         CaptureError.Contains("administrative privileges", StringComparison.OrdinalIgnoreCase) ||
         CaptureError.Contains("acceso denegado", StringComparison.OrdinalIgnoreCase) ||
         CaptureError.Contains("requiere permisos de administrador", StringComparison.OrdinalIgnoreCase));
    public ProcessChoice? SelectedProcess { get => selectedProcess; set { if (Set(ref selectedProcess, value) && value is not null) ProcessId = value.Id.ToString(); } }
    public int SelectedPage
    {
        get => selectedPage;
        set
        {
            if (!Set(ref selectedPage, value)) return;
            Changed(nameof(PageTitle)); Changed(nameof(PageDescription));
            if (value == 4 && !Fortnite.HasPreview && CanStartOperation) fortniteDetection = Fortnite.DetectAsync();
        }
    }
    public SavedReport? SelectedReport { get => selectedReport; set { Set(ref selectedReport, value); CommandManager.InvalidateRequerySuggested(); } }
    internal Task WaitForFortniteDetectionAsync() => (Task?)fortniteDetection ?? Task.CompletedTask;
    public SessionDisplay? SessionA { get => sessionA; private set { Set(ref sessionA, value); Changed(null); } }
    public SessionDisplay? SessionB { get => sessionB; private set { Set(ref sessionB, value); Changed(null); } }
    public IReadOnlyList<FindingRow> Findings { get; private set; } = [];
    public IReadOnlyList<FindingRow> HighlightFindings => Findings.Take(3).ToList();
    public string PageTitle => SelectedPage switch { 1 => "Hardware", 2 => "Drivers", 3 => "Mejoras", 4 => "Gaming", 5 => "Historial", _ => "Tu equipo, con evidencia" };
    public string PageDescription => SelectedPage switch
    {
        1 => "Inventario y recursos observados durante el análisis.",
        2 => "Revisa versiones, firmas e incidencias de tus dispositivos.",
        3 => "Aplica ajustes concretos y conserva su estado anterior para deshacerlos.",
        4 => "Ajusta Windows para jugar y mide la fluidez con el colector incluido.",
        5 => "Tus observaciones y las guías abiertas, guardadas en este equipo.",
        _ => "Entiende qué está pasando antes de cambiar algo."
    };
    public string Observation => snapshot is null ? "Sin observación todavía" : $"Observación del {snapshot.CapturedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm", Labels.Culture)}";
    public string MachineTitle => IsScanning ? "Observando recursos en vivo" : snapshot is null ? "Diagnóstico del equipo" : Labels.Known(string.Join(" ", new[] { snapshot.Machine.Manufacturer, string.IsNullOrWhiteSpace(snapshot.Machine.ProductName) ? snapshot.Machine.Model : snapshot.Machine.ProductName }.Where(s => !string.IsNullOrWhiteSpace(s))));
    public string DeviceKindLabel => snapshot?.Machine.DeviceKind switch { DeviceKind.Laptop => "Portátil", DeviceKind.Desktop => "Escritorio", _ => "Tipo de equipo desconocido" };
    public string DeviceIdentity => IsScanning || snapshot is null ? "El tipo de equipo y el modelo aparecerán al terminar." : $"{DeviceKindLabel} · Modelo {Labels.Known(snapshot.Machine.Model)} · SKU {Labels.Known(snapshot.Machine.SystemSku)}";
    public string MachineSubtitle => IsScanning ? "Lecturas reales del equipo actual; los huecos indican datos no disponibles." : snapshot is null ? "Windows · análisis local" : $"{Labels.Known(snapshot.Machine.OsName)} · {Labels.Known(snapshot.Machine.OsVersion)}";
    public string CpuName => IsScanning ? "CPU del equipo actual" : Labels.Known(snapshot?.Machine.CpuName);
    public string GpuName => IsScanning ? "Gráficos del equipo actual" : Labels.Known(snapshot?.Machine.GpuName);
    public string CpuValue => Labels.Number(Value(s => s.CpuPercent), "%");
    public string MemoryValue => Labels.Number(Value(s => s.MemoryPercent), "%");
    public string GpuValue => Labels.Number(Value(s => s.GpuPercent), "%");
    public string LiveSummary => $"CPU {CpuValue} · Memoria {MemoryValue} · GPU {GpuValue}";
    public string CpuTemperature => Labels.Number(Value(s => s.CpuTemperature), " °C");
    public string GpuTemperature => Labels.Number(Value(s => s.GpuTemperature), " °C");
    public string MemoryDetail => IsScanning ? "Memoria en uso en la última lectura" : snapshot?.Machine.TotalMemoryGb > 0 ? $"{Labels.Number(snapshot.Machine.TotalMemoryGb, " GB", 1)} instalados" : "Capacidad no disponible";
    public string SampleDetail => IsScanning ? $"{liveSamples.Count} muestras reales · última lectura" : snapshot is null ? "Se tomarán muestras durante unos segundos." : $"{snapshot.Samples.Count} muestras · promedio de esta observación";
    public string MetricSampleLabel => IsScanning ? "Última muestra recibida" : "Promedio de muestras válidas";
    private IReadOnlyList<MetricSample> DisplaySamples => IsScanning ? liveSamples : snapshot?.Samples ?? [];
    public IReadOnlyList<double?> CpuTrace => DisplaySamples.Select(s => s.CpuPercent).ToList();
    public IReadOnlyList<double?> MemoryTrace => DisplaySamples.Select(s => s.MemoryPercent).ToList();
    public IReadOnlyList<double?> GpuTrace => DisplaySamples.Select(s => s.GpuPercent).ToList();
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
        new("Tipo de equipo", DeviceKindLabel),
        new("Modelo / SKU", $"{Labels.Known(snapshot.Machine.Model)} / {Labels.Known(snapshot.Machine.SystemSku)}"),
        new("Evidencia del tipo", Labels.Known(snapshot.Machine.DeviceTypeEvidence)),
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
    private double? Value(Func<MetricSample, double?> selector) => IsScanning ? liveSamples.LastOrDefault() is { } sample ? selector(sample) : null : Mean(selector);
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
        if (!IsClosing) await Tools.RefreshAsync();
    }

    public async Task<bool> ScanAsync()
    {
        var completed = await RunBusyAsync("Preparando análisis…", async token =>
        {
            var generation = ++scanGeneration;
            liveSamples.Clear(); scanProgress = new("Preparando análisis", "Reuniendo las fuentes de Windows.", 0, 1); IsScanning = true;
            try
            {
                var progress = new Progress<string>(text => { if (generation == scanGeneration && !token.IsCancellationRequested) Status = text; });
                var detailed = new Progress<ScanProgress>(value => { if (generation == scanGeneration && !token.IsCancellationRequested) ApplyScanProgress(value); });
                var result = await services.Collect(progress, token, detailed);
                token.ThrowIfCancellationRequested();
                IsScanning = false; scanGeneration++;
                ApplySnapshot(result, true);
                try { await store.SaveAsync(result); await RefreshHistoryAsync(); Status = "Análisis guardado. Revisa Mejoras o consulta los drivers del modelo."; }
                catch (Exception ex) { Status = "Análisis completado; no se pudo guardar el historial."; Error = $"Puedes exportar el resultado. Error al guardar: {ex.Message}"; }
            }
            catch (OperationCanceledException) { Status = "Análisis cancelado. Las muestras parciales no se guardaron."; throw; }
            finally { IsScanning = false; scanGeneration++; liveSamples.Clear(); Changed(null); }
        });
        if (completed && !IsClosing)
        {
            var savedStatus = Status;
            var savedError = Error;
            var lookedUp = await SearchOemAsync(false);
            if (!IsClosing) Status = lookedUp ? $"{savedStatus} {(!HasOemPackages ? "Consulta la fuente oficial en Drivers." : $"{OemPackages.Count} paquetes de modelo en Drivers.")}" : $"{savedStatus} La búsqueda del fabricante no se completó; reintenta en Drivers.";
            if (!string.IsNullOrWhiteSpace(savedError)) Error = savedError;
        }
        return completed;
    }

    internal void ApplyScanProgress(ScanProgress value)
    {
        scanProgress = value;
        if (value.Sample is not null) liveSamples.Add(value.Sample);
        Changed(null);
    }

    public void ApplySnapshot(SystemSnapshot result, bool isCurrent = false)
    {
        snapshot = result;
        snapshotIsCurrent = isCurrent;
        Findings = RecommendationEngine.Analyze(result).OrderByDescending(f => f.Severity).Select(f => new FindingRow(f)).ToList();
        Drivers.Clear();
        foreach (var driver in result.Drivers.OrderBy(d => d.DeviceName)) Drivers.Add(driver);
        DriversView.Refresh();
        Updates.Clear();
        OemPackages.Clear(); oemResult = null; SelectedOemPackage = null; OemSearch = "";
        OemStatus = isCurrent ? "Buscando el modelo en la fuente oficial…" : "Observación guardada: busca por modelo si necesitas su catálogo. La serie no se consulta desde el historial.";
        if (isCurrent) SetCaptureProcesses(result.Processes);
        else
        {
            // PIDs can be reused. Archived inventory is evidence, never a live capture candidate.
            SetCaptureProcesses([]);
            ProcessId = "";
        }
        UpdateStatus = "Consulta las ofertas de Windows Update para el equipo actual. El inventario guardado no incluye ofertas de actualizaciones.";
        Changed(null);
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task<bool> RunBusyAsync(string message, Func<CancellationToken, Task> work)
    {
        if (!CanStartOperation) return false;
        IsBusy = true; Error = ""; Status = message;
        try { await lifetime.RunAsync(work); return true; }
        catch (OperationCanceledException) { if (!Status.StartsWith("Análisis cancelado", StringComparison.Ordinal)) Status = "Operación cancelada. Puedes volver a intentarlo."; return false; }
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

    internal async Task SearchUpdatesAsync()
    {
        UpdateStatus = "Buscando ofertas de drivers en Windows Update…";
        Updates.Clear(); Changed(nameof(HasUpdates));
        var completed = await RunBusyAsync(UpdateStatus, async token =>
        {
            var result = await services.FindUpdates(token);
            token.ThrowIfCancellationRequested();
            foreach (var update in result.Updates) Updates.Add(update);
            UpdateStatus = result.Success ? result.Updates.Count == 0 ? "Windows Update no ofrece drivers en esta consulta. El fabricante puede tener otras versiones." : $"{result.Updates.Count} ofertas encontradas. Revisa su instalación en Windows Update." : $"Consulta no disponible: {result.Message}";
            if (!result.Success) Error = result.Message;
            Status = result.Success ? "Consulta de Windows Update terminada." : "No se pudo consultar Windows Update.";
            Changed(nameof(HasUpdates));
        });
        if (!completed) UpdateStatus = "La consulta no se completó. Puedes volver a buscar.";
    }

    internal async Task<bool> SearchOemAsync(bool useSerial)
    {
        if (snapshot is null || useSerial && !CanSearchSerial || !CanStartOperation) return false;
        var machine = snapshot.Machine;
        OemStatus = useSerial ? "Consultando la serie en la fuente oficial…" : "Buscando el modelo en la fuente oficial…";
        OemPackages.Clear(); SelectedOemPackage = null; oemResult = null; Changed(null);
        var completed = await RunBusyAsync(OemStatus, async token =>
        {
            var result = await services.FindOem(machine, useSerial, token);
            token.ThrowIfCancellationRequested();
            oemResult = result;
            foreach (var package in result.Packages) OemPackages.Add(package);
            OemStatus = result.Success && result.Packages.Count > 0 ? $"{result.Packages.Count} paquetes del catálogo oficial encontrados." : result.Message;
            Status = result.Success ? "Consulta del fabricante terminada. Revisa los paquetes o abre su portal oficial." : "La consulta del fabricante no respondió. Puedes reintentar o abrir su portal oficial.";
            Changed(null);
        });
        if (!completed) { OemStatus = "La consulta no se completó. Reintenta la búsqueda por modelo."; Changed(null); }
        return completed;
    }

    internal async Task OpenOemAsync(bool package)
    {
        var url = package ? SelectedOemPackage?.DownloadUrl ?? SelectedOemPackage?.DetailUrl : oemResult?.SupportUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        await RunBusyAsync("Abriendo fuente oficial…", async _ =>
        {
            services.Open(ActionKind.OpenManufacturer, url);
            await RecordGuideAsync(package ? "oem-package" : "oem-portal", package ? $"Paquete oficial: {SelectedOemPackage!.Name}" : $"Portal oficial: {oemResult!.Provider}");
            Status = package ? "Enlace oficial abierto. Revisa compatibilidad y versión antes de instalar desde el fabricante." : "Portal oficial abierto. La selección del driver se realiza en el fabricante.";
        });
    }

    private async Task OpenFindingAsync(object? parameter)
    {
        if (parameter is not FindingRow row) return;
        if (await OpenIntegratedAsync(row.Finding.Action)) return;
        await RunBusyAsync("Abriendo guía…", async _ => { services.Open(row.Finding.Action, row.Finding.ActionUrl); await RecordGuideAsync(row.Finding.Id, row.Title); });
    }
    internal async Task OpenGuideAsync(object? parameter)
    {
        if (!Enum.TryParse<ActionKind>(parameter?.ToString(), out var action) || action is ActionKind.None or ActionKind.OpenManufacturer) return;
        if (await OpenIntegratedAsync(action)) return;
        await RunBusyAsync("Abriendo herramienta oficial…", async _ => { services.Open(action, null); await RecordGuideAsync(action.ToString(), Labels.Action(action)); });
    }
    private async Task<bool> OpenIntegratedAsync(ActionKind action)
    {
        var section = action switch
        {
            ActionKind.OpenStartup => "StartupSection",
            ActionKind.OpenPower => "PowerSection",
            ActionKind.OpenStorage => "TemporarySection",
            ActionKind.OpenGameMode => "GamingSettingsSection",
            _ => null
        };
        if (section is null) return false;
        if (!CanStartOperation) return true;
        SelectedPage = action == ActionKind.OpenGameMode ? 4 : 3;
        if (fortniteDetection is { IsCompleted: false }) await fortniteDetection;
        await Tools.RefreshAsync();
        if (!IsClosing)
        {
            SectionNavigationRequested?.Invoke(section);
            Status = action switch
            {
                ActionKind.OpenStartup => "Inicio del usuario dentro de Flyy: elige una entrada para retirarla o restaurarla.",
                ActionKind.OpenStorage => "Temporales dentro de Flyy: revisa los archivos antes de eliminarlos.",
                ActionKind.OpenPower => "Energía dentro de Flyy: elige uno de los planes disponibles en este equipo.",
                _ => "Modo Juego dentro de Flyy: revisa el estado antes de cambiarlo."
            };
        }
        return true;
    }
    private async Task OpenFortniteAsync()
    {
        if (!CanStartOperation) return;
        SelectedPage = 4;
        if (fortniteDetection is not null) await fortniteDetection;
        if (!Fortnite.HasPreview && CanStartOperation) await Fortnite.DetectAsync();
        if (!IsClosing) SectionNavigationRequested?.Invoke("FortniteProfileSection");
    }
    private static string? ManufacturerUrl(DriverInfo driver) => WindowsActions.ManufacturerUrl(driver.Manufacturer) ?? WindowsActions.ManufacturerUrl(driver.Provider);
    private async Task OpenManufacturerAsync(object? parameter)
    {
        if (parameter is not DriverInfo driver || ManufacturerUrl(driver) is not { } url) return;
        await RunBusyAsync("Abriendo fuente oficial…", async _ => { services.Open(ActionKind.OpenManufacturer, url); await RecordGuideAsync("manufacturer", $"Fabricante: {Labels.Known(driver.Manufacturer)} / {driver.DeviceName}"); });
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
            var session = await Task.Run(() => PresentMonCsv.Parse(dialog.FileName, token), token);
            token.ThrowIfCancellationRequested();
            if (targetB) SessionB = new(session); else { SessionA = new(session); SessionB = null; }
            Status = $"Sesión {(targetB ? "B" : "A")} importada: {session.FrameCount:N0} frames válidos.";
        });
    }
    private void SetCaptureProcesses(IEnumerable<ProcessInfo> processes)
    {
        var selectedId = SelectedProcess?.Id;
        CaptureProcesses.Clear();
        foreach (var process in processes.OrderByDescending(p => p.MemoryMb).Take(30)) CaptureProcesses.Add(new(process.Name, process.Id, process.MemoryMb));
        SelectedProcess = CaptureProcesses.FirstOrDefault(p => p.Id == selectedId);
        Changed(nameof(HasCaptureProcesses));
    }
    private async Task RefreshProcessesAsync()
    {
        await RunBusyAsync("Leyendo procesos actuales…", async token =>
        {
            var processes = await Task.Run(() =>
            {
                var items = new List<ProcessInfo>();
                foreach (var process in Process.GetProcesses())
                {
                    using (process)
                    {
                        token.ThrowIfCancellationRequested();
                        try { items.Add(new(process.ProcessName, process.Id, process.WorkingSet64 / 1048576d)); }
                        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                    }
                }
                return items;
            }, token);
            token.ThrowIfCancellationRequested(); SetCaptureProcesses(processes);
            Status = "Procesos actualizados. Elige tu juego o escribe su PID.";
        });
    }
    internal bool ValidateCaptureInputs(out int pid, out int duration)
    {
        pid = 0; duration = 0;
        string executable;
        try { executable = services.ResolvePresentMon(); }
        catch (InvalidOperationException ex) { CaptureError = ex.Message; RefreshCollectorState(); return false; }
        CaptureError = CaptureInputValidation.Validate(ProcessId, CaptureDuration, executable, File.Exists, out pid, out duration);
        return !HasCaptureError;
    }
    private void RefreshCollectorState()
    {
        try { services.ResolvePresentMon(); hasBundledCollector = true; collectorStatus = "PresentMon incluido y verificado. No necesitas instalar herramientas adicionales."; }
        catch (InvalidOperationException ex) { hasBundledCollector = false; collectorStatus = ex.Message; }
        Changed(nameof(HasBundledCollector)); Changed(nameof(CollectorStatus)); CommandManager.InvalidateRequerySuggested();
    }
    internal async Task CaptureAsync()
    {
        if (!ValidateCaptureInputs(out var pid, out var duration)) return;
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited) { CaptureError = "Ese proceso ya terminó. Actualiza la lista y elige el juego en ejecución."; return; }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { CaptureError = "Ese PID ya no está en ejecución. Actualiza la lista o revisa el PID en el Administrador de tareas."; return; }
        var completed = await RunBusyAsync($"Capturando {duration} segundos del proceso {pid}…", async token =>
        {
            var executable = services.ResolvePresentMon();
            var directory = services.CaptureDirectory;
            Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, $"PresentMon-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv");
            var session = await services.Capture(executable, pid, duration, output, token);
            token.ThrowIfCancellationRequested();
            SessionA = new(session);
            SessionB = null;
            Status = $"Captura terminada. CSV conservado: {Path.GetFileName(output)}";
        });
        if (!completed && HasError) { CaptureError = Error; Error = ""; }
    }
    private async Task OpenElevatedAsync()
    {
        if (!CanRequestCaptureElevation) return;
        var completed = await RunBusyAsync("Solicitando a Windows abrir FlyyOptimizer como administrador…", async token =>
        {
            try
            {
                await Task.Run(services.OpenElevated, token);
                CaptureError = "";
                Status = "FlyyOptimizer abierto como administrador. Esta sesión sigue disponible aquí.";
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                CaptureError = "La captura requiere permisos de administrador. Cancelaste la solicitud de Windows; puedes reintentar desde este botón.";
                Status = "Solicitud de administrador cancelada. Puedes seguir usando esta sesión.";
            }
        });
        if (!completed && HasError) { CaptureError = Error; Error = ""; }
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
