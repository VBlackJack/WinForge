<#
.SYNOPSIS
    Pester tests for ApplicationDatabase module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge ApplicationDatabase v2.5.0
    Tests all database operations, caching, and validation

.NOTES
    Author: Win11Forge Team
    Version: 2.5.0
    Requires: Pester v5+
#>

BeforeAll {
    # Import module under test
    $ModulePath = Join-Path $PSScriptRoot '..\Modules\ApplicationDatabase.psm1'
    Import-Module $ModulePath -Force -ErrorAction Stop

    # Database path
    $script:DatabasePath = Join-Path $PSScriptRoot '..\Apps\Database\applications.json'
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
            $lower = Search-Applications -SearchTerm 'chrome'
            $upper = Search-Applications -SearchTerm 'CHROME'
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

        It 'Should have Count property' {
            $tags = Get-ApplicationTags
            $tags[0].Count | Should -BeGreaterThan 0
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

        It 'All apps should have at least one source' {
            $apps = Get-AllApplications
            $apps | ForEach-Object {
                $hasSources = $_.Sources.Winget -or $_.Sources.Chocolatey -or $_.Sources.Store -or $_.Sources.DirectUrl
                $hasSources | Should -Be $true
            }
        }

        It 'All apps should have Category property' {
            $apps = Get-AllApplications
            $apps | ForEach-Object {
                $_.Category | Should -Not -BeNullOrEmpty
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
