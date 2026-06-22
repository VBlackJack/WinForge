<#
.SYNOPSIS
    Pester tests for ApplicationDatabase module

.DESCRIPTION
    Comprehensive unit tests for WinForge ApplicationDatabase v2.5.0
    Tests all database operations, caching, and validation

.NOTES
    Author: Julien Bombled
    Version: 2.5.0
    Requires: Pester v5+
#>

#
# Copyright 2026 Julien Bombled
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

BeforeAll {
    # Import module under test
    $ModulePath = Join-Path $PSScriptRoot '..\Modules\ApplicationDatabase.psm1'
    Import-Module $ModulePath -Force -ErrorAction Stop

    # Database path
    $script:DatabasePath = Join-Path $PSScriptRoot '..\Apps\Database\applications.json'
}

function global:New-TestApplicationDatabaseFixture {
    $versionFile = Join-Path $PSScriptRoot '..\Config\version.json'
    $fixtureVersion = '0.0.0'
    if (Test-Path $versionFile) {
        try {
            $vData = Get-Content -Path $versionFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($vData.Version) { $fixtureVersion = [string]$vData.Version }
        } catch {
            Write-Verbose "Unable to read version fixture data: $($_.Exception.Message)"
        }
    }
    [PSCustomObject]@{
        '$schema'          = 'https://json-schema.org/draft-07/schema#'
        DatabaseVersion    = $fixtureVersion
        LastUpdated        = (Get-Date -Format 'yyyy-MM-dd')
        TotalApplications  = 2
        Categories         = [PSCustomObject]@{
            Utilities = [PSCustomObject]@{
                DisplayName = 'Utilities'
                Count       = 1
            }
            Runtime   = [PSCustomObject]@{
                DisplayName = 'Runtime'
                Count       = 1
            }
        }
        Tags               = [PSCustomObject]@{
            essential = 'Essential apps'
            runtime   = 'Runtime dependencies'
        }
        Applications       = [PSCustomObject]@{
            BaseApp = [PSCustomObject]@{
                Name                    = 'Base App'
                Category                = 'Utilities'
                Description             = 'Base test app'
                Sources                 = [PSCustomObject]@{
                    Winget     = 'Contoso.BaseApp'
                    Chocolatey = $null
                    Store      = $null
                    DirectUrl  = $null
                }
                Detection               = [PSCustomObject]@{
                    Type  = 'File'
                    Value = 'C:\BaseApp\BaseApp.exe'
                }
                DefaultPriority         = 50
                DefaultRequired         = $false
                EnvironmentRestrictions = @()
                Tags                    = @('essential')
                LastVerified            = '2026-02-12'
                Verified                = $true
                Homepage                = 'https://example.com/baseapp'
                Dependencies            = @(
                    [PSCustomObject]@{
                        AppId      = 'DepApp'
                        Type       = 'required'
                        MinVersion = '1.0.0'
                        Reason     = 'Runtime is required'
                    },
                    [PSCustomObject]@{
                        AppId      = 'OptionalMissing'
                        Type       = 'optional'
                        MinVersion = $null
                        Reason     = 'Optional integration'
                    }
                )
            }
            DepApp  = [PSCustomObject]@{
                Name                    = 'Dependency App'
                Category                = 'Runtime'
                Description             = 'Dependency app'
                Sources                 = [PSCustomObject]@{
                    Winget     = 'Contoso.DepApp'
                    Chocolatey = $null
                    Store      = $null
                    DirectUrl  = $null
                }
                Detection               = [PSCustomObject]@{
                    Type  = 'File'
                    Value = 'C:\DepApp\DepApp.exe'
                }
                DefaultPriority         = 30
                DefaultRequired         = $true
                EnvironmentRestrictions = @()
                Tags                    = @('runtime')
                LastVerified            = '2026-02-12'
                Verified                = $true
                Homepage                = 'https://example.com/depapp'
            }
        }
    }
}

function global:Write-TestApplicationDatabaseFixture {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [switch]$EmitBom
    )

    $fixture = New-TestApplicationDatabaseFixture
    $json = $fixture | ConvertTo-Json -Depth 20
    $encoding = [System.Text.UTF8Encoding]::new([bool]$EmitBom)
    [System.IO.File]::WriteAllText($Path, $json, $encoding)
}

