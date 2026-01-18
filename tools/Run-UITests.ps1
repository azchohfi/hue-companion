<#
.SYNOPSIS
    Runs automated UI tests by navigating to key pages and capturing screenshots.

.DESCRIPTION
    Launches Hue Companion with command-line navigation to each testable page,
    captures screenshots, and stores them in the test-screenshots folder.

.PARAMETER SkipBuild
    Skip the build step - use existing executable.

.PARAMETER OutputDir
    Directory to save test screenshots (default: ./test-screenshots).

.PARAMETER Pages
    Array of pages to test (default: dashboard, settings).

.PARAMETER NamedPages
    Hashtable of named pages to test. Key format: "type:name" (e.g., "room:bathroom").
    Example: @{ "room:bathroom" = "Bathroom"; "zone:upstairs" = "Upstairs" }

.PARAMETER Delay
    Milliseconds to wait after launch before capturing (default: 5000).

.EXAMPLE
    .\Run-UITests.ps1
    # Build and test all default pages

.EXAMPLE
    .\Run-UITests.ps1 -SkipBuild -Pages dashboard,settings
    # Quick test specific pages

.EXAMPLE
    .\Run-UITests.ps1 -SkipBuild -NamedPages @{ "room:bathroom" = "Bathroom" }
    # Test specific room by name
#>

param(
    [switch]$SkipBuild,
    [string]$OutputDir = "$PSScriptRoot\..\test-screenshots",
    [string[]]$Pages = @("dashboard", "settings"),
    [hashtable]$NamedPages = @{},
    [int]$Delay = 5000
)

$ErrorActionPreference = "Stop"

# Project paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$CsprojPath = Join-Path $ProjectRoot "src\HueWindows\HueWindows.csproj"
$ExePath = Join-Path $ProjectRoot "src\HueWindows\bin\x64\Debug\net8.0-windows10.0.22621.0\HueWindows.exe"
$ScreenshotsDir = Join-Path $ProjectRoot "screenshots"

# Add required assemblies for screenshot capture
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Win32 API definitions for window capture
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

public class WindowCapture {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // PW_RENDERFULLCONTENT = 2 - Required for WinUI 3 / DirectX content
    public const uint PW_RENDERFULLCONTENT = 2;

    public static Bitmap CaptureWindow(IntPtr hWnd) {
        RECT rect;
        if (!GetWindowRect(hWnd, out rect)) {
            throw new Exception("Failed to get window rect");
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0) {
            throw new Exception("Invalid window dimensions");
        }

        Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics gfx = Graphics.FromImage(bmp)) {
            IntPtr hdc = gfx.GetHdc();
            try {
                // Use PW_RENDERFULLCONTENT for WinUI 3 apps
                if (!PrintWindow(hWnd, hdc, PW_RENDERFULLCONTENT)) {
                    throw new Exception("PrintWindow failed");
                }
            }
            finally {
                gfx.ReleaseHdc(hdc);
            }
        }

        return bmp;
    }

    public static IntPtr FindWindowByProcessId(uint processId) {
        IntPtr foundWindow = IntPtr.Zero;

        EnumWindows((hWnd, lParam) => {
            uint windowProcessId;
            GetWindowThreadProcessId(hWnd, out windowProcessId);

            if (windowProcessId == processId && IsWindowVisible(hWnd)) {
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();

                // Check if it's the main window (has a title)
                if (!string.IsNullOrEmpty(title)) {
                    foundWindow = hWnd;
                    return false; // Stop enumeration
                }
            }
            return true; // Continue enumeration
        }, IntPtr.Zero);

        return foundWindow;
    }
}
"@ -ReferencedAssemblies System.Drawing, System.Drawing.Primitives, System.Runtime.InteropServices

# Test results tracking
$results = @()
$passed = 0
$failed = 0

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Hue Companion UI Test Harness" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build (unless skipped)
if (-not $SkipBuild) {
    Write-Host "[BUILD] Building app..." -ForegroundColor Cyan
    $buildOutput = & dotnet build $CsprojPath -p:Platform=x64 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed:`n$buildOutput"
        exit 1
    }
    Write-Host "[BUILD] Build successful." -ForegroundColor Green
    Write-Host ""
}

# Step 2: Verify exe exists
if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found at: $ExePath`nPlease build the project first."
    exit 1
}

# Step 3: Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$OutputDir = Resolve-Path $OutputDir

Write-Host "[INFO] Output directory: $OutputDir" -ForegroundColor Gray
Write-Host "[INFO] Testing pages: $($Pages -join ', ')" -ForegroundColor Gray
Write-Host "[INFO] Capture delay: ${Delay}ms" -ForegroundColor Gray
Write-Host ""

# Calculate wait time - give some buffer after app captures before we try
$waitSeconds = [Math]::Ceiling($Delay / 1000) + 2

