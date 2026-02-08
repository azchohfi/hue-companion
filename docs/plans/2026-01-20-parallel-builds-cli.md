# Parallel Builds CLI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable running dev and stable builds side-by-side with a simple CLI (`hue dev`, `hue cut`, `hue stable`).

**Architecture:** MSBuild properties control app identity (package name, display name). CLI scripts manage building with correct properties and launching from correct paths. Version displayed in title bar.

**Tech Stack:** PowerShell CLI, MSBuild properties, WinUI 3 MSIX packaging

---

## Task 1: Add Build Configuration Properties

**Files:**
- Modify: `src/HueWindows/HueWindows.csproj`

**Step 1: Add conditional properties for dev vs stable builds**

Add after line 21 (`<Version>0.1.0</Version>`):

```xml
  <!-- Build type configuration -->
  <PropertyGroup>
    <BaseVersion>0.1.0</BaseVersion>
    <ApplicationTitle Condition="'$(DevBuild)' == 'true'">Hue Companion (Dev)</ApplicationTitle>
    <ApplicationTitle Condition="'$(DevBuild)' != 'true'">Hue Companion</ApplicationTitle>
    <InformationalVersion Condition="'$(VersionSuffix)' != ''">$(BaseVersion)-$(VersionSuffix)</InformationalVersion>
    <InformationalVersion Condition="'$(VersionSuffix)' == ''">$(BaseVersion)</InformationalVersion>
  </PropertyGroup>

  <!-- MSIX identity for dev builds (allows running alongside stable) -->
  <PropertyGroup Condition="'$(DevBuild)' == 'true'">
    <PackageId>HueWindows.Dev</PackageId>
  </PropertyGroup>
```

**Step 2: Build to verify no errors**

Run: `dotnet build src/HueWindows/HueWindows.csproj -p:Platform=x64`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/HueWindows/HueWindows.csproj
git commit -m "feat: add build configuration for dev vs stable"
```

---

## Task 2: Create Dev Package Manifest

**Files:**
- Create: `src/HueWindows/Package.Dev.appxmanifest`
- Modify: `src/HueWindows/HueWindows.csproj`

**Step 1: Create dev manifest with different identity**

Create `src/HueWindows/Package.Dev.appxmanifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">

  <Identity
    Name="HueWindows.Dev"
    Publisher="CN=HueWindows"
    Version="0.0.1.0" />

  <mp:PhoneIdentity PhoneProductId="B2C3D4E5-F6A7-8901-BCDE-F12345678901" PhonePublisherId="00000000-0000-0000-0000-000000000000"/>

  <Properties>
    <DisplayName>Hue Companion (Dev)</DisplayName>
    <PublisherDisplayName>Hue Companion</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-us"/>
  </Resources>

  <Applications>
    <Application Id="App"
      Executable="$targetnametoken$.exe"
      EntryPoint="$targetentrypoint$">
      <uap:VisualElements
        DisplayName="Hue Companion (Dev)"
        Description="Control your Philips Hue lights from Windows (Development Build)"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" Square71x71Logo="Assets\SmallTile.png">
          <uap:ShowNameOnTiles>
            <uap:ShowOn Tile="square150x150Logo"/>
            <uap:ShowOn Tile="wide310x150Logo"/>
          </uap:ShowNameOnTiles>
        </uap:DefaultTile>
        <uap:SplashScreen Image="Assets\SplashScreen.png"/>
      </uap:VisualElements>
    </Application>
  </Applications>

  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="privateNetworkClientServer" />
  </Capabilities>
</Package>
```

**Step 2: Add manifest selection to csproj**

Add after the dev build PropertyGroup in `HueWindows.csproj`:

```xml
  <!-- Select manifest based on build type -->
  <PropertyGroup Condition="'$(DevBuild)' == 'true'">
    <AppxManifest>Package.Dev.appxmanifest</AppxManifest>
  </PropertyGroup>
```

**Step 3: Build dev version to verify**

Run: `dotnet build src/HueWindows/HueWindows.csproj -p:Platform=x64 -p:DevBuild=true`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/HueWindows/Package.Dev.appxmanifest src/HueWindows/HueWindows.csproj
git commit -m "feat: add separate MSIX identity for dev builds"
```

---

## Task 3: Display Version in Title Bar

**Files:**
- Modify: `src/HueWindows/MainWindow.xaml`
- Modify: `src/HueWindows/MainWindow.xaml.cs`

**Step 1: Make title bar text dynamic**

In `MainWindow.xaml`, change line 50-52 from:

```xml
            <!-- Title -->
            <TextBlock Grid.Column="3" Text="Hue Companion"
                       Style="{StaticResource CaptionTextBlockStyle}"
                       VerticalAlignment="Center"/>
```

