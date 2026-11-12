# Webpoint Official Installer Script
Write-Host "Installing Webpoint as a registered Windows application..." -ForegroundColor Cyan

$webpointDir = "$env:LOCALAPPDATA\Webpoint"
if (-not (Test-Path $webpointDir)) {
    New-Item -ItemType Directory -Path $webpointDir -Force | Out-Null
}

$repoRoot = Split-Path -Parent $PSScriptRoot

# Locate built binary or build if needed
$sourceExe = $null
$possibleExes = @(
    "$PSScriptRoot\Webpoint.exe",
    "$repoRoot\bin\Webpoint.exe",
    "$repoRoot\Webpoint.exe",
    "$webpointDir\Webpoint.exe"
)
foreach ($p in $possibleExes) {
    if (Test-Path $p) {
        $sourceExe = $p
        break
    }
}

if (-not $sourceExe) {
    # Attempt automatic build
    $buildScript = "$repoRoot\build.ps1"
    if (Test-Path $buildScript) {
        Write-Host "Compiling Webpoint.exe..." -ForegroundColor Yellow
        & $buildScript
        if (Test-Path "$repoRoot\bin\Webpoint.exe") {
            $sourceExe = "$repoRoot\bin\Webpoint.exe"
        }
    }
}

if ($sourceExe -and ($sourceExe -ne "$webpointDir\Webpoint.exe")) {
    Copy-Item $sourceExe "$webpointDir\Webpoint.exe" -Force
}

# Copy icon
$possibleIcons = @(
    "$PSScriptRoot\app.ico",
    "$repoRoot\assets\app.ico",
    "$webpointDir\app.ico"
)
foreach ($i in $possibleIcons) {
    if (Test-Path $i) {
        if ($i -ne "$webpointDir\app.ico") {
            Copy-Item $i "$webpointDir\app.ico" -Force
        }
        break
    }
}

# Copy default settings if not already present
$possibleSettings = @(
    "$PSScriptRoot\settings.json",
    "$repoRoot\src\settings.json"
)
if (-not (Test-Path "$webpointDir\settings.json")) {
    foreach ($s in $possibleSettings) {
        if (Test-Path $s) {
            Copy-Item $s "$webpointDir\settings.json" -Force
            break
        }
    }
}

# Copy uninstaller script to target directory
$uninstallScript = "$PSScriptRoot\Uninstall.ps1"
if (Test-Path $uninstallScript) {
    Copy-Item $uninstallScript "$webpointDir\Uninstall.ps1" -Force
}

$exePath = "$webpointDir\Webpoint.exe"
$icoPath = "$webpointDir\app.ico"

# 1. Start Menu Shortcut
$wsh = New-Object -ComObject WScript.Shell
$programsFolder = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$shortcutPath = "$programsFolder\Webpoint.lnk"

$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $webpointDir
$shortcut.IconLocation = "$exePath,0"
$shortcut.Description = "Webpoint — Browser Profile Dispatcher"
$shortcut.Save()

# 2. Windows Installed Apps Registration
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Webpoint"
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "Webpoint"
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "Webpoint"
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value "$icoPath"
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value "$webpointDir"
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$webpointDir\Uninstall.ps1`""
Set-ItemProperty -Path $uninstallKey -Name "EstimatedSize" -Value 150
Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1
Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1

# 3. Protocol & Browser Association Registration
$progId = "Webpoint"
New-Item -Path "HKCU:\Software\Classes\$progId" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\$progId" -Name "(Default)" -Value "Webpoint HTML Document"
Set-ItemProperty -Path "HKCU:\Software\Classes\$progId" -Name "FriendlyTypeName" -Value "Webpoint Document"

New-Item -Path "HKCU:\Software\Classes\$progId\DefaultIcon" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\$progId\DefaultIcon" -Name "(Default)" -Value "$icoPath"

New-Item -Path "HKCU:\Software\Classes\$progId\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\$progId\shell\open\command" -Name "(Default)" -Value "`"$exePath`" `"%1`""

$smiPath = "HKCU:\Software\Clients\StartMenuInternet\Webpoint"
New-Item -Path $smiPath -Force | Out-Null
Set-ItemProperty -Path $smiPath -Name "(Default)" -Value "Webpoint"

New-Item -Path "$smiPath\DefaultIcon" -Force | Out-Null
Set-ItemProperty -Path "$smiPath\DefaultIcon" -Name "(Default)" -Value "$icoPath"

New-Item -Path "$smiPath\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "$smiPath\shell\open\command" -Name "(Default)" -Value "`"$exePath`""

$capPath = "$smiPath\Capabilities"
New-Item -Path $capPath -Force | Out-Null
Set-ItemProperty -Path $capPath -Name "ApplicationDescription" -Value "Webpoint Browser Profile Dispatcher"
Set-ItemProperty -Path $capPath -Name "ApplicationName" -Value "Webpoint"

New-Item -Path "$capPath\URLAssociations" -Force | Out-Null
Set-ItemProperty -Path "$capPath\URLAssociations" -Name "http" -Value $progId
Set-ItemProperty -Path "$capPath\URLAssociations" -Name "https" -Value $progId

New-Item -Path "$capPath\FileAssociations" -Force | Out-Null
Set-ItemProperty -Path "$capPath\FileAssociations" -Name ".htm" -Value $progId
Set-ItemProperty -Path "$capPath\FileAssociations" -Name ".html" -Value $progId

Set-ItemProperty -Path "HKCU:\Software\RegisteredApplications" -Name "Webpoint" -Value "Software\Clients\StartMenuInternet\Webpoint\Capabilities"

# 4. Notify Windows Shell
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class InstallShell {
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
"@
[InstallShell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

Write-Host "Webpoint installation complete!" -ForegroundColor Green
