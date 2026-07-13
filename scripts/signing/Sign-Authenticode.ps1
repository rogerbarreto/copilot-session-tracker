<#
.SYNOPSIS
    Signs one or more files with the active Authenticode certificate.
#>
param(
    [Parameter(Mandatory, ValueFromRemainingArguments = $true)]
    [string[]]$Path
)

$ErrorActionPreference = "Stop"
$signTool = & (Join-Path $PSScriptRoot "Find-SignTool.ps1")

foreach ($file in $Path) {
    if (-not (Test-Path $file)) {
        throw "File not found: $file"
    }

    Write-Host "Signing $file"
    & $signTool sign /tr http://time.certum.pl /td sha256 /fd sha256 /a $file
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $file"
    }
}