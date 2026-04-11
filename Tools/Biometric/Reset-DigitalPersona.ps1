$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-Msi {
    param(
        [Parameter(Mandatory = $true)][string]$Arguments,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    Write-Host "msiexec.exe $Arguments /L*v `"$LogPath`""
    $process = Start-Process msiexec.exe -ArgumentList "$Arguments /L*v `"$LogPath`"" -Wait -PassThru
    Write-Host "ExitCode: $($process.ExitCode)"
    if (Test-Path $LogPath) {
        Get-Content $LogPath | Select-Object -Last 25
    }

    return $process.ExitCode
}

if (-not (Test-IsAdmin)) {
    Write-Error "Run this script as Administrator."
}

$sdkProductCode = '{B59BEA44-55D2-4EAB-87E7-B04E8D7F4596}'
$driverProductCode = '{F1739711-AF16-416B-AE78-5F8583A7DBC2}'
$downloadsRoot = 'C:\Users\User\Downloads'
$uiSample = 'C:\Program Files\DigitalPersona\One Touch SDK\.NET\Samples\Visual Studio 2005\CSharp\UI Support\Release\UISupportSample CS.exe'
$tempRoot = Join-Path $env:LOCALAPPDATA 'Temp'

function Resolve-InstallerPath {
    param(
        [Parameter(Mandatory = $true)][string[]]$Candidates,
        [Parameter(Mandatory = $true)][string]$SearchRoot,
        [Parameter(Mandatory = $true)][string]$Filter
    )

    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $match = Get-ChildItem $SearchRoot -Recurse -Filter $Filter -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        Select-Object -First 1

    return $match.FullName
}

$sdkInstaller = Resolve-InstallerPath -SearchRoot $downloadsRoot -Filter 'Setup.msi' -Candidates @(
    (Join-Path $downloadsRoot 'SDK-DigitalPersona-master\SDK-DigitalPersona-master\SDK\Install\x64\Setup.msi'),
    (Join-Path $downloadsRoot 'SDK-DigitalPersona-master\SDK-DigitalPersona-master\SDK\Install\Setup.msi')
)

$driverInstaller = Resolve-InstallerPath -SearchRoot $downloadsRoot -Filter 'setup_x64.msi' -Candidates @(
    (Join-Path $downloadsRoot 'SFW-02580-DP4500 Fingerprint Reader Driver (Legacy) with installer v.4.1.1.221\Legacy-4.1.1.221\DP4500-4.1.1.221\setup_x64.msi')
)

Write-Step 'Stopping biometric-related user processes'
Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $_.ProcessName -match 'HRMS|DpHostW|DigitalPersonaOneTouchBridge|UISupportSample|EnrollmentSample' } |
    Stop-Process -Force -ErrorAction SilentlyContinue

Write-Step 'Disabling Windows Biometric Service for this session'
try {
    Stop-Service WbioSrvc -Force -ErrorAction Stop
} catch {
    Write-Warning $_.Exception.Message
}

Write-Step 'Stopping HID DigitalPersona Biometric Authentication Service'
try {
    Stop-Service DpHost -Force -ErrorAction Stop
} catch {
    Write-Warning $_.Exception.Message
}

Write-Step 'Uninstalling DigitalPersona One Touch SDK'
Invoke-Msi -Arguments "/x $sdkProductCode /qn /norestart" -LogPath (Join-Path $tempRoot 'dp_sdk_uninstall_admin.log') | Out-Null

Write-Step 'Uninstalling HID DigitalPersona 4500 driver'
Invoke-Msi -Arguments "/x $driverProductCode /qn /norestart" -LogPath (Join-Path $tempRoot 'dp_driver_uninstall_admin.log') | Out-Null

Write-Step 'Removing leftover DigitalPersona folders'
foreach ($folder in @('C:\Program Files\DigitalPersona', 'C:\Program Files (x86)\DigitalPersona')) {
    if (Test-Path $folder) {
        Write-Host "Removing $folder"
        Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Step 'Installing HID DigitalPersona 4500 driver from local package'
if (-not (Test-Path $driverInstaller)) {
    throw "Driver installer not found: $driverInstaller"
}
Invoke-Msi -Arguments "/i `"$driverInstaller`" /qn /norestart" -LogPath (Join-Path $tempRoot 'dp_driver_install_admin.log') | Out-Null

Write-Step 'Installing DigitalPersona One Touch SDK from local package'
if (-not (Test-Path $sdkInstaller)) {
    throw "SDK installer not found: $sdkInstaller"
}
Invoke-Msi -Arguments "/i `"$sdkInstaller`" /qn /norestart" -LogPath (Join-Path $tempRoot 'dp_sdk_install_admin.log') | Out-Null

Write-Step 'Starting biometric services'
foreach ($serviceName in @('WbioSrvc', 'DpHost')) {
    try {
        Start-Service $serviceName -ErrorAction Stop
        Get-Service $serviceName | Select-Object Name, Status, StartType
    } catch {
        Write-Warning $_.Exception.Message
    }
}

Write-Step 'Checking installed packages'
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" /s |
    findstr /i "DigitalPersona HID U.are.U Fingerprint Biometric DisplayName DisplayVersion UninstallString"

Write-Step 'Checking connected reader'
pnputil /enum-devices /connected /class "Authentication Devices"

Write-Step 'Launching official SDK UI Support sample'
if (Test-Path $uiSample) {
    Start-Process -FilePath $uiSample
    Write-Host "Launched: $uiSample"
} else {
    Write-Warning "Sample not found at $uiSample"
}

Write-Step 'Done'
Write-Host 'Test the official sample first. If it captures your finger there, then test HRMS again.'
