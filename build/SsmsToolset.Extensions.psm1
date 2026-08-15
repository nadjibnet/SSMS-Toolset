#requires -Version 5.1
<#
    Shared helpers for install.ps1 / uninstall.ps1.

    Resolves the SSMS 22 Extensions directory (current user or all users) and,
    crucially, forces SSMS to re-discover extensions. SSMS caches discovered
    extensions and will silently ignore a freshly-copied folder unless those
    caches are cleared and the configuration is updated.
#>

Set-StrictMode -Version Latest

$script:ExtensionFolderName = 'SsmsToolset'

# ---------------------------------------------------------------------------
# Install scope / target resolution
# ---------------------------------------------------------------------------

function Resolve-InstallScope {
    param([string]$Scope)

    if ($Scope -in @('CurrentUser', 'AllUsers')) { return $Scope }

    while ($true) {
        $answer = Read-Host "Install SSMS-Toolset for (C)urrent user only, or (A)ll users? [C/A]"
        switch ($answer.Trim().ToUpperInvariant()) {
            'C'           { return 'CurrentUser' }
            'CURRENTUSER' { return 'CurrentUser' }
            'A'           { return 'AllUsers' }
            'ALLUSERS'    { return 'AllUsers' }
            default       { Write-Host "Please enter C or A." -ForegroundColor Yellow }
        }
    }
}

function Test-IsAdministrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-SsmsIdeDir {
    # <install>\Release\Common7\IDE  (contains Ssms.exe)
    $candidates = @(
        (Join-Path ${env:ProgramFiles} 'Microsoft SQL Server Management Studio 22\Release\Common7\IDE'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft SQL Server Management Studio 22\Release\Common7\IDE')
    )
    foreach ($ide in $candidates) {
        if ($ide -and (Test-Path (Join-Path $ide 'Ssms.exe'))) { return $ide }
    }
    throw "Could not locate the SSMS 22 install directory. Pass -ExtensionsRoot / -SsmsExe explicitly if SSMS is installed in a non-default location."
}

function Get-SsmsExePath {
    return (Join-Path (Get-SsmsIdeDir) 'Ssms.exe')
}

function Get-CurrentUserExtensionsRoot {
    # e.g. %LocalAppData%\Microsoft\SSMS\22.0_9fb0eed0\Extensions
    $verDir = Get-SsmsProfileFolders | Select-Object -First 1
    if (-not $verDir) {
        throw "No SSMS 22 profile folder (22.*) found under '$env:LOCALAPPDATA\Microsoft\SSMS'. Launch SSMS 22 once, then retry."
    }
    return (Join-Path $verDir 'Extensions')
}

function Get-AllUsersExtensionsRoot {
    return (Join-Path (Get-SsmsIdeDir) 'Extensions')
}

function Get-ExtensionInstallDir {
    param(
        [Parameter(Mandatory)][ValidateSet('CurrentUser', 'AllUsers')][string]$Scope,
        [string]$ExtensionsRoot
    )

    if (-not $ExtensionsRoot) {
        if ($Scope -eq 'AllUsers') {
            if (-not (Test-IsAdministrator)) {
                throw "Installing for All Users writes to Program Files and requires an elevated (Administrator) PowerShell. Re-run as admin, or choose Current User."
            }
            $ExtensionsRoot = Get-AllUsersExtensionsRoot
        }
        else {
            $ExtensionsRoot = Get-CurrentUserExtensionsRoot
        }
    }

    return (Join-Path $ExtensionsRoot $script:ExtensionFolderName)
}

# ---------------------------------------------------------------------------
# Extension re-discovery (the part a plain folder-copy gets wrong)
# ---------------------------------------------------------------------------

function Get-SsmsProfileFolders {
    # All per-user SSMS 22 profile folders, newest first. These hold the
    # extension-discovery caches, regardless of where the extension is installed.
    $root = Join-Path $env:LOCALAPPDATA 'Microsoft\SSMS'
    if (-not (Test-Path $root)) { return @() }
    return Get-ChildItem -Path $root -Directory -Filter '22.*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | ForEach-Object FullName
}

function Test-SsmsRunning {
    return [bool](Get-Process -Name 'Ssms' -ErrorAction SilentlyContinue)
}

function Reset-SsmsExtensionCache {
    <#
        Clears the SSMS extension-discovery caches so a copied extension is
        detected. Mirrors the proven manual steps: delete the ExtensionMetadata
        caches and the MEF/ComponentModel caches in every SSMS 22 profile.
        SSMS must be closed for this to take effect.
    #>
    foreach ($profile in Get-SsmsProfileFolders) {
        $targets = @(
            (Join-Path $profile 'Extensions\ExtensionMetadataCache.mpack'),
            (Join-Path $profile 'Extensions\ExtensionMetadata2.0.mpack')
        )
        foreach ($f in $targets) {
            if (Test-Path $f) { Remove-Item $f -Force -ErrorAction SilentlyContinue; Write-Verbose "Deleted $f" }
        }

        foreach ($dir in @('ComponentModelCache', 'MEFCacheBackup')) {
            $d = Join-Path $profile $dir
            if (Test-Path $d) {
                Get-ChildItem $d -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
                Write-Verbose "Cleared $d"
            }
        }

        # Also bump the change sentinel (belt and suspenders).
        $sentinel = Join-Path $profile 'Extensions\extensions.configurationchanged'
        if (Test-Path (Split-Path -Parent $sentinel)) {
            Set-Content -Path $sentinel -Value ([DateTimeOffset]::UtcNow.ToString('o')) -Encoding ASCII -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-SsmsUpdateConfiguration {
    <#
        Runs "Ssms.exe /updateconfiguration", which makes the shell merge
        extension pkgdefs into its configuration store and exit (no full UI).
        This is the strongest way to force SSMS to pick up a new extension.
    #>
    param([int]$TimeoutSeconds = 120)

    $ssms = Get-SsmsExePath
    Write-Host "Running: Ssms.exe /updateconfiguration ..." -ForegroundColor Cyan
    $p = Start-Process -FilePath $ssms -ArgumentList '/updateconfiguration' -PassThru
    if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
        Write-Host "  /updateconfiguration is taking a while; continuing (it will finish in the background)." -ForegroundColor Yellow
    }
    else {
        Write-Host "  Configuration updated." -ForegroundColor Green
    }
}

Export-ModuleMember -Function `
    Resolve-InstallScope, Test-IsAdministrator, `
    Get-SsmsIdeDir, Get-SsmsExePath, `
    Get-CurrentUserExtensionsRoot, Get-AllUsersExtensionsRoot, Get-ExtensionInstallDir, `
    Get-SsmsProfileFolders, Test-SsmsRunning, Reset-SsmsExtensionCache, Invoke-SsmsUpdateConfiguration
