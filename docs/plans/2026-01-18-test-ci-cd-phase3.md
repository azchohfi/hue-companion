# Test & CI/CD System - Phase 3: Release Automation

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create GitHub Actions release workflow triggered by version tags, with MSIX signing and artifact publishing.

**Architecture:** When a `v*.*.*` tag is pushed, the release workflow builds signed MSIX packages for x64/ARM64, creates a GitHub Release with the packages attached, and optionally deploys to Windows Store.

**Tech Stack:** GitHub Actions, PowerShell, MSIX signing, GitHub Releases API

**Reference:** [GitHub Issue #5](https://github.com/ddrayne/hue-windows/issues/5) - Phase 3

---

## Task 1: Create Release Workflow

**Files:**
- Create: `.github/workflows/release.yml`

**Step 1: Create release workflow**

Create `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags:
      - 'v*.*.*'

env:
  DOTNET_VERSION: '8.0.x'

jobs:
  # Job 1: Run tests first
  test:
    runs-on: windows-latest

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Restore dependencies
      run: dotnet restore

    - name: Run tests
      run: dotnet test src/HueWindows.Tests --configuration Release --verbosity normal

  # Job 2: Build release packages
  build:
    runs-on: windows-latest
    needs: test
    strategy:
      matrix:
        platform: [x64, ARM64]

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Extract version from tag
      id: version
      shell: pwsh
      run: |
        $tag = "${{ github.ref_name }}"
        $version = $tag -replace '^v', ''
        echo "VERSION=$version" >> $env:GITHUB_OUTPUT
        echo "Version: $version"

    - name: Restore dependencies
      run: dotnet restore

    - name: Build MSIX (${{ matrix.platform }})
      run: |
        dotnet publish src/HueWindows/HueWindows.csproj `
          --configuration Release `
          -p:Platform=${{ matrix.platform }} `
          -p:Version=${{ steps.version.outputs.VERSION }} `
          --output ./publish/${{ matrix.platform }}

    - name: Upload build artifacts
      uses: actions/upload-artifact@v4
      with:
        name: hue-windows-${{ matrix.platform }}-${{ steps.version.outputs.VERSION }}
        path: ./publish/${{ matrix.platform }}/
        retention-days: 90

  # Job 3: Create GitHub Release
  release:
    runs-on: ubuntu-latest
    needs: build
    permissions:
      contents: write

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Extract version from tag
      id: version
      run: |
        VERSION="${GITHUB_REF_NAME#v}"
        echo "VERSION=$VERSION" >> $GITHUB_OUTPUT

    - name: Download x64 artifacts
      uses: actions/download-artifact@v4
      with:
        name: hue-windows-x64-${{ steps.version.outputs.VERSION }}
        path: ./artifacts/x64

    - name: Download ARM64 artifacts
      uses: actions/download-artifact@v4
      with:
        name: hue-windows-ARM64-${{ steps.version.outputs.VERSION }}
        path: ./artifacts/ARM64

    - name: Create release archive (x64)
      run: |
        cd artifacts/x64
        zip -r ../../HueWindows-${{ steps.version.outputs.VERSION }}-x64.zip .

    - name: Create release archive (ARM64)
      run: |
        cd artifacts/ARM64
        zip -r ../../HueWindows-${{ steps.version.outputs.VERSION }}-ARM64.zip .

    - name: Generate release notes
      id: notes
      run: |
        echo "## What's Changed" > release-notes.md
        echo "" >> release-notes.md
        echo "Release v${{ steps.version.outputs.VERSION }}" >> release-notes.md
        echo "" >> release-notes.md
        echo "### Downloads" >> release-notes.md
        echo "- **x64**: HueWindows-${{ steps.version.outputs.VERSION }}-x64.zip" >> release-notes.md
        echo "- **ARM64**: HueWindows-${{ steps.version.outputs.VERSION }}-ARM64.zip" >> release-notes.md

    - name: Create GitHub Release
      uses: softprops/action-gh-release@v1
      with:
        name: Hue Windows v${{ steps.version.outputs.VERSION }}
        body_path: release-notes.md
        draft: false
        prerelease: false
        files: |
          HueWindows-${{ steps.version.outputs.VERSION }}-x64.zip
          HueWindows-${{ steps.version.outputs.VERSION }}-ARM64.zip
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

**Step 2: Commit workflow**

```bash
git add .github/workflows/release.yml
git commit -m "ci: add release workflow for tag-triggered builds"
```

---

## Task 2: Update Bump-Version Script to Support Release Notes

**Files:**
- Modify: `tools/Bump-Version.ps1`

**Step 1: Add release notes prompt option**

Add a `-ReleaseNotes` parameter that prompts for release notes or accepts them as input.

The updated script should:
- Accept optional `-ReleaseNotes "notes"` parameter
- If provided, include in tag annotation
- Show reminder to push with tags after completion

**Step 2: Commit**

```bash
git add tools/Bump-Version.ps1
git commit -m "feat: add release notes support to version bump script"
```

---

## Task 3: Document Release Process

**Files:**
- Modify: `docs/DEVELOPMENT.md`

**Step 1: Add detailed release section**

Add a section covering:
- Pre-release checklist
- How to create a release
- What happens after pushing a tag
- How to handle failed releases
- Hotfix process

**Step 2: Commit**

```bash
git add docs/DEVELOPMENT.md
git commit -m "docs: expand release process documentation"
```

---

## Task 4: Create Store Deployment Script (Placeholder)

**Files:**
- Create: `tools/Deploy-ToStore.ps1`

**Step 1: Create placeholder script**

Create a script that documents what's needed for Store deployment:

```powershell
<#
.SYNOPSIS
    Deploys HueWindows to the Microsoft Store (placeholder).

.DESCRIPTION
    This script will deploy the application to the Microsoft Store
    using the Partner Center API. Currently a placeholder documenting
    the required setup.

.NOTES
    REQUIRED SETUP:
    1. Create Azure AD application for Partner Center API access
    2. Grant permissions in Partner Center
    3. Set GitHub secrets:
       - STORE_TENANT_ID: Azure AD tenant ID
       - STORE_CLIENT_ID: Partner Center app client ID
       - STORE_CLIENT_SECRET: Partner Center app client secret
    4. Configure app in Partner Center with correct package identity
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$PackagePath,

    [string]$TenantId = $env:STORE_TENANT_ID,
    [string]$ClientId = $env:STORE_CLIENT_ID,
    [string]$ClientSecret = $env:STORE_CLIENT_SECRET
)

