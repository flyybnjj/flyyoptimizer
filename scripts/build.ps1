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
        & $taskDotnetPath publish src/FlyyOptimizer.App/FlyyOptimizer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $taskOutput
        if ($LASTEXITCODE -ne 0) { throw 'La publicación falló.' }
        foreach ($taskLegacyFile in @('tools\PresentMon\PresentMon.exe','tools\PresentMon\provenance.json')) {
            $taskLegacyPath = Join-Path $taskOutput $taskLegacyFile
            if (Test-Path -LiteralPath $taskLegacyPath -PathType Leaf) { Remove-Item -LiteralPath $taskLegacyPath }
        }
        $taskZip = Join-Path $taskRoot 'artifacts\FlyyOptimizer-win-x64.zip'
        Compress-Archive -LiteralPath $taskOutput -DestinationPath $taskZip -Force
        Get-FileHash -LiteralPath $taskZip -Algorithm SHA256 | Format-List
        Write-Output "Aplicación: $(Join-Path $taskOutput 'FlyyOptimizer.exe')"
        Write-Output "Portable: $taskZip"
    }
}
finally { Pop-Location }
