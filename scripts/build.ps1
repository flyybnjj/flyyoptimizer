param([switch]$SkipTests, [switch]$NoPublish)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskPresentMon = Join-Path $taskRoot 'assets\PresentMon\PresentMon.exe'
if (-not (Test-Path -LiteralPath $taskPresentMon -PathType Leaf)) { throw 'Falta el colector gaming incluido en assets\PresentMon. Recupera el archivo oficial fijado antes de compilar; el build no descarga herramientas.' }
if ((Get-FileHash -LiteralPath $taskPresentMon -Algorithm SHA256).Hash -ne 'B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF') { throw 'El colector gaming incluido no coincide con el SHA256 oficial fijado. No se publicará un paquete alterado.' }
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
        $taskZip = Join-Path $taskRoot 'artifacts\FlyyOptimizer-win-x64.zip'
        Compress-Archive -LiteralPath $taskOutput -DestinationPath $taskZip -Force
        Get-FileHash -LiteralPath $taskZip -Algorithm SHA256 | Format-List
        Write-Output "Aplicación: $(Join-Path $taskOutput 'FlyyOptimizer.exe')"
        Write-Output "Portable: $taskZip"
    }
}
finally { Pop-Location }