Write-Host "=== Store Deployment ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "This is a placeholder script. To enable Store deployment:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Create Azure AD App Registration:" -ForegroundColor White
Write-Host "   https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationsListBlade"
Write-Host ""
Write-Host "2. Configure Partner Center API access:" -ForegroundColor White
Write-Host "   https://partner.microsoft.com/dashboard"
Write-Host ""
Write-Host "3. Add GitHub Secrets:" -ForegroundColor White
Write-Host "   - STORE_TENANT_ID"
Write-Host "   - STORE_CLIENT_ID"
Write-Host "   - STORE_CLIENT_SECRET"
Write-Host ""
Write-Host "4. Update this script with actual API calls" -ForegroundColor White
Write-Host ""
Write-Host "Documentation:" -ForegroundColor Cyan
Write-Host "https://learn.microsoft.com/en-us/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services"
```

**Step 2: Commit**

```bash
git add tools/Deploy-ToStore.ps1
git commit -m "chore: add placeholder Store deployment script with setup docs"
```

---

## Task 5: Test Release Workflow (Dry Run)

**Step 1: Verify workflow syntax**

The workflow will be validated by GitHub when pushed.

**Step 2: Do a test release**

```powershell
# Bump to v1.0.1 for test
.\tools\Bump-Version.ps1

# Push to trigger release workflow
git push origin main --tags
```

**Step 3: Verify in GitHub Actions**

- Check the release workflow runs
- Verify artifacts are created
- Verify GitHub Release is published

---

## Summary

After completing all tasks:

1. **Release workflow** that:
   - Triggers on `v*.*.*` tags
   - Runs tests before building
   - Builds x64 and ARM64 packages in parallel
   - Creates GitHub Release with attached artifacts

2. **Updated version bump script** with release notes support

3. **Expanded documentation** for release process

4. **Store deployment placeholder** documenting required setup

**Future work (Phase 4+):**
- Code signing with certificate
- Actual Store deployment API integration
- Gradual rollout configuration
