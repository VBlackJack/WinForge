# Add Detection to Proton apps in database

$dbPath = 'Apps/Database/applications.json'
$db = Get-Content $dbPath -Raw | ConvertFrom-Json

# Define Detection for Proton apps (Winget installs to user AppData)
$protonApps = @{
    'ProtonDrive' = @{
        Method = 'Registry'
        Path = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Proton Drive'
        Value = 'DisplayName'
    }
    'ProtonMailBridge' = @{
        Method = 'Registry'
        Path = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Proton Mail Bridge'
        Value = 'DisplayName'
    }
    'ProtonPass' = @{
        Method = 'Registry'
        Path = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Proton Pass'
        Value = 'DisplayName'
    }
}

$fixed = 0

foreach ($appId in $protonApps.Keys) {
    if ($db.Applications.$appId) {
        $detection = $protonApps[$appId]

        # Add Detection if missing
        if (-not $db.Applications.$appId.Detection) {
            $db.Applications.$appId | Add-Member -NotePropertyName 'Detection' -NotePropertyValue ([PSCustomObject]$detection) -Force
            Write-Host "Added Detection to $appId" -ForegroundColor Green
            $fixed++
        } else {
            Write-Host "$appId already has Detection" -ForegroundColor Yellow
        }
    } else {
        Write-Host "$appId not found in database" -ForegroundColor Red
    }
}

# Save updated database
$db | ConvertTo-Json -Depth 10 | Set-Content $dbPath -Encoding UTF8

Write-Host "`nFixed $fixed app(s) in database" -ForegroundColor Cyan
Write-Host "Database saved to: $dbPath" -ForegroundColor Cyan
