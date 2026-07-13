<#
.SYNOPSIS
    Publishes a self-contained win-x64 build of Copilot Session Tracker.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\publish"),
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Project = Join-Path $PSScriptRoot "..\src\CopilotSessionTracker\CopilotSessionTracker.csproj"

if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

dotnet publish $Project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:WindowsAppSDKSelfContained=true `
    -p:WindowsPackageType=None `
    -o $OutputDir `
    --tl:off

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Published to $OutputDir" -ForegroundColor Green
Get-ChildItem (Join-Path $OutputDir "CopilotSessionTracker.exe") | ForEach-Object {
    Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1MB, 1)) MB)"
}