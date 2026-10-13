using FlyyOptimizer.Core;
using Xunit;

namespace FlyyOptimizer.Tests;

public sealed class ReportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FlyyOptimizerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SavedSnapshotRoundTripsActualMeasurementsAndUnknownSensors()
    {
        var store = new ReportStore(directory);
        var snapshot = new SystemSnapshot { Machine = new() { CpuName = "Test CPU" }, Samples = [new() { MemoryPercent = 42, CpuTemperature = null }] };
        var file = await store.SaveAsync(snapshot);
        var result = await store.LoadAsync(file);
        Assert.Equal(snapshot.Id, result.Id);
        Assert.Equal("Test CPU", result.Machine.CpuName);
        Assert.Equal(42d, result.Samples[0].MemoryPercent);
        Assert.Null(result.Samples[0].CpuTemperature);
    }

    [Fact]
    public async Task HistoryIsNewestFirstAndIgnoresDamagedReportWithoutLosingValidOnes()
    {
        var store = new ReportStore(directory);
        await store.SaveAsync(new() { CapturedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z"), Machine = new() { CpuName = "Old" } });
        await store.SaveAsync(new() { CapturedAt = DateTimeOffset.Parse("2026-02-01T12:00:00Z"), Machine = new() { CpuName = "New" } });
        await File.WriteAllTextAsync(Path.Combine(directory, "damaged.json"), "{invalid");
        var history = await store.ListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("New", history[0].CpuName);
        Assert.Equal("Old", history[1].CpuName);
    }

    [Fact]
    public async Task LoadingCorruptReportExplainsInvalidData()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "damaged.json");
        await File.WriteAllTextAsync(file, "{invalid");
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReportStore(directory).LoadAsync(file));
    }

    [Fact]
    public async Task GuideHistoryRecordsOpeningRatherThanAssumingOptimizationWasApplied()
    {
        var store = new ReportStore(directory);
        var action = new ActionRecord(DateTimeOffset.Now, "startup-review", "Revisar inicio", "Guía abierta");
        await store.RecordActionAsync(action);
        var actions = await store.GetActionsAsync();
        Assert.Single(actions);
        Assert.Equal("Guía abierta", actions[0].Outcome);
        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task ConcurrentGuideRecordsAreNotLost()
    {
        var store = new ReportStore(directory);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => store.RecordActionAsync(new(DateTimeOffset.Now, $"action-{i}", "Guide", "Guía abierta"))));
        Assert.Equal(10, (await store.GetActionsAsync()).Count);
    }

    [Fact]
    public async Task BrokenActionLogIsPreservedInsteadOfSilentlyOverwritten()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "actions.json");
        await File.WriteAllTextAsync(file, "{invalid");
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReportStore(directory).RecordActionAsync(new(DateTimeOffset.Now, "one", "Guide", "Guía abierta")));
        Assert.Equal("{invalid", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public void MarkdownIncludesMeasuredEvidenceAndNamesUnknownMetrics()
    {
        var snapshot = new SystemSnapshot { Machine = new() { CpuName = "Test CPU" }, Samples = [new() { MemoryPercent = 42 }] };
        var report = ReportFormatter.ToMarkdown(snapshot, [new("one", "Space", "Review storage", "9 GB libres", Severity.Warning, "Storage")]);
        Assert.Contains("Test CPU", report);
        Assert.Contains("9 GB libres", report);
        Assert.Contains("No disponible", report);
        Assert.Contains("42", report);
    }

    [Fact]
    public void MarkdownEscapesDeviceNamesThatContainTableDelimiters()
    {
        var snapshot = new SystemSnapshot { Drivers = [new() { DeviceName = "GPU|name\nextra", Version = "1.0" }] };
        var report = ReportFormatter.ToMarkdown(snapshot, []);
        Assert.Contains("GPU\\|name extra", report);
    }

    [Fact]
    public void ComparisonKeepsUnavailableMetricsUnknown()
    {
        var before = new SystemSnapshot { Samples = [new() { MemoryPercent = 80 }] };
        var after = new SystemSnapshot { Samples = [new() { MemoryPercent = 60 }] };
        var comparison = SnapshotComparison.Compare(before, after);
        Assert.Equal(-20d, comparison.MemoryPercentagePointChange);
        Assert.Null(comparison.CpuPercentagePointChange);
        Assert.Null(comparison.CpuTemperatureChange);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
