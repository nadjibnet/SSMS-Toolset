#requires -Version 5.1
<#
.SYNOPSIS
    Copies the SSMS interop assemblies this extension builds against from the
    local SQL Server Management Studio 22 install into .\lib\Ssms22.

.DESCRIPTION
    These assemblies are Microsoft's and are NOT redistributed with this repo
    (lib\ is git-ignored). Every contributor copies them from their own SSMS 22
    install with this script. The build references them with Private=False, so
    they are never packaged into the VSIX either — SSMS provides them at runtime.

.EXAMPLE
    ./build/fetch-ssms-libs.ps1
#>
[CmdletBinding()]
param(
    # Override if SSMS 22 is installed somewhere non-default.
    [string]$SsmsIdeDir
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$destDir = Join-Path $repoRoot 'lib\Ssms22'

$required = @('SqlWorkbench.Interfaces.dll', 'SqlPackageBase.dll', 'Microsoft.SqlServer.RegSvrEnum.dll')

if (-not $SsmsIdeDir) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles} 'Microsoft SQL Server Management Studio 22\Release\Common7\IDE'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft SQL Server Management Studio 22\Release\Common7\IDE')
    )
    $SsmsIdeDir = $candidates | Where-Object { $_ -and (Test-Path (Join-Path $_ 'Ssms.exe')) } | Select-Object -First 1
}
if (-not $SsmsIdeDir -or -not (Test-Path $SsmsIdeDir)) {
    throw "Could not find the SSMS 22 IDE folder. Install SSMS 22, or pass -SsmsIdeDir '<...>\Common7\IDE'."
}

if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir | Out-Null }

foreach ($dll in $required) {
    $src = Join-Path $SsmsIdeDir $dll
    if (-not (Test-Path $src)) {
        throw "Required assembly not found in SSMS install: $src"
    }
    Copy-Item $src (Join-Path $destDir $dll) -Force
    Write-Host "  copied $dll" -ForegroundColor Green
}

Write-Host "SSMS interop assemblies are in: $destDir" -ForegroundColor Cyan
