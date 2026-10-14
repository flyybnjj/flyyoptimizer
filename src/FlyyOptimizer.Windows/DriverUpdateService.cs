using System.Globalization;
using System.Runtime.InteropServices;
using FlyyOptimizer.Core;

namespace FlyyOptimizer.Windows;

public sealed class DriverUpdateService
{
    private static readonly TimeSpan SearchLimit = TimeSpan.FromMinutes(2);

    public async Task<DriverUpdateResult> FindUpdatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var worker = Task.Factory.StartNew(() => Search(workerCancellation.Token), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try { return await worker.WaitAsync(SearchLimit, cancellationToken).ConfigureAwait(false); }
        catch (TimeoutException)
        {
            workerCancellation.Cancel();
            return new DriverUpdateResult(false, [], "La consulta a Windows Update superó dos minutos. Revisa la conexión o abre Windows Update e inténtalo nuevamente.");
        }
        catch (OperationCanceledException)
        {
            workerCancellation.Cancel();
            throw;
        }
        finally
        {
            // COM cleanup may outlive an aborted request. Keep the source alive until its worker finishes.
            _ = worker.ContinueWith(completed =>
            {
                _ = completed.Exception; // Observe a later cancellation/error after the caller has left.
                workerCancellation.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private static DriverUpdateResult Search(CancellationToken token)
    {
        object? session = null, searcher = null, job = null, result = null, collection = null;
        var callback = new SearchCompletedCallback();
        try
        {
            token.ThrowIfCancellationRequested();
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session") ?? throw new COMException("Windows Update Agent no disponible.");
            session = Activator.CreateInstance(sessionType) ?? throw new COMException("No se pudo crear la sesión de Windows Update.");
            ((dynamic)session).ClientApplicationID = "FlyyOptimizer";
            searcher = ((dynamic)session).CreateUpdateSearcher();
            ((dynamic)searcher).Online = true;
            job = ((dynamic)searcher).BeginSearch("Type='Driver' AND IsInstalled=0 AND IsHidden=0", callback, null);
            while (!((dynamic)job).IsCompleted)
            {
                if (token.WaitHandle.WaitOne(200)) token.ThrowIfCancellationRequested();
            }
            token.ThrowIfCancellationRequested();
            result = ((dynamic)searcher).EndSearch(job);
            int code = ((dynamic)result).ResultCode;
            collection = ((dynamic)result).Updates;
            var updates = new List<DriverUpdateInfo>();
            int count = ((dynamic)collection).Count;
            for (var index = 0; index < count; index++)
            {
                token.ThrowIfCancellationRequested();
                object? update = null, identity = null;
                try
                {
                    update = ((dynamic)collection).Item(index);
                    var title = ComText(() => ((dynamic)update).Title);
                    var date = ComText(() => ((dynamic)update).DriverVerDate);
                    if (DateTime.TryParse(date, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed)) date = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    // WUA exposes DriverVerDate, but no structured driver-version property. Do not guess from the title.
                    identity = ((dynamic)update).Identity;
                    updates.Add(new DriverUpdateInfo(title, ComText(() => ((dynamic)update).DriverManufacturer), ComText(() => ((dynamic)update).DriverClass), "", date,
                        ComText(() => ((dynamic)identity).UpdateID), (int)((dynamic)identity).RevisionNumber, (bool)((dynamic)update).IsDownloaded, (bool)((dynamic)update).EulaAccepted));
                }
                finally { Release(identity); Release(update); }
            }
            if (code != 2) return new DriverUpdateResult(false, updates, "Windows Update devolvió una consulta incompleta. Abre Windows Update para revisar su estado.");
            return new DriverUpdateResult(true, updates, updates.Count == 0
                ? "Windows Update no ofreció drivers en esta consulta. El fabricante puede tener otras versiones disponibles."
                : $"Windows Update ofrece {updates.Count} actualización(es) de drivers. Revisa e instala desde la herramienta oficial.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidOperationException or NotSupportedException)
        {
            var message = ex.HResult == unchecked((int)0x80070005)
                ? "Windows Update rechazó el acceso. Abre Windows Update para consultar los drivers con los permisos disponibles."
                : $"No se pudo consultar Windows Update (0x{ex.HResult:X8}). Revisa conexión, servicio de Windows Update o políticas del equipo.";
            return new DriverUpdateResult(false, [], message);
        }
        finally
        {
            if (job is not null)
            {
                try { if (!((dynamic)job).IsCompleted) ((dynamic)job).RequestAbort(); }
                catch (COMException) { }
                try { ((dynamic)job).CleanUp(); }
                catch (COMException) { }
            }
            Release(collection); Release(result); Release(job); Release(searcher); Release(session);
            GC.KeepAlive(callback);
        }
    }

    private static string ComText(Func<object?> read)
    {
        try { return Convert.ToString(read(), CultureInfo.CurrentCulture)?.Trim() ?? ""; }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { return ""; }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}

// BeginSearch accepts an automation callback with DISPID 0; it must remain alive until CleanUp returns.
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
public sealed class SearchCompletedCallback
{
    [DispId(0)]
    public void Invoke(object searchJob, object callbackArgs) { }
}
