# Technology Stack

**Analysis Date:** 2026-01-20

## Languages

**Primary:**
- C# 12 (.NET 8.0) - All application code (MSIX app, core services, UI)

## Runtime

**Environment:**
- .NET 8.0 with Windows App SDK (WinUI 3)
- Target platforms: x64, ARM64
- Minimum Windows version: 10.0.17763.0 (Windows 10 1809)
- Tested up to: 10.0.22621.0 (Windows 11)

**Package Manager:**
- NuGet (.NET package manager)
- Lockfile: `packages.lock.json` (implied via project dependencies)

## Frameworks

**Core UI:**
- Windows App SDK 1.8.251106002 - WinUI 3 desktop application framework
- Microsoft.Graphics.Win2D 1.3.0 - Graphics rendering (used in timeline editor)

**MVVM & DI:**
- CommunityToolkit.Mvvm 8.4.0 - MVVM pattern implementation with ObservableObject, RelayCommand
- Microsoft.Extensions.DependencyInjection 9.0.1 - Service container and dependency injection
- Microsoft.Extensions.Hosting 9.0.1 - Application hosting/lifecycle

**Testing:**
- xunit 2.9.2 - Unit test framework
- xunit.runner.visualstudio 3.0.0 - Test runner for Visual Studio
- Moq 4.20.72 - Mocking framework for unit tests
- FluentAssertions 6.12.2 - Fluent assertion syntax for tests
- Microsoft.NET.Test.Sdk 17.12.0 - Test SDK infrastructure
- coverlet.collector 6.0.4 - Code coverage collection (XPlat Cobertura format)

## Key Dependencies

**Critical:**
- HueApi 3.1.2 - Philips Hue CLIP v2 API client SDK for bridge communication
- HueApi.ColorConverters 3.1.0 - Color space conversions (XY to RGB, color temperature handling)

**Infrastructure:**
- Microsoft.Windows.SDK.BuildTools 10.0.26100.7175 - Windows SDK build tools for MSIX packaging

## Configuration

**Environment:**
- No required environment variables for runtime (app settings stored in JSON)
- Development builds vs stable builds controlled via MSBuild property `DevBuild`:
  - `DevBuild=true` → separate MSIX identity (HueCompanion.Dev), title shows "(Dev)" with git hash
  - `DevBuild=false` (default) → standard package identity, release version from BaseVersion
- Version configured in `HueCompanion.csproj`: BaseVersion = 0.1.0

**Build:**
- MSBuild project files: `HueCompanion.csproj`, `HueCompanion.Core.csproj`, `HueCompanion.Tests.csproj`
- Directory.Build.props: Global C# language settings (LangVersion=latest, Nullable=enable, ImplicitUsings=enable)
- Publish profiles: `win-x64.pubxml`, `win-ARM64.pubxml` for platform-specific MSIX generation
- Multi-platform support via `<Platforms>x64;ARM64</Platforms>`

## Platform Requirements

**Development:**
- .NET 8.0 SDK
- Visual Studio 2022 or compatible .NET toolchain
- Windows 10 1809+ or Windows 11 for development machine
- PowerShell 7+ (for CLI scripts like `hue.cmd` and `hue-cli.ps1`)

**Production:**
- Windows 10 1809+ or Windows 11
- MSIX deployed via Microsoft Store or sideloading
- No external runtime dependencies beyond Windows App SDK (self-contained MSIX)
- Hue Bridge on local network (CLIP v2 API endpoint)

## Architecture Notes

**Application Structure:**
- WinUI 3 desktop app with MVVM pattern
- Separate projects: `HueCompanion` (UI), `HueCompanion.Core` (business logic), `HueCompanion.Tests` (unit tests)
- Dependency injection configured in `App.xaml.cs` with service registration

**Key Services Layer (`HueCompanion.Core.Services`):**
- `HueBridgeService` - Hue API communication with event streaming
- `BridgeDiscoveryService` - Bridge discovery via HTTP and mDNS
- `MultiBridgeService` - Multi-bridge support wrapper
- `SettingsService` - Settings persistence to JSON (LocalAppData\HueCompanion\settings.json)
- `SceneStorageService` - Scene storage (built-in: Assets\Scenes\, user: LocalAppData\HueCompanion\Scenes\)
- `AnimationService` - Scene playback management
- `AnimationEngine` - Timeline-based light animation execution
- `SystemTrayService` - Windows system tray integration (Win32 Shell_NotifyIcon)
- `HotkeyService` - Global hotkey registration (Win32)

**XAML/View Layer (`HueCompanion`):**
- Pages: DashboardPage, RoomDetailPage, LightDetailPage, SceneBuilderPage, ScenesPage, SettingsPage
- Controls: RoomCard, LightCard (custom WinUI controls)
- Styles: AppStyles.xaml with WinUI 3 resource overrides
- Converters: BoolToOpacityConverter, HexToGradientBackgroundConverter, etc.

---

*Stack analysis: 2026-01-20*
