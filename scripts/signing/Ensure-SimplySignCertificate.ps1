<#
.SYNOPSIS
    Authenticates Certum SimplySign Desktop and waits for a code-signing certificate.
#>
param(
    [Parameter(Mandatory)]
    [string]$TotpCode,

    [string]$SimplySignPath = 'C:\Program Files\Certum\SimplySign Desktop\SimplySignDesktop.exe',

    [int]$CertificateWaitSeconds = 60
)

$ErrorActionPreference = "Stop"

function Test-SigningCertAvailable {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -like "*Open Source Developer*" -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) }
    return $null -ne $cert
}

if ($TotpCode -notmatch '^\d{6}$') {
    throw "TOTP code must be exactly 6 digits."
}

if (-not (Test-Path $SimplySignPath)) {
    throw "SimplySign Desktop not found at: $SimplySignPath"
}

$existing = Get-Process -Name "SimplySignDesktop" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing SimplySign process..."
    Stop-Process -Id $existing.Id -Force
    Start-Sleep -Seconds 3
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Win32Window {
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll", CharSet = CharSet.Auto)]
  public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")]
  public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [StructLayout(LayoutKind.Sequential)]
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
Add-Type -AssemblyName System.Windows.Forms

function Find-LoginDialog {
    $script:dialogHwnd = [IntPtr]::Zero
    $script:_procIds = @(Get-Process -Name "SimplySignDesktop" -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })

    [void][Win32Window]::EnumWindows({
        param($hWnd, $lParam)
        $p = [uint32]0
        [Win32Window]::GetWindowThreadProcessId($hWnd, [ref]$p) | Out-Null
        if ($p -in $script:_procIds -and [Win32Window]::IsWindowVisible($hWnd)) {
            $r = New-Object Win32Window+RECT
            [void][Win32Window]::GetWindowRect($hWnd, [ref]$r)
            $w = $r.Right - $r.Left
            $h = $r.Bottom - $r.Top
            if ($w -gt 100 -and $h -gt 100) {
                $script:dialogHwnd = $hWnd
            }
        }
        return $true
    }, [IntPtr]::Zero)

    return ($script:dialogHwnd -ne [IntPtr]::Zero)
}

Write-Host "Launching SimplySign Desktop..."
Start-Process -FilePath $SimplySignPath
Start-Sleep -Seconds 5
Start-Process -FilePath $SimplySignPath

$found = $false
for ($w = 0; $w -lt 15; $w++) {
    Start-Sleep -Seconds 2
    if (Find-LoginDialog) {
        $found = $true
        break
    }
}

if (-not $found) {
    throw "SimplySign login dialog not found."
}

Write-Host "Submitting TOTP..."
[Win32Window]::SetForegroundWindow($script:dialogHwnd) | Out-Null
Start-Sleep -Milliseconds 500
[System.Windows.Forms.SendKeys]::SendWait($TotpCode)
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")

for ($i = 0; $i -lt $CertificateWaitSeconds; $i++) {
    if (Test-SigningCertAvailable) {
        Write-Host "Signing certificate available after ${i}s."
        return
    }
    Start-Sleep -Seconds 1
}

throw "Signing certificate not available within ${CertificateWaitSeconds}s."