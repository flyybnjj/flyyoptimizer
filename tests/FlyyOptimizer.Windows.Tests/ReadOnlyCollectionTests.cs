using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace FlyyOptimizer.Windows.Tests;

public sealed class ReadOnlyCollectionTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "ReadOnlyIntegration")]
    public async Task Actual_windows_scan_collects_ten_spaced_samples_without_exporting_profile_paths()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var elapsed = Stopwatch.StartNew();
        var snapshot = await new WindowsCollector().CollectAsync(cancellationToken: cancellation.Token);
        Assert.Equal(10, snapshot.Samples.Count);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(9));
        Assert.Equal(10, snapshot.Samples.Select(s => s.At).Distinct().Count());
        Assert.All(snapshot.Samples, sample =>
        {
            Assert.True(sample.CpuPercent is null or >= 0 and <= 100);
            Assert.True(sample.MemoryPercent is null or >= 0 and <= 100);
        });
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.DoesNotContain(profile, string.Join('\n', snapshot.Startup.Select(s => s.Command)), StringComparison.OrdinalIgnoreCase);
        Assert.All(snapshot.Drivers, driver => Assert.True(driver.HardwareId.Count(character => character == '\\') <= 1));
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Samples = snapshot.Samples.Count, Drivers = snapshot.Drivers.Count, Drives = snapshot.Drives.Count,
            Startup = snapshot.Startup.Count, Processes = snapshot.Processes.Count, Networks = snapshot.Networks.Count,
            CpuReadings = snapshot.Samples.Count(s => s.CpuPercent is not null), MemoryReadings = snapshot.Samples.Count(s => s.MemoryPercent is not null),
            CpuTemperatureReadings = snapshot.Samples.Count(s => s.CpuTemperature is not null), GpuTemperatureReadings = snapshot.Samples.Count(s => s.GpuTemperature is not null),
            snapshot.Notes
        }));
    }
}
