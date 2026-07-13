$ErrorActionPreference = "Stop"

$candidates = @(
    "$env:ProgramFiles (x86)\Windows Kits\10\bin\*\x64\signtool.exe",
    "$env:ProgramFiles (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools\*\bin\*\x64\signtool.exe",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\*\Common7\Tools\signtool.exe"
)

foreach ($pattern in $candidates) {
    $match = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($match) {
        return $match.FullName
    }
}

throw "signtool.exe not found. Install the Windows SDK or Visual Studio build tools."