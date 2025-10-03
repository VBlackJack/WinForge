<#
.SYNOPSIS
    Clean up obsolete files from Win11Forge v2.2.0

.DESCRIPTION
    Archives obsolete files created during migration and development to keep the project clean

.PARAMETER DryRun
    Show what would be done without actually moving files

.EXAMPLE
    .\Cleanup-ObsoleteFiles.ps1 -DryRun
    .\Cleanup-ObsoleteFiles.ps1

.NOTES
    Version: 1.0.0
    Safe to run - creates Archive folder with timestamp
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ============================================================================
# CONFIGURATION
# ============================================================================

$ScriptRoot = $PSScriptRoot
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ArchiveRoot = Join-Path $ScriptRoot "Archive"
$ArchiveFolder = Join-Path $ArchiveRoot "Cleanup-$Timestamp"

# Files to archive (relative to project root)
$FilesToArchive = @(
    'Test-NewProfiles.ps1',
    'Switch-ToProduction.ps1',
    'Profiles/Test.json',
    'Profiles/Example-DatabaseStyle.json'
)

# Files to move to specific locations
$FilesToMove = @{
    'Validate-Framework.ps1' = 'Tools/Validate-Framework.ps1'
}

# ============================================================================
# FUNCTIONS
# ============================================================================

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White'
    )
    Write-Host $Message -ForegroundColor $Color
}

function Show-Summary {
    param(
        [int]$Archived,
        [int]$Moved,
        [int]$Skipped
    )

    Write-Host ""
    Write-ColorOutput "═══════════════════════════════════════════════════════════════════" -Color Cyan
    Write-ColorOutput "  📊 CLEANUP SUMMARY" -Color Cyan
    Write-ColorOutput "═══════════════════════════════════════════════════════════════════" -Color Cyan
    Write-Host ""
    Write-Host "  Files archived:  " -NoNewline
    Write-ColorOutput $Archived -Color Green
    Write-Host "  Files relocated: " -NoNewline
    Write-ColorOutput $Moved -Color Green
    Write-Host "  Files skipped:   " -NoNewline
    Write-ColorOutput $Skipped -Color Yellow
    Write-Host ""

    if ($Archived -gt 0 -and -not $DryRun) {
        Write-Host "  Archive location: " -NoNewline -ForegroundColor Yellow
        Write-ColorOutput $ArchiveFolder -Color White
    }

    Write-ColorOutput "═══════════════════════════════════════════════════════════════════" -Color Cyan
    Write-Host ""
}

# ============================================================================
# MAIN LOGIC
# ============================================================================

Write-Host ""
Write-ColorOutput "═══════════════════════════════════════════════════════════════════" -Color Magenta
Write-ColorOutput "  🧹 Win11Forge - Cleanup Obsolete Files" -Color Magenta
Write-ColorOutput "═══════════════════════════════════════════════════════════════════" -Color Magenta
Write-Host ""

if ($DryRun) {
    Write-ColorOutput "🔍 DRY RUN MODE - No files will be modified" -Color Yellow
    Write-Host ""
}

$archivedCount = 0
$movedCount = 0
$skippedCount = 0

# ============================================================================
# ARCHIVE OBSOLETE FILES
# ============================================================================

Write-ColorOutput "📦 Archiving obsolete files..." -Color Cyan
Write-Host ""

foreach ($file in $FilesToArchive) {
    $sourcePath = Join-Path $ScriptRoot $file

    if (Test-Path $sourcePath) {
        $fileName = Split-Path $file -Leaf
        $destinationPath = Join-Path $ArchiveFolder $fileName

        Write-Host "  ➜ " -NoNewline -ForegroundColor Gray
        Write-Host $file -NoNewline -ForegroundColor White

        if ($DryRun) {
            Write-ColorOutput " → [DRY RUN] Would archive to $ArchiveFolder" -Color Yellow
        }
        else {
            try {
                # Create archive folder if needed
                if (-not (Test-Path $ArchiveFolder)) {
                    New-Item -Path $ArchiveFolder -ItemType Directory -Force | Out-Null
                }

                # Move file
                Move-Item -Path $sourcePath -Destination $destinationPath -Force
                Write-ColorOutput " → Archived ✓" -Color Green
                $archivedCount++
            }
            catch {
                Write-ColorOutput " → Failed: $($_.Exception.Message)" -Color Red
                $skippedCount++
            }
        }
    }
    else {
        Write-Host "  ➜ " -NoNewline -ForegroundColor Gray
        Write-Host $file -NoNewline -ForegroundColor White
        Write-ColorOutput " → Not found (already cleaned)" -Color Gray
        $skippedCount++
    }
}

Write-Host ""

# ============================================================================
# RELOCATE FILES
# ============================================================================

if ($FilesToMove.Count -gt 0) {
    Write-ColorOutput "📁 Relocating files..." -Color Cyan
    Write-Host ""

    foreach ($source in $FilesToMove.Keys) {
        $sourcePath = Join-Path $ScriptRoot $source
        $destination = $FilesToMove[$source]
        $destinationPath = Join-Path $ScriptRoot $destination

        if (Test-Path $sourcePath) {
            Write-Host "  ➜ " -NoNewline -ForegroundColor Gray
            Write-Host $source -NoNewline -ForegroundColor White
            Write-Host " → " -NoNewline
            Write-Host $destination -NoNewline -ForegroundColor Cyan

            if ($DryRun) {
                Write-ColorOutput " [DRY RUN]" -Color Yellow
            }
            else {
                try {
                    # Create destination directory
                    $destDir = Split-Path $destinationPath -Parent
                    if (-not (Test-Path $destDir)) {
                        New-Item -Path $destDir -ItemType Directory -Force | Out-Null
                    }

                    # Move file
                    Move-Item -Path $sourcePath -Destination $destinationPath -Force
                    Write-ColorOutput " ✓" -Color Green
                    $movedCount++
                }
                catch {
                    Write-ColorOutput " Failed: $($_.Exception.Message)" -Color Red
                    $skippedCount++
                }
            }
        }
        else {
            Write-Host "  ➜ " -NoNewline -ForegroundColor Gray
            Write-Host $source -NoNewline -ForegroundColor White
            Write-ColorOutput " → Not found (already relocated)" -Color Gray
            $skippedCount++
        }
    }

    Write-Host ""
}

# ============================================================================
# SUMMARY
# ============================================================================

Show-Summary -Archived $archivedCount -Moved $movedCount -Skipped $skippedCount

if ($DryRun) {
    Write-ColorOutput "💡 Run without -DryRun to actually clean up files" -Color Yellow
    Write-Host ""
}
elseif ($archivedCount -gt 0 -or $movedCount -gt 0) {
    Write-ColorOutput "✅ Cleanup completed successfully!" -Color Green
    Write-Host ""
}
else {
    Write-ColorOutput "ℹ️  No files to clean up - project is already clean!" -Color Cyan
    Write-Host ""
}

exit 0
