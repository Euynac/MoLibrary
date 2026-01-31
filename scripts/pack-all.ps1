#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packages all Monica framework modules for NuGet distribution.

.DESCRIPTION
    This script finds all Monica.*.csproj files (excluding tests, examples, and rules),
    builds them in Release configuration, and creates NuGet packages in ./artifacts/packages.

.PARAMETER SkipBuild
    Skip the build step and only pack already-built assemblies.

.EXAMPLE
    ./scripts/pack-all.ps1

.EXAMPLE
    ./scripts/pack-all.ps1 -SkipBuild
#>

param(
    [switch]$SkipBuild
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
    Write-Host "=== Monica Framework NuGet Packaging ===" -ForegroundColor Cyan
    Write-Host ""

    # Create output directory
    $outputDir = Join-Path $repoRoot "artifacts" "packages"
    if (Test-Path $outputDir) {
        Write-Host "Cleaning existing packages..." -ForegroundColor Yellow
        Remove-Item -Path $outputDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Host "Output directory: $outputDir" -ForegroundColor Green
    Write-Host ""

    # Find all Monica projects (exclude tests, examples, rules)
    $projects = Get-ChildItem -Path $repoRoot -Filter "Monica.*.csproj" -Recurse |
        Where-Object {
            $_.FullName -notmatch "\\tests\\" -and
            $_.FullName -notmatch "\\examples\\" -and
            $_.FullName -notmatch "\\rules\\"
        } |
        Sort-Object Name

    Write-Host "Found $($projects.Count) projects to package:" -ForegroundColor Cyan
    $projects | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
    Write-Host ""

    # Build if not skipped
    if (-not $SkipBuild) {
        Write-Host "Building all projects in Release configuration..." -ForegroundColor Cyan
        dotnet build -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
        Write-Host "Build completed successfully!" -ForegroundColor Green
        Write-Host ""
    } else {
        Write-Host "Skipping build (using existing binaries)..." -ForegroundColor Yellow
        Write-Host ""
    }

    # Pack each project
    $successCount = 0
    $failedProjects = @()

    foreach ($project in $projects) {
        $projectName = $project.BaseName
        Write-Host "Packing $projectName..." -ForegroundColor Cyan

        # Convert paths for Windows dotnet
        $projectPath = Convert-WslPath $project.FullName
        $outputPath = Convert-WslPath $outputDir

        $packArgs = @(
            "pack"
            $projectPath
            "-c", "Release"
            "-o", $outputPath
            "--no-restore"
        )

        if ($SkipBuild) {
            $packArgs += "--no-build"
        }

        dotnet @packArgs

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ $projectName packed successfully" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "  ✗ $projectName failed to pack" -ForegroundColor Red
            $failedProjects += $projectName
        }
        Write-Host ""
    }

    # Summary
    Write-Host "=== Packaging Summary ===" -ForegroundColor Cyan
    Write-Host "Total projects: $($projects.Count)" -ForegroundColor White
    Write-Host "Successful: $successCount" -ForegroundColor Green
    Write-Host "Failed: $($failedProjects.Count)" -ForegroundColor $(if ($failedProjects.Count -gt 0) { "Red" } else { "Green" })

    if ($failedProjects.Count -gt 0) {
        Write-Host ""
        Write-Host "Failed projects:" -ForegroundColor Red
        $failedProjects | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    }

    Write-Host ""
    Write-Host "Packages location: $outputDir" -ForegroundColor Cyan

    # List generated packages
    $packages = Get-ChildItem -Path $outputDir -Filter "*.nupkg" | Where-Object { $_.Name -notmatch "\.symbols\.nupkg$" }
    Write-Host "Generated $($packages.Count) packages:" -ForegroundColor Green
    $packages | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }

    if ($failedProjects.Count -gt 0) {
        exit 1
    }

} finally {
    Pop-Location
}
