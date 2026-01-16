<#
.SYNOPSIS
    Bumps the version number for Hue Windows app.

.DESCRIPTION
    This script increments the version number according to semantic versioning,
    updates all relevant files, commits the changes, and creates a git tag.

.PARAMETER Type
    The type of version bump: major, minor, or patch (default: patch)

.PARAMETER DryRun
    Preview changes without making any modifications

.EXAMPLE
    .\Bump-Version.ps1 -Type minor
    Bumps the minor version (e.g., 1.2.3 -> 1.3.0)

.EXAMPLE
    .\Bump-Version.ps1 -Type patch -DryRun
    Preview a patch version bump without making changes
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('major', 'minor', 'patch')]
    [string]$Type = 'patch',
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Define file paths
$rootDir = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $rootDir "src\HueWindows\HueWindows.csproj"
$manifestPath = Join-Path $rootDir "src\HueWindows\Package.appxmanifest"

Write-Host "Hue Windows Version Bumper" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan
Write-Host ""

# Function to get current version from git tags
function Get-CurrentVersion {
    $tags = git tag --list "v*" --sort=-version:refname
    if ($tags) {
        $latestTag = $tags[0]
        $version = $latestTag -replace '^v', ''
        return $version
    }
    
    # Fallback to reading from csproj if no tags exist
    $csprojContent = Get-Content $csprojPath -Raw
    if ($csprojContent -match '<Version>(\d+\.\d+\.\d+)</Version>') {
        return $matches[1]
    }
    
    # Default version if nothing found
    return "0.1.0"
}

# Function to increment version
function Get-NextVersion {
    param([string]$currentVersion, [string]$bumpType)
    
    $parts = $currentVersion -split '\.'
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    
    switch ($bumpType) {
        'major' {
            $major++
            $minor = 0
            $patch = 0
        }
        'minor' {
            $minor++
            $patch = 0
        }
        'patch' {
            $patch++
        }
    }
    
    return "$major.$minor.$patch"
}

# Function to update csproj version
function Update-CsprojVersion {
    param([string]$filePath, [string]$newVersion)
    
    $content = Get-Content $filePath -Raw
    $updated = $content -replace '<Version>(\d+\.\d+\.\d+)</Version>', "<Version>$newVersion</Version>"
    
    if ($content -eq $updated) {
        # Version property doesn't exist, add it
        $updated = $content -replace '(<PropertyGroup>)', "`$1`n    <Version>$newVersion</Version>"
    }
    
    if (-not $DryRun) {
        Set-Content $filePath $updated -NoNewline
        Write-Host "  ✓ Updated $filePath" -ForegroundColor Green
    } else {
        Write-Host "  [DRY RUN] Would update $filePath" -ForegroundColor Yellow
    }
}

# Function to update appxmanifest version
function Update-ManifestVersion {
    param([string]$filePath, [string]$newVersion)
    
    $content = Get-Content $filePath -Raw
    $manifestVersion = "$newVersion.0"  # Manifest requires 4-part version
    $updated = $content -replace 'Version="(\d+\.\d+\.\d+\.\d+)"', "Version=`"$manifestVersion`""
    
    if (-not $DryRun) {
        Set-Content $filePath $updated -NoNewline
        Write-Host "  ✓ Updated $filePath" -ForegroundColor Green
    } else {
        Write-Host "  [DRY RUN] Would update $filePath" -ForegroundColor Yellow
    }
}

# Main execution
try {
    # Check if we're in a git repository
    $gitStatus = git status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Not in a git repository" -ForegroundColor Red
        exit 1
    }
    
    # Check for uncommitted changes
    $changes = git status --porcelain
    if ($changes -and -not $DryRun) {
        Write-Host "Error: You have uncommitted changes. Please commit or stash them first." -ForegroundColor Red
        exit 1
    }
    
    # Get current version
    $currentVersion = Get-CurrentVersion
    Write-Host "Current version: $currentVersion" -ForegroundColor White
    
    # Calculate next version
    $nextVersion = Get-NextVersion -currentVersion $currentVersion -bumpType $Type
    Write-Host "Next version:    $nextVersion ($Type bump)" -ForegroundColor Green
    Write-Host ""
    
    if ($DryRun) {
        Write-Host "DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
        Write-Host ""
    }
    
    # Update version files
    Write-Host "Updating version files..." -ForegroundColor Cyan
    Update-CsprojVersion -filePath $csprojPath -newVersion $nextVersion
    Update-ManifestVersion -filePath $manifestPath -newVersion $nextVersion
    
    if (-not $DryRun) {
        Write-Host ""
        Write-Host "Creating git commit and tag..." -ForegroundColor Cyan
        
        # Stage changes
        git add $csprojPath $manifestPath
        
        # Commit changes
        $commitMessage = "chore: Bump version to $nextVersion"
        git commit -m $commitMessage
        Write-Host "  ✓ Created commit: $commitMessage" -ForegroundColor Green
        
        # Create tag
        $tagName = "v$nextVersion"
        git tag -a $tagName -m "Release $nextVersion"
        Write-Host "  ✓ Created tag: $tagName" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "Version bump complete! 🎉" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "  1. Review the changes: git show" -ForegroundColor White
        Write-Host "  2. Push to GitHub: git push && git push --tags" -ForegroundColor White
        Write-Host "  3. This will trigger the release workflow" -ForegroundColor White
    } else {
        Write-Host ""
        Write-Host "Dry run complete. No changes were made." -ForegroundColor Yellow
    }
}
catch {
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
