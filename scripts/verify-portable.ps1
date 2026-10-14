$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskPackage = Join-Path $taskRoot 'artifacts\FlyyOptimizer-win-x64'
$taskZip = Join-Path $taskRoot 'artifacts\FlyyOptimizer-win-x64.zip'
$taskItems = @(Get-ChildItem -LiteralPath $taskPackage -Force)
if ($taskItems.Count -ne 2 -or -not (Test-Path -LiteralPath (Join-Path $taskPackage 'FlyyOptimizer.exe') -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $taskPackage 'Archivos') -PathType Container)) {
    throw 'La carpeta portable debe contener solamente FlyyOptimizer.exe y Archivos.'
}
foreach ($taskRequired in @('README.md', 'THIRD-PARTY-NOTICES.md', 'licenses\WinUtil-MIT.txt', 'tools\WinUtil\fortnite-winutil.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskPackage "Archivos\$taskRequired") -PathType Leaf)) { throw "Falta el archivo de soporte: $taskRequired" }
}
$taskPinned = Join-Path $taskPackage 'Archivos\tools\WinUtil\fortnite-winutil.json'
if ((Get-FileHash -LiteralPath $taskPinned -Algorithm SHA256).Hash -ne '354CF19DD3FDB0354C8015137CE9A013C04D0EE9D781C4C460351CF82EE53A9B') { throw 'El recurso fijado de WinUtil fue alterado.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive = [System.IO.Compression.ZipFile]::OpenRead($taskZip)
try {
    $taskFiles = @(Get-ChildItem -LiteralPath $taskPackage -Recurse -File)
    $taskZipFiles = @($taskArchive.Entries | Where-Object { $_.Name })
    if ($taskFiles.Count -ne $taskZipFiles.Count) { throw 'El ZIP no coincide con los archivos publicados.' }
    $taskNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($taskEntry in $taskArchive.Entries) {
        $taskName = $taskEntry.FullName.Replace('\', '/')
        if (-not $taskName.StartsWith('FlyyOptimizer-win-x64/') -or $taskName.Contains('../') -or $taskName.Contains(':') -or -not $taskNames.Add($taskName)) { throw "Ruta inesperada o repetida en el ZIP: $taskName" }
        if (-not $taskEntry.Name) { continue }
        $taskRelative = $taskName.Substring('FlyyOptimizer-win-x64/'.Length)
        $taskFile = Join-Path $taskPackage $taskRelative
        if (-not (Test-Path -LiteralPath $taskFile -PathType Leaf)) { throw "Archivo inesperado en el ZIP: $taskRelative" }
        $taskStream = $taskEntry.Open()
        try { $taskHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($taskStream)) }
        finally { $taskStream.Dispose() }
        if ($taskHash -ne (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash) { throw "El ZIP contiene una versión diferente de $taskRelative" }
    }
}
finally { $taskArchive.Dispose() }

# Execute the extracted release in a different path, with spaces and no SDK lookup.
$taskEvidence = Join-Path $taskRoot ('artifacts\portable verification ' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($taskEvidence) | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory($taskZip, $taskEvidence)
$taskExtracted = Join-Path $taskEvidence 'FlyyOptimizer-win-x64'
$taskSmoke = Join-Path $taskEvidence 'smoke'
$taskStart = [System.Diagnostics.ProcessStartInfo]::new()
$taskStart.FileName = Join-Path $taskExtracted 'FlyyOptimizer.exe'
$taskStart.WorkingDirectory = $env:TEMP
$taskStart.UseShellExecute = $false
$taskStart.CreateNoWindow = $true
$taskStart.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$taskStart.RedirectStandardOutput = $true
$taskStart.RedirectStandardError = $true
$taskStart.ArgumentList.Add('--smoke-test')
$taskStart.ArgumentList.Add($taskSmoke)
$taskStart.Environment['PATH'] = Join-Path $env:WINDIR 'System32'
$taskStart.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
foreach ($taskVariable in @('DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROOT(x86)', 'MSBuildSDKsPath')) { $taskStart.Environment.Remove($taskVariable) | Out-Null }
$taskProcess = [System.Diagnostics.Process]::Start($taskStart)
try {
    $taskStdout = $taskProcess.StandardOutput.ReadToEndAsync()
    $taskStderr = $taskProcess.StandardError.ReadToEndAsync()
    if (-not $taskProcess.WaitForExit(60000)) { $taskProcess.Kill($true); throw 'La prueba portable excedió 60 segundos.' }
    $taskStdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $taskEvidence 'stdout.txt')
    $taskStderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $taskEvidence 'stderr.txt')
    if ($taskProcess.ExitCode -ne 0) { throw "El ZIP extraído falló con código $($taskProcess.ExitCode). Evidencia: $taskEvidence" }
}
finally { $taskProcess.Dispose() }
$taskStatus = Get-Content -LiteralPath (Join-Path $taskSmoke 'status.json') -Raw | ConvertFrom-Json
if (-not $taskStatus.Success -or $taskStatus.AppliedSystemChanges -or $taskStatus.CapturedGaming -or $taskStatus.InteractionChecks -lt 105 -or -not $taskStatus.FortnitePreviewReady) { throw 'La prueba de la app portable no cumple los contratos esperados.' }
[pscustomobject]@{ Success = $true; RootItems = @($taskItems.Name); VerifiedArchiveFiles = $taskFiles.Count; InteractionChecks = $taskStatus.InteractionChecks; Evidence = $taskEvidence } | ConvertTo-Json -Depth 4
