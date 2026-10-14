using System.IO.Compression;
using System.Text;
using FlyyOptimizer.Windows;
using Xunit;

namespace FlyyOptimizer.Windows.Tests;

public sealed class PortableAppUpdateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FlyyUpdateTests", Guid.NewGuid().ToString("N"));
    private string Install => Path.Combine(root, "installation");
    private static readonly byte[] NewExe = Encoding.ASCII.GetBytes("MZnew executable fixture");
    private static readonly byte[] OldExe = Encoding.ASCII.GetBytes("MZold executable fixture");
    private string Archive(params (string Name, byte[] Data, int Attributes)[] entries)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, data, attributes) in entries)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            entry.ExternalAttributes = attributes;
            using var target = entry.Open();
            target.Write(data);
        }
        return path;
    }
    private string ValidArchive() => Archive(("FlyyOptimizer-win-x64/FlyyOptimizer.exe", NewExe, 0), ("FlyyOptimizer-win-x64/Archivos/data.txt", [42], 0));
    private void ExistingInstall()
    {
        Directory.CreateDirectory(Path.Combine(Install, "Archivos"));
        File.WriteAllBytes(Path.Combine(Install, "FlyyOptimizer.exe"), OldExe);
        File.WriteAllText(Path.Combine(Install, "Archivos", "old.txt"), "old data");
        File.WriteAllText(Path.Combine(Install, "unrelated.txt"), "preserve");
    }
    [Fact]
    public async Task Applies_only_portable_files_and_restarts_new_executable()
    {
        ExistingInstall();
        string? restarted = null;
        await AppUpdateInstaller.ApplyAsync(ValidArchive(), Install, path => { restarted = path; return Task.CompletedTask; }, default);
        Assert.Equal(NewExe, File.ReadAllBytes(Path.Combine(Install, "FlyyOptimizer.exe")));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(Path.Combine(Install, "Archivos", "data.txt")));
        Assert.Equal(Path.Combine(Install, "FlyyOptimizer.exe"), restarted);
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(Install, "unrelated.txt")));
    }
    [Fact]
    public async Task Failed_restart_restores_old_executable_and_resources()
    {
        ExistingInstall();
        await Assert.ThrowsAsync<IOException>(() => AppUpdateInstaller.ApplyAsync(ValidArchive(), Install, _ => throw new IOException("restart fixture"), default));
        Assert.Equal(OldExe, File.ReadAllBytes(Path.Combine(Install, "FlyyOptimizer.exe")));
        Assert.Equal("old data", File.ReadAllText(Path.Combine(Install, "Archivos", "old.txt")));
        Assert.False(File.Exists(Path.Combine(Install, "Archivos", "data.txt")));
    }
    [Fact]
    public async Task Release_with_wrong_executable_version_is_rejected_before_replacement()
    {
        ExistingInstall();
        await Assert.ThrowsAsync<InvalidDataException>(() => AppUpdateInstaller.ApplyAsync(ValidArchive(), Install, _ => Task.CompletedTask, default, new Version(1, 2, 0)));
        Assert.Equal(OldExe, File.ReadAllBytes(Path.Combine(Install, "FlyyOptimizer.exe")));
    }
    [Theory]
    [InlineData("FlyyOptimizer.exe")]
    [InlineData("Archivos/old.txt")]
    public async Task Locked_installed_files_leave_previous_installation_usable(string relative)
    {
        ExistingInstall();
        using (File.Open(Path.Combine(Install, relative), FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => AppUpdateInstaller.ApplyAsync(ValidArchive(), Install, _ => Task.CompletedTask, default));
        Assert.Equal(OldExe, File.ReadAllBytes(Path.Combine(Install, "FlyyOptimizer.exe")));
        Assert.Equal("old data", File.ReadAllText(Path.Combine(Install, "Archivos", "old.txt")));
    }
    [Fact]
    public async Task Concurrent_installer_is_rejected_before_replacement()
    {
        ExistingInstall();
        using var active = new FileStream(Path.Combine(Install, ".flyy-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await Assert.ThrowsAnyAsync<IOException>(() => AppUpdateInstaller.ApplyAsync(ValidArchive(), Install, _ => Task.CompletedTask, default));
        Assert.Equal(OldExe, File.ReadAllBytes(Path.Combine(Install, "FlyyOptimizer.exe")));
    }
    [Fact]
    public async Task Stale_helper_cannot_replace_an_installation_changed_by_another_helper()
    {
        ExistingInstall();
        var originalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(OldExe));
        await AppUpdateInstaller.ApplyAsync(ValidArchive(), Install, _ => Task.CompletedTask, default);
        await Assert.ThrowsAsync<InvalidDataException>(() => AppUpdateInstaller.ApplyAsync(ValidArchive(), Install, _ => Task.CompletedTask, default, expectedOriginalSha256: originalHash));
        Assert.Equal(NewExe, File.ReadAllBytes(Path.Combine(Install, "FlyyOptimizer.exe")));
    }
    [Theory]
    [InlineData("FlyyOptimizer-win-x64/../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/outside.txt")]
    [InlineData("FlyyOptimizer-win-x64/Archivos/a.txt:stream")]
    [InlineData("FlyyOptimizer-win-x64/other.txt")]
    [InlineData("FlyyOptimizer-win-x64/Archivos/CON.txt")]
    [InlineData("FlyyOptimizer-win-x64/Archivos/trailing. ")]
    [InlineData("FlyyOptimizer-win-x64/Archivos/../a.txt")]
    public async Task Hostile_entry_is_rejected_before_install_changes(string name)
    {
        ExistingInstall();
        var archive = Archive(("FlyyOptimizer-win-x64/FlyyOptimizer.exe", NewExe, 0), (name, [1], 0));
        await Assert.ThrowsAsync<InvalidDataException>(() => AppUpdateInstaller.ApplyAsync(archive, Install, _ => Task.CompletedTask, default));
        Assert.Equal(OldExe, File.ReadAllBytes(Path.Combine(Install, "FlyyOptimizer.exe")));
    }
    [Fact]
    public async Task Duplicate_case_and_symlink_entries_are_rejected()
    {
        ExistingInstall();
        foreach (var archive in new[] {
            Archive(("FlyyOptimizer-win-x64/FlyyOptimizer.exe", NewExe, 0), ("FlyyOptimizer-win-x64/flyyoptimizer.EXE", NewExe, 0)),
            Archive(("FlyyOptimizer-win-x64/FlyyOptimizer.exe", NewExe, 0), ("FlyyOptimizer-win-x64/Archivos/link", [1], unchecked((int)0xA1FF0000))) })
            await Assert.ThrowsAsync<InvalidDataException>(() => AppUpdateInstaller.ApplyAsync(archive, Install, _ => Task.CompletedTask, default));
    }
    [Fact]
    public async Task Malformed_archive_and_non_executable_payload_are_rejected()
    {
        ExistingInstall();
        var bad = Path.Combine(root, "bad.zip");
        File.WriteAllText(bad, "broken");
        await Assert.ThrowsAsync<InvalidDataException>(() => AppUpdateInstaller.ApplyAsync(bad, Install, _ => Task.CompletedTask, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => AppUpdateInstaller.ApplyAsync(Archive(("FlyyOptimizer-win-x64/FlyyOptimizer.exe", [1, 2, 3], 0)), Install, _ => Task.CompletedTask, default));
    }
    [Fact]
    public async Task Compressed_bomb_and_too_many_entries_are_rejected()
    {
        ExistingInstall();
        var bomb = Archive(("FlyyOptimizer-win-x64/FlyyOptimizer.exe", NewExe, 0), ("FlyyOptimizer-win-x64/Archivos/bomb.bin", new byte[4 * 1024 * 1024], 0));
        await Assert.ThrowsAsync<InvalidDataException>(() => AppUpdateInstaller.ApplyAsync(bomb, Install, _ => Task.CompletedTask, default));
        var entries = Enumerable.Range(0, 4100).Select(n => ($"FlyyOptimizer-win-x64/Archivos/{n}", new byte[] { 1 }, 0)).ToArray();
        await Assert.ThrowsAsync<InvalidDataException>(() => AppUpdateInstaller.ApplyAsync(Archive(entries), Install, _ => Task.CompletedTask, default));
    }
    [Fact]
    public void Job_protection_detects_tampering()
    {
        var original = Encoding.UTF8.GetBytes("bounded trusted job fixture");
        var protectedBytes = AppUpdateJobProtection.Protect(original);
        Assert.Equal(original, AppUpdateJobProtection.Unprotect(protectedBytes));
        protectedBytes[protectedBytes.Length / 2] ^= 1;
        Assert.Throws<InvalidDataException>(() => AppUpdateJobProtection.Unprotect(protectedBytes));
    }
    [Theory]
    [InlineData("C:\\")]
    [InlineData("\\\\server\\share\\app")]
    [InlineData("relative")]
    public void Dangerous_installation_roots_are_rejected(string path) => Assert.Throws<InvalidDataException>(() => AppUpdatePaths.Validate(path));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
