#requires -Version 5.1
<#
.SYNOPSIS
    Installs the SSMS-Toolset extension into SQL Server Management Studio 22.

.DESCRIPTION
    Copies the VSIX payload into the SSMS Extensions folder, clears the SSMS
    extension-discovery caches, and runs "Ssms.exe /updateconfiguration" so SSMS
    actually picks up the extension. SSMS must be CLOSED while this runs.

    VSIXInstaller.exe is intentionally not used: it is not a reliable path for
    SSMS extensions.

.PARAMETER Scope
    'CurrentUser' or 'AllUsers'. If omitted, you are prompted.

.PARAMETER VsixPath
    Path to the .vsix. Defaults to the newest under .\artifacts.

.PARAMETER SkipUpdateConfiguration
    Skip running Ssms.exe /updateconfiguration (cache is still cleared, so a
    normal SSMS restart will pick up the change).

.EXAMPLE
    ./build/install.ps1
    ./build/install.ps1 -Scope CurrentUser
    ./build/install.ps1 -Scope AllUsers    # from an elevated prompt
#>
[CmdletBinding()]
param(
    [ValidateSet('CurrentUser', 'AllUsers')]
    [string]$Scope,

    [string]$VsixPath,
    [string]$ExtensionsRoot,
    [switch]$SkipUpdateConfiguration
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'SsmsToolset.Extensions.psm1') -Force

# 0. SSMS must be closed (caches are locked / rewritten while it runs).
if (Test-SsmsRunning) {
    throw "SSMS is currently running. Close all SSMS 22 windows, then re-run this script."
}

# 1. Locate the VSIX.
if (-not $VsixPath) {
    $VsixPath = Get-ChildItem -Path (Join-Path $repoRoot 'artifacts') -Filter '*.vsix' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object FullName
}
if (-not $VsixPath -or -not (Test-Path $VsixPath)) {
    throw "No VSIX found. Run ./build/build.ps1 first, or pass -VsixPath."
}

# 2. Resolve scope + target folder.
$resolvedScope = Resolve-InstallScope -Scope $Scope
$installDir = Get-ExtensionInstallDir -Scope $resolvedScope -ExtensionsRoot $ExtensionsRoot

Write-Host "VSIX   : $VsixPath"
Write-Host "Scope  : $resolvedScope"
Write-Host "Target : $installDir"
Write-Host ""

# 3. Extract the VSIX (zip) into a temp folder, then copy into the target.
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("SsmsToolset_" + [System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $zip = Join-Path $temp 'payload.zip'
    Copy-Item $VsixPath $zip -Force
    Expand-Archive -Path $zip -DestinationPath $temp -Force
    Remove-Item $zip -Force

    if (Test-Path $installDir) {
        Write-Host "Removing existing install ..." -ForegroundColor Yellow
        Remove-Item $installDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installDir | Out-Null

    # Copy the extension files (skip VSIX packaging metadata SSMS ignores).
    Get-ChildItem -Path $temp -Recurse -File |
        Where-Object { $_.Name -notin @('[Content_Types].xml') } |
        ForEach-Object {
            $rel = $_.FullName.Substring($temp.Length).TrimStart('\')
            $out = Join-Path $installDir $rel
            $outDir = Split-Path -Parent $out
            if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
            Copy-Item $_.FullName $out -Force
        }
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Files copied." -ForegroundColor Green

# 4. Force SSMS to re-discover extensions.
Write-Host "Clearing SSMS extension caches ..." -ForegroundColor Cyan
Reset-SsmsExtensionCache
if (-not $SkipUpdateConfiguration) {
    Invoke-SsmsUpdateConfiguration
}

Write-Host ""
Write-Host "Installed." -ForegroundColor Green
Write-Host "Start SSMS 22, then right-click a DATABASE in Object Explorer -> 'SSMS Toolset'." -ForegroundColor Yellow
