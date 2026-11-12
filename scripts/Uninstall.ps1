# Webpoint Official Uninstaller Script
Write-Host "Uninstalling Webpoint..." -ForegroundColor Cyan

# 1. Remove Start Menu shortcut
$shortcutPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Webpoint.lnk"
if (Test-Path $shortcutPath) {
    Remove-Item -Path $shortcutPath -Force -ErrorAction SilentlyContinue
}

# 2. Remove Windows Installed Apps entry
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Webpoint" -Recurse -Force -ErrorAction SilentlyContinue

# 3. Remove Browser & Protocol registrations
Remove-Item -Path "HKCU:\Software\Classes\Webpoint" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Clients\StartMenuInternet\Webpoint" -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKCU:\Software\RegisteredApplications" -Name "Webpoint" -Force -ErrorAction SilentlyContinue

# 4. Notify Windows Shell
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class UninstallShell {
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
"@
[UninstallShell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

Write-Host "Webpoint has been uninstalled successfully." -ForegroundColor Green
Write-Host "Opening Windows Default Apps settings so you can reset your default browser..." -ForegroundColor Yellow
Start-Process "ms-settings:defaultapps"

# Clean folder
$dir = "$env:LOCALAPPDATA\Webpoint"
Start-Process cmd.exe -ArgumentList "/c timeout /t 2 & rmdir /s /q `"$dir`"" -WindowStyle Hidden
