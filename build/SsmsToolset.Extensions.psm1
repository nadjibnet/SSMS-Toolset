#requires -Version 5.1
<#
    Shared helpers for install.ps1 / uninstall.ps1.
    Resolves the SSMS 22 Extensions directory for either the current user or all users.
#>

Set-StrictMode -Version Latest

# Folder name we install the extension under, inside .../Extensions/.
$script:ExtensionFolderName = 'SsmsToolset'

function Resolve-InstallScope {
    <#
        Returns 'CurrentUser' or 'AllUsers'. If $Scope is empty, prompts the user.
    #>
    param([string]$Scope)

    if ($Scope -in @('CurrentUser', 'AllUsers')) { return $Scope }

    while ($true) {
        $answer = Read-Host "Install SSMS-Toolset for (C)urrent user only, or (A)ll users? [C/A]"
        switch ($answer.Trim().ToUpperInvariant()) {
            'C'          { return 'CurrentUser' }
            'CURRENTUSER'{ return 'CurrentUser' }
            'A'          { return 'AllUsers' }
            'ALLUSERS'   { return 'AllUsers' }
            default      { Write-Host "Please enter C or A." -ForegroundColor Yellow }
        }
    }
}

function Test-IsAdministrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-CurrentUserExtensionsRoot {
    # e.g. %LocalAppData%\Microsoft\SSMS\22.0_9fb0eed0\Extensions
    $ssmsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\SSMS'
    if (-not (Test-Path $ssmsRoot)) {
        throw "SSMS user folder not found at '$ssmsRoot'. Has SSMS 22 been launched at least once?"
    }
    $verDir = Get-ChildItem -Path $ssmsRoot -Directory -Filter '22.*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1
    if (-not $verDir) {
        throw "No SSMS 22 profile folder (22.*) found under '$ssmsRoot'. Launch SSMS 22 once, then retry."
    }
    return (Join-Path $verDir.FullName 'Extensions')
}

function Get-AllUsersExtensionsRoot {
    # SSMS 22 install-dir Extensions folder: <install>\Release\Common7\IDE\Extensions
    $candidates = @(
        (Join-Path ${env:ProgramFiles} 'Microsoft SQL Server Management Studio 22\Release\Common7\IDE'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft SQL Server Management Studio 22\Release\Common7\IDE')
    )
    foreach ($ide in $candidates) {
        if ($ide -and (Test-Path (Join-Path $ide 'Ssms.exe'))) {
            return (Join-Path $ide 'Extensions')
        }
    }
    throw "Could not locate the SSMS 22 install directory. Pass -ExtensionsRoot explicitly if SSMS is installed in a non-default location."
}

function Get-ExtensionInstallDir {
    <#
        Resolves the full target folder (.../Extensions/SsmsToolset) for a scope.
        Validates admin rights for AllUsers.
    #>
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

Export-ModuleMember -Function Resolve-InstallScope, Test-IsAdministrator, `
    Get-CurrentUserExtensionsRoot, Get-AllUsersExtensionsRoot, Get-ExtensionInstallDir