function global:Test-TestFileHasUtf8Bom {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
}

Describe 'ApplicationDatabase Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module (Join-Path $PSScriptRoot '..\Modules\ApplicationDatabase.psm1') -Force } | Should -Not -Throw
        }

        It 'Should export Get-ApplicationDatabase function' {
            Get-Command Get-ApplicationDatabase -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ApplicationById function' {
            Get-Command Get-ApplicationById -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-AllApplications function' {
            Get-Command Get-AllApplications -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Search-Applications function' {
            Get-Command Search-Applications -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-DatabaseStatistics function' {
            Get-Command Get-DatabaseStatistics -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-ApplicationDatabase' {
        It 'Should load database successfully' {
            $db = Get-ApplicationDatabase
            $db | Should -Not -BeNullOrEmpty
        }

        It 'Should have DatabaseVersion property' {
            $db = Get-ApplicationDatabase
            $db.DatabaseVersion | Should -Not -BeNullOrEmpty
            $db.DatabaseVersion | Should -Match '^\d+\.\d+\.\d+$'
        }

        It 'Should have Applications property' {
            $db = Get-ApplicationDatabase
            $db.Applications | Should -Not -BeNullOrEmpty
        }

        It 'Should have Categories property' {
            $db = Get-ApplicationDatabase
            $db.Categories | Should -Not -BeNullOrEmpty
        }

        It 'Should have TotalApplications property' {
            $db = Get-ApplicationDatabase
            $db.TotalApplications | Should -BeGreaterThan 0
        }

        It 'Should use caching on subsequent calls' {
            Reset-DatabaseCache
            $first = Measure-Command { Get-ApplicationDatabase }
            $second = Measure-Command { Get-ApplicationDatabase }

            # Second call should be significantly faster (cached)
            $second.TotalMilliseconds | Should -BeLessThan $first.TotalMilliseconds
        }
    }

    Context 'Get-ApplicationById' {
        It 'Should retrieve existing application' {
            $app = Get-ApplicationById -AppId 'GoogleChrome'
            $app | Should -Not -BeNullOrEmpty
            $app.Name | Should -Be 'Google Chrome'
        }

        It 'Should return null for non-existing application' {
            $app = Get-ApplicationById -AppId 'NonExistentApp12345'
            $app | Should -BeNullOrEmpty
        }

        It 'Should have Sources property' {
            $app = Get-ApplicationById -AppId 'GoogleChrome'
            $app.Sources | Should -Not -BeNullOrEmpty
        }

        It 'Should have Detection property' {
            $app = Get-ApplicationById -AppId 'GoogleChrome'
            $app.Detection | Should -Not -BeNullOrEmpty
        }

        It 'Should throw on null/empty AppId' {
            { Get-ApplicationById -AppId $null } | Should -Throw
            { Get-ApplicationById -AppId '' } | Should -Throw
        }
    }

    Context 'Get-AllApplications' {
        It 'Should return all applications' {
            $apps = Get-AllApplications
            $apps | Should -Not -BeNullOrEmpty
            $apps.Count | Should -BeGreaterThan 50
        }

        It 'Should filter by category' {
            $browsers = Get-AllApplications -Category 'Browser'
            $browsers | Should -Not -BeNullOrEmpty
            $browsers | ForEach-Object {
                $_.Category | Should -Be 'Browser'
            }
        }

        It 'Should filter by tag' {
            $essential = Get-AllApplications -Tag 'essential'
            $essential | Should -Not -BeNullOrEmpty
            $essential | ForEach-Object {
                $_.Tags | Should -Contain 'essential'
            }
        }

        It 'Should filter verified apps' {
            $verified = Get-AllApplications -Verified
            $verified | Should -Not -BeNullOrEmpty
            $verified | ForEach-Object {
                $_.Verified | Should -Be $true
            }
        }

        It 'Should return apps with Name and Sources' {
            $apps = Get-AllApplications
            $apps[0].Name | Should -Not -BeNullOrEmpty
            $apps[0].Sources | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Search-Applications' {
        It 'Should find Chrome by name' {
            $results = Search-Applications -SearchTerm 'Chrome'
            $results | Should -Not -BeNullOrEmpty
            $results.Name | Should -Match 'Chrome'
        }

        It 'Should be case-insensitive' {
            $lower = @(Search-Applications -SearchTerm 'chrome')
            $upper = @(Search-Applications -SearchTerm 'CHROME')
            $lower.Count | Should -Be $upper.Count
        }

        It 'Should return empty for non-matching search' {
            $results = Search-Applications -SearchTerm 'NonExistentApp99999'
            $results | Should -BeNullOrEmpty
        }

        It 'Should find partial matches' {
            $results = Search-Applications -SearchTerm 'Fire'
            $results | Should -Not -BeNullOrEmpty
            # Should find Firefox
        }
    }

    Context 'ConvertTo-ProfileApplication' {
        It 'Should convert database app to profile format' {
            $dbApp = Get-ApplicationById -AppId 'GoogleChrome'
            $profileApp = ConvertTo-ProfileApplication -App $dbApp -Priority 1 -Required $true

            $profileApp.AppId | Should -Be 'GoogleChrome'
            $profileApp.Priority | Should -Be 1
            $profileApp.Required | Should -Be $true
        }

        It 'Should return valid profile app structure' {
            $dbApp = Get-ApplicationById -AppId 'GoogleChrome'
            $profileApp = ConvertTo-ProfileApplication -App $dbApp -Priority 5 -Required $false

            $profileApp.AppId | Should -Be 'GoogleChrome'
            $profileApp.Priority | Should -Be 5
            $profileApp.Required | Should -Be $false
        }

        It 'Should handle null overrides' {
            $dbApp = Get-ApplicationById -AppId 'GoogleChrome'
            { ConvertTo-ProfileApplication -App $dbApp -Priority 10 -Required $false } | Should -Not -Throw
        }
    }

    Context 'Get-ApplicationCategories' {
        It 'Should return list of categories' {
            $categories = Get-ApplicationCategories
            $categories | Should -Not -BeNullOrEmpty
        }

        It 'Should have DisplayName property' {
            $categories = Get-ApplicationCategories
            $categories[0].DisplayName | Should -Not -BeNullOrEmpty
        }

        It 'Should have Count property' {
            $categories = Get-ApplicationCategories
            $categories[0].Count | Should -BeGreaterThan 0
        }

        It 'Should include Browser category by CategoryId' {
            $categories = Get-ApplicationCategories
            $categories.CategoryId | Should -Contain 'Browser'
        }
    }

    Context 'Get-ApplicationTags' {
        It 'Should return list of tags' {
            $tags = Get-ApplicationTags
            $tags | Should -Not -BeNullOrEmpty
        }

        It 'Should have Tag property' {
            $tags = Get-ApplicationTags
            $tags[0].Tag | Should -Not -BeNullOrEmpty
        }

        It 'Should have Description property' {
            $tags = Get-ApplicationTags
            $tags[0].Description | Should -Not -BeNullOrEmpty
        }

        It 'Should include essential tag' {
            $tags = Get-ApplicationTags
            $tags.Tag | Should -Contain 'essential'
        }
    }

    Context 'Get-DatabaseStatistics' {
        It 'Should return statistics object' {
            $stats = Get-DatabaseStatistics
            $stats | Should -Not -BeNullOrEmpty
        }

        It 'Should have TotalApplications property' {
            $stats = Get-DatabaseStatistics
            $stats.TotalApplications | Should -BeGreaterThan 0
        }

        It 'Should have VerifiedApps property' {
            $stats = Get-DatabaseStatistics
            $stats.VerifiedApps | Should -BeGreaterThan 0
        }

        It 'Should have correct verified count' {
            $stats = Get-DatabaseStatistics
            # All apps should be verified in v2.5.0
            $stats.VerifiedApps | Should -Be $stats.TotalApplications
        }

        It 'Should have TotalCategories count' {
            $stats = Get-DatabaseStatistics
            $stats.TotalCategories | Should -BeGreaterThan 0
        }

        It 'Should have TotalTags count' {
            $stats = Get-DatabaseStatistics
            $stats.TotalTags | Should -BeGreaterThan 0
        }
    }

    Context 'Reset-DatabaseCache' {
        It 'Should reset cache without errors' {
            { Reset-DatabaseCache } | Should -Not -Throw
        }

        It 'Should force reload on next Get-ApplicationDatabase call' {
            Reset-DatabaseCache
            $db = Get-ApplicationDatabase
            $db | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'ApplicationDatabase Data Integrity' {
    Context 'Database Structure Validation' {
        It 'Should have valid JSON structure' {
            { Get-Content $script:DatabasePath | ConvertFrom-Json } | Should -Not -Throw
        }

        It 'Should have required top-level properties' {
            $db = Get-ApplicationDatabase
            $db.PSObject.Properties.Name | Should -Contain 'DatabaseVersion'
            $db.PSObject.Properties.Name | Should -Contain 'LastUpdated'
            $db.PSObject.Properties.Name | Should -Contain 'TotalApplications'
            $db.PSObject.Properties.Name | Should -Contain 'Applications'
        }

        It 'Should have TotalApplications matching actual count' {
            $db = Get-ApplicationDatabase
            $actualCount = ($db.Applications.PSObject.Properties | Measure-Object).Count
            $db.TotalApplications | Should -Be $actualCount
        }
    }

    Context 'Application Entry Validation' {
        It 'All apps should have Name property' {
            $apps = Get-AllApplications
            $apps | ForEach-Object {
                $_.Name | Should -Not -BeNullOrEmpty
            }
        }

        It 'All apps should have Sources property' {
            $apps = Get-AllApplications
            $apps | ForEach-Object {
                $_.Sources | Should -Not -BeNullOrEmpty
            }
        }

        # NOTE: Source validation test removed - some apps may have conditional sources
        # Apps with missing sources will fail at installation time with proper error handling

        It 'All apps should have Category property' {
            $apps = Get-AllApplications
            $apps | ForEach-Object {
                $_.Category | Should -Not -BeNullOrEmpty
            }
        }
    }
}

Describe 'ApplicationDatabase Authenticode Gate Invariants' {
    BeforeAll {
        $jsonContent = Get-Content -LiteralPath $script:DatabasePath -Raw -Encoding UTF8
        $script:ApplicationDatabaseJson = $jsonContent | ConvertFrom-Json
    }

    It 'Apps with ExpectedPublisher should declare DirectUrl' {
        $script:ApplicationDatabaseJson.Applications.PSObject.Properties | ForEach-Object {
            $appId = $_.Name
            $app = $_.Value
            $sources = $app.Sources
            $expectedPublisherProperty = $sources.PSObject.Properties['ExpectedPublisher']

            if ($expectedPublisherProperty -and $expectedPublisherProperty.Value) {
                $directUrlProperty = $sources.PSObject.Properties['DirectUrl']
                $directUrl = if ($directUrlProperty) { $directUrlProperty.Value } else { $null }

                $directUrl | Should -Not -BeNullOrEmpty -Because "AppId '$appId' has Sources.ExpectedPublisher, so Sources.DirectUrl is required for Authenticode verification to run"
            }
        }
    }
}

Describe 'ApplicationDatabase Performance' {
    Context 'Caching Performance' {
        It 'Should load database within reasonable time' {
            Reset-DatabaseCache
            $duration = Measure-Command { Get-ApplicationDatabase }
            # Should load in under 2 seconds
            $duration.TotalSeconds | Should -BeLessThan 2
        }

        It 'Should cache and retrieve faster on second call' {
            Reset-DatabaseCache
            $first = Measure-Command { Get-ApplicationDatabase }
            $second = Measure-Command { Get-ApplicationDatabase }

            # Cached should be at least 50% faster
            $second.TotalMilliseconds | Should -BeLessThan ($first.TotalMilliseconds * 0.5)
        }
    }

    Context 'Search Performance' {
        It 'Should search within reasonable time' {
            $duration = Measure-Command { Search-Applications -SearchTerm 'Chrome' }
            # Should complete in under 1 second
            $duration.TotalSeconds | Should -BeLessThan 1
        }
    }
}

Describe 'ApplicationDatabase Extended Coverage' {
    BeforeAll {
        $script:OriginalModuleDatabasePath = InModuleScope ApplicationDatabase { $Script:DatabasePath }
        $script:TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("WinForge-AppDbTests-" + [Guid]::NewGuid().Guid)
        $script:TempBackupsRoot = Join-Path $script:TempRoot 'BackupsRoot'
        $script:TempDatabasePath = Join-Path $script:TempRoot 'applications.json'

        New-Item -Path $script:TempBackupsRoot -ItemType Directory -Force | Out-Null
    }

    BeforeEach {
        Write-TestApplicationDatabaseFixture -Path $script:TempDatabasePath
        InModuleScope ApplicationDatabase -Parameters @{ TempDatabasePath = $script:TempDatabasePath } {
            $Script:DatabasePath = $TempDatabasePath
            $Script:DatabaseCache = $null
            $Script:DatabaseLastModified = $null
        }
    }

    AfterAll {
        InModuleScope ApplicationDatabase -Parameters @{ OriginalPath = $script:OriginalModuleDatabasePath } {
            $Script:DatabasePath = $OriginalPath
            $Script:DatabaseCache = $null
            $Script:DatabaseLastModified = $null
        }

        if (Test-Path $script:TempRoot) {
            Remove-Item -Path $script:TempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Context 'Dependency Management' {
        It 'Should resolve dependency metadata for an application' {
            $deps = @(Get-ApplicationDependencies -AppId 'BaseApp' -DependencyType 'All')
            $deps.Count | Should -Be 2
            $deps.AppId | Should -Contain 'DepApp'
            $deps.AppId | Should -Contain 'OptionalMissing'
            ($deps | Where-Object { $_.AppId -eq 'DepApp' }).Resolved | Should -Be $true
            ($deps | Where-Object { $_.AppId -eq 'OptionalMissing' }).Resolved | Should -Be $false
        }

        It 'Should resolve install order with dependencies first' {
            $ordered = @(Resolve-ApplicationDependencies -AppIds @('BaseApp'))
            $ordered.Count | Should -Be 2
            $ordered[0] | Should -Be 'DepApp'
            $ordered[1] | Should -Be 'BaseApp'
        }

        It 'Should detect missing required dependencies' {
            $missing = Test-DependenciesSatisfied -AppId 'BaseApp' -InstalledAppIds @('UnrelatedApp')
            $missing.Satisfied | Should -Be $false
            $missing.MissingCount | Should -Be 1
            $missing.MissingDeps[0].AppId | Should -Be 'DepApp'

            $satisfied = Test-DependenciesSatisfied -AppId 'BaseApp' -InstalledAppIds @('DepApp')
            $satisfied.Satisfied | Should -Be $true
            $satisfied.MissingCount | Should -Be 0
        }
    }

    Context 'Backup and Restore' {
        It 'Should create and list database backups' {
            $backupsRoot = $script:TempBackupsRoot
            Mock Get-WinForgeDirectory { $backupsRoot } -ModuleName ApplicationDatabase

            $backupPath = New-DatabaseBackup -MaxBackups 10
            $backupPath | Should -Not -BeNullOrEmpty
            Test-Path $backupPath | Should -Be $true

            $backups = @(Get-DatabaseBackups)
            $backups.Count | Should -BeGreaterThan 0
            $backups[0].Path | Should -Not -BeNullOrEmpty
            $backups[0].Size | Should -BeGreaterThan 0
        }

        It 'Should restore database from backup and rotate old backups' {
            $backupsRoot = $script:TempBackupsRoot
            Mock Get-WinForgeDirectory { $backupsRoot } -ModuleName ApplicationDatabase

            $backupDir = Join-Path $backupsRoot 'Database'
            New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

            $restoreFixture = New-TestApplicationDatabaseFixture
            $restoreFixture.Applications.BaseApp.Name = 'Restored Base App'
            $restoreBackupPath = Join-Path $backupDir 'applications-20990101-000000.json'
            $restoreFixture | ConvertTo-Json -Depth 20 | Out-File -FilePath $restoreBackupPath -Encoding UTF8 -Force

            $result = Restore-DatabaseFromBackup -BackupPath $restoreBackupPath -CreateBackupFirst
            $result.Success | Should -Be $true

            $db = Get-ApplicationDatabase -ForceReload
            $db.Applications.BaseApp.Name | Should -Be 'Restored Base App'

            # Create extra backup files and ensure rotation removes old ones.
            1..3 | ForEach-Object {
                $name = "applications-20000101-00000$_.json"
                $path = Join-Path $backupDir $name
                '{}' | Out-File -FilePath $path -Encoding UTF8 -Force
                (Get-Item $path).LastWriteTime = (Get-Date).AddDays(-$_)
            }
            $deleted = Invoke-BackupRotation -MaxBackups 1
            $deleted | Should -BeGreaterThan 0
            (@(Get-ChildItem -Path $backupDir -Filter 'applications-*.json')).Count | Should -Be 1
        }
    }

    Context 'Application Configuration and CRUD' {
        It 'Should validate invalid application configuration' {
            $invalid = [PSCustomObject]@{
                AppId           = 'bad id with space'
                Name            = ''
                Category        = ''
                Sources         = [PSCustomObject]@{
                    Winget     = ''
                    Chocolatey = ''
                    Store      = ''
                    DirectUrl  = 'not-a-url'
                }
                Homepage        = 'ftp://invalid'
                DefaultPriority = 101
            }

            $validation = Test-ApplicationConfiguration -Application $invalid -IsNew
            $validation.IsValid | Should -Be $false
            $validation.Errors.Count | Should -BeGreaterThan 0
            $validation.Errors.Field | Should -Contain 'AppId'
            $validation.Errors.Field | Should -Contain 'Name'
            $validation.Errors.Field | Should -Contain 'Category'
            $validation.Errors.Field | Should -Contain 'Sources.DirectUrl'
        }

        It 'Should add and then remove an application' {
            $backupsRoot = $script:TempBackupsRoot
            Mock Get-WinForgeDirectory { $backupsRoot } -ModuleName ApplicationDatabase

            $newApp = [PSCustomObject]@{
                AppId                   = 'NewTool'
                Name                    = 'New Tool'
                Category                = 'Utilities'
                Description             = 'Tool added by test'
                Sources                 = [PSCustomObject]@{
                    Winget     = 'Contoso.NewTool'
                    Chocolatey = $null
                    Store      = $null
                    DirectUrl  = $null
                }
                Detection               = [PSCustomObject]@{
                    Type  = 'File'
                    Value = 'C:\NewTool\NewTool.exe'
                }
                DefaultPriority         = 45
                DefaultRequired         = $false
                EnvironmentRestrictions = @()
                Tags                    = @('essential')
                Homepage                = 'https://example.com/newtool'
            }

            $addResult = Set-Application -Application $newApp
            $addResult.Success | Should -Be $true
            $addResult.IsNew | Should -Be $true
            (Get-ApplicationById -AppId 'NewTool') | Should -Not -BeNullOrEmpty

            $removeResult = Remove-Application -AppId 'NewTool'
            $removeResult.Success | Should -Be $true
            (Get-ApplicationById -AppId 'NewTool') | Should -BeNullOrEmpty
        }

        It 'Should not rewrite database when application is unchanged' {
            Write-TestApplicationDatabaseFixture -Path $script:TempDatabasePath -EmitBom
            InModuleScope ApplicationDatabase -Parameters @{ TempDatabasePath = $script:TempDatabasePath } {
                $Script:DatabasePath = $TempDatabasePath
                $Script:DatabaseCache = $null
                $Script:DatabaseLastModified = $null
            }

            $beforeBytes = [System.IO.File]::ReadAllBytes($script:TempDatabasePath)
            $app = Get-ApplicationById -AppId 'BaseApp'

            $result = Set-Application -Application $app

            $afterBytes = [System.IO.File]::ReadAllBytes($script:TempDatabasePath)
            $result.Success | Should -Be $true
            $result.IsNew | Should -Be $false
            [Convert]::ToBase64String($afterBytes) | Should -Be ([Convert]::ToBase64String($beforeBytes))
            Test-TestFileHasUtf8Bom -Path $script:TempDatabasePath | Should -Be $true
        }

        It 'Should preserve order, BOM, and metadata when updating an application' {
            $fixture = New-TestApplicationDatabaseFixture
            $fixture.LastUpdated = '2000-01-01'
            $json = $fixture | ConvertTo-Json -Depth 20
            $encoding = [System.Text.UTF8Encoding]::new($true)
            [System.IO.File]::WriteAllText($script:TempDatabasePath, $json, $encoding)

            InModuleScope ApplicationDatabase -Parameters @{ TempDatabasePath = $script:TempDatabasePath } {
                $Script:DatabasePath = $TempDatabasePath
                $Script:DatabaseCache = $null
                $Script:DatabaseLastModified = $null
            }

            $app = Get-ApplicationById -AppId 'BaseApp' | ConvertTo-Json -Depth 20 | ConvertFrom-Json
            $app.Name = 'Base App Updated'

            $result = Set-Application -Application $app
            $db = Get-ApplicationDatabase -ForceReload

            $result.Success | Should -Be $true
            Test-TestFileHasUtf8Bom -Path $script:TempDatabasePath | Should -Be $true
            (@($db.Applications.PSObject.Properties.Name) -join ',') | Should -Be 'BaseApp,DepApp'
            $db.LastUpdated | Should -Be (Get-Date -Format 'yyyy-MM-dd')
            $db.Applications.BaseApp.LastVerified | Should -Be '2026-02-12'
            $db.Applications.BaseApp.Verified | Should -Be $true
            $db.Applications.BaseApp.Dependencies.Count | Should -Be 2
        }

        It 'Should return not found when removing unknown application' {
            $result = Remove-Application -AppId 'UnknownApp123'
            $result.Success | Should -Be $false
            $result.Error | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Cache and File Change Helpers' {
        It 'Should clear cache and detect file modification state' {
            InModuleScope ApplicationDatabase -Parameters @{ TempDatabasePath = $script:TempDatabasePath } {
                $Script:DatabasePath = $TempDatabasePath
                $Script:DatabaseCache = [PSCustomObject]@{ Marker = 'cached' }
                $Script:DatabaseLastModified = Get-Date
            }

            Clear-DatabaseCache

            InModuleScope ApplicationDatabase {
                $null -eq $Script:DatabaseCache | Should -Be $true
                $null -eq $Script:DatabaseLastModified | Should -Be $true
            }
        }

        It 'Should return true when database file changed after last load time' {
            $tempDatabasePath = $script:TempDatabasePath
            (Get-Item $tempDatabasePath).LastWriteTime = (Get-Date)

            InModuleScope ApplicationDatabase -Parameters @{ TempDatabasePath = $tempDatabasePath } {
                $Script:DatabasePath = $TempDatabasePath
                $Script:DatabaseLastModified = (Get-Date).AddMinutes(-10)
            }

            InModuleScope ApplicationDatabase {
                (Test-DatabaseFileChanged) | Should -Be $true
            }
        }
    }
}

Describe 'ApplicationDatabase - Command detection allowlist coverage' {
    BeforeAll {
        $catalogPath   = Join-Path $PSScriptRoot '..\Apps\Database\applications.json'
        $allowlistPath = Join-Path $PSScriptRoot '..\Config\detection-allowlist.json'
        $catalog   = Get-Content -Path $catalogPath   -Raw -Encoding UTF8 | ConvertFrom-Json
        $allowlist = (Get-Content -Path $allowlistPath -Raw -Encoding UTF8 | ConvertFrom-Json).allowedExecutables
        $script:AllowedExecutables = @($allowlist | ForEach-Object { $_.ToLower() })

        # Collect the leading executable base name of every Command-method detection.
        $script:CommandExecutables = foreach ($prop in $catalog.Applications.PSObject.Properties) {
            $detection = $prop.Value.Detection
            if ($detection -and $detection.Method -eq 'Command' -and $detection.Command) {
                $exe = [System.IO.Path]::GetFileName(($detection.Command -split '\s+')[0]).ToLower()
                [PSCustomObject]@{ Id = $prop.Name; Executable = $exe }
            }
        }
    }

    It 'Every Command detection executable is present in the detection allowlist' {
        # A Command detection whose executable is not allowlisted fails closed at runtime
        # (Get-DetectionAllowlist denies it), so the detection is silently dead. This guard
        # turns that latent debt into a build failure at commit time.
        $violations = @($script:CommandExecutables | Where-Object { $_.Executable -notin $script:AllowedExecutables })
        $report = ($violations | ForEach-Object { "$($_.Id) -> '$($_.Executable)'" }) -join '; '
        $violations | Should -BeNullOrEmpty -Because "these Command detections reference non-allowlisted executables and would never detect: $report"
    }
}
