# Test & CI/CD System - Phase 2: Version Management

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement version bumping script and document versioning strategy.

**Architecture:** PowerShell script reads current version from git tags, increments based on type (major/minor/patch), updates .csproj and Package.appxmanifest, commits, and creates git tag.

**Tech Stack:** PowerShell, Git, Semantic Versioning

**Reference:** [GitHub Issue #5](https://github.com/ddrayne/hue-windows/issues/5) - Phase 2

---

## Task 1: Create Bump-Version.ps1 Script

**Files:**
- Create: `tools/Bump-Version.ps1`

**Step 1: Create the version bump script**

Create `tools/Bump-Version.ps1`:

```powershell
<#
.SYNOPSIS
    Bumps the version number for HueWindows application.

.DESCRIPTION
    Reads the current version from git tags (or defaults to 1.0.0),
    increments based on the specified type (major, minor, patch),
    updates version in .csproj and Package.appxmanifest files,
    commits the changes, and creates a git tag.

.PARAMETER Type
    The type of version bump: major, minor, or patch (default: patch)

.PARAMETER DryRun
    If specified, shows what would happen without making changes

.EXAMPLE
    .\Bump-Version.ps1 -Type minor
    Bumps the minor version (e.g., 1.2.0 -> 1.3.0)

.EXAMPLE
    .\Bump-Version.ps1 -DryRun
    Shows what changes would be made without applying them
#>

param(
    [ValidateSet('major', 'minor', 'patch')]
    [string]$Type = 'patch',

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# File paths
$RepoRoot = Split-Path -Parent $PSScriptRoot
$CsprojPath = Join-Path $RepoRoot 'src\HueWindows\HueWindows.csproj'
$ManifestPath = Join-Path $RepoRoot 'src\HueWindows\Package.appxmanifest'

function Get-CurrentVersion {
    # Try to get version from latest git tag
    $tag = git describe --tags --abbrev=0 2>$null
    if ($tag -match '^v?(\d+)\.(\d+)\.(\d+)') {
        return @{
            Major = [int]$Matches[1]
            Minor = [int]$Matches[2]
            Patch = [int]$Matches[3]
        }
    }

    # Default to 1.0.0 if no tags exist
    Write-Host "No version tags found, starting from 1.0.0" -ForegroundColor Yellow
    return @{
        Major = 1
        Minor = 0
        Patch = 0
    }
}

function Get-NextVersion {
    param($Current, $BumpType)

    switch ($BumpType) {
        'major' {
            return @{
                Major = $Current.Major + 1
                Minor = 0
                Patch = 0
            }
        }
        'minor' {
            return @{
                Major = $Current.Major
                Minor = $Current.Minor + 1
                Patch = 0
            }
        }
        'patch' {
            return @{
                Major = $Current.Major
                Minor = $Current.Minor
                Patch = $Current.Patch + 1
            }
        }
    }
}

function Format-Version {
    param($Version, [switch]$FourPart)

    if ($FourPart) {
        return "$($Version.Major).$($Version.Minor).$($Version.Patch).0"
    }
    return "$($Version.Major).$($Version.Minor).$($Version.Patch)"
}

function Update-CsprojVersion {
    param($FilePath, $NewVersion)

    $content = Get-Content $FilePath -Raw
    $versionString = Format-Version $NewVersion

    # Update or add Version element
    if ($content -match '<Version>.*</Version>') {
        $content = $content -replace '<Version>.*</Version>', "<Version>$versionString</Version>"
    }
    elseif ($content -match '</PropertyGroup>') {
        # Add Version to first PropertyGroup
        $content = $content -replace '(</PropertyGroup>)', "  <Version>$versionString</Version>`n  `$1"
        # Only replace first occurrence
        $content = $content -replace '(  <Version>.*</Version>\n  </PropertyGroup>.*)(  <Version>.*</Version>\n  </PropertyGroup>)', '$1</PropertyGroup>'
    }

    return $content
}

function Update-ManifestVersion {
    param($FilePath, $NewVersion)

    $content = Get-Content $FilePath -Raw
    $versionString = Format-Version $NewVersion -FourPart

    # Update Identity Version attribute
    $content = $content -replace 'Version="[\d.]+"', "Version=`"$versionString`""

    return $content
}

# Main execution
Write-Host "`n=== HueWindows Version Bump ===" -ForegroundColor Cyan

# Get current version
$currentVersion = Get-CurrentVersion
$currentVersionString = Format-Version $currentVersion
Write-Host "Current version: $currentVersionString" -ForegroundColor White

# Calculate new version
$newVersion = Get-NextVersion -Current $currentVersion -BumpType $Type
$newVersionString = Format-Version $newVersion
$newVersionStringFourPart = Format-Version $newVersion -FourPart
Write-Host "New version:     $newVersionString ($Type bump)" -ForegroundColor Green

