# Development Workflow

This document describes the complete versioning, testing, and release flow for HueWindows.

## Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         DEVELOPMENT WORKFLOW                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. DEVELOP          2. TEST            3. REVIEW         4. RELEASE    │
│  ─────────           ─────              ──────            ───────       │
│                                                                          │
│  Write code    →    Run tests    →    Create PR    →    Bump version   │
│  Local build        Local coverage     CI validates      Tag & push     │
│                                        Code review       CI releases    │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

## 1. Local Development

### Build Commands

```bash
# Build (default platform)
dotnet build src/HueWindows/HueWindows.csproj

# Build for specific architecture
dotnet build src/HueWindows/HueWindows.csproj -p:Platform=x64
dotnet build src/HueWindows/HueWindows.csproj -p:Platform=ARM64

# Run the app
dotnet run --project src/HueWindows/HueWindows.csproj
```

### Project Structure

```
src/
├── HueWindows/           # WinUI 3 App (views, controls, styles)
├── HueWindows.Core/      # Business logic (models, viewmodels, services)
└── HueWindows.Tests/     # Unit tests (xUnit + FluentAssertions)
```

## 2. Testing

### Running Tests Locally

```bash
# Run all tests
dotnet test src/HueWindows.Tests

# Run with verbose output
dotnet test src/HueWindows.Tests -v normal

# Run specific test class
dotnet test src/HueWindows.Tests --filter "FullyQualifiedName~HueColorTests"

# Run with code coverage
dotnet test src/HueWindows.Tests --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### Test Categories

| Category | Location | Description |
|----------|----------|-------------|
| **Models** | `Tests/Models/` | Pure logic (HueColor, Result<T>) |
| **Services** | `Tests/Services/` | Persistence (SettingsService, SceneStorageService) |

### Test Patterns

- **FluentAssertions** for readable assertions
- **IDisposable** pattern for test cleanup
- **Testable wrappers** for services with file I/O
- **AAA pattern** (Arrange, Act, Assert)

### Coverage Reports

After running tests with coverage, reports are in `TestResults/*/coverage.cobertura.xml`.

View coverage in CI via Codecov: https://codecov.io/gh/ddrayne/hue-windows

## 3. Continuous Integration

### CI Workflows

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| **PR Validation** | Pull requests to main | Build, test, coverage upload |
| **Main CI** | Push to main | Multi-platform build (x64/ARM64), package artifacts |

### PR Validation Flow

```
PR opened/updated
       │
       ▼
┌──────────────────┐
│  Checkout code   │
│  Setup .NET 8.0  │
│  Restore deps    │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  Build (Release) │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│   Run tests      │
│   with coverage  │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ Upload artifacts │
│ Upload to Codecov│
└──────────────────┘
```

### Quality Gates

- All tests must pass
- Coverage uploaded to Codecov
- Codecov thresholds: 50% project, 70% patch

## 4. Versioning

### Semantic Versioning

Format: `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes or major milestones
- **MINOR**: New features (backwards compatible)
- **PATCH**: Bug fixes

### Version Files

Version is maintained in two locations:

| File | Format | Example |
|------|--------|---------|
| `src/HueWindows/HueWindows.csproj` | `<Version>X.Y.Z</Version>` | `<Version>1.0.0</Version>` |
| `src/HueWindows/Package.appxmanifest` | `Version="X.Y.Z.0"` | `Version="1.0.0.0"` |

### Version Bump Script

```powershell
# Preview changes (dry run)
.\tools\Bump-Version.ps1 -DryRun

# Bump versions
.\tools\Bump-Version.ps1              # Patch: 1.0.0 → 1.0.1
.\tools\Bump-Version.ps1 -Type minor  # Minor: 1.0.0 → 1.1.0
.\tools\Bump-Version.ps1 -Type major  # Major: 1.0.0 → 2.0.0

# With custom release message
.\tools\Bump-Version.ps1 -Message "Added dark mode support"

# Auto-push after bumping (triggers release workflow)
.\tools\Bump-Version.ps1 -Push
```

The script:
1. Reads current version from git tags
2. Calculates new version
3. Updates .csproj and Package.appxmanifest
4. Creates git commit
5. Creates annotated git tag `vX.Y.Z`
6. Optionally pushes to origin (with `-Push`)

## 5. Release Process

### Release Workflow

When a tag matching `v*.*.*` is pushed, the release workflow automatically:

```
Tag pushed (v1.2.3)
       │
       ▼
┌──────────────────┐
│   Run tests      │  ← Must pass before building
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  Build packages  │  ← x64 and ARM64 in parallel
│  (matrix build)  │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ Create GitHub    │  ← Automatic release with:
│    Release       │     - Release notes
└────────┬─────────┘     - x64 zip attached
         │               - ARM64 zip attached
         ▼
    Release published!
```

### Creating a Release

**Quick release (recommended):**
```powershell
# Bump and push in one command
.\tools\Bump-Version.ps1 -Type patch -Push
```

**Manual release:**
```powershell
# 1. Ensure clean state
git status
dotnet test src/HueWindows.Tests

# 2. Bump version
.\tools\Bump-Version.ps1 -Type minor

# 3. Push to trigger workflow
git push origin main --tags
```

### Release Checklist

- [ ] All tests pass locally (`dotnet test src/HueWindows.Tests`)
- [ ] Working directory is clean (`git status`)
- [ ] Version bump executed (`.\tools\Bump-Version.ps1`)
- [ ] Tag pushed to origin
- [ ] Release workflow passed (check GitHub Actions)
- [ ] GitHub Release created with artifacts

### Monitoring Releases

After pushing a tag:
1. Go to https://github.com/ddrayne/hue-windows/actions
2. Watch the "Release" workflow
3. Once complete, check https://github.com/ddrayne/hue-windows/releases

### Failed Release Recovery

If the release workflow fails:

```bash
# 1. Delete the local and remote tag
git tag -d v1.2.3
git push origin :refs/tags/v1.2.3

# 2. Fix the issue

# 3. Re-run version bump (will recreate same version)
.\tools\Bump-Version.ps1 -Push
```

### Hotfix Process

For urgent fixes on released versions:

```bash
# 1. Checkout the release tag
git checkout v1.0.0

# 2. Create hotfix branch
git checkout -b hotfix/1.0.1

# 3. Make fix, commit
git commit -m "fix: critical bug"

# 4. Bump patch version and push
.\tools\Bump-Version.ps1 -Push
```

## 6. Configuration Files

### codecov.yml

Controls Codecov behavior:

```yaml
coverage:
  status:
    project:
      default:
        target: 50%      # Overall coverage target
        threshold: 5%    # Allow 5% decrease
    patch:
      default:
        target: 70%      # New code coverage target

ignore:
  - "**/*.xaml.cs"       # UI code-behind
  - "**/obj/**"          # Build artifacts
  - "**/bin/**"
