# Hue Windows

[![Build Status](https://github.com/ddrayne/hue-windows/workflows/PR%20Validation/badge.svg)](https://github.com/ddrayne/hue-windows/actions)
[![codecov](https://codecov.io/gh/ddrayne/hue-windows/branch/main/graph/badge.svg)](https://codecov.io/gh/ddrayne/hue-windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern Windows 11 app for controlling Philips Hue smart lights, built with WinUI 3 and Fluent Design.

## Features

- **Dashboard with Room & Zone Cards** - Liquid glass widget tiles for quick control
- **Zone Support** - Control Hue zones alongside rooms
- **Drag to Dim** - Press and drag on cards to adjust brightness
- **Color-Coded Toggles** - Toggle switches show the room's active light color
- **Scene Selection** - Quickly activate saved Hue scenes
- **Color Control** - Full color picker with color wheel and temperature slider
- **Individual Light Control** - Fine-grained control of each light
- **Dark/Light Theme** - Follows system theme or manual selection
- **Mica Backdrop** - Modern Windows 11 visual style with premium styling

## Command-Line Deep Linking

Launch the app directly to specific pages using command-line arguments. Useful for automation and screenshot capture.

### Usage

```bash
# Navigate to specific pages
HueWindows.exe --page dashboard
HueWindows.exe --page settings
HueWindows.exe --page room --id "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
HueWindows.exe --page light --id "12345678-90ab-cdef-1234-567890abcdef"

# Screenshot mode (auto-close after delay)
HueWindows.exe --page dashboard --screenshot
HueWindows.exe --page room --id "..." --screenshot --delay 8000
```

### Supported Pages

| Page | Requires `--id` | Description |
|------|-----------------|-------------|
| `dashboard` | No | Main dashboard with all rooms |
| `mydashboard` | No | Pinned items dashboard |
| `rooms` | No | Rooms list |
| `zones` | No | Zones list |
| `room` | Yes | Room detail page |
| `zone` | Yes | Zone detail page |
| `light` | Yes | Individual light control |
| `settings` | No | App settings |
| `setup` | No | Bridge setup |

### Options

| Option | Short | Description |
|--------|-------|-------------|
| `--page <name>` | `-p` | Target page to navigate to |
| `--id <guid>` | | ID for pages that require it |
| `--screenshot` | `-s` | Auto-close app after delay |
| `--delay <ms>` | | Delay before auto-close (default: 5000ms) |

## Requirements

- Windows 10 version 1809 (build 17763) or later
- Windows 11 recommended for full visual experience
- Philips Hue Bridge on the same local network
- .NET 8.0 Runtime

## Building

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- Visual Studio 2022 (optional, for IDE development)

### Build from Command Line

```bash
# Restore and build
dotnet build src/HueWindows/HueWindows.csproj

# Run the app
dotnet run --project src/HueWindows/HueWindows.csproj
```

### Build for Specific Architecture

```bash
# x64
dotnet build src/HueWindows/HueWindows.csproj -p:Platform=x64

# ARM64
dotnet build src/HueWindows/HueWindows.csproj -p:Platform=ARM64
```

## Testing

### Run Unit Tests

```bash
# Run all tests
dotnet test src/HueWindows.Tests

# Run tests with detailed output
dotnet test src/HueWindows.Tests --verbosity normal

# Run tests with code coverage
dotnet test src/HueWindows.Tests --collect:"XPlat Code Coverage"
```

### Code Coverage

This project uses [Coverlet](https://github.com/coverlet-coverage/coverlet) for code coverage and reports to [Codecov](https://codecov.io/).

Coverage reports are automatically generated in CI/CD pipelines. To generate a local coverage report:

```bash
# Install report generator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test src/HueWindows.Tests --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate HTML report
reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage-report -reporttypes:HtmlInline

# Open the report (Windows)
start ./coverage-report/index.html
```

### Continuous Integration

This project uses GitHub Actions for automated testing and deployment:

- **PR Validation** - Runs on every pull request
  - Build verification
  - Unit tests
  - Code coverage reporting
  - Build warning checks

- **Main CI** - Runs on every push to main
  - All PR validation checks
  - Coverage report generation
  - Artifact publishing

See [`.github/workflows/`](.github/workflows/) for workflow definitions.

## Version Management

This project follows [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`

### Bumping the Version

Use the `Bump-Version.ps1` script to increment the version:

```powershell
# Patch version (bug fixes): 1.2.3 -> 1.2.4
.\tools\Bump-Version.ps1 -Type patch

# Minor version (new features): 1.2.3 -> 1.3.0
.\tools\Bump-Version.ps1 -Type minor

# Major version (breaking changes): 1.2.3 -> 2.0.0
.\tools\Bump-Version.ps1 -Type major

# Preview changes without committing
.\tools\Bump-Version.ps1 -Type minor -DryRun
```

The script will:
1. Increment the version in all required files
2. Create a git commit with the changes
3. Create a git tag (e.g., `v1.3.0`)

### Creating a Release

```powershell
# After bumping the version, push to GitHub
git push
git push --tags

# The git tag push will automatically trigger the release workflow
```

## Project Structure

```
hue-windows/
├── src/
│   ├── HueWindows/                 # WinUI 3 App (MSIX packaged)
│   │   ├── Views/                  # XAML pages
│   │   ├── Controls/               # Custom controls (RoomCard)
│   │   ├── Styles/                 # Global styles and theme overrides
│   │   ├── Converters/             # Value converters
│   │   ├── Helpers/                # Navigation and utilities
│   │   └── Assets/                 # App icons and images
│   │
│   ├── HueWindows.Core/            # Business logic library
│   │   ├── Models/                 # Data models
│   │   ├── ViewModels/             # MVVM ViewModels
│   │   └── Services/               # Hue API and settings services
│   │
│   └── HueWindows.Tests/           # Unit tests
│
├── tools/                          # Development tooling
│   ├── Capture-AppScreenshot.ps1   # Screenshot capture script
│   └── screenshot-mcp/             # MCP server for Claude integration
│
├── screenshots/                    # Captured app screenshots (gitignored)
└── Directory.Build.props           # Shared build settings
```

## Technology Stack

- **Framework**: WinUI 3 / Windows App SDK 1.8
- **Language**: C# / .NET 8
- **Architecture**: MVVM with CommunityToolkit.Mvvm
- **Styling**: WinUI 3 lightweight styling with theme resource overrides
- **Hue Integration**: HueApi NuGet package (CLIP v2 API)
- **Packaging**: MSIX for Windows Store

## First Run Setup

1. Launch the app
2. The app will automatically discover Hue bridges on your network
3. Select your bridge from the list
4. Press the link button on your Hue bridge when prompted
5. The app will connect and display your rooms

## Screenshot Validation (Claude Code Integration)

This project includes tooling for visual UI validation with Claude Code. The system captures app screenshots that Claude can analyze for UI feedback during development.

### Components

- **`tools/Capture-AppScreenshot.ps1`** - PowerShell script that builds, launches, and captures the app window
- **`tools/screenshot-mcp/`** - MCP server that exposes screenshot tools to Claude
- **`.mcp.json`** - MCP server configuration for Claude Code

### Manual Screenshot Capture

```powershell
# Full build + capture
.\tools\Capture-AppScreenshot.ps1

# Quick capture (skip build, use existing executable)
.\tools\Capture-AppScreenshot.ps1 -SkipBuild

# Keep app running after capture
.\tools\Capture-AppScreenshot.ps1 -SkipBuild -KeepRunning

# Custom wait time before capture
.\tools\Capture-AppScreenshot.ps1 -WaitSeconds 10
```

Screenshots are saved to `screenshots/hue-{timestamp}.png` and the path is copied to clipboard.

### Claude Code MCP Integration

When running Claude Code in this repository, the MCP server provides these tools:

| Tool | Description |
|------|-------------|
| `capture_screenshot` | Build and capture a new screenshot |
| `get_latest_screenshot` | View the most recent screenshot |
| `list_screenshots` | List all available screenshots |
| `get_screenshot` | Get a specific screenshot by filename |

**Usage in Claude Code:**
- "Capture a screenshot of the app"
- "Show me the latest screenshot"
- "What does the current UI look like?"

### Setup (First Time)

```powershell
cd tools/screenshot-mcp
npm install
```

The MCP server is automatically enabled via `.mcp.json` when running Claude Code in this repository.

## License

MIT License - See LICENSE file for details.