# Step 4: Test each page
foreach ($page in $Pages) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "[TEST] Testing page: $page" -ForegroundColor Yellow

    $testResult = @{
        Page = $page
        Status = "UNKNOWN"
        Screenshot = $null
        Error = $null
    }

    try {
        # Launch app with page navigation and screenshot mode
        # Use a delay long enough for us to capture, then app auto-closes
        $appArgs = "--page $page --screenshot --delay $($Delay + 3000)"
        Write-Host "       Launching: $ExePath $appArgs" -ForegroundColor Gray

        $process = Start-Process -FilePath $ExePath -ArgumentList $appArgs -PassThru

        # Wait for window to initialize
        Write-Host "       Waiting for window..." -ForegroundColor Gray
        Start-Sleep -Seconds $waitSeconds

        # Find the window by process ID
        $hwnd = [WindowCapture]::FindWindowByProcessId([uint32]$process.Id)

        if ($hwnd -eq [IntPtr]::Zero) {
            throw "Could not find application window"
        }

        # Bring window to foreground
        [WindowCapture]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 500

        # Capture screenshot
        Write-Host "       Capturing screenshot..." -ForegroundColor Gray
        $bitmap = [WindowCapture]::CaptureWindow($hwnd)

        # Save to output directory with page name
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $filename = "$page-$timestamp.png"
        $filepath = Join-Path $OutputDir $filename

        $bitmap.Save($filepath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()

        # Wait for app to close (it should auto-close from screenshot mode)
        if (-not $process.HasExited) {
            $null = $process.WaitForExit(5000)
            if (-not $process.HasExited) {
                $process | Stop-Process -Force
            }
        }

        $testResult.Status = "PASS"
        $testResult.Screenshot = $filepath
        $passed++

        Write-Host "[PASS] $page - Screenshot saved: $filename" -ForegroundColor Green
    }
    catch {
        $testResult.Status = "FAIL"
        $testResult.Error = $_.Exception.Message
        $failed++

        Write-Host "[FAIL] $page - $($_.Exception.Message)" -ForegroundColor Red

        # Clean up process if still running
        if ($process -and -not $process.HasExited) {
            try {
                $process | Stop-Process -Force
            }
            catch {
                # Ignore cleanup errors
            }
        }
    }

    $results += $testResult
}

# Step 5: Test named pages (rooms/zones/lights by name)
foreach ($entry in $NamedPages.GetEnumerator()) {
    $parts = $entry.Key -split ":"
    if ($parts.Count -ne 2) {
        Write-Host "[SKIP] Invalid key format: $($entry.Key) (expected 'type:name')" -ForegroundColor Yellow
        continue
    }

    $pageType = $parts[0]  # room, zone, light
    $pageName = $entry.Value

    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "[TEST] Testing page: $pageType ($pageName)" -ForegroundColor Yellow

    $testResult = @{
        Page = "$pageType-$pageName"
        Status = "UNKNOWN"
        Screenshot = $null
        Error = $null
    }

    try {
        # Build safe filename from name
        $safePageName = $pageName -replace '\s+', '-' -replace '[^\w\-]', ''
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $filename = "$pageType-$safePageName-$timestamp.png"
        $filepath = Join-Path $OutputDir $filename

        # Launch app with name-based navigation and screenshot mode
        $appArgs = "--page $pageType --name `"$pageName`" --screenshot --delay $($Delay + 3000)"
        Write-Host "       Launching: $ExePath $appArgs" -ForegroundColor Gray

        $process = Start-Process -FilePath $ExePath -ArgumentList $appArgs -PassThru

        # Wait for window to initialize
        Write-Host "       Waiting for window..." -ForegroundColor Gray
        Start-Sleep -Seconds $waitSeconds

        # Find the window by process ID
        $hwnd = [WindowCapture]::FindWindowByProcessId([uint32]$process.Id)

        if ($hwnd -eq [IntPtr]::Zero) {
            throw "Could not find application window"
        }

        # Bring window to foreground
        [WindowCapture]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 500

        # Capture screenshot
        Write-Host "       Capturing screenshot..." -ForegroundColor Gray
        $bitmap = [WindowCapture]::CaptureWindow($hwnd)

        # Save to output directory
        $bitmap.Save($filepath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()

        # Wait for app to close
        if (-not $process.HasExited) {
            $null = $process.WaitForExit(5000)
            if (-not $process.HasExited) {
                $process | Stop-Process -Force
            }
        }

        $testResult.Status = "PASS"
        $testResult.Screenshot = $filepath
        $passed++

        Write-Host "[PASS] $pageType ($pageName) - Screenshot saved: $filename" -ForegroundColor Green
    }
    catch {
        $testResult.Status = "FAIL"
        $testResult.Error = $_.Exception.Message
        $failed++

        Write-Host "[FAIL] $pageType ($pageName) - $($_.Exception.Message)" -ForegroundColor Red

        # Clean up process if still running
        if ($process -and -not $process.HasExited) {
            try {
                $process | Stop-Process -Force
            }
            catch {
                # Ignore cleanup errors
            }
        }
    }

    $results += $testResult
}

# Step 6: Print summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$total = $passed + $failed
Write-Host "Total:  $total" -ForegroundColor White
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($passed -gt 0) {
    Write-Host "Screenshots saved to: $OutputDir" -ForegroundColor Gray
    Write-Host ""
    foreach ($result in $results | Where-Object { $_.Status -eq "PASS" }) {
        Write-Host "  - $($result.Screenshot)" -ForegroundColor Gray
    }
    Write-Host ""
}

if ($failed -gt 0) {
    Write-Host "Failed tests:" -ForegroundColor Red
    foreach ($result in $results | Where-Object { $_.Status -eq "FAIL" }) {
        Write-Host "  - $($result.Page): $($result.Error)" -ForegroundColor Red
    }
    Write-Host ""
    exit 1
}

Write-Host "All tests passed!" -ForegroundColor Green
exit 0
