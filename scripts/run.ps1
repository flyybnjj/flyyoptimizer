$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskExe = Join-Path $taskRoot 'artifacts\FlyyOptimizer-win-x64\FlyyOptimizer.exe'
if (Test-Path -LiteralPath $taskExe) {
    Start-Process -FilePath $taskExe
    return
}
$taskBundled = Join-Path $env:USERPROFILE '.codex\runtimes\dotnet-10-sdk\dotnet.exe'
$taskDotnet = if (Test-Path -LiteralPath $taskBundled) { $taskBundled } else { 'dotnet' }
Push-Location $taskRoot
try { & $taskDotnet run --project src/FlyyOptimizer.App/FlyyOptimizer.App.csproj }
finally { Pop-Location }
