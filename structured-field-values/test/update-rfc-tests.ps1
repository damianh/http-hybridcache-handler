#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates RFC 8941 test files from the official httpwg/structured-field-tests repository.

.DESCRIPTION
    This script clones the httpwg/structured-field-tests repository to a temporary location,
    copies the JSON test files to the RfcTests folder, and cleans up. The JSON files are
    configured to be copied to the output directory for use in xUnit tests.

.PARAMETER Force
    Force update even if RfcTests folder already exists with files.

.EXAMPLE
    .\update-rfc-tests.ps1
    
.EXAMPLE
    .\update-rfc-tests.ps1 -Force
#>

[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Configuration
$repoUrl = "https://github.com/httpwg/structured-field-tests.git"
$targetFolder = Join-Path $PSScriptRoot "RfcTests"
$tempFolder = Join-Path ([System.IO.Path]::GetTempPath()) "structured-field-tests-$(New-Guid)"

try {
    Write-Host "RFC 8941 Test Update Script" -ForegroundColor Cyan
    Write-Host "=============================" -ForegroundColor Cyan
    Write-Host ""

    # Check if git is available
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "Git is not installed or not in PATH. Please install Git to continue."
    }

    # Check if target folder exists and has files
    if ((Test-Path $targetFolder) -and -not $Force) {
        $fileCount = (Get-ChildItem -Path $targetFolder -Filter "*.json" -ErrorAction SilentlyContinue).Count
        if ($fileCount -gt 0) {
            Write-Host "RfcTests folder already contains $fileCount JSON file(s)." -ForegroundColor Yellow
            $response = Read-Host "Do you want to update them? (y/n)"
            if ($response -ne 'y' -and $response -ne 'Y') {
                Write-Host "Update cancelled." -ForegroundColor Yellow
                exit 0
            }
        }
    }

    # Create target folder if it doesn't exist
    if (-not (Test-Path $targetFolder)) {
        Write-Host "Creating RfcTests folder..." -ForegroundColor Green
        New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
    }

    # Clone repository to temp folder
    Write-Host "Cloning structured-field-tests repository..." -ForegroundColor Green
    Write-Host "  Source: $repoUrl" -ForegroundColor Gray
    Write-Host "  Temp:   $tempFolder" -ForegroundColor Gray
    git clone --depth 1 --quiet $repoUrl $tempFolder

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to clone repository. Exit code: $LASTEXITCODE"
    }

    # Get list of JSON files
    $jsonFiles = Get-ChildItem -Path $tempFolder -Filter "*.json" -File

    if ($jsonFiles.Count -eq 0) {
        throw "No JSON files found in the cloned repository."
    }

    Write-Host ""
    Write-Host "Found $($jsonFiles.Count) JSON test file(s):" -ForegroundColor Green
    
    # Copy JSON files
    $copiedCount = 0
    foreach ($file in $jsonFiles) {
        Write-Host "  - $($file.Name)" -ForegroundColor Gray
        Copy-Item -Path $file.FullName -Destination $targetFolder -Force
        $copiedCount++
    }

    # Get commit information
    Push-Location $tempFolder
    try {
        $commitHash = git rev-parse --short HEAD
        $commitDate = git log -1 --format=%cd --date=short
        $commitMessage = git log -1 --format=%s
    }
    finally {
        Pop-Location
    }

    # Create or update README in RfcTests folder
    $readmeContent = @"
# RFC 8941 Official Test Files

This folder contains the official test files from the HTTP Working Group's
structured-field-tests repository.

**Repository:** https://github.com/httpwg/structured-field-tests

## Last Update

- **Date:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
- **Commit:** $commitHash
- **Commit Date:** $commitDate
- **Commit Message:** $commitMessage

## Files

$($jsonFiles | ForEach-Object { "- $($_.Name)" } | Out-String)

## Updating

To update these test files, run:

``````powershell
.\update-rfc-tests.ps1
``````

Or to force update:

``````powershell
.\update-rfc-tests.ps1 -Force
``````

## Usage in Tests

These files are automatically copied to the test output directory and can be
loaded using:

``````csharp
var json = File.ReadAllText("RfcTests/item.json");
var tests = JsonSerializer.Deserialize<RfcTestCase[]>(json);
``````
"@

    $readmePath = Join-Path $targetFolder "README.md"
    Set-Content -Path $readmePath -Value $readmeContent -Encoding UTF8

    Write-Host ""
    Write-Host "Successfully copied $copiedCount file(s) to RfcTests/" -ForegroundColor Green
    Write-Host ""
    Write-Host "Commit Info:" -ForegroundColor Cyan
    Write-Host "  Hash:    $commitHash" -ForegroundColor Gray
    Write-Host "  Date:    $commitDate" -ForegroundColor Gray
    Write-Host "  Message: $commitMessage" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Review the updated files in RfcTests/" -ForegroundColor Gray
    Write-Host "  2. Build the test project to copy files to output" -ForegroundColor Gray
    Write-Host "  3. Run tests to verify RFC compliance" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
finally {
    # Cleanup temp folder
    if (Test-Path $tempFolder) {
        Write-Host "Cleaning up temporary files..." -ForegroundColor Gray
        Remove-Item -Path $tempFolder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Done!" -ForegroundColor Green
