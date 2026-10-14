using System.Collections.ObjectModel;
using System.Windows.Input;
using FlyyOptimizer.Windows;

namespace FlyyOptimizer.App;

/// <summary>Concrete changes use the main operation lifetime, including cancellation and closing.</summary>
public sealed class ToolsViewModel : ObservableObject
{
    private readonly INativeOptimizationService service;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly Func<bool> canRun;
    private string status = "Lee los ajustes actuales para elegir una mejora. No hace falta analizar primero.";
    private string cleanupStatus = "La vista previa buscará archivos de al menos 24 horas en tus temporales. Elige qué borrar.";
    private bool inspected;
    private bool previewed;
    private bool actionError;
    private PowerScheme? selectedPowerScheme;
    private StartupChoice? selectedStartup;
    private AppliedOptimization? selectedChange;

    internal ToolsViewModel(INativeOptimizationService service,
        Func<string, Func<CancellationToken, Task>, Task<bool>> run, Func<bool> canRun)
    {
        this.service = service; this.run = run; this.canRun = canRun;
        RefreshCommand = Command(_ => RefreshAsync());
        EnableToggleCommand = Command(p => SetToggleAsync(p, true), p => p is ToggleChoice t && t.Setting.Enabled != true);
        DisableToggleCommand = Command(p => SetToggleAsync(p, false), p => p is ToggleChoice t && t.Setting.Enabled != false);
        ApplyPowerCommand = Command(_ => ApplyPowerAsync(), _ => SelectedPowerScheme is { Active: false });
        ToggleStartupCommand = Command(_ => ToggleStartupAsync(), _ => SelectedStartup is not null);
        UndoCommand = Command(_ => UndoAsync(), _ => SelectedChange is { CanUndo: true });
        PreviewTempCommand = Command(_ => PreviewTempAsync());
        CleanTempCommand = Command(_ => CleanTempAsync(), _ => TemporaryFiles.Any(f => f.Selected));
        SelectAllTempCommand = new RelayCommand(_ => SelectFiles(true), _ => canRun() && TemporaryFiles.Count > 0);
        ClearTempSelectionCommand = new RelayCommand(_ => SelectFiles(false), _ => canRun() && TemporaryFiles.Any(f => f.Selected));
    }

    private ICommand Command(Func<object?, Task> work, Func<object?, bool>? extra = null) =>
        new AsyncCommand(work, p => canRun() && (extra?.Invoke(p) ?? true), ex => Status = ex.Message);

    public ICommand RefreshCommand { get; }
    public ICommand EnableToggleCommand { get; }
    public ICommand DisableToggleCommand { get; }
    public ICommand ApplyPowerCommand { get; }
    public ICommand ToggleStartupCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand PreviewTempCommand { get; }
    public ICommand CleanTempCommand { get; }
    public ICommand SelectAllTempCommand { get; }
    public ICommand ClearTempSelectionCommand { get; }
    public ObservableCollection<ToggleChoice> ImprovementToggles { get; } = [];
    public ObservableCollection<ToggleChoice> GamingToggles { get; } = [];
    public ObservableCollection<PowerScheme> PowerSchemes { get; } = [];
    public ObservableCollection<StartupChoice> StartupEntries { get; } = [];
    public ObservableCollection<AppliedOptimization> Changes { get; } = [];
    public ObservableCollection<TemporaryFileChoice> TemporaryFiles { get; } = [];
    public string Status { get => status; private set => Set(ref status, value); }
    public string CleanupStatus { get => cleanupStatus; private set => Set(ref cleanupStatus, value); }
    public bool HasInspection => inspected;
    public bool HasStartup => StartupEntries.Count > 0;
    public bool HasChanges => Changes.Count > 0;
    public bool HasPowerSchemes => PowerSchemes.Count > 0;
    public bool HasTemporaryFiles => TemporaryFiles.Count > 0;
    public bool HasPreview => previewed;
    public bool HasActionError => actionError;
    public string PowerStatus => PowerSchemes.FirstOrDefault(p => p.Active) is { } active ? $"Plan actual: {active.Name}" : "No se pudo identificar el plan activo. Actualiza los ajustes.";
    public string StartupActionLabel => SelectedStartup?.Entry.Enabled == false ? "Restaurar entrada" : "Retirar del inicio";
    public string TempSelectionSummary => $"{TemporaryFiles.Count(f => f.Selected)} de {TemporaryFiles.Count} archivos · {Size(TemporaryFiles.Where(f => f.Selected).Sum(f => f.File.Bytes))} seleccionados";
    public PowerScheme? SelectedPowerScheme { get => selectedPowerScheme; set { Set(ref selectedPowerScheme, value); CommandManager.InvalidateRequerySuggested(); } }
    public StartupChoice? SelectedStartup { get => selectedStartup; set { if (Set(ref selectedStartup, value)) Changed(nameof(StartupActionLabel)); CommandManager.InvalidateRequerySuggested(); } }
    public AppliedOptimization? SelectedChange { get => selectedChange; set { Set(ref selectedChange, value); CommandManager.InvalidateRequerySuggested(); } }

