param([switch]$SkipTests, [switch]$NoPublish)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskWinUtil = Join-Path $taskRoot 'assets\WinUtil\fortnite-winutil.json'
if (-not (Test-Path -LiteralPath $taskWinUtil -PathType Leaf) -or (Get-FileHash -LiteralPath $taskWinUtil -Algorithm SHA256).Hash -ne '354CF19DD3FDB0354C8015137CE9A013C04D0EE9D781C4C460351CF82EE53A9B') { throw 'La selección integrada de WinUtil falta o fue alterada. Recupera el recurso fijado; el build no descarga ni ejecuta scripts.' }
$taskDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$taskBundled = Join-Path $env:USERPROFILE '.codex\runtimes\dotnet-10-sdk\dotnet.exe'
if (Test-Path -LiteralPath $taskBundled) { $taskDotnetPath = $taskBundled }
elseif ($taskDotnet) { $taskDotnetPath = $taskDotnet.Source }
else { throw 'Instala el SDK .NET 10 desde https://dotnet.microsoft.com/download/dotnet/10.0' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $taskRoot
try {
    if (-not $SkipTests) {
        & $taskDotnetPath test FlyyOptimizer.sln -c Release --logger 'console;verbosity=minimal'
        if ($LASTEXITCODE -ne 0) { throw 'Las pruebas fallaron.' }
    }
    & $taskDotnetPath build FlyyOptimizer.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }
    if (-not $NoPublish) {
        $taskOutput = Join-Path $taskRoot 'artifacts\FlyyOptimizer-win-x64'
        $taskArtifacts = [System.IO.Path]::GetFullPath((Join-Path $taskRoot 'artifacts'))
        $taskStaging = Join-Path $taskArtifacts ('portable-staging-' + [Guid]::NewGuid().ToString('N'))
        & $taskDotnetPath publish src/FlyyOptimizer.App/FlyyOptimizer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o $taskStaging
        if ($LASTEXITCODE -ne 0) { throw 'La publicación falló.' }
        $taskItems = @(Get-ChildItem -LiteralPath $taskStaging -Force)
        if ($taskItems.Count -ne 2 -or -not (Test-Path -LiteralPath (Join-Path $taskStaging 'FlyyOptimizer.exe') -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $taskStaging 'Archivos') -PathType Container)) {
            throw "La publicación no produjo la estructura esperada. Revisa $taskStaging."
        }
        # Only replace our own build output, after a fresh publish has succeeded.
        $taskOutput = [System.IO.Path]::GetFullPath($taskOutput)
        if ((Split-Path -Parent $taskOutput) -ne $taskArtifacts -or (Split-Path -Leaf $taskOutput) -ne 'FlyyOptimizer-win-x64') { throw 'La salida está fuera del directorio de artifacts esperado.' }
        if (Test-Path -LiteralPath $taskOutput) {
            if ((Get-Item -LiteralPath $taskOutput -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'La salida no puede ser un enlace.' }
            Remove-Item -LiteralPath $taskOutput -Recurse -Force
        }
        Move-Item -LiteralPath $taskStaging -Destination $taskOutput
        $taskZip = Join-Path $taskRoot 'artifacts\FlyyOptimizer-win-x64.zip'
        Compress-Archive -LiteralPath $taskOutput -DestinationPath $taskZip -Force
        Get-FileHash -LiteralPath $taskZip -Algorithm SHA256 | Format-List
        Write-Output "Aplicación: $(Join-Path $taskOutput 'FlyyOptimizer.exe')"
        Write-Output "Portable: $taskZip"
    }
}
finally { Pop-Location }
