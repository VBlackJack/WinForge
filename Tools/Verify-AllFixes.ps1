Write-Host "=== VERIFICATION COMPLETE DES CORRECTIONS ===" -ForegroundColor Cyan
Write-Host ""

# 1. Verify module versions
Write-Host "[1/3] Verification des versions des modules..." -ForegroundColor Yellow
$modules = @(
    'Modules/ApplicationDatabase.psm1',
    'Modules/EnvironmentDetection.psm1',
    'Modules/Prerequisites.psm1',
    'Modules/ProfileManager.psm1',
    'Modules/StartMenuLayout.psm1',
    'Modules/StartMenuPinning.psm1',
    'Modules/StartupManager.psm1',
    'Modules/SystemConfig.psm1'
)

$versionOK = 0
foreach ($module in $modules) {
    $content = Get-Content $module -Raw
    if ($content -match 'Version:\s*(\d+\.\d+\.\d+)') {
        $version = $matches[1]
        if ($version -eq '2.5.0') {
            $versionOK++
        } else {
            Write-Host "  ✗ $module : $version (should be 2.5.0)" -ForegroundColor Red
        }
    }
}
Write-Host "  ✓ $versionOK/$($modules.Count) modules at v2.5.0" -ForegroundColor Green

# 2. Verify database Detection properties
Write-Host "`n[2/3] Verification de la base de donnees..." -ForegroundColor Yellow
$db = Get-Content 'Apps/Database/applications.json' | ConvertFrom-Json
$protonApps = @('ProtonDrive', 'ProtonMailBridge', 'ProtonPass')

$detectionOK = 0
foreach ($appId in $protonApps) {
    $app = $db.Applications.$appId
    if ($app.Detection -and $app.Detection.Method -eq 'Registry') {
        $detectionOK++
    } else {
        Write-Host "  ✗ $appId : Missing Detection" -ForegroundColor Red
    }
}
Write-Host "  ✓ $detectionOK/$($protonApps.Count) Proton apps have Detection" -ForegroundColor Green

# 3. Verify WindowsSandbox exception
$sandbox = $db.Applications.WindowsSandbox
if ($sandbox.InstallMethod -eq 'WindowsFeature' -and $sandbox.Detection.Method -eq 'WindowsFeature') {
    Write-Host "  ✓ WindowsSandbox correctly configured as Windows Feature" -ForegroundColor Green
} else {
    Write-Host "  ✗ WindowsSandbox configuration issue" -ForegroundColor Red
}

# 4. Run full consistency analysis
Write-Host "`n[3/3] Analyse complete de coherence..." -ForegroundColor Yellow
$result = & "$PSScriptRoot/Analyze-ProjectConsistency.ps1"
if ($result -match "No inconsistencies found") {
    Write-Host "  ✓ Aucune incoherence detectee" -ForegroundColor Green
} else {
    Write-Host "  ✗ Des incoherences subsistent" -ForegroundColor Red
}

Write-Host "`n=== RESULTAT ===" -ForegroundColor Cyan
if ($versionOK -eq 8 -and $detectionOK -eq 3) {
    Write-Host "✓ TOUTES LES CORRECTIONS ONT ETE APPLIQUEES AVEC SUCCES!" -ForegroundColor Green
} else {
    Write-Host "✗ Certaines corrections n'ont pas ete appliquees" -ForegroundColor Red
}