    public Task<bool> RefreshAsync() => run("Leyendo ajustes actuales…", async token =>
    {
        await RefreshStateAsync(token);
        actionError = false; Changed(nameof(HasActionError));
        Status = "Ajustes actuales leídos. Solo se aplican al pulsar un control.";
    });

    private async Task RefreshStateAsync(CancellationToken token)
    {
        var overview = await service.InspectAsync(token);
        token.ThrowIfCancellationRequested();
        var powerId = SelectedPowerScheme?.Id;
        var startupId = SelectedStartup?.Entry.Id;
        var changeId = SelectedChange?.Id;
        ImprovementToggles.Clear(); GamingToggles.Clear();
        foreach (var toggle in overview.Toggles)
        {
            var row = new ToggleChoice(toggle);
            if (toggle.Id == "animations") ImprovementToggles.Add(row);
            else if (toggle.Id is "game-mode" or "xbox-captures") GamingToggles.Add(row);
        }
        PowerSchemes.Clear(); foreach (var scheme in overview.PowerSchemes) PowerSchemes.Add(scheme);
        StartupEntries.Clear(); foreach (var entry in overview.StartupEntries) StartupEntries.Add(new(entry));
        Changes.Clear(); foreach (var change in overview.Changes.OrderByDescending(c => c.AppliedAt)) Changes.Add(change);
        SelectedPowerScheme = PowerSchemes.FirstOrDefault(p => p.Id == powerId) ?? PowerSchemes.FirstOrDefault(p => p.Active);
        SelectedStartup = StartupEntries.FirstOrDefault(p => p.Entry.Id == startupId);
        SelectedChange = Changes.FirstOrDefault(c => c.Id == changeId);
        inspected = true; Changed(null);
    }

    private Task<bool> ApplyAsync(string title, Func<CancellationToken, Task<OptimizationOutcome>> action) => run(title, async token =>
    {
        try
        {
            var outcome = await action(token);
            Status = outcome.Message;
            actionError = !outcome.Success; Changed(nameof(HasActionError));
        }
        catch (OperationCanceledException)
        {
            Status = "Operación cancelada. Revisa el estado actual y los cambios guardados antes de volver a aplicar.";
            actionError = true; Changed(nameof(HasActionError));
            throw;
        }
        catch (Exception ex)
        {
            Status = ex.Message; actionError = true; Changed(nameof(HasActionError));
            throw;
        }
        finally
        {
            // A partially applied change may have a durable backup even when its operation is cancelled.
            // Keep the lifetime active until the read refresh finishes so close cannot hide that backup.
            try { await RefreshStateAsync(CancellationToken.None); }
            catch (Exception ex)
            {
                Status += $" No se pudo actualizar el estado: {ex.Message}. Pulsa Actualizar ajustes para volver a leerlo.";
                actionError = true; Changed(nameof(HasActionError));
            }
        }
    });

    private Task SetToggleAsync(object? parameter, bool enabled) => parameter is ToggleChoice row
        ? ApplyAsync($"{(enabled ? "Activando" : "Desactivando")} {row.Setting.Name}…", token => service.SetToggleAsync(row.Setting.Id, enabled, token))
        : Task.CompletedTask;

