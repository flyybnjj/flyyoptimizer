using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace FlyyOptimizer.App;

internal static class Elevation
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static ProcessStartInfo CreateStartInfo()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "FlyyOptimizer.exe");
        if (!File.Exists(executable))
            throw new FileNotFoundException("No se encontró FlyyOptimizer.exe en su carpeta. Vuelve a extraer el paquete completo de FlyyOptimizer.");
        return new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas"
        };
    }

    public static void OpenElevated()
    {
        using var launched = Process.Start(CreateStartInfo());
    }
    internal static ProcessStartInfo CreateDriverStartInfo()
    {
        var start = CreateStartInfo();
        start.ArgumentList.Add("--drivers");
        return start;
    }
    public static void OpenElevatedDrivers()
    {
        using var launched = Process.Start(CreateDriverStartInfo());
    }
}
