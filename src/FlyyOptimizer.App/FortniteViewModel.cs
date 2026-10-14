using System.Collections.ObjectModel;
using System.Windows.Input;
using FlyyOptimizer.Windows;

namespace FlyyOptimizer.App;

/// <summary>The profile shows a preview before applying selected, individually reported changes.</summary>
public sealed class FortniteViewModel : ObservableObject
{
    private readonly IFortniteProfileService service;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly Func<bool> canRun;
    private readonly Func<CancellationToken, Task> refreshTools;
    private readonly Func<string?> chooseFolder;
    private FortniteProfilePreview? preview;
    private string? manualDirectory;
    private string status = "Detecta Fortnite y revisa qué ajustes quieres aplicar antes de optimizar.";
    private string progressTitle = "";
    private int completedSteps;
    private int totalSteps;
    private bool actionError;
    private bool applying;
    private int progressGeneration;
    private AppliedOptimization? selectedChange;

    internal FortniteViewModel(IFortniteProfileService service,
        Func<string, Func<CancellationToken, Task>, Task<bool>> run, Func<bool> canRun,
        Func<CancellationToken, Task> refreshTools, Func<string?> chooseFolder)
    {
        this.service = service; this.run = run; this.canRun = canRun;
        this.refreshTools = refreshTools; this.chooseFolder = chooseFolder;
        DetectCommand = Command(_ => DetectAsync());
        ChooseFolderCommand = Command(_ => ChooseFolderAsync());
        ApplyCommand = Command(_ => ApplyAsync(), _ => HasPreview && Options.Any(o => o.Option.Available && o.Selected));
        UndoCommand = Command(_ => UndoAsync(), _ => SelectedChange is { CanUndo: true });
    }

    private ICommand Command(Func<object?, Task> work, Func<object?, bool>? extra = null) =>
        new AsyncCommand(work, p => canRun() && (extra?.Invoke(p) ?? true), ex => { Status = ex.Message; SetError(true); });

    public ICommand DetectCommand { get; }
    public ICommand ChooseFolderCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand UndoCommand { get; }
    public ObservableCollection<FortniteOptionChoice> Options { get; } = [];
    public ObservableCollection<FortniteProfileStepResult> Steps { get; } = [];
    public ObservableCollection<AppliedOptimization> Changes { get; } = [];
    public bool HasPreview => preview is not null;
    public bool IsInstalled => preview?.Installation.Installed == true;
    public string InstallPath => preview?.Installation.InstallDirectory ?? "No se encontró una instalación de Fortnite.";
    public string SettingsPath => preview?.Installation.SettingsPath ?? "La configuración del juego aún no está disponible.";
    public string InstallationMessage => preview?.Installation.Message ?? "Detección automática desde Epic Games; también puedes elegir la carpeta.";
    public bool IsGameRunning => preview?.Installation.IsRunning == true;
    public bool HasChanges => Changes.Count > 0;
    public bool HasResults => Steps.Count > 0;
    public bool HasActionError => actionError;
    public bool IsApplying => applying;
    public string Status { get => status; private set => Set(ref status, value); }
    public string ProgressTitle { get => progressTitle; private set => Set(ref progressTitle, value); }
    public string ProgressLabel => $"{completedSteps}/{totalSteps} pasos terminados";
    public double ProgressPercent => totalSteps > 0 ? 100d * completedSteps / totalSteps : 0;
    public string SelectionSummary => $"{Options.Count(o => o.Option.Available && o.Selected)} de {Options.Count(o => o.Option.Available)} ajustes disponibles seleccionados";
    public AppliedOptimization? SelectedChange { get => selectedChange; set { Set(ref selectedChange, value); CommandManager.InvalidateRequerySuggested(); } }

    internal Task<bool> DetectAsync() => PreviewAsync(null);

    private Task<bool> ChooseFolderAsync()
    {
        var directory = chooseFolder();
        return directory is null ? Task.FromResult(false) : PreviewAsync(directory);
    }

    private Task<bool> PreviewAsync(string? directory) => run("Detectando Fortnite y preparando ajustes…", async token =>
    {
        preview = null; Options.Clear(); Steps.Clear(); SetError(false); Changed(null);
        manualDirectory = directory;
        try
        {
            await RefreshPreviewAsync(token, false);
            Status = preview!.Message;
        }
        catch (OperationCanceledException) { Status = "Detección cancelada. Vuelve a detectar Fortnite antes de optimizar."; SetError(true); throw; }
        catch (Exception ex) { Status = $"No se pudo detectar Fortnite: {ex.Message}"; SetError(true); throw; }
    });

