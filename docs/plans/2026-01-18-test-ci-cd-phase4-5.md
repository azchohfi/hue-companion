# Test & CI/CD System - Phase 4-5: UI Testing & Visual Regression

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement automated UI screenshot testing and visual regression detection using existing command-line navigation and screenshot capture infrastructure.

**Architecture:** The app already supports `--page` navigation and `--screenshot` mode. We'll create a test harness that navigates to key pages, captures screenshots, and compares them against baselines. CI workflow captures screenshots on every build; visual diffs flag unintended UI changes.

**Tech Stack:** PowerShell (test harness), ImageMagick (image comparison), GitHub Actions (CI), existing MCP screenshot tools

**Reference:** [GitHub Issue #5](https://github.com/ddrayne/hue-companion/issues/5) - Phase 4-5

---

## Task 1: Create UI Test Harness Script

**Files:**
- Create: `tools/Run-UITests.ps1`

**Step 1: Create the UI test harness script**

Create `tools/Run-UITests.ps1`:

```powershell
<#
.SYNOPSIS
    Runs automated UI tests by navigating to key pages and capturing screenshots.

.DESCRIPTION
    Launches the app with command-line navigation to each testable page,
    captures screenshots, and stores them in the test-screenshots folder.

.PARAMETER SkipBuild
    Skip the build step - use existing executable.

.PARAMETER OutputDir
    Directory to save test screenshots (default: ./test-screenshots).

.PARAMETER Pages
    Array of pages to test (default: dashboard, settings).

.EXAMPLE
    .\Run-UITests.ps1
    # Build and test all default pages

.EXAMPLE
    .\Run-UITests.ps1 -SkipBuild -Pages dashboard,settings
    # Quick test specific pages
#>

param(
    [switch]$SkipBuild,
    [string]$OutputDir = "$PSScriptRoot\..\test-screenshots",
    [string[]]$Pages = @("dashboard", "settings")
)

$ErrorActionPreference = "Stop"

# Project paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$CsprojPath = Join-Path $ProjectRoot "src\HueCompanion\HueCompanion.csproj"
$ExePath = Join-Path $ProjectRoot "src\HueCompanion\bin\x64\Debug\net8.0-windows10.0.22621.0\HueCompanion.exe"

# Test configuration
$WaitSeconds = 5
$TestResults = @()

function Write-TestStatus {
    param([string]$Page, [string]$Status, [string]$Message = "")

    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        default { "White" }
    }

    Write-Host "[$Status] " -NoNewline -ForegroundColor $color
    Write-Host "$Page" -NoNewline
    if ($Message) { Write-Host " - $Message" -ForegroundColor Gray }
    else { Write-Host "" }
}

# Step 1: Build (unless skipped)
Write-Host ""
Write-Host "=== HueCompanion UI Tests ===" -ForegroundColor Cyan
Write-Host ""

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

# Verify executable exists
if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found at: $ExePath"
    exit 1
}

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$OutputDir = Resolve-Path $OutputDir

# Step 2: Run tests for each page
Write-Host "Testing pages: $($Pages -join ', ')" -ForegroundColor Cyan
Write-Host ""

foreach ($page in $Pages) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $screenshotPath = Join-Path $OutputDir "$page-$timestamp.png"

    try {
        # Launch app with navigation and screenshot mode
        $args = "--page $page --screenshot --delay $($WaitSeconds * 1000)"

        $process = Start-Process -FilePath $ExePath -ArgumentList $args -PassThru -WindowStyle Normal

        # Wait for app to capture and close
        $timeout = $WaitSeconds + 10
        $exited = $process.WaitForExit($timeout * 1000)

        if (-not $exited) {
            $process | Stop-Process -Force
            throw "App did not exit within timeout"
        }

        # Check if screenshot was created (app saves to screenshots folder)
        $screenshotsDir = Join-Path $ProjectRoot "screenshots"
        $latestScreenshot = Get-ChildItem $screenshotsDir -Filter "*.png" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($latestScreenshot -and $latestScreenshot.LastWriteTime -gt (Get-Date).AddSeconds(-$timeout)) {
            # Move screenshot to test output with page name
            Copy-Item $latestScreenshot.FullName $screenshotPath
            Write-TestStatus $page "PASS" "Screenshot saved"
            $TestResults += @{ Page = $page; Status = "PASS"; Path = $screenshotPath }
        }
        else {
            throw "No screenshot captured"
        }
    }
    catch {
        Write-TestStatus $page "FAIL" $_.Exception.Message
        $TestResults += @{ Page = $page; Status = "FAIL"; Error = $_.Exception.Message }
    }

    # Brief pause between tests
    Start-Sleep -Seconds 1
}

# Step 3: Summary
Write-Host ""
Write-Host "=== Test Results ===" -ForegroundColor Cyan

$passed = ($TestResults | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($TestResults | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })
Write-Host ""
Write-Host "Screenshots saved to: $OutputDir" -ForegroundColor Gray

# Exit with failure if any tests failed
if ($failed -gt 0) {
    exit 1
}

exit 0
```

**Step 2: Test the script locally**

Run:
```powershell
.\tools\Run-UITests.ps1 -SkipBuild
```

Expected: Dashboard and settings pages captured, screenshots saved to `test-screenshots/`

**Step 3: Commit**

```bash
git add tools/Run-UITests.ps1
git commit -m "feat: add UI test harness for screenshot-based testing"
```

---

## Task 2: Add Visual Regression Comparison Script

**Files:**
- Create: `tools/Compare-Screenshots.ps1`
- Create: `tests/visual-baselines/.gitkeep`

**Step 1: Create the comparison script**

Create `tools/Compare-Screenshots.ps1`:

```powershell
<#
.SYNOPSIS
    Compares test screenshots against baseline images.

.DESCRIPTION
    Uses pixel comparison to detect visual differences between
    current screenshots and stored baselines. Generates diff images
    and a report of changes.

.PARAMETER TestDir
    Directory containing test screenshots.

.PARAMETER BaselineDir
    Directory containing baseline screenshots.

.PARAMETER DiffDir
    Directory to save diff images (default: ./visual-diffs).

.PARAMETER Threshold
    Percentage difference threshold for failure (default: 1.0).

.PARAMETER UpdateBaselines
    Update baseline images with current test screenshots.

.EXAMPLE
    .\Compare-Screenshots.ps1
    # Compare test-screenshots against baselines

.EXAMPLE
    .\Compare-Screenshots.ps1 -UpdateBaselines
    # Update baselines with current screenshots
#>

param(
    [string]$TestDir = "$PSScriptRoot\..\test-screenshots",
    [string]$BaselineDir = "$PSScriptRoot\..\tests\visual-baselines",
    [string]$DiffDir = "$PSScriptRoot\..\visual-diffs",
    [double]$Threshold = 1.0,
    [switch]$UpdateBaselines
)

$ErrorActionPreference = "Stop"

# Add required assemblies
Add-Type -AssemblyName System.Drawing

function Compare-Images {
    param(
        [string]$Image1Path,
        [string]$Image2Path,
        [string]$DiffPath
    )

    $img1 = [System.Drawing.Image]::FromFile($Image1Path)
    $img2 = [System.Drawing.Image]::FromFile($Image2Path)

    # Check dimensions match
    if ($img1.Width -ne $img2.Width -or $img1.Height -ne $img2.Height) {
        $img1.Dispose()
        $img2.Dispose()
        return @{
            Match = $false
            DifferencePercent = 100.0
            Message = "Dimension mismatch: $($img1.Width)x$($img1.Height) vs $($img2.Width)x$($img2.Height)"
        }
    }

    $bmp1 = New-Object System.Drawing.Bitmap($img1)
    $bmp2 = New-Object System.Drawing.Bitmap($img2)
    $diffBmp = New-Object System.Drawing.Bitmap($img1.Width, $img1.Height)

    $totalPixels = $img1.Width * $img1.Height
    $differentPixels = 0

    for ($x = 0; $x -lt $img1.Width; $x++) {
        for ($y = 0; $y -lt $img1.Height; $y++) {
            $pixel1 = $bmp1.GetPixel($x, $y)
            $pixel2 = $bmp2.GetPixel($x, $y)

            if ($pixel1.ToArgb() -ne $pixel2.ToArgb()) {
                $differentPixels++
                # Mark difference in red
                $diffBmp.SetPixel($x, $y, [System.Drawing.Color]::Red)
            }
            else {
                # Keep original pixel (dimmed)
                $gray = [int](($pixel1.R + $pixel1.G + $pixel1.B) / 3 * 0.3)
                $diffBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($gray, $gray, $gray))
            }
        }
    }

    $differencePercent = ($differentPixels / $totalPixels) * 100

    # Save diff image if there are differences
    if ($differentPixels -gt 0 -and $DiffPath) {
        $diffBmp.Save($DiffPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $img1.Dispose()
    $img2.Dispose()
    $bmp1.Dispose()
    $bmp2.Dispose()
    $diffBmp.Dispose()

    return @{
        Match = $differencePercent -le $Threshold
        DifferencePercent = [math]::Round($differencePercent, 2)
        DifferentPixels = $differentPixels
        TotalPixels = $totalPixels
    }
}

# Create directories if needed
if (-not (Test-Path $BaselineDir)) {
    New-Item -ItemType Directory -Path $BaselineDir -Force | Out-Null
}
if (-not (Test-Path $DiffDir)) {
    New-Item -ItemType Directory -Path $DiffDir -Force | Out-Null
}

Write-Host ""
Write-Host "=== Visual Regression Testing ===" -ForegroundColor Cyan
Write-Host ""

# Get test screenshots
$testScreenshots = Get-ChildItem $TestDir -Filter "*.png" -ErrorAction SilentlyContinue

if (-not $testScreenshots) {
    Write-Host "No test screenshots found in: $TestDir" -ForegroundColor Yellow
    Write-Host "Run .\tools\Run-UITests.ps1 first to capture screenshots." -ForegroundColor Gray
    exit 1
}

# Update baselines mode
if ($UpdateBaselines) {
    Write-Host "Updating baselines..." -ForegroundColor Yellow

    foreach ($screenshot in $testScreenshots) {
        # Extract page name from filename (e.g., "dashboard-20260118-120000.png" -> "dashboard")
        $pageName = $screenshot.Name -replace '-\d{8}-\d{6}\.png$', ''
        $baselinePath = Join-Path $BaselineDir "$pageName.png"

        Copy-Item $screenshot.FullName $baselinePath -Force
        Write-Host "  Updated: $pageName.png" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Baselines updated. Commit these to the repository." -ForegroundColor Cyan
    exit 0
}

# Compare mode
$results = @()
$failures = 0

foreach ($screenshot in $testScreenshots) {
    # Extract page name
    $pageName = $screenshot.Name -replace '-\d{8}-\d{6}\.png$', ''
    $baselinePath = Join-Path $BaselineDir "$pageName.png"
    $diffPath = Join-Path $DiffDir "$pageName-diff.png"

    if (-not (Test-Path $baselinePath)) {
        Write-Host "[$pageName] " -NoNewline
        Write-Host "SKIP" -ForegroundColor Yellow -NoNewline
        Write-Host " - No baseline found" -ForegroundColor Gray
        $results += @{ Page = $pageName; Status = "SKIP"; Message = "No baseline" }
        continue
    }

    $comparison = Compare-Images -Image1Path $screenshot.FullName -Image2Path $baselinePath -DiffPath $diffPath

    if ($comparison.Match) {
        Write-Host "[$pageName] " -NoNewline
        Write-Host "PASS" -ForegroundColor Green -NoNewline
        Write-Host " - $($comparison.DifferencePercent)% difference" -ForegroundColor Gray
        $results += @{ Page = $pageName; Status = "PASS"; Diff = $comparison.DifferencePercent }

        # Clean up diff image if passed
        if (Test-Path $diffPath) { Remove-Item $diffPath }
    }
    else {
        Write-Host "[$pageName] " -NoNewline
        Write-Host "FAIL" -ForegroundColor Red -NoNewline
        Write-Host " - $($comparison.DifferencePercent)% difference" -ForegroundColor Gray
        if ($comparison.Message) {
            Write-Host "         $($comparison.Message)" -ForegroundColor Gray
        }
        $results += @{ Page = $pageName; Status = "FAIL"; Diff = $comparison.DifferencePercent }
        $failures++
    }
}

# Summary
Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan

$passed = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$skipped = ($results | Where-Object { $_.Status -eq "SKIP" }).Count
$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Skipped: $skipped" -ForegroundColor Yellow
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })

if ($failed -gt 0) {
    Write-Host ""
    Write-Host "Diff images saved to: $DiffDir" -ForegroundColor Gray
    Write-Host "Review changes and run with -UpdateBaselines if intentional." -ForegroundColor Yellow
    exit 1
}

exit 0
```

**Step 2: Create baseline directory**

```bash
mkdir -p tests/visual-baselines
touch tests/visual-baselines/.gitkeep
```

**Step 3: Commit**

```bash
git add tools/Compare-Screenshots.ps1 tests/visual-baselines/.gitkeep
git commit -m "feat: add visual regression comparison script"
```

---

## Task 3: Create Initial Baseline Screenshots

**Step 1: Capture baseline screenshots**

Run:
```powershell
.\tools\Run-UITests.ps1
```

**Step 2: Update baselines from captured screenshots**

Run:
```powershell
.\tools\Compare-Screenshots.ps1 -UpdateBaselines
```

Expected: Baseline images created in `tests/visual-baselines/`

**Step 3: Verify baselines exist**

```bash
ls tests/visual-baselines/
```

Expected: `dashboard.png`, `settings.png`

**Step 4: Commit baselines**

```bash
git add tests/visual-baselines/*.png
git commit -m "chore: add initial visual baseline screenshots"
```

---

## Task 4: Add UI Tests to CI Workflow

**Files:**
- Modify: `.github/workflows/main-ci.yml`

**Step 1: Add UI test job to main CI workflow**

Add a new job after `build-and-test`:

```yaml
  ui-tests:
    runs-on: windows-latest
    needs: build-and-test

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build for UI tests
      run: dotnet build src/HueCompanion/HueCompanion.csproj -p:Platform=x64

    - name: Run UI tests
      run: |
        powershell -ExecutionPolicy Bypass -File ./tools/Run-UITests.ps1 -SkipBuild
      continue-on-error: true

    - name: Run visual regression tests
      run: |
        powershell -ExecutionPolicy Bypass -File ./tools/Compare-Screenshots.ps1

    - name: Upload test screenshots
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: ui-test-screenshots
        path: test-screenshots/
        retention-days: 7

    - name: Upload visual diffs
      uses: actions/upload-artifact@v4
      if: failure()
      with:
        name: visual-diffs
        path: visual-diffs/
        retention-days: 7
```

**Step 2: Commit workflow update**

```bash
git add .github/workflows/main-ci.yml
git commit -m "ci: add UI and visual regression tests to main CI workflow"
```

---

## Task 5: Add Visual Regression to PR Workflow

**Files:**
- Modify: `.github/workflows/pr-validation.yml`

**Step 1: Add visual regression job to PR validation**

Add after the existing test job:

```yaml
  visual-regression:
    runs-on: windows-latest
    needs: build-and-test

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restore and build
      run: |
        dotnet restore
        dotnet build src/HueCompanion/HueCompanion.csproj -p:Platform=x64

    - name: Capture UI screenshots
      run: |
        powershell -ExecutionPolicy Bypass -File ./tools/Run-UITests.ps1 -SkipBuild
      continue-on-error: true

    - name: Compare against baselines
      id: visual-compare
      run: |
        powershell -ExecutionPolicy Bypass -File ./tools/Compare-Screenshots.ps1
      continue-on-error: true

    - name: Upload screenshots for review
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: pr-screenshots
        path: |
          test-screenshots/
          visual-diffs/
        retention-days: 7

    - name: Comment on PR with visual diff status
      if: steps.visual-compare.outcome == 'failure'
      uses: actions/github-script@v7
      with:
        script: |
          github.rest.issues.createComment({
            issue_number: context.issue.number,
            owner: context.repo.owner,
            repo: context.repo.repo,
            body: '⚠️ **Visual Regression Detected**\n\nUI screenshots differ from baselines. Please review the `visual-diffs` artifact.\n\nIf changes are intentional, update baselines with:\n```powershell\n.\\tools\\Compare-Screenshots.ps1 -UpdateBaselines\n```'
          })
```

**Step 2: Commit workflow update**

```bash
git add .github/workflows/pr-validation.yml
git commit -m "ci: add visual regression check to PR validation"
```

---

## Task 6: Update Documentation

**Files:**
- Modify: `docs/DEVELOPMENT.md`

**Step 1: Add UI Testing section**

Add after the Testing section:

```markdown
## 8. UI & Visual Regression Testing

### Running UI Tests Locally

```bash
# Full build + capture all test pages
.\tools\Run-UITests.ps1

# Quick capture (skip build)
.\tools\Run-UITests.ps1 -SkipBuild

# Test specific pages
.\tools\Run-UITests.ps1 -SkipBuild -Pages dashboard,settings
```

Screenshots are saved to `test-screenshots/`.

### Visual Regression Testing

```bash
# Compare current screenshots against baselines
.\tools\Compare-Screenshots.ps1

# Update baselines (after reviewing changes)
.\tools\Compare-Screenshots.ps1 -UpdateBaselines
```

**Threshold:** 1% pixel difference allowed by default.

### Baseline Management

Baseline screenshots are stored in `tests/visual-baselines/`. When UI changes are intentional:

1. Run UI tests: `.\tools\Run-UITests.ps1`
2. Review screenshots in `test-screenshots/`
3. Update baselines: `.\tools\Compare-Screenshots.ps1 -UpdateBaselines`
4. Commit: `git add tests/visual-baselines/*.png`

### CI Integration

- **PR Validation:** Compares screenshots, comments on PR if differences detected
- **Main CI:** Captures screenshots and runs regression tests
- **Artifacts:** Screenshots and diff images uploaded for review
```

**Step 2: Commit documentation**

```bash
git add docs/DEVELOPMENT.md
git commit -m "docs: add UI and visual regression testing documentation"
```

---

## Task 7: Test the Complete Flow

**Step 1: Run UI tests locally**

```powershell
.\tools\Run-UITests.ps1
```

Expected: Screenshots captured for dashboard and settings pages.

**Step 2: Run visual regression (should pass on first run after setting baselines)**

```powershell
.\tools\Compare-Screenshots.ps1
```

Expected: All pages pass (or skip if no baselines yet).

**Step 3: Create baselines if needed**

```powershell
.\tools\Compare-Screenshots.ps1 -UpdateBaselines
```

**Step 4: Push and verify CI**

```bash
git push origin main
```

Check GitHub Actions for:
- UI test screenshots captured
- Visual regression tests run
- Artifacts uploaded

---

## Summary

After completing all tasks:

1. **UI Test Harness** (`tools/Run-UITests.ps1`)
   - Navigates to key pages via command-line args
   - Captures screenshots using app's `--screenshot` mode
   - Reports pass/fail status

2. **Visual Regression Tool** (`tools/Compare-Screenshots.ps1`)
   - Pixel-by-pixel comparison with threshold
   - Generates diff images for failures
   - `-UpdateBaselines` flag for intentional changes

3. **Baseline Screenshots** (`tests/visual-baselines/`)
   - Stored in repo for comparison
   - Updated when UI changes are intentional

4. **CI Integration**
   - PR workflow: Compares and comments on diffs
   - Main CI: Full test suite with artifact upload

**Testable Pages:**
- `dashboard` - Main room/zone cards view
- `settings` - App settings page

**Note:** Pages requiring IDs (room, zone, light) need a connected bridge with known IDs to test. These can be added later when test fixtures are available.

---

## Task 8: Add Human-Readable Deep Linking

**Files:**
- Modify: `src/HueCompanion/Helpers/CommandLineArgs.cs`
- Modify: `src/HueCompanion/Helpers/CommandLineParser.cs`
- Modify: `src/HueCompanion/Helpers/NavigationTarget.cs`
- Modify: `src/HueCompanion/MainWindow.xaml.cs`

**Goal:** Enable navigation by human-readable names (e.g., `--name bathroom` instead of `--id <guid>`).

**Step 1: Update CommandLineArgs to support name parameter**

Modify `src/HueCompanion/Helpers/CommandLineArgs.cs`:

```csharp
namespace HueCompanion.Helpers;

/// <summary>
/// Represents parsed command-line arguments for deep linking.
/// </summary>
public record CommandLineArgs
{
    /// <summary>
    /// The target page to navigate to (null for default behavior).
    /// </summary>
    public string? Page { get; init; }

    /// <summary>
    /// The ID parameter for pages that require it (room, zone, light).
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// The human-readable name for rooms/zones/lights (alternative to ID).
    /// Will be resolved to an ID at runtime by querying the bridge.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Whether screenshot mode is enabled (auto-close after navigation).
    /// </summary>
    public bool ScreenshotMode { get; init; }

    /// <summary>
    /// Delay in milliseconds before auto-close in screenshot mode. Default is 5000ms.
    /// </summary>
    public int ScreenshotDelayMs { get; init; } = 5000;

    /// <summary>
    /// Whether the arguments are valid.
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>
    /// Error message if arguments are invalid.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
```

**Step 2: Update CommandLineParser to parse --name**

Modify `src/HueCompanion/Helpers/CommandLineParser.cs` to add name parsing:

```csharp
// Add in the Parse method, after the --id parsing:
else if (arg.Equals("--name", StringComparison.OrdinalIgnoreCase) ||
         arg.Equals("-n", StringComparison.OrdinalIgnoreCase))
{
    if (i + 1 >= argsToProcess.Length)
    {
        return InvalidArgs("--name requires a value");
    }
    name = argsToProcess[++i];
}
```

And update the validation:
```csharp
// Validate ID or name requirement
if (page != null && PagesRequiringId.Contains(page) && !id.HasValue && string.IsNullOrEmpty(name))
{
    return InvalidArgs($"Page '{page}' requires --id or --name parameter");
}

return new CommandLineArgs
{
    Page = page,
    Id = id,
    Name = name,
    ScreenshotMode = screenshotMode,
    ScreenshotDelayMs = screenshotDelayMs,
    IsValid = true
};
```

**Step 3: Update NavigationTarget to resolve names**

Add async resolution in `src/HueCompanion/Helpers/NavigationTarget.cs`:

```csharp
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;
using HueCompanion.Views;

namespace HueCompanion.Helpers;

public record NavigationTarget
{
    public required Type PageType { get; init; }
    public object? Parameter { get; init; }

    public static NavigationTarget? FromCommandLineArgs(CommandLineArgs args)
    {
        // ... existing code for pages without ID requirements
    }

    /// <summary>
    /// Resolves command-line arguments to a navigation target, looking up names via bridge service.
    /// </summary>
    public static async Task<NavigationTarget?> ResolveAsync(CommandLineArgs args, IHueBridgeService bridgeService)
    {
        if (args.Page == null) return null;

        var page = args.Page.ToLowerInvariant();

        // For pages that don't require ID, use sync method
        if (!RequiresId(page))
        {
            return FromCommandLineArgs(args);
        }

        // Resolve ID from name if provided
        Guid? resolvedId = args.Id;

        if (!resolvedId.HasValue && !string.IsNullOrEmpty(args.Name))
        {
            resolvedId = await ResolveNameToIdAsync(page, args.Name, bridgeService);
            if (!resolvedId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"Could not resolve name '{args.Name}' for page '{page}'");
                return null;
            }
        }

        if (!resolvedId.HasValue) return null;

        return page switch
        {
            "room" => new NavigationTarget
            {
                PageType = typeof(RoomDetailPage),
                Parameter = new NavigationTag(LightGroupType.Room, resolvedId.Value)
            },
            "zone" => new NavigationTarget
            {
                PageType = typeof(RoomDetailPage),
                Parameter = new NavigationTag(LightGroupType.Zone, resolvedId.Value)
            },
            "light" => new NavigationTarget
            {
                PageType = typeof(LightDetailPage),
                Parameter = resolvedId.Value
            },
            _ => null
        };
    }

    private static bool RequiresId(string page) =>
        page is "room" or "zone" or "light";

    private static async Task<Guid?> ResolveNameToIdAsync(string page, string name, IHueBridgeService bridgeService)
    {
        var normalizedName = NormalizeName(name);

        return page switch
        {
            "room" => await FindRoomByNameAsync(normalizedName, bridgeService),
            "zone" => await FindZoneByNameAsync(normalizedName, bridgeService),
            "light" => await FindLightByNameAsync(normalizedName, bridgeService),
            _ => null
        };
    }

    private static string NormalizeName(string name) =>
        name.ToLowerInvariant().Replace("-", " ").Replace("_", " ").Trim();

    private static async Task<Guid?> FindRoomByNameAsync(string name, IHueBridgeService bridgeService)
    {
        var rooms = await bridgeService.GetRoomsAsync();
        var room = rooms.FirstOrDefault(r =>
            NormalizeName(r.Name ?? "") == name ||
            (r.Name ?? "").Equals(name, StringComparison.OrdinalIgnoreCase));
        return room?.Id;
    }

    private static async Task<Guid?> FindZoneByNameAsync(string name, IHueBridgeService bridgeService)
    {
        var zones = await bridgeService.GetZonesAsync();
        var zone = zones.FirstOrDefault(z =>
            NormalizeName(z.Name ?? "") == name ||
            (z.Name ?? "").Equals(name, StringComparison.OrdinalIgnoreCase));
        return zone?.Id;
    }

    private static async Task<Guid?> FindLightByNameAsync(string name, IHueBridgeService bridgeService)
    {
        // Get all lights from all rooms
        var rooms = await bridgeService.GetRoomsAsync();
        foreach (var room in rooms)
        {
            var light = room.Lights.FirstOrDefault(l =>
                NormalizeName(l.Name ?? "") == name ||
                (l.Name ?? "").Equals(name, StringComparison.OrdinalIgnoreCase));
            if (light != null) return light.Id;
        }
        return null;
    }
}
```

**Step 4: Update MainWindow to use async resolution**

Modify `src/HueCompanion/MainWindow.xaml.cs` in `NavigateToInitialPage`:

```csharp
private async void NavigateToInitialPage()
{
    var hasBridge = await _settingsService.HasConfiguredBridgeAsync();

    // Get navigation target from command-line args
    var cmdArgs = App.CommandLineArgs;
    NavigationTarget? navTarget = null;

    if (cmdArgs.IsValid && cmdArgs.Page != null)
    {
        // Use async resolution if name is provided
        if (!string.IsNullOrEmpty(cmdArgs.Name))
        {
            navTarget = await NavigationTarget.ResolveAsync(cmdArgs, _bridgeService);
        }
        else
        {
            navTarget = NavigationTarget.FromCommandLineArgs(cmdArgs);
        }
    }

    // ... rest of existing navigation logic
}
```

**Step 5: Update README with new usage**

Add to README.md Command-Line section:
```markdown
# Navigate by name (human-readable)
HueCompanion.exe --page room --name bathroom
HueCompanion.exe --page room --name "living room"
HueCompanion.exe --page zone --name upstairs
HueCompanion.exe --page light --name "desk lamp"

# Names are case-insensitive and support various formats:
# bathroom, Bathroom, BATHROOM → all match "Bathroom"
# living-room, living_room, "living room" → all match "Living Room"
```

**Step 6: Commit**

```bash
git add src/HueCompanion/Helpers/CommandLineArgs.cs \
        src/HueCompanion/Helpers/CommandLineParser.cs \
        src/HueCompanion/Helpers/NavigationTarget.cs \
        src/HueCompanion/MainWindow.xaml.cs \
        README.md
git commit -m "feat: add human-readable name support for deep linking navigation"
```

---

## Task 9: Create Simple App Launch Script for Human Testing

**Files:**
- Create: `tools/Launch-App.ps1`
- Modify: `tools/screenshot-mcp/server.js` (update capture behavior)

**Goal:** Provide a simple way to launch the app for human testing without auto-close.

**Step 1: Create Launch-App.ps1 script**

Create `tools/Launch-App.ps1`:

```powershell
<#
.SYNOPSIS
    Launches the HueCompanion app for interactive testing.

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
$CsprojPath = Join-Path $ProjectRoot "src\HueCompanion\HueCompanion.csproj"
$ExePath = Join-Path $ProjectRoot "src\HueCompanion\bin\x64\Debug\net8.0-windows10.0.22621.0\HueCompanion.exe"

Write-Host ""
Write-Host "=== HueCompanion Launch ===" -ForegroundColor Cyan
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

Write-Host "App launched. Use Ctrl+C to exit this script." -ForegroundColor Green
Write-Host ""
```

**Step 2: Update MCP server to not auto-close by default**

Update `tools/screenshot-mcp/server.js` capture_screenshot description:

```javascript
{
    name: 'capture_screenshot',
    description: 'Build and launch the HueCompanion app, capture a screenshot, and close the app. Use for automated testing. For interactive testing without auto-close, use Launch-App.ps1.',
    // ... rest of schema
}
```

**Step 3: Commit**

```bash
git add tools/Launch-App.ps1 tools/screenshot-mcp/server.js
git commit -m "feat: add simple app launch script for human testing"
```

---

## Task 10: Update UI Test Script to Use Name-Based Navigation

**Files:**
- Modify: `tools/Run-UITests.ps1`

**Goal:** Update the UI test harness to support name-based navigation for room/zone tests.

**Step 1: Update Run-UITests.ps1 to support named pages**

Add support for testing specific rooms/zones by name:

```powershell
# Add to params:
param(
    [switch]$SkipBuild,
    [string]$OutputDir = "$PSScriptRoot\..\test-screenshots",
    [string[]]$Pages = @("dashboard", "settings"),
    [hashtable]$NamedPages = @{}
)

# Add in the test loop, handling named pages:
# Example usage: -NamedPages @{ "room:bathroom" = "bathroom"; "zone:upstairs" = "upstairs" }

foreach ($entry in $NamedPages.GetEnumerator()) {
    $parts = $entry.Key -split ":"
    $pageType = $parts[0]  # room, zone, light
    $pageName = $entry.Value

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $safePageName = $pageName -replace '\s+', '-'
    $screenshotPath = Join-Path $OutputDir "$pageType-$safePageName-$timestamp.png"

    try {
        $args = "--page $pageType --name `"$pageName`" --screenshot --delay $($WaitSeconds * 1000)"
        # ... same process handling as before
    }
    catch {
        # ... error handling
    }
}
```

**Step 2: Commit**

```bash
git add tools/Run-UITests.ps1
git commit -m "feat: add name-based navigation support to UI test harness"
```

---

## Summary

After completing all tasks:

1. **UI Test Harness** (`tools/Run-UITests.ps1`)
   - Navigates to key pages via command-line args
   - Supports both ID and name-based navigation
   - Captures screenshots using app's `--screenshot` mode

2. **Visual Regression Tool** (`tools/Compare-Screenshots.ps1`)
   - Pixel-by-pixel comparison with threshold
   - Generates diff images for failures
   - `-UpdateBaselines` flag for intentional changes

3. **Baseline Screenshots** (`tests/visual-baselines/`)
   - Stored in repo for comparison
   - Updated when UI changes are intentional

4. **CI Integration**
   - PR workflow: Compares and comments on diffs
   - Main CI: Full test suite with artifact upload

5. **Human-Readable Deep Linking**
   - `--name bathroom` instead of `--id <guid>`
   - Case-insensitive, supports hyphens/underscores/spaces
   - Works for rooms, zones, and lights

6. **Simple App Launch** (`tools/Launch-App.ps1`)
   - Launches app without auto-close
   - Supports page and name navigation
   - Better for human testing

**Testable Pages (automatic):**
- `dashboard` - Main room/zone cards view
- `settings` - App settings page

**Testable Pages (with name):**
- `room --name <room-name>` - Room detail page
- `zone --name <zone-name>` - Zone detail page
- `light --name <light-name>` - Light detail page

---

## Task 11: Create Proper MSIX Installer for Releases

**Files:**
- Modify: `.github/workflows/release.yml`
- Modify: `src/HueCompanion/HueCompanion.csproj`
- Create: `tools/Create-SelfSignedCert.ps1` (for local testing)

**Goal:** Generate proper MSIX installers instead of zip files for releases.

**Background:** The app is already configured for MSIX packaging (`WindowsPackageType=MSIX`). The current release workflow uses `dotnet publish` which outputs unpackaged files. We need to build actual MSIX packages.

**Step 1: Update csproj for proper MSIX output**

Verify/add these properties in `src/HueCompanion/HueCompanion.csproj`:

```xml
<PropertyGroup>
  <!-- MSIX packaging settings -->
  <WindowsPackageType>MSIX</WindowsPackageType>
  <AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>
  <GenerateAppxPackageOnBuild>false</GenerateAppxPackageOnBuild>

  <!-- For CI: enable package generation during publish -->
  <AppxPackageDir>$(OutputPath)AppPackages\</AppxPackageDir>
</PropertyGroup>
```

**Step 2: Create self-signed certificate script for local development**

Create `tools/Create-SelfSignedCert.ps1`:

```powershell
<#
.SYNOPSIS
    Creates a self-signed certificate for local MSIX signing.

.DESCRIPTION
    Generates a code signing certificate for testing MSIX packages locally.
    The certificate is stored in the current user's certificate store.

.PARAMETER Subject
    Certificate subject (default: CN=HueCompanion-Dev).

.PARAMETER OutputPath
    Path to export the .pfx file (optional).

.PARAMETER Password
    Password for the .pfx file (default: dev-password).

.EXAMPLE
    .\Create-SelfSignedCert.ps1
    # Create cert in store only

.EXAMPLE
    .\Create-SelfSignedCert.ps1 -OutputPath .\dev-cert.pfx
    # Create and export to file
#>

param(
    [string]$Subject = "CN=HueCompanion-Dev",
    [string]$OutputPath,
    [string]$Password = "dev-password"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=== Create Self-Signed Certificate ===" -ForegroundColor Cyan
Write-Host ""

# Check if cert already exists
$existingCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject }

if ($existingCert) {
    Write-Host "Certificate already exists: $($existingCert.Thumbprint)" -ForegroundColor Yellow
    $cert = $existingCert
}
else {
    Write-Host "Creating new certificate..." -ForegroundColor Cyan

    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -KeyUsage DigitalSignature `
        -FriendlyName "HueCompanion Development Signing Certificate" `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(5)

    Write-Host "Certificate created: $($cert.Thumbprint)" -ForegroundColor Green
}

# Export if path provided
if ($OutputPath) {
    Write-Host "Exporting to: $OutputPath" -ForegroundColor Cyan

    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $OutputPath -Password $securePassword | Out-Null

    Write-Host "Exported with password: $Password" -ForegroundColor Green
}

Write-Host ""
Write-Host "To use this certificate for MSIX signing, add to Package.appxmanifest:" -ForegroundColor Cyan
Write-Host "  Publisher=`"$Subject`"" -ForegroundColor Gray
Write-Host ""

Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor White
```

**Step 3: Update release workflow to build MSIX packages**

Modify `.github/workflows/release.yml` build job:

```yaml
  build:
    runs-on: windows-latest
    needs: test
    strategy:
      matrix:
        platform: [x64, ARM64]

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Extract version from tag
      id: version
      shell: pwsh
      run: |
        $tag = "${{ github.ref_name }}"
        $version = $tag -replace '^v', ''
        echo "VERSION=$version" >> $env:GITHUB_OUTPUT
        echo "Version: $version"

    - name: Restore dependencies
      run: dotnet restore

    - name: Build MSIX package (${{ matrix.platform }})
      shell: pwsh
      run: |
        # Build the MSIX package
        dotnet publish src/HueCompanion/HueCompanion.csproj `
          --configuration Release `
          -p:Platform=${{ matrix.platform }} `
          -p:Version=${{ steps.version.outputs.VERSION }} `
          -p:AppxPackageSigningEnabled=false `
          -p:GenerateAppInstallerFile=false `
          --output ./publish/${{ matrix.platform }}

    - name: Create zip archive
      shell: pwsh
      run: |
        $version = "${{ steps.version.outputs.VERSION }}"
        $platform = "${{ matrix.platform }}"

        # Zip the publish output
        Compress-Archive -Path "./publish/$platform/*" `
          -DestinationPath "./HueCompanion-$version-$platform.zip"

    - name: Upload artifacts
      uses: actions/upload-artifact@v4
      with:
        name: hue-companion-${{ matrix.platform }}-${{ steps.version.outputs.VERSION }}
        path: ./HueCompanion-${{ steps.version.outputs.VERSION }}-${{ matrix.platform }}.zip
        retention-days: 90
```

**Step 4: Update release job to attach MSIX/zip files**

Update the release job in `.github/workflows/release.yml`:

```yaml
  release:
    runs-on: ubuntu-latest
    needs: build
    permissions:
      contents: write

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Extract version from tag
      id: version
      run: |
        VERSION="${GITHUB_REF_NAME#v}"
        echo "VERSION=$VERSION" >> $GITHUB_OUTPUT

    - name: Download x64 artifact
      uses: actions/download-artifact@v4
      with:
        name: hue-companion-x64-${{ steps.version.outputs.VERSION }}
        path: ./artifacts

    - name: Download ARM64 artifact
      uses: actions/download-artifact@v4
      with:
        name: hue-companion-ARM64-${{ steps.version.outputs.VERSION }}
        path: ./artifacts

    - name: Generate release notes
      run: |
        cat > release-notes.md << 'EOF'
        ## Installation

        ### Option 1: Download and Extract
        1. Download the zip file for your platform (x64 for most PCs, ARM64 for ARM devices)
        2. Extract to a folder
        3. Run `HueCompanion.exe`

        ### Option 2: Developer Mode (for unsigned MSIX)
        1. Enable Developer Mode in Windows Settings
        2. Extract the zip and run the app

        ## Downloads
        - **x64 (Intel/AMD)**: HueCompanion-${{ steps.version.outputs.VERSION }}-x64.zip
        - **ARM64**: HueCompanion-${{ steps.version.outputs.VERSION }}-ARM64.zip

        ## Notes
        - Requires Windows 10 version 1809 or later
        - Windows 11 recommended for full visual experience
        - First run will require Hue Bridge pairing

        **Full Changelog**: https://github.com/ddrayne/hue-companion/compare/${{ github.event.before }}...v${{ steps.version.outputs.VERSION }}
        EOF

    - name: Create GitHub Release
      uses: softprops/action-gh-release@v1
      with:
        name: Hue Windows v${{ steps.version.outputs.VERSION }}
        body_path: release-notes.md
        draft: false
        prerelease: false
        files: |
          ./artifacts/HueCompanion-${{ steps.version.outputs.VERSION }}-x64.zip
          ./artifacts/HueCompanion-${{ steps.version.outputs.VERSION }}-ARM64.zip
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

**Step 5: Document MSIX signing for future Store deployment**

Add to `docs/DEVELOPMENT.md`:

```markdown
## 9. MSIX Packaging

### Local Development (Self-Signed)

For local testing with MSIX packages:

```powershell
# Create a self-signed certificate (one-time)
.\tools\Create-SelfSignedCert.ps1 -OutputPath .\dev-cert.pfx

# Build with signing
dotnet publish src/HueCompanion/HueCompanion.csproj `
  -p:Platform=x64 `
  -p:AppxPackageSigningEnabled=true `
  -p:PackageCertificateThumbprint=<thumbprint>
```

### Production Signing

For Store deployment, you need:
1. EV Code Signing Certificate from a trusted CA
2. Or use Windows Store signing (automatic when publishing to Store)

Store the certificate as GitHub secret `SIGNING_CERT_BASE64` and password as `SIGNING_CERT_PASSWORD`.
```

**Step 6: Commit**

```bash
git add .github/workflows/release.yml \
        src/HueCompanion/HueCompanion.csproj \
        tools/Create-SelfSignedCert.ps1 \
        docs/DEVELOPMENT.md
git commit -m "feat: improve release workflow with better MSIX support"
```

**Note on Signed MSIX:**
True signed MSIX installers require a code signing certificate. Options:
1. **Self-signed (dev)**: Works locally with Developer Mode enabled
2. **Store signing**: Microsoft signs when you publish to Windows Store
3. **EV certificate**: Purchase from a CA like DigiCert for side-loading

For now, the release provides zip files that work without signing. Store deployment (Task 4 from Phase 3) handles proper signing.

---

## Summary

After completing all tasks:

1. **UI Test Harness** (`tools/Run-UITests.ps1`)
   - Navigates to key pages via command-line args
   - Supports both ID and name-based navigation
   - Captures screenshots using app's `--screenshot` mode

2. **Visual Regression Tool** (`tools/Compare-Screenshots.ps1`)
   - Pixel-by-pixel comparison with threshold
   - Generates diff images for failures
   - `-UpdateBaselines` flag for intentional changes

3. **Baseline Screenshots** (`tests/visual-baselines/`)
   - Stored in repo for comparison
   - Updated when UI changes are intentional

4. **CI Integration**
   - PR workflow: Compares and comments on diffs
   - Main CI: Full test suite with artifact upload

5. **Human-Readable Deep Linking**
   - `--name bathroom` instead of `--id <guid>`
   - Case-insensitive, supports hyphens/underscores/spaces
   - Works for rooms, zones, and lights

6. **Simple App Launch** (`tools/Launch-App.ps1`)
   - Launches app without auto-close
   - Supports page and name navigation
   - Better for human testing

7. **MSIX Installer Support**
   - Self-signed certificate script for local dev
   - Release workflow with proper packaging
   - Documentation for production signing

**Testable Pages (automatic):**
- `dashboard` - Main room/zone cards view
- `settings` - App settings page

**Testable Pages (with name):**
- `room --name <room-name>` - Room detail page
- `zone --name <zone-name>` - Zone detail page
- `light --name <light-name>` - Light detail page

**Future Enhancements:**
- EV code signing certificate for signed MSIX
- Windows Store auto-deployment with Store signing
- Integrate with Percy.io or Applitools for cloud-based comparison
- Add WinAppDriver for interactive UI testing (click, drag, etc.)
