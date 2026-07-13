<#
.SYNOPSIS
    Verifies a code-signing certificate is available on this machine.

.DESCRIPTION
    The self-hosted runner is expected to have SimplySign (or another provider)
    already authenticated before the Release workflow starts. This script only
    checks that a usable certificate is present — it does not drive any login UI.
#>
param(
    [int]$WaitSeconds = 0
)

$ErrorActionPreference = "Stop"

function Get-AvailableSigningCertificate {
    Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

$deadline = (Get-Date).AddSeconds([Math]::Max(0, $WaitSeconds))

do {
    $cert = Get-AvailableSigningCertificate
    if ($cert) {
        Write-Host "Signing certificate ready: $($cert.Subject)"
        Write-Host "  Thumbprint: $($cert.Thumbprint)"
        Write-Host "  Expires:    $($cert.NotAfter)"
        return
    }

    if ($WaitSeconds -gt 0 -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
    }
} while ((Get-Date) -lt $deadline)

throw @"
No code-signing certificate found in Cert:\CurrentUser\My.
Sign in to SimplySign (or your signing provider) on this runner machine, then re-run the workflow.
"@