if ($DryRun) {
    Write-Host "`n[DRY RUN] Would update:" -ForegroundColor Yellow
    Write-Host "  - $CsprojPath -> $newVersionString"
    Write-Host "  - $ManifestPath -> $newVersionStringFourPart"
    Write-Host "  - Create git commit"
    Write-Host "  - Create git tag: v$newVersionString"
    exit 0
}

# Verify files exist
if (-not (Test-Path $CsprojPath)) {
    throw "Could not find csproj at: $CsprojPath"
}
if (-not (Test-Path $ManifestPath)) {
    throw "Could not find manifest at: $ManifestPath"
}

# Update files
Write-Host "`nUpdating files..." -ForegroundColor Cyan

$csprojContent = Update-CsprojVersion -FilePath $CsprojPath -NewVersion $newVersion
Set-Content -Path $CsprojPath -Value $csprojContent -NoNewline
Write-Host "  Updated: $CsprojPath"

$manifestContent = Update-ManifestVersion -FilePath $ManifestPath -NewVersion $newVersion
Set-Content -Path $ManifestPath -Value $manifestContent -NoNewline
Write-Host "  Updated: $ManifestPath"

# Git operations
Write-Host "`nCommitting changes..." -ForegroundColor Cyan

git add $CsprojPath $ManifestPath
git commit -m "chore: bump version to $newVersionString"
Write-Host "  Created commit"

git tag -a "v$newVersionString" -m "Release v$newVersionString"
Write-Host "  Created tag: v$newVersionString"

Write-Host "`n=== Version bump complete ===" -ForegroundColor Green
Write-Host "To push changes and trigger release workflow:"
Write-Host "  git push origin main --tags" -ForegroundColor Yellow
```

**Step 2: Test the script with dry-run**

Run: `powershell -ExecutionPolicy Bypass -File tools/Bump-Version.ps1 -DryRun`
Expected: Shows what would happen without making changes

**Step 3: Commit the script**

```bash
git add tools/Bump-Version.ps1
git commit -m "chore: add version bump script for release automation"
```

---

## Task 2: Add Version Property to .csproj

**Files:**
- Modify: `src/HueWindows/HueWindows.csproj`

**Step 1: Add Version element**

Add `<Version>1.0.0</Version>` to the first PropertyGroup in `src/HueWindows/HueWindows.csproj`:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>
  ...
  <Version>1.0.0</Version>
</PropertyGroup>
```

**Step 2: Commit**

```bash
git add src/HueWindows/HueWindows.csproj
git commit -m "chore: add explicit Version property to csproj"
```

---

## Task 3: Create Initial Version Tag

**Step 1: Create v1.0.0 tag**

```bash
git tag -a v1.0.0 -m "Initial release v1.0.0"
```

**Step 2: Verify tag**

Run: `git describe --tags`
Expected: `v1.0.0` or similar

---

## Task 4: Document Versioning Strategy

**Files:**
- Modify: `README.md` or create `docs/VERSIONING.md`

**Step 1: Add versioning documentation**

Add a section to README.md or create `docs/VERSIONING.md`:

```markdown
## Versioning

This project uses [Semantic Versioning](https://semver.org/) (SemVer):

- **MAJOR** version for incompatible API changes or major milestones
- **MINOR** version for new features (backwards compatible)
- **PATCH** version for bug fixes (backwards compatible)

### Version Locations

Version is maintained in two files:
- `src/HueWindows/HueWindows.csproj` - `<Version>X.Y.Z</Version>`
- `src/HueWindows/Package.appxmanifest` - `<Identity Version="X.Y.Z.0"/>`

### Bumping Version

Use the version bump script:

```powershell
# Patch bump (1.0.0 -> 1.0.1)
.\tools\Bump-Version.ps1

# Minor bump (1.0.0 -> 1.1.0)
.\tools\Bump-Version.ps1 -Type minor

# Major bump (1.0.0 -> 2.0.0)
.\tools\Bump-Version.ps1 -Type major

# Preview changes without applying
.\tools\Bump-Version.ps1 -DryRun
```

After bumping, push with tags to trigger the release workflow:
```bash
git push origin main --tags
```
```

**Step 2: Commit**

```bash
git add README.md  # or docs/VERSIONING.md
git commit -m "docs: add versioning strategy documentation"
```

---

## Task 5: Test Version Bump Flow

**Step 1: Run a patch bump with dry-run**

Run: `powershell -ExecutionPolicy Bypass -File tools/Bump-Version.ps1 -DryRun`
Expected: Shows "Current version: 1.0.0", "New version: 1.0.1"

**Step 2: Verify script works end-to-end (optional)**

If you want to test the full flow:
```powershell
.\tools\Bump-Version.ps1 -Type patch
git push origin main --tags
```

---

## Summary

After completing all tasks:

1. **Bump-Version.ps1 script** that:
   - Reads current version from git tags
   - Increments major/minor/patch
   - Updates .csproj and Package.appxmanifest
   - Creates commit and git tag

2. **Version property** added to .csproj

3. **Initial v1.0.0 tag** created

4. **Documentation** for versioning strategy

**Next phase:** Phase 3 - Release automation and Store deployment
