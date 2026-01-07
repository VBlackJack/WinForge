<#
.SYNOPSIS
    Search for application sources across all package managers

.DESCRIPTION
    Searches Winget, Chocolatey, Microsoft Store for an application and finds direct download URLs

.PARAMETER AppName
    Name of the application to search for

.PARAMETER Interactive
    Run in interactive mode with detailed results

.EXAMPLE
    .\Search-ApplicationSources.ps1 -AppName "Notepad++"

.EXAMPLE
    .\Search-ApplicationSources.ps1 -AppName "Discord" -Interactive

.NOTES
    Author: Julien Bombled
    Version: 1.0.0
    Requires: PowerShell 5.1+, Winget (optional), Chocolatey (optional)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$AppName,

    [Parameter()]
    [switch]$Interactive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

# Import Core module for shared utilities
$scriptRoot = Split-Path -Parent $PSCommandPath
$repositoryRoot = Split-Path -Parent $scriptRoot
$coreModulePath = Join-Path $repositoryRoot 'Core\Core.psm1'
$downloadSourcesPath = Join-Path $repositoryRoot 'Config\download-sources.json'

if (Test-Path $coreModulePath) {
    Import-Module $coreModulePath -Force
}

# Load download sources configuration
$script:DownloadSources = $null
if (Test-Path $downloadSourcesPath) {
    try {
        $script:DownloadSources = Get-Content -Path $downloadSourcesPath -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        Write-Warning "Failed to load download sources config: $($_.Exception.Message)"
    }
}

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White',
        [switch]$NoNewline
    )

    if ($NoNewline) {
        Write-Host $Message -ForegroundColor $Color -NoNewline
    } else {
        Write-Host $Message -ForegroundColor $Color
    }
}

# Test-CommandExists is now imported from Core.psm1

# ============================================================================
# WINGET SEARCH
# ============================================================================

function Search-Winget {
    param([string]$Query)

    Write-ColorOutput "`n🔍 Searching Winget..." -Color Cyan

    if (-not (Test-CommandExists 'winget')) {
        Write-ColorOutput "  ⚠️  Winget not installed" -Color Yellow
        return @()
    }

    try {
        $output = winget search $Query 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "  ❌ Winget search failed" -Color Red
            return @()
        }

        # Parse winget output
        $results = @()
        $lines = $output -split "`n"
        $headerFound = $false

        foreach ($line in $lines) {
            if ($line -match '^Name\s+Id\s+Version') {
                $headerFound = $true
                continue
            }

            if ($headerFound -and $line.Trim() -and $line -notmatch '^-+') {
                # Parse the line (Name, Id, Version are space-separated)
                if ($line -match '^\s*(.+?)\s{2,}([\w\.\-]+)\s+(.+)$') {
                    $results += [PSCustomObject]@{
                        Name    = $matches[1].Trim()
                        Id      = $matches[2].Trim()
                        Version = $matches[3].Trim()
                        Source  = 'Winget'
                    }
                }
            }
        }

        if ($results.Count -gt 0) {
            Write-ColorOutput "  ✅ Found $($results.Count) results" -Color Green

            if ($Interactive) {
                $results | Format-Table -AutoSize | Out-String | Write-Host
            }
        } else {
            Write-ColorOutput "  ℹ️  No results found" -Color Gray
        }

        return $results

    } catch {
        Write-ColorOutput "  ❌ Error: $($_.Exception.Message)" -Color Red
        return @()
    }
}

# ============================================================================
# CHOCOLATEY SEARCH
# ============================================================================

