using Xunit;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace FlyyOptimizer.Windows.Tests;

public sealed class TemporaryCleanupTests
{
    [Fact]
    public async Task Preview_only_includes_old_files_under_owned_temp_without_deleting_anything()
    {
        using var fixture = new OptimizationFixture();
        var old = Create(fixture.Temp, "nested/old.log", "old", 48);
        var fresh = Create(fixture.Temp, "fresh.log", "fresh", 1);
        var outside = Create(fixture.Root, "outside.txt", "outside", 48);
        var preview = await fixture.Service().PreviewTempAsync();
        var item = Assert.Single(preview.Files);
        Assert.Equal(Path.Combine("nested", "old.log"), item.RelativePath);
        Assert.Equal(3, item.Bytes);
        Assert.Equal(3, preview.TotalBytes);
        Assert.True(File.Exists(old));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Cleanup_only_deletes_selected_preview_ids_and_reports_actual_bytes()
    {
        using var fixture = new OptimizationFixture();
        var selected = Create(fixture.Temp, "selected.tmp", "selected", 48);
        var kept = Create(fixture.Temp, "kept.tmp", "kept", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        var id = preview.Files.Single(x => x.RelativePath == "selected.tmp").Id;
        var result = await service.CleanTempAsync([id, id]);
        Assert.Equal(1, result.Removed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(8, result.BytesFreed);
        Assert.False(File.Exists(selected));
        Assert.True(File.Exists(kept));
        Assert.True(Directory.Exists(fixture.Temp));
    }

    [Fact]
    public async Task Path_shaped_or_unknown_ids_cannot_delete_files_outside_preview()
    {
        using var fixture = new OptimizationFixture();
        var outside = Create(fixture.Root, "outside.txt", "keep", 48);
        var service = fixture.Service();
        await service.PreviewTempAsync();
        var result = await service.CleanTempAsync([outside, "../outside.txt", "unknown"]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(3, result.Skipped);
        Assert.True(File.Exists(outside));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Changed_size_or_write_time_is_skipped(bool sizeChange)
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "old.tmp", "1234", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        if (sizeChange) { File.WriteAllText(path, "longer"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-48)); }
        else File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-1));
        var result = await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task In_use_file_is_skipped_and_result_does_not_claim_reclaimed_bytes()
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "locked.tmp", "locked", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.BytesFreed);
    }

    [Fact]
    public async Task Directory_junction_is_not_traversed()
    {
        using var fixture = new OptimizationFixture();
        var outside = Create(fixture.Root, "outside/kept.tmp", "keep", 48);
        var link = Path.Combine(fixture.Temp, "linked-directory");
        CleanupTestLinks.CreateJunction(link, Path.GetDirectoryName(outside)!);
        try
        {
            Assert.Empty((await fixture.Service().PreviewTempAsync()).Files);
            Assert.True(File.Exists(outside));
        }
        finally { CleanupTestLinks.RemoveJunction(link); }
    }

