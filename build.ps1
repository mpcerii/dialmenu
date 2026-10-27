# Baut DialMenu ohne Visual Studio.
#
#   .\build.ps1            Release-Build
#   .\build.ps1 -Clean     vorher aufraeumen

param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'DialMenu.csproj'

$msbuild = @(
    'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $msbuild) {
    Write-Error 'MSBuild nicht gefunden. Visual Studio oder die Build Tools installieren.'
}

if ($Clean) {
    foreach ($dir in @('bin', 'obj')) {
        $p = Join-Path $root $dir
        if (Test-Path $p) { Remove-Item $p -Recurse -Force }
    }
}

Write-Host "MSBuild: $msbuild" -ForegroundColor DarkGray
& $msbuild $proj /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $root 'bin\Release\DialMenu.exe'
if (Test-Path $exe) {
    $size = [math]::Round((Get-Item $exe).Length / 1KB)
    Write-Host ""
    Write-Host "Fertig: $exe ($size KB)" -ForegroundColor Green
}
