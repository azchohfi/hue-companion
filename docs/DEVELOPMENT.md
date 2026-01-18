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
# See current version and preview bump
.\tools\Bump-Version.ps1 -DryRun

# Bump versions
.\tools\Bump-Version.ps1              # Patch: 1.0.0 → 1.0.1
.\tools\Bump-Version.ps1 -Type minor  # Minor: 1.0.0 → 1.1.0
.\tools\Bump-Version.ps1 -Type major  # Major: 1.0.0 → 2.0.0
```

The script:
1. Reads current version from git tags
2. Calculates new version
3. Updates .csproj and Package.appxmanifest
4. Creates git commit
5. Creates git tag `vX.Y.Z`

## 5. Release Process

### Standard Release Flow

```
1. Ensure all tests pass locally
   $ dotnet test src/HueWindows.Tests

2. Ensure working directory is clean
   $ git status

3. Bump version (creates commit + tag)
   $ .\tools\Bump-Version.ps1 -Type <major|minor|patch>

4. Push to trigger CI
   $ git push origin main --tags

5. CI automatically:
   - Runs full test suite
   - Builds multi-platform packages
   - Creates artifacts
```

### Release Checklist

- [ ] All tests pass locally
- [ ] No uncommitted changes
- [ ] CHANGELOG updated (if applicable)
- [ ] Version bump executed
- [ ] Pushed with tags
- [ ] CI pipeline passed
- [ ] Artifacts available in GitHub Actions

### Hotfix Process

For urgent fixes on released versions:

```bash
# 1. Checkout the release tag
git checkout v1.0.0

# 2. Create hotfix branch
git checkout -b hotfix/1.0.1

# 3. Make fix, commit
git commit -m "fix: critical bug"

# 4. Bump patch version
.\tools\Bump-Version.ps1

# 5. Push hotfix
git push origin hotfix/1.0.1 --tags
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
