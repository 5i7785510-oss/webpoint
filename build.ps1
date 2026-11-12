<#
.SYNOPSIS
    Builds Webpoint.exe using the built-in Windows .NET Framework C# compiler.
.DESCRIPTION
    Requires no third-party build tools. Works on any stock Windows 10/11 machine.
.PARAMETER OutputDir
    Directory to place the compiled executable. Defaults to "bin".
#>
[CmdletBinding()]
param(
    [string]$OutputDir = "$PSScriptRoot\bin"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building Webpoint ===" -ForegroundColor Cyan

# 1. Locate csc.exe
$cscPaths = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$csc = $null
foreach ($path in $cscPaths) {
    if (Test-Path $path) {
        $csc = $path
        break
    }
}

if (-not $csc) {
    throw "C# compiler (csc.exe) not found in Windows Microsoft.NET framework directories."
}

Write-Host "Using compiler: $csc" -ForegroundColor Gray

# 2. Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$sourceFile = "$PSScriptRoot\src\App.cs"
$iconFile   = "$PSScriptRoot\assets\app.ico"
$outputExe  = "$OutputDir\Webpoint.exe"
$wpfLibDir  = Split-Path -Parent $csc | Join-Path -ChildPath "WPF"

if (-not (Test-Path $sourceFile)) {
    throw "Source file not found at: $sourceFile"
}

# 3. Terminate running instance if writing to an existing executable
$running = Get-Process -Name "Webpoint" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running Webpoint instances..." -ForegroundColor Yellow
    Stop-Process -Name "Webpoint" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
}

# 4. Compile
$compileArgs = @(
    "/nologo",
    "/optimize+",
    "/target:winexe",
    "/out:`"$outputExe`"",
    "/win32icon:`"$iconFile`"",
    "/lib:`"$wpfLibDir`"",
    "/r:System.dll,System.Core.dll,System.Drawing.dll,System.Xaml.dll,WindowsBase.dll,PresentationCore.dll,PresentationFramework.dll",
    "`"$sourceFile`""
)

Write-Host "Compiling $sourceFile -> $outputExe..." -ForegroundColor Gray
$process = Start-Process -FilePath $csc -ArgumentList ($compileArgs -join " ") -NoNewWindow -Wait -PassThru

if ($process.ExitCode -ne 0) {
    throw "Compilation failed with exit code $($process.ExitCode)."
}

# Copy default settings.json to bin
Copy-Item "$PSScriptRoot\src\settings.json" "$OutputDir\settings.json" -Force
Copy-Item "$PSScriptRoot\assets\app.ico" "$OutputDir\app.ico" -Force

Write-Host "Build succeeded: $outputExe" -ForegroundColor Green