```

### Directory.Build.props

Shared build settings for all projects:

```xml
<PropertyGroup>
  <LangVersion>latest</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

## 7. Troubleshooting

### Tests Fail Locally but Pass in CI

- Check .NET SDK version matches (`dotnet --version`)
- Ensure clean build: `dotnet clean && dotnet build`
- Check for environment-specific paths

### Coverage Not Uploading to Codecov

- Verify CODECOV_TOKEN secret is set in GitHub
- Check coverage file exists: `TestResults/**/coverage.cobertura.xml`
- Codecov failures don't break CI (`fail_ci_if_error: false`)

### Version Bump Script Doesn't Find Tags

- Ensure tags are fetched: `git fetch --tags`
- Check tag format matches `v*.*.*` pattern
- Try `git tag -l "v*.*.*"` to list matching tags

### Build Fails for ARM64

- Some NuGet packages may not support ARM64
- Check Package.appxmanifest for correct TargetDeviceFamily
- Try x64 build first to isolate platform-specific issues

## 8. UI & Visual Regression Testing

### Running UI Tests Locally

```powershell
# Full build + capture all test pages
.\tools\Run-UITests.ps1

# Quick capture (skip build)
.\tools\Run-UITests.ps1 -SkipBuild

# Test specific pages
.\tools\Run-UITests.ps1 -SkipBuild -Pages dashboard,settings
```

Screenshots are saved to `test-screenshots/`.

### Visual Regression Testing

```powershell
# Compare current screenshots against baselines
.\tools\Compare-Screenshots.ps1

# Update baselines (after reviewing changes)
.\tools\Compare-Screenshots.ps1 -UpdateBaselines

# Use stricter threshold (default is 1%)
.\tools\Compare-Screenshots.ps1 -Threshold 0.5
```

**Threshold:** 1% pixel difference allowed by default.

### Baseline Management

Baseline screenshots are stored in `tests/visual-baselines/`. When UI changes are intentional:

1. Run UI tests: `.\tools\Run-UITests.ps1`
2. Review screenshots in `test-screenshots/`
3. Update baselines: `.\tools\Compare-Screenshots.ps1 -UpdateBaselines`
4. Commit: `git add tests/visual-baselines/*.png`

### CI Integration

| Workflow | What it does |
|----------|--------------|
| **Main CI** | Captures screenshots, runs regression tests, uploads artifacts |
| **PR Validation** | Compares screenshots, comments on PR if differences detected |

**Artifacts:** Screenshots and diff images are uploaded for review when tests run.

### Testable Pages

| Page | Command |
|------|---------|
| Dashboard | `--page dashboard` |
| Settings | `--page settings` |
| Room (with bridge) | `--page room --name <room-name>` |
| Zone (with bridge) | `--page zone --name <zone-name>` |
| Light (with bridge) | `--page light --name <light-name>` |

**Note:** Pages requiring room/zone/light need a connected Hue bridge.

## 9. MSIX Packaging

### Local Development (Self-Signed)

For local testing with signed MSIX packages:

```powershell
# Create a self-signed certificate (one-time)
.\tools\Create-SelfSignedCert.ps1

# Optionally export to file
.\tools\Create-SelfSignedCert.ps1 -OutputPath .\dev-cert.pfx

# Build with signing (if needed)
dotnet publish src/HueWindows/HueWindows.csproj `
  -p:Platform=x64 `
  -p:AppxPackageSigningEnabled=true `
  -p:PackageCertificateThumbprint=<thumbprint>
```

### Production Signing Options

For signed releases:

1. **EV Code Signing Certificate** - Purchase from a trusted CA (DigiCert, Sectigo)
2. **Windows Store Signing** - Automatic when publishing to Microsoft Store
3. **Self-Signed (Dev Only)** - Works locally with Developer Mode enabled

### Release Workflow

The automated release workflow:
1. Builds for x64 and ARM64
2. Creates zip archives with the app files
3. Generates release notes
4. Creates GitHub Release with artifacts attached

To create a release:
```powershell
.\tools\Bump-Version.ps1 -Push
```

### Artifact Structure

Downloaded release zips contain:
```
HueWindows-{version}-{platform}.zip
├── HueWindows.exe          # Main executable
├── HueWindows.dll          # Core library
├── *.dll                   # Dependencies
└── Assets/                 # App resources
```
