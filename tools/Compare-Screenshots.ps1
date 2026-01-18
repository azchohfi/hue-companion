<#
.SYNOPSIS
    Compares test screenshots against baseline images for visual regression testing.

.DESCRIPTION
    Loads test screenshots and compares them pixel-by-pixel against baseline images.
    Reports differences and generates diff images highlighting changed pixels.
    Can also update baselines from current test screenshots.

.PARAMETER TestDir
    Directory containing test screenshots (default: ./test-screenshots).

.PARAMETER BaselineDir
    Directory containing baseline images (default: ./tests/visual-baselines).

.PARAMETER DiffDir
    Directory to save diff images (default: ./visual-diffs).

.PARAMETER Threshold
    Percentage difference threshold for failure (default: 1.0).

.PARAMETER UpdateBaselines
    Update baselines with current test screenshots instead of comparing.

.EXAMPLE
    .\Compare-Screenshots.ps1
    # Compare test screenshots against baselines

.EXAMPLE
    .\Compare-Screenshots.ps1 -UpdateBaselines
    # Update baselines from current test screenshots

.EXAMPLE
    .\Compare-Screenshots.ps1 -Threshold 0.5 -DiffDir ./my-diffs
    # Compare with stricter threshold and custom diff output
#>

param(
    [string]$TestDir = "$PSScriptRoot\..\test-screenshots",
    [string]$BaselineDir = "$PSScriptRoot\..\tests\visual-baselines",
    [string]$DiffDir = "$PSScriptRoot\..\visual-diffs",
    [double]$Threshold = 1.0,
    [switch]$UpdateBaselines
)

$ErrorActionPreference = "Stop"

# Add required assemblies for image processing
Add-Type -AssemblyName System.Drawing

# Project root for path resolution
$ProjectRoot = Split-Path -Parent $PSScriptRoot

function Compare-Images {
    <#
    .SYNOPSIS
        Compares two images pixel-by-pixel and generates a diff image.

    .PARAMETER TestImagePath
        Path to the test image.

    .PARAMETER BaselineImagePath
        Path to the baseline image.

    .PARAMETER DiffImagePath
        Path to save the diff image (optional).

    .OUTPUTS
        PSObject with DifferencePercent and IsMatch properties.
    #>
    param(
        [string]$TestImagePath,
        [string]$BaselineImagePath,
        [string]$DiffImagePath = $null
    )

    $testImage = $null
    $baselineImage = $null
    $diffImage = $null

    try {
        # Load images
        $testImage = [System.Drawing.Bitmap]::new($TestImagePath)
        $baselineImage = [System.Drawing.Bitmap]::new($BaselineImagePath)

        # Check dimensions match
        if ($testImage.Width -ne $baselineImage.Width -or $testImage.Height -ne $baselineImage.Height) {
            Write-Host "       Dimension mismatch: Test($($testImage.Width)x$($testImage.Height)) vs Baseline($($baselineImage.Width)x$($baselineImage.Height))" -ForegroundColor Yellow
            return [PSCustomObject]@{
                DifferencePercent = 100.0
                IsMatch = $false
            }
        }

        $width = $testImage.Width
        $height = $testImage.Height
        $totalPixels = $width * $height
        $differentPixels = 0

        # Create diff image if path provided
        if ($DiffImagePath) {
            $diffImage = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        }

        # Compare pixel by pixel
        for ($y = 0; $y -lt $height; $y++) {
            for ($x = 0; $x -lt $width; $x++) {
                $testPixel = $testImage.GetPixel($x, $y)
                $baselinePixel = $baselineImage.GetPixel($x, $y)

                # Check if pixels are different (allow small tolerance for compression artifacts)
                $rDiff = [Math]::Abs([int]$testPixel.R - [int]$baselinePixel.R)
                $gDiff = [Math]::Abs([int]$testPixel.G - [int]$baselinePixel.G)
                $bDiff = [Math]::Abs([int]$testPixel.B - [int]$baselinePixel.B)
                $aDiff = [Math]::Abs([int]$testPixel.A - [int]$baselinePixel.A)

                $isDifferent = ($rDiff -gt 2) -or ($gDiff -gt 2) -or ($bDiff -gt 2) -or ($aDiff -gt 2)

                if ($isDifferent) {
                    $differentPixels++
                    if ($diffImage) {
                        # Mark different pixels in red
                        $diffImage.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 255, 0, 0))
                    }
                }
                elseif ($diffImage) {
                    # Unchanged pixels as dimmed gray
                    $gray = [int](([int]$baselinePixel.R + [int]$baselinePixel.G + [int]$baselinePixel.B) / 3 * 0.3)
                    $diffImage.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $gray, $gray, $gray))
                }
            }
        }

        # Save diff image if created
        if ($diffImage -and $DiffImagePath) {
            $diffDir = Split-Path -Parent $DiffImagePath
            if (-not (Test-Path $diffDir)) {
                New-Item -ItemType Directory -Path $diffDir -Force | Out-Null
            }
            $diffImage.Save($DiffImagePath, [System.Drawing.Imaging.ImageFormat]::Png)
        }

        $differencePercent = ($differentPixels / $totalPixels) * 100

        return [PSCustomObject]@{
            DifferencePercent = [Math]::Round($differencePercent, 4)
            IsMatch = ($differencePercent -le $Threshold)
        }
    }
    finally {
        # Clean up resources
        if ($testImage) { $testImage.Dispose() }
        if ($baselineImage) { $baselineImage.Dispose() }
        if ($diffImage) { $diffImage.Dispose() }
    }
}