function Search-Chocolatey {
    param([string]$Query)

    Write-ColorOutput "`n🍫 Searching Chocolatey..." -Color Cyan

    if (-not (Test-CommandExists 'choco')) {
        Write-ColorOutput "  ⚠️  Chocolatey not installed" -Color Yellow
        return @()
    }

    try {
        $output = choco search $Query --limit-output 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "  ❌ Chocolatey search failed" -Color Red
            return @()
        }

        # Parse chocolatey output (format: PackageId|Version)
        $results = @()
        $lines = $output -split "`n"

        foreach ($line in $lines) {
            if ($line -match '^(.+?)\|(.+)$') {
                $results += [PSCustomObject]@{
                    Name    = $matches[1].Trim()
                    Id      = $matches[1].Trim()
                    Version = $matches[2].Trim()
                    Source  = 'Chocolatey'
                }
            }
        }

        if ($results.Count -gt 0) {
            Write-ColorOutput "  ✅ Found $($results.Count) results" -Color Green

            if ($Interactive) {
                $results | Format-Table -AutoSize | Out-String | Write-Host
            }
        } else {
            Write-ColorOutput "  ℹ️  No results found" -Color Gray
        }

        return $results

    } catch {
        Write-ColorOutput "  ❌ Error: $($_.Exception.Message)" -Color Red
        return @()
    }
}

# ============================================================================
# MICROSOFT STORE SEARCH
# ============================================================================

function Search-MicrosoftStore {
    param([string]$Query)

    Write-ColorOutput "`n🏪 Searching Microsoft Store..." -Color Cyan

    try {
        # Use winget to search the Microsoft Store
        if (-not (Test-CommandExists 'winget')) {
            Write-ColorOutput "  ⚠️  Winget not installed (required for Store search)" -Color Yellow
            return @()
        }

        $output = winget search $Query --source msstore 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "  ❌ Store search failed" -Color Red
            return @()
        }

        # Parse winget output
        $results = @()
        $lines = $output -split "`n"
        $headerFound = $false

        foreach ($line in $lines) {
            if ($line -match '^Name\s+Id\s+Version') {
                $headerFound = $true
                continue
            }

            if ($headerFound -and $line.Trim() -and $line -notmatch '^-+') {
                if ($line -match '^\s*(.+?)\s{2,}([\w\.\-]+)\s+(.+)$') {
                    $results += [PSCustomObject]@{
                        Name    = $matches[1].Trim()
                        Id      = $matches[2].Trim()
                        Version = $matches[3].Trim()
                        Source  = 'MicrosoftStore'
                    }
                }
            }
        }

        if ($results.Count -gt 0) {
            Write-ColorOutput "  ✅ Found $($results.Count) results" -Color Green

            if ($Interactive) {
                $results | Format-Table -AutoSize | Out-String | Write-Host
            }
        } else {
            Write-ColorOutput "  ℹ️  No results found" -Color Gray
        }

        return $results

    } catch {
        Write-ColorOutput "  ❌ Error: $($_.Exception.Message)" -Color Red
        return @()
    }
}

# ============================================================================
# DIRECT DOWNLOAD SEARCH
# ============================================================================

function Search-DirectDownload {
    param([string]$Query)

    Write-ColorOutput "`n🌐 Searching for Direct Download URLs..." -Color Cyan

    $suggestions = @()

    # Load known patterns from download-sources.json configuration
    $commonPatterns = @{}
    if ($script:DownloadSources -and $script:DownloadSources.directDownloads -and $script:DownloadSources.directDownloads.applications) {
        $apps = $script:DownloadSources.directDownloads.applications
        foreach ($prop in $apps.PSObject.Properties) {
            $commonPatterns[$prop.Name] = $prop.Value.url
        }
    }

    foreach ($pattern in $commonPatterns.GetEnumerator()) {
        if ($Query -match $pattern.Key) {
            $suggestions += [PSCustomObject]@{
                Name = $pattern.Key
                Url  = $pattern.Value
                Type = 'Known Pattern (from config)'
            }
        }
    }

    if ($suggestions.Count -gt 0) {
        Write-ColorOutput "  ✅ Found $($suggestions.Count) known download URLs" -Color Green

        if ($Interactive) {
            $suggestions | Format-Table -AutoSize | Out-String | Write-Host
        }
    } else {
        Write-ColorOutput "  ℹ️  No known direct download URL" -Color Gray
        Write-ColorOutput "  💡 Try searching manually on the official website" -Color Yellow
    }

    return $suggestions
}

# ============================================================================
# MAIN LOGIC
# ============================================================================

Write-ColorOutput "`n═══════════════════════════════════════════════════════════════════" -Color Magenta
Write-ColorOutput "  🔎 Win11Forge - Application Source Search" -Color Magenta
Write-ColorOutput "═══════════════════════════════════════════════════════════════════" -Color Magenta

