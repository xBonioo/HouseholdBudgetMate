param(
    [string]$Configuration = "Release",
    [switch]$SkipPublish,
    [switch]$DesktopShortcut
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$installerProject = Join-Path $repoRoot "src\HouseholdBudgetMate.Installer\HouseholdBudgetMate.Installer.wixproj"

$buildArgs = @(
    "build",
    $installerProject,
    "-c", $Configuration
)

if ($SkipPublish) {
    $buildArgs += "-p:SkipPublish=true"
}

if ($DesktopShortcut) {
    $buildArgs += "-p:ADDDESKTOPSHORTCUT=1"
}

Write-Host "Building MSI installer..." -ForegroundColor Cyan
dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed with exit code $LASTEXITCODE"
}

Write-Host "Done. MSI is in src\HouseholdBudgetMate.Installer\bin\$Configuration" -ForegroundColor Green
