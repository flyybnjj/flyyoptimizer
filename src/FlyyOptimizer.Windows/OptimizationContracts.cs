namespace FlyyOptimizer.Windows;

public sealed record ToggleSetting(string Id, string Name, string Description, bool? Enabled, string Status);
public sealed record PowerScheme(string Id, string Name, bool Active);
public sealed record StartupEntry(string Id, string Name, string Command, bool Enabled, string Scope);
public sealed record AppliedOptimization(string Id, string Title, DateTimeOffset AppliedAt, bool CanUndo, string Detail);
public sealed record OptimizationOverview(List<ToggleSetting> Toggles, List<PowerScheme> PowerSchemes, List<StartupEntry> StartupEntries, List<AppliedOptimization> Changes);
public sealed record OptimizationOutcome(bool Success, string Message, bool Changed = false);
public sealed record TemporaryFileItem(string Id, string RelativePath, long Bytes);
public sealed record TemporaryCleanupPreview(List<TemporaryFileItem> Files, long TotalBytes, bool Truncated, string Message);
public sealed record TemporaryCleanupResult(int Removed, int Skipped, long BytesFreed, string Message);

public interface INativeOptimizationService
{
    Task<OptimizationOverview> InspectAsync(CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> SetToggleAsync(string id, bool enabled, CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> SetPowerSchemeAsync(string id, CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> SetStartupEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default);
    Task<TemporaryCleanupPreview> PreviewTempAsync(CancellationToken cancellationToken = default);
    Task<TemporaryCleanupResult> CleanTempAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);
}
