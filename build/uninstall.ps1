#requires -Version 5.1
<#
.SYNOPSIS
    Removes the SSMS-Toolset extension from SQL Server Management Studio 22.

.PARAMETER Scope
    'CurrentUser' or 'AllUsers'. If omitted, you are prompted.

.EXAMPLE
    ./build/uninstall.ps1
    ./build/uninstall.ps1 -Scope AllUsers    # from an elevated prompt
#>
[CmdletBinding()]
param(
    [ValidateSet('CurrentUser', 'AllUsers')]
    [string]$Scope,

    [string]$ExtensionsRoot,
    [switch]$SkipUpdateConfiguration
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SsmsToolset.Extensions.psm1') -Force

if (Test-SsmsRunning) {
    throw "SSMS is currently running. Close all SSMS 22 windows, then re-run this script."
}

$resolvedScope = Resolve-InstallScope -Scope $Scope
$installDir = Get-ExtensionInstallDir -Scope $resolvedScope -ExtensionsRoot $ExtensionsRoot

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
    Write-Host "Removed: $installDir" -ForegroundColor Green

    Write-Host "Clearing SSMS extension caches ..." -ForegroundColor Cyan
    Reset-SsmsExtensionCache
    if (-not $SkipUpdateConfiguration) {
        Invoke-SsmsUpdateConfiguration
    }
    Write-Host "Done. Start SSMS 22 to complete uninstall." -ForegroundColor Yellow
}
else {
    Write-Host "Nothing to remove at: $installDir" -ForegroundColor Yellow
}