    internal Task<bool> ApplyAsync() => run("Optimizando PC para Fortnite…", async token =>
    {
        var optionIds = Options.Where(o => o.Option.Available && o.Selected).Select(o => o.Option.Id).ToArray();
        if (!HasPreview || optionIds.Length == 0) return;
        Steps.Clear(); applying = true; completedSteps = 0; totalSteps = optionIds.Length;
        ProgressTitle = "Preparando los ajustes seleccionados"; SetError(false); Changed(null);
        var generation = ++progressGeneration;
        var progress = new Progress<FortniteProfileProgress>(value =>
        {
            if (!applying || generation != progressGeneration) return;
            completedSteps = value.Completed; totalSteps = value.Total; ProgressTitle = value.Title;
            Changed(nameof(ProgressPercent)); Changed(nameof(ProgressLabel));
        });
        try
        {
            var result = await service.ApplyAsync(optionIds, progress, token);
            foreach (var step in result.Steps) Steps.Add(step);
            Status = result.Message; SetError(result.Cancelled || result.Steps.Any(s => !s.Success));
        }
        catch (OperationCanceledException) { Status = "Perfil cancelado. Los cambios que se alcanzaron a guardar siguen disponibles para restaurarlos."; SetError(true); throw; }
        catch (Exception ex) { Status = $"No se completó el perfil: {ex.Message}"; SetError(true); throw; }
        finally
        {
            applying = false; progressGeneration++; Changed(null);
            await RefreshAfterChangeAsync();
        }
    });

    internal Task<bool> UndoAsync() => run("Restaurando el ajuste seleccionado de Fortnite…", async token =>
    {
        if (SelectedChange is not { CanUndo: true } change) return;
        try { var result = await service.UndoAsync(change.Id, token); Status = result.Message; SetError(!result.Success); }
        catch (OperationCanceledException) { Status = "Restauración cancelada. Revisa el estado y el respaldo conservado."; SetError(true); throw; }
        catch (Exception ex) { Status = $"No se pudo restaurar: {ex.Message}"; SetError(true); throw; }
        finally { await RefreshAfterChangeAsync(); }
    });

    private async Task RefreshAfterChangeAsync()
    {
        try { await RefreshPreviewAsync(CancellationToken.None, true); }
        catch (Exception ex) { preview = null; Options.Clear(); Changed(null); Status += $" No se pudo actualizar el perfil: {ex.Message}. Pulsa Detectar Fortnite."; SetError(true); }
        try { await refreshTools(CancellationToken.None); }
        catch (Exception ex) { Status += $" No se pudo actualizar Mejoras: {ex.Message}. Pulsa Actualizar ajustes."; SetError(true); }
    }

    private async Task RefreshPreviewAsync(CancellationToken token, bool keepSelection)
    {
        var selected = keepSelection ? Options.ToDictionary(o => o.Option.Id, o => o.Selected) : null;
        var result = await service.PreviewAsync(manualDirectory, token);
        token.ThrowIfCancellationRequested(); preview = result;
        Options.Clear();
        foreach (var option in result.Options)
        {
            var row = new FortniteOptionChoice(option) { Selected = option.Available && (selected?.GetValueOrDefault(option.Id, option.SelectedByDefault) ?? option.SelectedByDefault) };
            row.PropertyChanged += (_, _) => { Changed(nameof(SelectionSummary)); CommandManager.InvalidateRequerySuggested(); };
            Options.Add(row);
        }
        var previousId = SelectedChange?.Id;
        Changes.Clear(); foreach (var change in result.Changes.OrderByDescending(c => c.AppliedAt)) Changes.Add(change);
        SelectedChange = Changes.FirstOrDefault(c => c.Id == previousId);
        Changed(null);
    }

    private void SetError(bool value) { actionError = value; Changed(nameof(HasActionError)); }
}

public sealed class FortniteOptionChoice(FortniteProfileOption option) : ObservableObject
{
    private bool selected;
    public FortniteProfileOption Option { get; } = option;
    public bool Selected { get => selected; set => Set(ref selected, value && Option.Available); }
}
