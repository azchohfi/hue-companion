<#
.SYNOPSIS
    Hue Companion build and launch CLI.

.DESCRIPTION
    Manages dev and stable builds of Hue Companion.

.EXAMPLE
    hue dev          # Build and launch dev version
    hue stable       # Launch existing stable build
    hue cut          # Cut a new stable build
    hue list         # Show running instances
    hue kill dev     # Kill dev instances
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet('dev', 'stable', 'cut', 'list', 'kill', 'help')]
    [string]$Command = 'help',

    [Parameter(Position = 1)]
    [string]$Target,

    [switch]$NoLaunch,
    [switch]$Restart,
    [string]$Message,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$CsprojPath = Join-Path $ProjectRoot "src\HueWindows\HueWindows.csproj"
$DevExePath = Join-Path $ProjectRoot "src\HueWindows\bin\x64\Debug\net8.0-windows10.0.22621.0\HueWindows.exe"
$StablePath = Join-Path $ProjectRoot "builds\stable"
$StableExePath = Join-Path $StablePath "HueWindows.exe"
$BuildInfoPath = Join-Path $StablePath "build-info.json"

function Get-GitShortHash {
    $hash = git -C $ProjectRoot rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -eq 0) { return $hash }
    return "unknown"
}

function Get-GitDirty {
    $status = git -C $ProjectRoot status --porcelain 2>$null
    return [bool]$status
}

function Get-BaseVersion {
    [xml]$csproj = Get-Content $CsprojPath
    $version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $version) { $version = "0.0.0" }
    return $version
}

function Get-HueProcesses {
    $processes = Get-Process -Name "HueWindows" -ErrorAction SilentlyContinue
    $results = @()
    foreach ($proc in $processes) {
        $path = $proc.Path
        $type = if ($path -like "*builds\stable*") { "stable" } else { "dev" }
        $results += [PSCustomObject]@{
            Id = $proc.Id
            Type = $type
            Path = $path
            StartTime = $proc.StartTime
        }
    }
    return $results
}

function Invoke-Dev {
    Write-Host ""
    Write-Host "=== Hue Dev Build ===" -ForegroundColor Cyan
    Write-Host ""

    if ($Restart) {
        $devProcs = Get-HueProcesses | Where-Object { $_.Type -eq "dev" }
        if ($devProcs) {
            Write-Host "Killing existing dev instances..." -ForegroundColor Yellow
            $devProcs | ForEach-Object { Stop-Process -Id $_.Id -Force }
            Start-Sleep -Milliseconds 500
        }
    }

    $hash = Get-GitShortHash
    $versionSuffix = "dev+$hash"
    $baseVersion = Get-BaseVersion

    Write-Host "Building dev version: $baseVersion-$versionSuffix" -ForegroundColor Cyan

    $buildArgs = @(
        "build", $CsprojPath,
        "-p:Platform=x64",
        "-p:DevBuild=true",
        "-p:VersionSuffix=$versionSuffix"
    )

    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }

    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green

    if (-not $NoLaunch) {
        Write-Host "Launching..." -ForegroundColor Cyan
        Start-Process -FilePath $DevExePath
        Write-Host "Dev instance launched." -ForegroundColor Green
    }

    Write-Host ""
}

function Invoke-Stable {
    Write-Host ""
    Write-Host "=== Hue Stable ===" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Path $StableExePath)) {
        Write-Error "No stable build exists. Run 'hue cut' first."
        exit 1
    }

    if (Test-Path $BuildInfoPath) {
        $info = Get-Content $BuildInfoPath | ConvertFrom-Json
        Write-Host "Version: $($info.version)" -ForegroundColor Gray
        Write-Host "Cut at:  $($info.cutAt)" -ForegroundColor Gray
        if ($info.message) {
            Write-Host "Note:    $($info.message)" -ForegroundColor Gray
        }
        Write-Host ""
    }

    Write-Host "Launching stable..." -ForegroundColor Cyan
    Start-Process -FilePath $StableExePath
    Write-Host "Stable instance launched." -ForegroundColor Green
    Write-Host ""
}

