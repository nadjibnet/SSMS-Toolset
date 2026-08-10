#requires -Version 5.1
<#
.SYNOPSIS
    Restores and builds the SSMS-Toolset VSIX, then copies it to .\artifacts.

.DESCRIPTION
    Uses MSBuild from a Visual Studio install that has the VSSDK component
    (required to build/package a VSIX). Works with VS 2022 (17.x) or VS 2026
    (18.x); SSMS 22 itself is not used to build.

.EXAMPLE
    ./build/build.ps1                 # Release build
    ./build/build.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # Optional explicit path to MSBuild.exe; auto-detected via vswhere if omitted.
    [string]$MSBuildPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'SsmsToolset.sln'
$artifacts = Join-Path $repoRoot 'artifacts'

function Find-MSBuild {
    if ($MSBuildPath) {
        if (-not (Test-Path $MSBuildPath)) { throw "MSBuild not found at '$MSBuildPath'." }
        return $MSBuildPath
    }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found. Install Visual Studio with the 'Visual Studio extension development' (VSSDK) workload, or pass -MSBuildPath."
    }
    # Require the VSSDK component so we pick the VS install (not SSMS) that can package a VSIX.
    $found = & $vswhere -latest -products * `
        -requires Microsoft.Component.MSBuild Microsoft.VisualStudio.Component.VSSDK `
        -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
    if (-not $found) {
        throw "Could not locate an MSBuild with the VSSDK. Install the 'Visual Studio extension development' workload, or pass -MSBuildPath."
    }
    return $found
}

$msbuild = Find-MSBuild
Write-Host "Using MSBuild: $msbuild" -ForegroundColor Cyan
Write-Host "Building $Configuration ..." -ForegroundColor Cyan

& $msbuild $solution "/t:Restore;Rebuild" "/p:Configuration=$Configuration" "/p:Platform=Any CPU" /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

$binDir = Join-Path (Join-Path $repoRoot 'src\SsmsToolset\bin') $Configuration
$vsix = Get-ChildItem -Path $binDir -Filter '*.vsix' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $vsix) { throw "Build succeeded but no .vsix was produced under src\SsmsToolset\bin\$Configuration." }

if (-not (Test-Path $artifacts)) { New-Item -ItemType Directory -Path $artifacts | Out-Null }
$dest = Join-Path $artifacts $vsix.Name
Copy-Item $vsix.FullName $dest -Force

Write-Host ""
Write-Host "Built VSIX:" -ForegroundColor Green
Write-Host "  $dest"
Write-Host ""
Write-Host "Next: install it with  ./build/install.ps1" -ForegroundColor Yellow
