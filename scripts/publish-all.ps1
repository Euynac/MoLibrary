#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publishes all Monica framework packages to NuGet.

.DESCRIPTION
    This script pushes all .nupkg files from artifacts/packages to NuGet.org or a custom NuGet source.

.PARAMETER ApiKey
    NuGet API key for authentication (required).

.PARAMETER Source
    NuGet source URL. Defaults to https://api.nuget.org/v3/index.json

.PARAMETER SkipSymbols
    Skip publishing symbol packages (.snupkg).

.PARAMETER Force
    Skip confirmation prompt.

.EXAMPLE
    ./scripts/publish-all.ps1 -ApiKey "your-api-key"

.EXAMPLE
    ./scripts/publish-all.ps1 -ApiKey "your-api-key" -SkipSymbols -Force
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey,

    [string]$Source = "https://api.nuget.org/v3/index.json",

    [switch]$SkipSymbols,

    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Helper function to convert WSL paths to Windows paths
function Convert-WslPath {
    param([string]$Path)

    if ($Path -match '^/mnt/([a-z])/(.*)$') {
        $drive = $Matches[1].ToUpper()
        $rest = $Matches[2] -replace '/', '\'
        return "${drive}:\${rest}"
    }
    return $Path
}

# Navigate to repository root
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    Write-Host "=== Monica Framework NuGet Publishing ===" -ForegroundColor Cyan
    Write-Host ""

    # Find packages
    $packagesDir = Join-Path $repoRoot "artifacts" "packages"
    if (-not (Test-Path $packagesDir)) {
        throw "Packages directory not found: $packagesDir. Run pack-all.ps1 first."
    }

    $packages = Get-ChildItem -Path $packagesDir -Filter "*.nupkg" |
        Where-Object { $_.Name -notmatch "\.symbols\.nupkg$" } |
        Sort-Object Name

    if ($packages.Count -eq 0) {
        throw "No packages found in $packagesDir. Run pack-all.ps1 first."
    }

    Write-Host "Found $($packages.Count) packages to publish:" -ForegroundColor Cyan
    $packages | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
    Write-Host ""
    Write-Host "Target: $Source" -ForegroundColor Cyan
    Write-Host ""

    # Confirmation
    if (-not $Force) {
        $confirmation = Read-Host "Are you sure you want to publish these packages? (yes/no)"
        if ($confirmation -ne "yes") {
            Write-Host "Publishing cancelled." -ForegroundColor Yellow
            exit 0
        }
        Write-Host ""
    }

    # Publish each package
    $successCount = 0
    $failedPackages = @()

    foreach ($package in $packages) {
        Write-Host "Publishing $($package.Name)..." -ForegroundColor Cyan

        # Convert path for Windows dotnet
        $packagePath = Convert-WslPath $package.FullName

        $pushArgs = @(
            "nuget", "push"
            $packagePath
            "--api-key", $ApiKey
            "--source", $Source
            "--skip-duplicate"
        )

        if ($SkipSymbols) {
            $pushArgs += "--no-symbols"
        }

        dotnet @pushArgs

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ $($package.Name) published successfully" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "  ✗ $($package.Name) failed to publish" -ForegroundColor Red
            $failedPackages += $package.Name
        }
        Write-Host ""
    }

    # Summary
    Write-Host "=== Publishing Summary ===" -ForegroundColor Cyan
    Write-Host "Total packages: $($packages.Count)" -ForegroundColor White
    Write-Host "Successful: $successCount" -ForegroundColor Green
    Write-Host "Failed: $($failedPackages.Count)" -ForegroundColor $(if ($failedPackages.Count -gt 0) { "Red" } else { "Green" })

    if ($failedPackages.Count -gt 0) {
        Write-Host ""
        Write-Host "Failed packages:" -ForegroundColor Red
        $failedPackages | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    }

    Write-Host ""
    Write-Host "All packages published successfully!" -ForegroundColor Green
    Write-Host "View your packages at: https://www.nuget.org/profiles/Momean" -ForegroundColor Cyan

} finally {
    Pop-Location
}
