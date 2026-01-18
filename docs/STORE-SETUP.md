# Microsoft Store Deployment Setup

This guide walks you through configuring automated Microsoft Store deployment for Hue Companion.

## Prerequisites

- Microsoft Partner Center developer account with Hue Companion registered
- Azure subscription (free tier works)
- GitHub repository admin access

## Overview

The release workflow automatically publishes to the Microsoft Store when:
1. A version tag (e.g., `v0.1.0`) is pushed
2. The `STORE_ENABLED` repository variable is set to `true`
3. All required secrets are configured

## Step 1: Create Azure AD Application

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Configure the application:
   - **Name:** `HueCompanion-StorePublish`
   - **Supported account types:** Accounts in this organizational directory only
   - **Redirect URI:** Leave blank
5. Click **Register**
6. On the Overview page, note these values:
   - **Application (client) ID** → This becomes `STORE_CLIENT_ID`
   - **Directory (tenant) ID** → This becomes `STORE_TENANT_ID`

## Step 2: Create Client Secret

1. In your app registration, go to **Certificates & secrets**
2. Click **New client secret**
3. Configure:
   - **Description:** `GitHub Actions`
   - **Expires:** 24 months (recommended)
4. Click **Add**
5. **Immediately copy the secret value** → This becomes `STORE_CLIENT_SECRET`

> **Important:** The secret value is only shown once. If you lose it, you'll need to create a new one.

## Step 3: Link Azure AD App to Partner Center

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Click the gear icon (⚙️) > **Account settings**
3. Navigate to **User management** > **Azure AD applications**
4. Click **Add Azure AD application**
5. Select the application you created (`HueCompanion-StorePublish`)
6. Assign the **Developer** role (minimum required for submissions)
7. Click **Save**

## Step 4: Get Your Product ID

1. In Partner Center, go to **Apps and games**
2. Select **Hue Companion**
3. Navigate to **Product management** > **Product identity**
4. Copy the **Store ID** → This becomes `STORE_PRODUCT_ID`

## Step 5: Configure GitHub Secrets

1. Go to your GitHub repository
2. Navigate to **Settings** > **Secrets and variables** > **Actions**
3. Add these secrets (click **New repository secret** for each):

| Secret Name | Value |
|-------------|-------|
| `STORE_TENANT_ID` | Directory (tenant) ID from Step 1 |
| `STORE_CLIENT_ID` | Application (client) ID from Step 1 |
| `STORE_CLIENT_SECRET` | Secret value from Step 2 |
| `STORE_PRODUCT_ID` | Store ID from Step 4 |

## Step 6: Enable Store Deployment

1. In the same **Secrets and variables** > **Actions** section
2. Switch to the **Variables** tab
3. Click **New repository variable**
4. Add:
   - **Name:** `STORE_ENABLED`
   - **Value:** `true`
5. Click **Save**

## Testing the Setup

To test without creating a real release:

1. Create a pre-release tag:
   ```bash
   git tag v0.0.1-test
   git push origin v0.0.1-test
   ```

2. Watch the workflow in GitHub Actions
3. The `store-publish` job should run and show authentication succeeded
4. Delete the test tag when done:
   ```bash
   git tag -d v0.0.1-test
   git push origin :refs/tags/v0.0.1-test
   ```

## Troubleshooting

### "Authentication failed" Error

- Verify `STORE_TENANT_ID`, `STORE_CLIENT_ID`, and `STORE_CLIENT_SECRET` are correct
- Check if the client secret has expired
- Ensure the Azure AD app is linked to Partner Center with Developer role

### "Product not found" Error

- Verify `STORE_PRODUCT_ID` matches your app in Partner Center
- Ensure you're using the Store ID, not the Package Family Name

### "Package validation failed" Error

- Check the MSIX manifest matches Partner Center identity
- Verify version number is higher than the current Store version
- Review Partner Center for detailed validation errors

### Store Deployment Skipped

- Ensure `STORE_ENABLED` variable is set to `true` (not `True` or `TRUE`)
- Check if all four secrets are configured
- Verify the variable is in **Variables**, not **Secrets**

## Disabling Store Deployment

To temporarily disable Store deployment without removing secrets:

1. Go to **Settings** > **Secrets and variables** > **Actions** > **Variables**
2. Edit `STORE_ENABLED` and set value to `false`
3. Save

Or delete the variable entirely to disable.

## Security Notes

- **Rotate secrets annually:** Azure AD client secrets expire. Create a new secret before expiration.
- **Minimum permissions:** The Azure AD app only needs "Developer" role, not "Manager" or "Owner".
- **Audit access:** Periodically review who has access to Partner Center and the GitHub repository.

## Manual Fallback

If automated deployment fails, you can manually upload:

1. Download the MSIX files from the GitHub Release
2. Go to Partner Center > Your app > **Submissions**
3. Create a new submission
4. Upload the MSIX packages
5. Submit for certification

## Related Documentation

- [Microsoft Store submission API](https://learn.microsoft.com/en-us/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services)
- [Azure AD app registration](https://learn.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app)
- [Partner Center API access](https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-your-partner-center-account)