function Get-PageNameFromFilename {
    <#
    .SYNOPSIS
        Extracts page name from test screenshot filename.

    .DESCRIPTION
        Converts "dashboard-20260118-120000.png" to "dashboard".
    #>
    param([string]$Filename)

    # Pattern: pagename-YYYYMMDD-HHMMSS.png
    if ($Filename -match '^(.+)-\d{8}-\d{6}\.png$') {
        return $Matches[1]
    }

    # Fallback: just remove extension
    return [System.IO.Path]::GetFileNameWithoutExtension($Filename)
}

# Main script
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Visual Regression Comparison" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Resolve paths
$TestDir = if ([System.IO.Path]::IsPathRooted($TestDir)) { $TestDir } else { Join-Path $ProjectRoot $TestDir.TrimStart(".\").TrimStart("./") }
$BaselineDir = if ([System.IO.Path]::IsPathRooted($BaselineDir)) { $BaselineDir } else { Join-Path $ProjectRoot $BaselineDir.TrimStart(".\").TrimStart("./") }
$DiffDir = if ([System.IO.Path]::IsPathRooted($DiffDir)) { $DiffDir } else { Join-Path $ProjectRoot $DiffDir.TrimStart(".\").TrimStart("./") }

Write-Host "[INFO] Test directory:     $TestDir" -ForegroundColor Gray
Write-Host "[INFO] Baseline directory: $BaselineDir" -ForegroundColor Gray
Write-Host "[INFO] Diff directory:     $DiffDir" -ForegroundColor Gray
Write-Host "[INFO] Threshold:          $Threshold%" -ForegroundColor Gray
Write-Host ""

# Check test directory exists
if (-not (Test-Path $TestDir)) {
    Write-Error "Test directory not found: $TestDir"
    exit 1
}

# Get test screenshots
$testFiles = Get-ChildItem -Path $TestDir -Filter "*.png" | Sort-Object Name
if ($testFiles.Count -eq 0) {
    Write-Host "[WARN] No PNG files found in test directory." -ForegroundColor Yellow
    exit 0
}

Write-Host "[INFO] Found $($testFiles.Count) test screenshot(s)" -ForegroundColor Gray
Write-Host ""

if ($UpdateBaselines) {
    # Update Baselines mode
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  Updating Baselines" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""

    # Create baseline directory if it doesn't exist
    if (-not (Test-Path $BaselineDir)) {
        New-Item -ItemType Directory -Path $BaselineDir -Force | Out-Null
        Write-Host "[INFO] Created baseline directory: $BaselineDir" -ForegroundColor Gray
    }

    $updated = 0
    foreach ($testFile in $testFiles) {
        $pageName = Get-PageNameFromFilename -Filename $testFile.Name
        $baselinePath = Join-Path $BaselineDir "$pageName.png"

        Copy-Item -Path $testFile.FullName -Destination $baselinePath -Force
        $updated++

        Write-Host "[UPDATE] $($testFile.Name) -> $pageName.png" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Updated $updated baseline(s) in: $BaselineDir" -ForegroundColor Green
    Write-Host ""
    exit 0
}
else {
    # Compare mode
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  Comparing Screenshots" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""

    # Create diff directory if needed
    if (-not (Test-Path $DiffDir)) {
        New-Item -ItemType Directory -Path $DiffDir -Force | Out-Null
    }

    $results = @()
    $passed = 0
    $failed = 0
    $skipped = 0

    foreach ($testFile in $testFiles) {
        $pageName = Get-PageNameFromFilename -Filename $testFile.Name
        $baselinePath = Join-Path $BaselineDir "$pageName.png"

        Write-Host "----------------------------------------" -ForegroundColor DarkGray
        Write-Host "[TEST] $pageName" -ForegroundColor Yellow
        Write-Host "       Test:     $($testFile.Name)" -ForegroundColor Gray

        # Check if baseline exists
        if (-not (Test-Path $baselinePath)) {
            Write-Host "[SKIP] No baseline found: $pageName.png" -ForegroundColor Yellow
            $skipped++
            $results += [PSCustomObject]@{
                Page = $pageName
                Status = "SKIP"
                Difference = $null
                Message = "No baseline"
            }
            continue
        }

        Write-Host "       Baseline: $pageName.png" -ForegroundColor Gray

        # Generate diff path
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $diffPath = Join-Path $DiffDir "$pageName-diff-$timestamp.png"

        # Compare images
        $comparison = Compare-Images -TestImagePath $testFile.FullName -BaselineImagePath $baselinePath -DiffImagePath $diffPath

        if ($comparison.IsMatch) {
            $passed++
            Write-Host "[PASS] $pageName - Difference: $($comparison.DifferencePercent)% (threshold: $Threshold%)" -ForegroundColor Green

            # Remove diff file if it passes (no need to keep it)
            if (Test-Path $diffPath) {
                Remove-Item $diffPath -Force
            }

            $results += [PSCustomObject]@{
                Page = $pageName
                Status = "PASS"
                Difference = $comparison.DifferencePercent
                Message = "Within threshold"
            }
        }
        else {
            $failed++
            Write-Host "[FAIL] $pageName - Difference: $($comparison.DifferencePercent)% (threshold: $Threshold%)" -ForegroundColor Red
            Write-Host "       Diff saved: $diffPath" -ForegroundColor Red

            $results += [PSCustomObject]@{
                Page = $pageName
                Status = "FAIL"
                Difference = $comparison.DifferencePercent
                Message = "Exceeds threshold"
                DiffPath = $diffPath
            }
        }
    }

    # Print summary
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $total = $passed + $failed + $skipped
    Write-Host "Total:   $total" -ForegroundColor White
    Write-Host "Passed:  $passed" -ForegroundColor Green
    Write-Host "Failed:  $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
    Write-Host "Skipped: $skipped" -ForegroundColor Yellow
    Write-Host ""

    if ($failed -gt 0) {
        Write-Host "Failed comparisons:" -ForegroundColor Red
        foreach ($result in $results | Where-Object { $_.Status -eq "FAIL" }) {
            Write-Host "  - $($result.Page): $($result.Difference)% difference" -ForegroundColor Red
            Write-Host "    Diff: $($result.DiffPath)" -ForegroundColor Gray
        }
        Write-Host ""
        Write-Host "To update baselines, run:" -ForegroundColor Yellow
        Write-Host "  .\Compare-Screenshots.ps1 -UpdateBaselines" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }

    if ($skipped -gt 0 -and $passed -eq 0) {
        Write-Host "No comparisons performed (all skipped)." -ForegroundColor Yellow
        Write-Host "To create baselines, run:" -ForegroundColor Yellow
        Write-Host "  .\Compare-Screenshots.ps1 -UpdateBaselines" -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "All comparisons passed!" -ForegroundColor Green
    }

    exit 0
}
