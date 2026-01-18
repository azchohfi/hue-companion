<#
.SYNOPSIS
    Launches the Hue Companion app for interactive testing.

.DESCRIPTION
    Builds (optional) and launches the app. Unlike the screenshot capture script,
    this keeps the app running for human interaction and testing.

.PARAMETER Page
    Navigate to a specific page on launch.

.PARAMETER Name
    Human-readable name for rooms/zones/lights (used with Page).

.PARAMETER Id
    GUID for rooms/zones/lights (alternative to Name).

.PARAMETER SkipBuild
    Skip the build step - launch existing executable.

.EXAMPLE
    .\Launch-App.ps1
    # Build and launch to default page (dashboard)

.EXAMPLE
    .\Launch-App.ps1 -SkipBuild
    # Quick launch without building

.EXAMPLE
    .\Launch-App.ps1 -Page room -Name bathroom
    # Launch directly to bathroom room page

.EXAMPLE
    .\Launch-App.ps1 -Page settings -SkipBuild
    # Quick launch to settings page
#>

param(
    [string]$Page,
    [string]$Name,
    [string]$Id,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Project paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$CsprojPath = Join-Path $ProjectRoot "src\HueWindows\HueWindows.csproj"
$ExePath = Join-Path $ProjectRoot "src\HueWindows\bin\x64\Debug\net8.0-windows10.0.22621.0\HueWindows.exe"

Write-Host ""
Write-Host "=== Hue Companion Launch ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build (unless skipped)
if (-not $SkipBuild) {
    Write-Host "Building app..." -ForegroundColor Cyan
    $buildOutput = & dotnet build $CsprojPath -p:Platform=x64 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed:`n$buildOutput"
        exit 1
    }
    Write-Host "Build successful." -ForegroundColor Green
    Write-Host ""
}

# Verify exe exists
if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found at: $ExePath`nPlease build the project first."
    exit 1
}

# Build command-line arguments
$appArgs = @()

if ($Page) {
    $appArgs += "--page"
    $appArgs += $Page

    if ($Name) {
        $appArgs += "--name"
        $appArgs += "`"$Name`""
    }
    elseif ($Id) {
        $appArgs += "--id"
        $appArgs += $Id
    }
}

# Launch app
Write-Host "Launching app..." -ForegroundColor Cyan
if ($appArgs.Count -gt 0) {
    Write-Host "  Arguments: $($appArgs -join ' ')" -ForegroundColor Gray
}

$argString = $appArgs -join ' '
if ($argString) {
    Start-Process -FilePath $ExePath -ArgumentList $argString
}
else {
    Start-Process -FilePath $ExePath
}

Write-Host ""
Write-Host "App launched successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Quick launch examples:" -ForegroundColor Cyan
Write-Host "  .\Launch-App.ps1 -SkipBuild                  # Quick launch" -ForegroundColor Gray
Write-Host "  .\Launch-App.ps1 -Page dashboard -SkipBuild  # Go to dashboard" -ForegroundColor Gray
Write-Host "  .\Launch-App.ps1 -Page room -Name office     # Go to specific room" -ForegroundColor Gray
Write-Host ""

exit 0
