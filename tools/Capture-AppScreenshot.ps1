<#
.SYNOPSIS
    Captures a screenshot of the HueWindows app window.

.DESCRIPTION
    Builds (optional), launches the app, waits for the window to appear,
    captures it using Win32 PrintWindow API, and saves to screenshots folder.

.PARAMETER SkipBuild
    Skip the build step - just launch and capture.

.PARAMETER WaitSeconds
    Seconds to wait after launch before capturing (default: 5).

.PARAMETER KeepRunning
    Don't close the app after capturing.

.PARAMETER OutputDir
    Directory to save screenshots (default: ../screenshots).

.EXAMPLE
    .\Capture-AppScreenshot.ps1
    # Full build + capture

.EXAMPLE
    .\Capture-AppScreenshot.ps1 -SkipBuild -WaitSeconds 3
    # Quick capture without building
#>

param(
    [switch]$SkipBuild,
    [int]$WaitSeconds = 5,
    [switch]$KeepRunning,
    [string]$OutputDir = "$PSScriptRoot\..\screenshots"
)

$ErrorActionPreference = "Stop"

# Project paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$CsprojPath = Join-Path $ProjectRoot "src\HueWindows\HueWindows.csproj"
$ExePath = Join-Path $ProjectRoot "src\HueWindows\bin\x64\Debug\net8.0-windows10.0.22621.0\HueWindows.exe"
$WindowTitle = "Hue"

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

# Step 4: Launch app
Write-Host "Launching app..." -ForegroundColor Cyan
$process = Start-Process -FilePath $ExePath -PassThru

# Step 5: Wait for window
Write-Host "Waiting $WaitSeconds seconds for window to initialize..." -ForegroundColor Cyan
Start-Sleep -Seconds $WaitSeconds

# Step 6: Find window
Write-Host "Finding window..." -ForegroundColor Cyan

# Try finding by title first
$hwnd = [WindowCapture]::FindWindow($null, $WindowTitle)

# If not found by title, try finding by process ID
if ($hwnd -eq [IntPtr]::Zero) {
    Write-Host "Window not found by title, searching by process ID..." -ForegroundColor Yellow
    $hwnd = [WindowCapture]::FindWindowByProcessId([uint32]$process.Id)
}

if ($hwnd -eq [IntPtr]::Zero) {
    Write-Error "Could not find application window. The app may have crashed or the window title may have changed."
    if (-not $KeepRunning -and -not $process.HasExited) {
        $process | Stop-Process -Force
    }
    exit 1
}

Write-Host "Window found: 0x$($hwnd.ToString('X'))" -ForegroundColor Green

# Step 7: Bring window to foreground and wait a moment
[WindowCapture]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

# Step 8: Capture screenshot
Write-Host "Capturing screenshot..." -ForegroundColor Cyan

try {
    $bitmap = [WindowCapture]::CaptureWindow($hwnd)

    # Generate filename with timestamp
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $filename = "hue-$timestamp.png"
    $filepath = Join-Path $OutputDir $filename

    # Save as PNG
    $bitmap.Save($filepath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()

    Write-Host "Screenshot saved: $filepath" -ForegroundColor Green

    # Copy path to clipboard
    Set-Clipboard -Value $filepath
    Write-Host "(Path copied to clipboard)" -ForegroundColor Gray
}
catch {
    Write-Error "Failed to capture screenshot: $_"
    if (-not $KeepRunning -and -not $process.HasExited) {
        $process | Stop-Process -Force
    }
    exit 1
}

# Step 9: Close app (unless -KeepRunning)
if (-not $KeepRunning) {
    Write-Host "Closing app..." -ForegroundColor Cyan
    if (-not $process.HasExited) {
        $process | Stop-Process -Force
    }
}

Write-Host "Done!" -ForegroundColor Green
exit 0