    private Task ApplyPowerAsync() => SelectedPowerScheme is { } scheme
        ? ApplyAsync("Aplicando plan de energía…", token => service.SetPowerSchemeAsync(scheme.Id, token)) : Task.CompletedTask;

    private Task ToggleStartupAsync() => SelectedStartup is { } row
        ? ApplyAsync("Actualizando inicio del usuario…", token => service.SetStartupEnabledAsync(row.Entry.Id, !row.Entry.Enabled, token)) : Task.CompletedTask;

    private Task UndoAsync() => SelectedChange is { CanUndo: true } change
        ? ApplyAsync("Restaurando el estado anterior…", token => service.UndoAsync(change.Id, token)) : Task.CompletedTask;

    private Task PreviewTempAsync() => run("Preparando vista previa de temporales…", async token =>
    {
        TemporaryFiles.Clear();
        previewed = false; CleanupStatus = "Preparando una nueva vista previa. Las selecciones anteriores se han descartado."; Changed(null);
        try
        {
            var preview = await service.PreviewTempAsync(token);
            token.ThrowIfCancellationRequested();
            foreach (var file in preview.Files)
            {
                var row = new TemporaryFileChoice(file);
                row.PropertyChanged += (_, _) => { Changed(nameof(TempSelectionSummary)); CommandManager.InvalidateRequerySuggested(); };
                TemporaryFiles.Add(row);
            }
            previewed = true;
            CleanupStatus = $"{preview.Message} {Size(preview.TotalBytes)} disponibles.{(preview.Truncated ? " Se alcanzó el límite; vuelve a previsualizar después de limpiar." : "")}";
            Changed(null);
        }
        catch (OperationCanceledException) { CleanupStatus = "Vista previa cancelada. Pulsa Ver archivos para limpiar y vuelve a seleccionar los archivos."; throw; }
        catch (Exception ex) { CleanupStatus = $"No se pudo preparar la vista previa: {ex.Message}. Vuelve a previsualizar antes de limpiar."; throw; }
    });

    private Task CleanTempAsync()
    {
        var ids = TemporaryFiles.Where(f => f.Selected).Select(f => f.File.Id).ToArray();
        if (ids.Length == 0) return Task.CompletedTask;
        return run("Limpiando los temporales seleccionados…", async token =>
        {
            var result = await service.CleanTempAsync(ids, token);
            CleanupStatus = $"{result.Message} {result.Removed} eliminados · {result.Skipped} omitidos · {Size(result.BytesFreed)} recuperados.";
            TemporaryFiles.Clear(); previewed = false; Changed(null);
        });
    }

    private void SelectFiles(bool selected) { foreach (var file in TemporaryFiles) file.Selected = selected; }
    private static string Size(long bytes) => bytes >= 1048576 ? $"{(bytes / 1048576d).ToString("N1", Labels.Culture)} MiB" : bytes >= 1024 ? $"{(bytes / 1024d).ToString("N1", Labels.Culture)} KiB" : $"{bytes.ToString("N0", Labels.Culture)} B";
}

public sealed record ToggleChoice(ToggleSetting Setting)
{
    public string State => Setting.Enabled switch { true => "Activado", false => "Desactivado", _ => "Predeterminado de Windows / sin dato" };
}

public sealed record StartupChoice(FlyyOptimizer.Windows.StartupEntry Entry)
{
    public string State => Entry.Enabled ? "Presente en Run" : "Retirada de Run";
}

public sealed class TemporaryFileChoice(TemporaryFileItem file) : ObservableObject
{
    private bool selected;
    public TemporaryFileItem File { get; } = file;
    public bool Selected { get => selected; set => Set(ref selected, value); }
    public string SizeLabel => File.Bytes >= 1048576 ? $"{(File.Bytes / 1048576d).ToString("N1", Labels.Culture)} MiB" : File.Bytes >= 1024 ? $"{(File.Bytes / 1024d).ToString("N1", Labels.Culture)} KiB" : $"{File.Bytes.ToString("N0", Labels.Culture)} B";
}
