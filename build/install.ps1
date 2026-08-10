#requires -Version 5.1
<#
.SYNOPSIS
    Installs the SSMS-Toolset extension into SQL Server Management Studio 22.

.DESCRIPTION
    Extracts the VSIX payload (a zip) into the SSMS Extensions folder, then you
    restart SSMS. Choose Current-User (no admin) or All-Users (admin, install dir).
    VSIXInstaller.exe is intentionally not used: it is not a reliable path for
    SSMS extensions.

.PARAMETER Scope
    'CurrentUser' or 'AllUsers'. If omitted, you are prompted.

.PARAMETER VsixPath
    Path to the .vsix. Defaults to the newest under .\artifacts.

.EXAMPLE
    ./build/install.ps1
    ./build/install.ps1 -Scope CurrentUser
    ./build/install.ps1 -Scope AllUsers   # run from an elevated prompt
#>
[CmdletBinding()]
param(
    [ValidateSet('CurrentUser', 'AllUsers')]
    [string]$Scope,

    [string]$VsixPath,

    # Advanced: override the Extensions root (e.g. non-default SSMS install path).
    [string]$ExtensionsRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'SsmsToolset.Extensions.psm1') -Force

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
    # Copy to a .zip name so Expand-Archive accepts it.
    $zip = Join-Path $temp 'payload.zip'
    Copy-Item $VsixPath $zip -Force
    Expand-Archive -Path $zip -DestinationPath $temp -Force
    Remove-Item $zip -Force

    if (Test-Path $installDir) {
        Write-Host "Removing existing install ..." -ForegroundColor Yellow
        Remove-Item $installDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installDir | Out-Null

    # Copy the extension files (skip VSIX packaging metadata that SSMS ignores).
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

Write-Host "Installed." -ForegroundColor Green
Write-Host ""
Write-Host "Restart SSMS 22. Then look under the Tools menu for 'SSMS Toolset: Hello'." -ForegroundColor Yellow
