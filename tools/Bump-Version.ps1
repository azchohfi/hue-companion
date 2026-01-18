<#
.SYNOPSIS
    Bumps the version number for HueWindows releases.

.DESCRIPTION
    Reads the current version from git tags (or defaults to 1.0.0),
    increments based on the specified type (major/minor/patch),
    updates version in .csproj and Package.appxmanifest files,
    and creates a git commit and tag.

.PARAMETER Type
    The type of version bump: major, minor, or patch (default: patch).
    - major: 1.2.3 -> 2.0.0
    - minor: 1.2.3 -> 1.3.0
    - patch: 1.2.3 -> 1.2.4

.PARAMETER DryRun
    Preview changes without modifying files or creating commits/tags.

.EXAMPLE
    .\Bump-Version.ps1
    # Patch bump: 1.0.0 -> 1.0.1

.EXAMPLE
    .\Bump-Version.ps1 -Type minor
    # Minor bump: 1.0.0 -> 1.1.0

.EXAMPLE
    .\Bump-Version.ps1 -Type major -DryRun
    # Preview major bump without making changes
#>

param(
    [ValidateSet("major", "minor", "patch")]
    [string]$Type = "patch",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Project paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$CsprojPath = Join-Path $ProjectRoot "src\HueWindows\HueWindows.csproj"
$ManifestPath = Join-Path $ProjectRoot "src\HueWindows\Package.appxmanifest"

function Get-CurrentVersion {
    <#
    .SYNOPSIS
        Gets the current version from git tags.
    .DESCRIPTION
        Looks for tags matching v*.*.* pattern and returns the latest.
        Returns 1.0.0 if no tags exist.
    #>

    # Get all version tags sorted by version number
    $tags = git tag -l "v*.*.*" 2>$null | ForEach-Object {
        if ($_ -match '^v(\d+)\.(\d+)\.(\d+)$') {
            [PSCustomObject]@{
                Tag = $_
                Major = [int]$Matches[1]
                Minor = [int]$Matches[2]
                Patch = [int]$Matches[3]
            }
        }
    } | Sort-Object Major, Minor, Patch -Descending

    if ($tags -and $tags.Count -gt 0) {
        $latest = $tags[0]
        return @{
            Major = $latest.Major
            Minor = $latest.Minor
            Patch = $latest.Patch
            Tag = $latest.Tag
        }
    }

    # Default version if no tags exist
    return @{
        Major = 1
        Minor = 0
        Patch = 0
        Tag = $null
    }
}

function Get-BumpedVersion {
    param(
        [hashtable]$Current,
        [string]$BumpType
    )

    $new = @{
        Major = $Current.Major
        Minor = $Current.Minor
        Patch = $Current.Patch
    }

    switch ($BumpType) {
        "major" {
            $new.Major++
            $new.Minor = 0
            $new.Patch = 0
        }
        "minor" {
            $new.Minor++
            $new.Patch = 0
        }
        "patch" {
            $new.Patch++
        }
    }

    return $new
}

function Update-CsprojVersion {
    param(
        [string]$Path,
        [string]$Version,
        [switch]$DryRun
    )

    $content = Get-Content $Path -Raw

    # Check if Version element exists
    if ($content -match '<Version>[\d\.]+</Version>') {
        # Update existing Version element
        $newContent = $content -replace '<Version>[\d\.]+</Version>', "<Version>$Version</Version>"
    }
    else {
        # Add Version element after OutputType in first PropertyGroup
        $newContent = $content -replace '(<OutputType>WinExe</OutputType>)', "`$1`n    <Version>$Version</Version>"
    }

    if ($DryRun) {
        Write-Host "  Would update $Path" -ForegroundColor Yellow
        Write-Host "  <Version>$Version</Version>" -ForegroundColor Gray
    }
    else {
        Set-Content -Path $Path -Value $newContent -NoNewline
        Write-Host "  Updated $Path" -ForegroundColor Green
    }
}

function Update-ManifestVersion {
    param(
        [string]$Path,
        [string]$Version,
        [switch]$DryRun
    )

    # Manifest requires 4-part version (X.Y.Z.0)
    $fourPartVersion = "$Version.0"

    $content = Get-Content $Path -Raw

    # Update Version attribute in Identity element
    $newContent = $content -replace '(Version=")[\d\.]+(")', "`${1}$fourPartVersion`$2"

    if ($DryRun) {
        Write-Host "  Would update $Path" -ForegroundColor Yellow
        Write-Host "  Version=`"$fourPartVersion`"" -ForegroundColor Gray
    }
    else {
        Set-Content -Path $Path -Value $newContent -NoNewline
        Write-Host "  Updated $Path" -ForegroundColor Green
    }
}

function New-VersionCommitAndTag {
    param(
        [string]$Version,
        [switch]$DryRun
    )

    $tag = "v$Version"
    $message = "chore: bump version to $Version"

    if ($DryRun) {
        Write-Host "  Would create commit: $message" -ForegroundColor Yellow
        Write-Host "  Would create tag: $tag" -ForegroundColor Yellow
    }
    else {
        # Stage the changed files
        git add $CsprojPath $ManifestPath

        # Create commit
        git commit -m $message
        Write-Host "  Created commit: $message" -ForegroundColor Green

        # Create tag
        git tag $tag
        Write-Host "  Created tag: $tag" -ForegroundColor Green
    }
}

# Main script execution
Write-Host ""
if ($DryRun) {
    Write-Host "=== DRY RUN - No changes will be made ===" -ForegroundColor Magenta
    Write-Host ""
}

# Step 1: Get current version
Write-Host "Reading current version..." -ForegroundColor Cyan
$currentVersion = Get-CurrentVersion

if ($currentVersion.Tag) {
    $currentVersionStr = "$($currentVersion.Major).$($currentVersion.Minor).$($currentVersion.Patch)"
    Write-Host "  Current version: $currentVersionStr (from tag $($currentVersion.Tag))" -ForegroundColor White
}
else {
    Write-Host "  No version tags found, using default: 1.0.0" -ForegroundColor Yellow
    $currentVersionStr = "1.0.0"
}

# Step 2: Calculate new version
Write-Host ""
Write-Host "Calculating new version ($Type bump)..." -ForegroundColor Cyan
$newVersion = Get-BumpedVersion -Current $currentVersion -BumpType $Type
$newVersionStr = "$($newVersion.Major).$($newVersion.Minor).$($newVersion.Patch)"
Write-Host "  New version: $newVersionStr" -ForegroundColor White

# Step 3: Verify files exist
Write-Host ""
Write-Host "Verifying files..." -ForegroundColor Cyan
if (-not (Test-Path $CsprojPath)) {
    Write-Error "Csproj not found: $CsprojPath"
    exit 1
}
if (-not (Test-Path $ManifestPath)) {
    Write-Error "Manifest not found: $ManifestPath"
    exit 1
}
Write-Host "  Files verified" -ForegroundColor Green

# Step 4: Update files
Write-Host ""
Write-Host "Updating version in files..." -ForegroundColor Cyan
Update-CsprojVersion -Path $CsprojPath -Version $newVersionStr -DryRun:$DryRun
Update-ManifestVersion -Path $ManifestPath -Version $newVersionStr -DryRun:$DryRun

# Step 5: Create commit and tag
Write-Host ""
Write-Host "Creating commit and tag..." -ForegroundColor Cyan
New-VersionCommitAndTag -Version $newVersionStr -DryRun:$DryRun

# Done
Write-Host ""
if ($DryRun) {
    Write-Host "=== DRY RUN COMPLETE ===" -ForegroundColor Magenta
    Write-Host "Run without -DryRun to apply changes." -ForegroundColor Gray
}
else {
    Write-Host "Version bump complete: $currentVersionStr -> $newVersionStr" -ForegroundColor Green
    Write-Host "Don't forget to push the tag: git push origin v$newVersionStr" -ForegroundColor Gray
}
Write-Host ""

exit 0