Write-ColorOutput "`nSearching for: " -Color White -NoNewline
Write-ColorOutput "'$AppName'" -Color Cyan

# Search all sources
$wingetResults = Search-Winget -Query $AppName
$chocoResults = Search-Chocolatey -Query $AppName
$storeResults = Search-MicrosoftStore -Query $AppName
$directResults = Search-DirectDownload -Query $AppName

# ============================================================================
# SUMMARY
# ============================================================================

Write-ColorOutput "`n═══════════════════════════════════════════════════════════════════" -Color Magenta
Write-ColorOutput "  📊 SUMMARY" -Color Magenta
Write-ColorOutput "═══════════════════════════════════════════════════════════════════" -Color Magenta

$wingetCount = if ($wingetResults) { $wingetResults.Count } else { 0 }
$chocoCount = if ($chocoResults) { $chocoResults.Count } else { 0 }
$storeCount = if ($storeResults) { $storeResults.Count } else { 0 }
$directCount = if ($directResults) { $directResults.Count } else { 0 }

$summary = [PSCustomObject]@{
    Winget          = if ($wingetCount -gt 0) { "✅ $wingetCount found" } else { "❌ Not found" }
    Chocolatey      = if ($chocoCount -gt 0) { "✅ $chocoCount found" } else { "❌ Not found" }
    MicrosoftStore  = if ($storeCount -gt 0) { "✅ $storeCount found" } else { "❌ Not found" }
    DirectDownload  = if ($directCount -gt 0) { "✅ $directCount found" } else { "ℹ️  Not available" }
}

$summary | Format-List | Out-String | Write-Host

# ============================================================================
# GENERATE JSON TEMPLATE
# ============================================================================

if ($wingetCount -gt 0 -or $chocoCount -gt 0 -or $storeCount -gt 0) {

    Write-ColorOutput "`n═══════════════════════════════════════════════════════════════════" -Color Magenta
    Write-ColorOutput "  📝 JSON TEMPLATE (Top Result)" -Color Magenta
    Write-ColorOutput "═══════════════════════════════════════════════════════════════════" -Color Magenta

    $topWinget = if ($wingetCount -gt 0) { $wingetResults[0].Id } else { $null }
    $topChoco = if ($chocoCount -gt 0) { $chocoResults[0].Id } else { $null }
    $topStore = if ($storeCount -gt 0) { $storeResults[0].Id } else { $null }
    $topDirect = if ($directCount -gt 0) { $directResults[0].Url } else { $null }

    $appId = $AppName -replace '\s+', ''

    $template = @{
        $appId = @{
            Name                     = $AppName
            Category                 = "TODO"
            Description              = "TODO: Add description"
            Sources                  = @{
                Winget      = $topWinget
                Chocolatey  = $topChoco
                Store       = $topStore
                DirectUrl   = $topDirect
            }
            Detection                = @{
                Method = "TODO: Registry/File/Command/StoreApp"
                Path   = "TODO: Add detection path"
            }
            DefaultPriority          = 99
            DefaultRequired          = $false
            EnvironmentRestrictions  = @()
            Tags                     = @("TODO")
            LastVerified             = (Get-Date -Format "yyyy-MM-dd")
            Verified                 = $false
            Homepage                 = "TODO: Add homepage URL"
        }
    }

    $jsonTemplate = $template | ConvertTo-Json -Depth 10
    Write-Host $jsonTemplate

    # Optionally save to file
    if ($Interactive) {
        Write-ColorOutput "`n💾 Save this template to file? (Y/N): " -Color Yellow -NoNewline
        $save = Read-Host

        if ($save -eq 'Y' -or $save -eq 'y') {
            $outputPath = Join-Path $PSScriptRoot "$appId-template.json"
            $jsonTemplate | Out-File -FilePath $outputPath -Encoding UTF8
            Write-ColorOutput "✅ Template saved to: $outputPath" -Color Green
        }
    }
}

Write-ColorOutput "`n═══════════════════════════════════════════════════════════════════" -Color Magenta
Write-ColorOutput "  ✅ Search Complete!" -Color Green
Write-ColorOutput "═══════════════════════════════════════════════════════════════════`n" -Color Magenta