    [Fact]
    public async Task Ancestor_replaced_with_junction_after_preview_cannot_delete_its_target()
    {
        using var fixture = new OptimizationFixture();
        var initial = Create(fixture.Temp, "nested/old.tmp", "same", 48);
        var outside = Create(fixture.Root, "outside/old.tmp", "same", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        File.Delete(initial);
        Directory.Delete(Path.GetDirectoryName(initial)!);
        var link = Path.Combine(fixture.Temp, "nested");
        CleanupTestLinks.CreateJunction(link, Path.GetDirectoryName(outside)!);
        try
        {
            var result = await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
            Assert.Equal(0, result.Removed);
            Assert.Equal(1, result.Skipped);
            Assert.True(File.Exists(outside));
        }
        finally { CleanupTestLinks.RemoveJunction(link); }
    }

    [Fact]
    public async Task A_new_preview_invalidates_ids_from_the_previous_preview()
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "old.tmp", "old", 48);
        var service = fixture.Service();
        var first = await service.PreviewTempAsync();
        await service.PreviewTempAsync();
        var result = await service.CleanTempAsync([Assert.Single(first.Files).Id]);
        Assert.Equal(0, result.Removed);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Hardlink_to_file_outside_temp_is_skipped_and_cannot_claim_freed_bytes()
    {
        using var fixture = new OptimizationFixture();
        var outside = Create(fixture.Root, "outside.txt", "keep", 48);
        var link = Path.Combine(fixture.Temp, "hardlink.tmp");
        CleanupTestLinks.CreateHardLink(link, outside);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        var result = await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.BytesFreed);
        Assert.True(File.Exists(link));
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Preview_bounds_file_count_and_reports_truncation()
    {
        using var fixture = new OptimizationFixture();
        for (var i = 0; i < 4; i++) Create(fixture.Temp, $"{i}.tmp", "old", 48);
        var preview = await fixture.Service(maximumFiles: 2).PreviewTempAsync();
        Assert.Equal(2, preview.Files.Count);
        Assert.True(preview.Truncated);
    }

    [Fact]
    public async Task Precancelled_cleanup_keeps_all_files()
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "old.tmp", "old", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CleanTempAsync([Assert.Single(preview.Files).Id], source.Token));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Cancellation_after_a_deletion_preserves_actual_partial_counts()
    {
        using var fixture = new OptimizationFixture();
        Create(fixture.Temp, "a.tmp", "first", 48);
        Create(fixture.Temp, "b.tmp", "second", 48);
        Create(fixture.Temp, "c.tmp", "third", 48);
        using var source = new CancellationTokenSource();
        var cleaning = false;
        var checks = 0;
        var service = new NativeOptimizationService(fixture.Platform, fixture.Journal, fixture.Temp, () =>
        {
            if (cleaning && ++checks == 2) source.Cancel();
            return DateTimeOffset.UtcNow;
        });
        var preview = await service.PreviewTempAsync();
        cleaning = true;
        var result = await service.CleanTempAsync(preview.Files.Select(x => x.Id).ToArray(), source.Token);
        Assert.Equal(1, result.Removed);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(2, Directory.EnumerateFiles(fixture.Temp).Count());
        Assert.Contains("cancel", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(result.BytesFreed, 5, 6);
    }

    private static string Create(string root, string relative, string text, int ageHours)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-ageHours));
        return path;
    }
}

internal static class CleanupTestLinks
{
    internal static void CreateJunction(string link, string target)
    {
        // Junction creation is permitted for the owning user without SeCreateSymbolicLinkPrivilege.
        Directory.CreateDirectory(link);
        var substitute = Encoding.Unicode.GetBytes(@"\??\" + Path.GetFullPath(target));
        var print = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
        var buffer = new byte[16 + substitute.Length + 2 + print.Length + 2];
        BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);
        BitConverter.GetBytes(checked((ushort)(buffer.Length - 8))).CopyTo(buffer, 4);
        BitConverter.GetBytes(checked((ushort)substitute.Length)).CopyTo(buffer, 10);
        BitConverter.GetBytes(checked((ushort)(substitute.Length + 2))).CopyTo(buffer, 12);
        BitConverter.GetBytes(checked((ushort)print.Length)).CopyTo(buffer, 14);
        substitute.CopyTo(buffer, 16);
        print.CopyTo(buffer, 16 + substitute.Length + 2);
        using var handle = CreateFile(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid || !DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static void CreateHardLink(string link, string target)
    {
        if (!CreateHardLinkNative(link, target, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static void RemoveJunction(string link)
    {
        using (var handle = CreateFile(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero))
        {
            var buffer = new byte[8];
            BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);
            if (handle.IsInvalid || !DeviceIoControl(handle, 0x000900AC, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        Directory.Delete(link);
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint control, byte[] input, int inputSize, IntPtr output, int outputSize, out int bytesReturned, IntPtr overlapped);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkNative(string link, string existing, IntPtr security);
}
