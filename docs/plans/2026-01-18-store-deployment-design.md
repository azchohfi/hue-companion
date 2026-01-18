# Windows Store Deployment Design

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Automate Windows Store deployment using GitHub Actions when a release tag is pushed.

**Architecture:** Use Microsoft's official `store-publish-action` GitHub Action to handle authentication, upload, and submission. Store deployment is opt-in - only runs when secrets are configured.

**Tech Stack:** GitHub Actions, Microsoft Store Publish Action, Azure AD for authentication

---

## Overview

When a version tag (e.g., `v0.1.0`) is pushed:
1. Tests run
2. MSIX packages built for x64 and ARM64
3. GitHub Release created with artifacts
4. **NEW:** If Store secrets configured, submit to Microsoft Store

## Required Secrets

| Secret | Description | Where to find |
|--------|-------------|---------------|
| `STORE_TENANT_ID` | Azure AD tenant ID | Azure Portal > Azure AD > Overview |
| `STORE_CLIENT_ID` | Azure AD app client ID | Azure Portal > App registrations > Your app |
| `STORE_CLIENT_SECRET` | Azure AD app secret | Azure Portal > App registrations > Certificates & secrets |
| `STORE_PRODUCT_ID` | App's Store product ID | Partner Center > Your app > Product identity |

## Azure AD Setup (One-Time)

### Step 1: Create Azure AD Application

1. Go to [Azure Portal](https://portal.azure.com) > Azure Active Directory > App registrations
2. Click "New registration"
3. Name: `HueCompanion-StorePublish`
4. Supported account types: "Accounts in this organizational directory only"
5. Click "Register"
6. Note the **Application (client) ID** - this is `STORE_CLIENT_ID`
7. Note the **Directory (tenant) ID** - this is `STORE_TENANT_ID`

### Step 2: Create Client Secret

1. In your app registration, go to "Certificates & secrets"
2. Click "New client secret"
3. Description: `GitHub Actions`
4. Expiry: 24 months (or your preference)
5. Click "Add"
6. **Copy the secret value immediately** - this is `STORE_CLIENT_SECRET`

### Step 3: Link to Partner Center

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Navigate to Account settings > User management > Azure AD applications
3. Click "Add Azure AD application"
4. Select the application you created
5. Assign the "Developer" role (minimum required for submissions)

### Step 4: Get Product ID

1. In Partner Center, go to your app (Hue Companion)
2. Navigate to Product management > Product identity
3. Copy the **Store ID** - this is `STORE_PRODUCT_ID`

### Step 5: Add Secrets to GitHub

1. Go to your GitHub repo > Settings > Secrets and variables > Actions
2. Add each secret:
   - `STORE_TENANT_ID`
   - `STORE_CLIENT_ID`
   - `STORE_CLIENT_SECRET`
   - `STORE_PRODUCT_ID`

---

## Implementation Tasks

### Task 1: Update Release Workflow

**File:** `.github/workflows/release.yml`

Add Store publishing step after GitHub Release creation:

```yaml
- name: Publish to Microsoft Store
  if: ${{ vars.STORE_ENABLED == 'true' }}
  uses: microsoft/store-publish-action@v1
  with:
    tenant-id: ${{ secrets.STORE_TENANT_ID }}
    client-id: ${{ secrets.STORE_CLIENT_ID }}
    client-secret: ${{ secrets.STORE_CLIENT_SECRET }}
    product-id: ${{ secrets.STORE_PRODUCT_ID }}
    package-path: |
      ./artifacts/HueWindows-*-x64.msix
      ./artifacts/HueWindows-*-ARM64.msix
```

**Note:** Uses repository variable `STORE_ENABLED` as a feature flag. This allows disabling Store deployment without removing secrets.

### Task 2: Build MSIX Packages

Currently the release workflow creates ZIP files. For Store submission, we need proper MSIX packages.

**Update build job to produce MSIX:**

```yaml
- name: Create MSIX package
  run: |
    dotnet publish src/HueWindows/HueWindows.csproj `
      --configuration Release `
      -p:Platform=${{ matrix.platform }} `
      -p:GenerateAppxPackageOnBuild=true `
      -p:AppxPackageDir=./packages/ `
      -p:AppxBundle=Never `
      --output ./publish/${{ matrix.platform }}
```

### Task 3: Update Documentation

**File:** `docs/DEVELOPMENT.md`

Add section "10. Windows Store Deployment" with:
- Overview of automated deployment
- Link to setup guide
- How to enable/disable Store deployment
- Troubleshooting common issues

### Task 4: Create Setup Guide

**File:** `docs/STORE-SETUP.md`

Standalone guide for Azure AD and Partner Center configuration (extracted from this design doc).

---

## Workflow Diagram

```
Push tag v0.1.0
       │
       ▼
┌──────────────┐
│  Run Tests   │
└──────┬───────┘
       │
       ▼
┌──────────────────────────────┐
│  Build MSIX (x64 + ARM64)    │
└──────┬───────────────────────┘
       │
       ▼
┌──────────────────────────────┐
│  Create GitHub Release       │
│  (attach MSIX + ZIP files)   │
└──────┬───────────────────────┘
       │
       ▼
┌──────────────────────────────┐
│  STORE_ENABLED == 'true'?    │
└──────┬───────────────────────┘
       │ yes
       ▼
┌──────────────────────────────┐
│  Publish to Microsoft Store  │
│  (store-publish-action)      │
└──────────────────────────────┘
       │
       ▼
  Store certification
       │
       ▼
  Available in Store
```

## Error Handling

**If Store submission fails:**
- GitHub Release is still created (Store step runs after)
- Workflow logs show error details
- Can manually retry via Partner Center
- Fix issue and push new patch version

**Common issues:**
- Expired client secret → Regenerate in Azure Portal
- Package validation errors → Check MSIX manifest
- Certification failure → Review Partner Center feedback

## Security

- Secrets stored in GitHub encrypted secrets
- Client secret should be rotated annually
- Minimum permissions: "Developer" role in Partner Center
- Store deployment can be disabled via `STORE_ENABLED` variable

---

## Success Criteria

- [ ] Release workflow builds MSIX packages (not just ZIP)
- [ ] Store publish step runs when secrets configured
- [ ] Store publish step skipped gracefully when secrets missing
- [ ] Documentation covers setup and troubleshooting
- [ ] First successful Store submission via CI