function Invoke-Cut {
    Write-Host ""
    Write-Host "=== Cut Stable Build ===" -ForegroundColor Cyan
    Write-Host ""

    # Check for dirty state
    if ((Get-GitDirty) -and -not $Force) {
        Write-Host "Warning: Working directory has uncommitted changes." -ForegroundColor Yellow
        $response = Read-Host "Cut anyway? [y/N]"
        if ($response -ne 'y' -and $response -ne 'Y') {
            Write-Host "Aborted." -ForegroundColor Yellow
            exit 0
        }
    }

    $hash = Get-GitShortHash
    $date = Get-Date -Format "yyyyMMdd"
    $versionSuffix = "stable.$date"
    $baseVersion = Get-BaseVersion
    $fullVersion = "$baseVersion-$versionSuffix"

    Write-Host "Cutting stable version: $fullVersion" -ForegroundColor Cyan

    # Create output directory
    if (-not (Test-Path $StablePath)) {
        New-Item -ItemType Directory -Path $StablePath -Force | Out-Null
    }

    # Build self-contained publish
    $publishArgs = @(
        "publish", $CsprojPath,
        "-p:Platform=x64",
        "-p:VersionSuffix=$versionSuffix",
        "-c", "Release",
        "-o", $StablePath,
        "--self-contained", "true"
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }

    # Write build info
    $buildInfo = @{
        version = $fullVersion
        cutAt = (Get-Date -Format "o")
        gitHash = $hash
        message = $Message
    }
    $buildInfo | ConvertTo-Json | Set-Content $BuildInfoPath

    Write-Host ""
    Write-Host "Stable build cut successfully!" -ForegroundColor Green
    Write-Host "Version: $fullVersion" -ForegroundColor Gray
    Write-Host "Path:    $StablePath" -ForegroundColor Gray
    Write-Host ""
}

function Invoke-List {
    Write-Host ""
    Write-Host "=== Running Hue Instances ===" -ForegroundColor Cyan
    Write-Host ""

    $procs = Get-HueProcesses
    if (-not $procs) {
        Write-Host "No running instances." -ForegroundColor Gray
    }
    else {
        foreach ($proc in $procs) {
            $typeColor = if ($proc.Type -eq "dev") { "Yellow" } else { "Green" }
            Write-Host "[$($proc.Type.ToUpper().PadRight(6))]" -ForegroundColor $typeColor -NoNewline
            Write-Host " PID $($proc.Id) - Started $($proc.StartTime.ToString('HH:mm:ss'))" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

function Invoke-Kill {
    param([string]$KillTarget)

    Write-Host ""
    Write-Host "=== Kill Hue Instances ===" -ForegroundColor Cyan
    Write-Host ""

    $procs = Get-HueProcesses
    if ($KillTarget) {
        $procs = $procs | Where-Object { $_.Type -eq $KillTarget }
    }

    if (-not $procs) {
        Write-Host "No matching instances to kill." -ForegroundColor Gray
    }
    else {
        foreach ($proc in $procs) {
            Write-Host "Killing $($proc.Type) instance (PID $($proc.Id))..." -ForegroundColor Yellow
            Stop-Process -Id $proc.Id -Force
        }
        Write-Host "Done." -ForegroundColor Green
    }
    Write-Host ""
}

function Show-Help {
    Write-Host ""
    Write-Host "Hue Companion CLI" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: hue <command> [options]" -ForegroundColor White
    Write-Host ""
    Write-Host "Commands:" -ForegroundColor White
    Write-Host "  dev              Build and launch dev version" -ForegroundColor Gray
    Write-Host "    --no-launch    Build without launching" -ForegroundColor DarkGray
    Write-Host "    --restart      Kill existing dev instances first" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  stable           Launch existing stable build" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  cut              Build current code as new stable" -ForegroundColor Gray
    Write-Host "    --message      Add note about this build" -ForegroundColor DarkGray
    Write-Host "    --force        Skip uncommitted changes warning" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  list             Show running Hue instances" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  kill [dev|stable] Kill running instances" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor White
    Write-Host "  hue dev                     # Build and launch dev" -ForegroundColor DarkGray
    Write-Host "  hue dev --restart           # Kill old dev, build, launch" -ForegroundColor DarkGray
    Write-Host "  hue cut --message 'v1 done' # Cut stable with note" -ForegroundColor DarkGray
    Write-Host "  hue stable                  # Launch stable" -ForegroundColor DarkGray
    Write-Host "  hue kill dev                # Kill dev instances only" -ForegroundColor DarkGray
    Write-Host ""
}

# Main dispatch
switch ($Command) {
    'dev'    { Invoke-Dev }
    'stable' { Invoke-Stable }
    'cut'    { Invoke-Cut }
    'list'   { Invoke-List }
    'kill'   { Invoke-Kill -KillTarget $Target }
    'help'   { Show-Help }
    default  { Show-Help }
}