to:

```xml
            <!-- Title -->
            <TextBlock x:Name="TitleTextBlock" Grid.Column="3"
                       Style="{StaticResource CaptionTextBlockStyle}"
                       VerticalAlignment="Center"/>
```

**Step 2: Set title at runtime from assembly info**

In `MainWindow.xaml.cs`, add a helper method after `CleanupServices()` (around line 281):

```csharp
    private string GetAppTitle()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var titleAttr = assembly.GetCustomAttribute<System.Reflection.AssemblyTitleAttribute>();
        var infoVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>();

        var title = titleAttr?.Title ?? "Hue Companion";
        var version = infoVersion?.InformationalVersion ?? "0.0.0";

        return $"{title} - {version}";
    }
```

**Step 3: Call the helper in constructor**

In `MainWindow.xaml.cs`, add after `SetupTitleBar();` (around line 87):

```csharp
        // Set dynamic title with version
        var appTitle = GetAppTitle();
        this.Title = appTitle;
        TitleTextBlock.Text = appTitle;
```

**Step 4: Add assembly title attribute to csproj**

In `HueWindows.csproj`, add to the build configuration PropertyGroup:

```xml
    <AssemblyTitle>$(ApplicationTitle)</AssemblyTitle>
```

**Step 5: Build and verify title shows**

Run: `dotnet build src/HueWindows/HueWindows.csproj -p:Platform=x64 -p:DevBuild=true -p:VersionSuffix=dev+abc123`
Launch the exe and verify title shows "Hue Companion (Dev) - 0.1.0-dev+abc123"

**Step 6: Commit**

```bash
git add src/HueWindows/MainWindow.xaml src/HueWindows/MainWindow.xaml.cs src/HueWindows/HueWindows.csproj
git commit -m "feat: display version and build type in title bar"
```

---

## Task 4: Create CLI Entry Point

**Files:**
- Create: `hue.cmd`

**Step 1: Create batch wrapper**

Create `hue.cmd` in repo root:

```batch
@echo off
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0tools\hue-cli.ps1" %*
```

**Step 2: Commit**

```bash
git add hue.cmd
git commit -m "feat: add hue.cmd CLI entry point"
```

---

## Task 5: Create CLI Implementation

**Files:**
- Create: `tools/hue-cli.ps1`

**Step 1: Create the CLI script**

Create `tools/hue-cli.ps1`:

```powershell
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
```

**Step 2: Test the CLI help**

Run: `.\hue.cmd help`
Expected: Shows help text

**Step 3: Commit**

```bash
git add tools/hue-cli.ps1
git commit -m "feat: add hue-cli.ps1 implementation"
```

---

## Task 6: Update Gitignore

**Files:**
- Modify: `.gitignore`

**Step 1: Add builds directory**

Add to `.gitignore`:

```
# Local stable builds
builds/
```

**Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: ignore builds directory"
```

---

## Task 7: End-to-End Verification

**Step 1: Test dev build**

Run: `.\hue.cmd dev`
Expected:
- Builds successfully
- Launches with title "Hue Companion (Dev) - 0.1.0-dev+<hash>"

**Step 2: Test cut stable**

Run: `.\hue.cmd cut --message "Initial stable"`
Expected:
- Creates `builds/stable/` directory
- Creates `builds/stable/build-info.json`
- Shows success message

**Step 3: Test launch stable**

Run: `.\hue.cmd stable`
Expected:
- Launches with title "Hue Companion - 0.1.0-stable.YYYYMMDD"
- Different MSIX identity (can run alongside dev)

**Step 4: Test list**

Run: `.\hue.cmd list`
Expected: Shows both running instances with type labels

**Step 5: Test kill**

Run: `.\hue.cmd kill dev`
Expected: Kills only dev instance, stable keeps running

**Step 6: Final commit**

```bash
git add -A
git commit -m "feat: complete parallel builds CLI

Adds hue.cmd CLI for managing dev and stable builds:
- hue dev: Build and launch dev version
- hue stable: Launch existing stable
- hue cut: Cut new stable checkpoint
- hue list: Show running instances
- hue kill: Kill instances by type

Dev and stable builds have separate MSIX identities and can
run simultaneously. Version shows in title bar."
```

---

## Summary

| Task | Description |
|------|-------------|
| 1 | Add MSBuild properties for dev/stable configuration |
| 2 | Create separate MSIX manifest for dev builds |
| 3 | Display version in title bar dynamically |
| 4 | Create hue.cmd entry point |
| 5 | Implement hue-cli.ps1 with all commands |
| 6 | Update gitignore for builds directory |
| 7 | End-to-end verification |
