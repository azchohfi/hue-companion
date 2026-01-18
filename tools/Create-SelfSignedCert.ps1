<#
.SYNOPSIS
    Creates a self-signed certificate for local MSIX signing.

.DESCRIPTION
    Generates a code signing certificate for testing MSIX packages locally.
    The certificate is stored in the current user's certificate store.

.PARAMETER Subject
    Certificate subject (default: CN=HueWindows-Dev).

.PARAMETER OutputPath
    Path to export the .pfx file (optional).

.PARAMETER Password
    Password for the .pfx file (default: dev-password).

.EXAMPLE
    .\Create-SelfSignedCert.ps1
    # Create cert in store only

.EXAMPLE
    .\Create-SelfSignedCert.ps1 -OutputPath .\dev-cert.pfx
    # Create and export to file
#>

param(
    [string]$Subject = "CN=HueWindows-Dev",
    [string]$OutputPath,
    [string]$Password = "dev-password"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=== Create Self-Signed Certificate ===" -ForegroundColor Cyan
Write-Host ""

# Check if cert already exists
$existingCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject }

if ($existingCert) {
    Write-Host "Certificate already exists: $($existingCert.Thumbprint)" -ForegroundColor Yellow
    $cert = $existingCert
}
else {
    Write-Host "Creating new certificate..." -ForegroundColor Cyan

    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -KeyUsage DigitalSignature `
        -FriendlyName "Hue Companion Development Signing Certificate" `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(5)

    Write-Host "Certificate created: $($cert.Thumbprint)" -ForegroundColor Green
}

# Export if path provided
if ($OutputPath) {
    Write-Host "Exporting to: $OutputPath" -ForegroundColor Cyan

    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $OutputPath -Password $securePassword | Out-Null

    Write-Host "Exported with password: $Password" -ForegroundColor Green
}

Write-Host ""
Write-Host "To use this certificate for MSIX signing, add to Package.appxmanifest:" -ForegroundColor Cyan
Write-Host "  Publisher=`"$Subject`"" -ForegroundColor Gray
Write-Host ""

Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor White
Write-Host ""
