<#
.SYNOPSIS
    Builds the Inno Setup installer from the publish folder.
#>
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}
if (-not (Test-Path $iscc)) {
    throw "Inno Setup (ISCC.exe) not found"
}

& $iscc (Join-Path $RepoRoot "installer.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed"
}

Get-ChildItem (Join-Path $RepoRoot "installer-output\*.exe") | ForEach-Object {
    Write-Host "Installer: $($_.FullName) ($([math]::Round($_.Length / 1MB, 1)) MB)"
}