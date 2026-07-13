<#
.SYNOPSIS
    Builds and installs Copilot Session Tracker locally (unsigned).
#>
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "publish"),
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot

& (Join-Path $RepoRoot "scripts\publish.ps1") -OutputDir $PublishDir

$iscc = Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $iscc = $c; break }
    }
}

if (-not $iscc) {
    Write-Host "Inno Setup not found. Installing via winget..." -ForegroundColor Yellow
    winget install JRSoftware.InnoSetup --accept-source-agreements --accept-package-agreements --silent
    foreach ($c in $candidates) {
        if (Test-Path $c) { $iscc = $c; break }
    }
}

if (-not $iscc) {
    throw "Inno Setup (ISCC.exe) not found"
}

& $iscc (Join-Path $RepoRoot "installer.iss") /Q
if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed"
}

$setupExe = Join-Path $RepoRoot "installer-output\CopilotSessionTracker-Setup.exe"
Write-Host "Installer: $setupExe" -ForegroundColor Green

if (-not $SkipInstall) {
    Start-Process -FilePath $setupExe
}