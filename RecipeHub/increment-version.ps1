# Portfolio Deployment Script for simply.com
# This script builds the production version and uploads it to your simply.com server

param(
    [switch]$SkipBuild = $false,
    [ValidateSet('patch', 'minor', 'major', 'feature')]
    [string]$BumpType = 'patch'
)

# Auto-increment version
Write-Host "`nBumping version ($BumpType)..." -ForegroundColor Yellow
$packageJsonPath = Join-Path $PSScriptRoot "package.json"
$packageJson = Get-Content $packageJsonPath -Raw | ConvertFrom-Json

# Parse current version
$currentVersion = $packageJson.version
$versionParts = $currentVersion -split '\.'
$major = [int]$versionParts[0]
$minor = [int]$versionParts[1]
$patch = [int]$versionParts[2]

# Increment version based on type
switch ($BumpType) {
    'major' {
        $major++
        $minor = 0
        $patch = 0
    }
    'minor' {
        $minor++
        $patch = 0
    }
    'feature' {
        $minor++
        $patch = 0
    }
    'patch' {
        $patch++
    }
}

$newVersion = "$major.$minor.$patch"

# Update package.json
$packageJson.version = $newVersion
$packageJson | ConvertTo-Json -Depth 100 | Set-Content $packageJsonPath

Write-Host "Version bumped: $currentVersion -> $newVersion" -ForegroundColor Green

# Update version.ts
Write-Host "Updating version.ts..." -ForegroundColor Yellow
$updateVersionScript = Join-Path $PSScriptRoot "scripts/update-version.js"
node $updateVersionScript