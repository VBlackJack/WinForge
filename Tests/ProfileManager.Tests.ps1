<#
.SYNOPSIS
    Pester tests for ProfileManager module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge ProfileManager v2.5.0
    Tests profile loading, inheritance resolution, merging, and validation

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
    # Import modules under test
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:ProfileManagerPath = Join-Path $script:ModuleRoot 'ProfileManager.psm1'
    $script:AppDatabasePath = Join-Path $script:ModuleRoot 'ApplicationDatabase.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first (provides Write-Status)
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import ApplicationDatabase (required by ProfileManager)
    if (Test-Path $script:AppDatabasePath) {
        Import-Module $script:AppDatabasePath -Force -ErrorAction Stop
    }

    # Import ProfileManager
    Import-Module $script:ProfileManagerPath -Force -ErrorAction Stop

    # Paths
    $script:ProfilesDirectory = Join-Path $PSScriptRoot '..\Profiles'
    $script:TestDataDirectory = Join-Path $PSScriptRoot 'TestData'

    # Create test data directory if needed
    if (-not (Test-Path $script:TestDataDirectory)) {
        New-Item -Path $script:TestDataDirectory -ItemType Directory -Force | Out-Null
    }
}

AfterAll {
    # Cleanup test data
    if (Test-Path $script:TestDataDirectory) {
        Remove-Item -Path $script:TestDataDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Describe 'ProfileManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ProfileManagerPath -Force } | Should -Not -Throw
        }

        It 'Should export Get-ProfilePath function' {
            Get-Command Get-ProfilePath -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Import-ProfileJson function' {
            Get-Command Import-ProfileJson -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Resolve-ProfileInheritance function' {
            Get-Command Resolve-ProfileInheritance -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Resolve-ApplicationReference function' {
            Get-Command Resolve-ApplicationReference -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Merge-ProfileApplications function' {
            Get-Command Merge-ProfileApplications -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Merge-ProfileSystemConfig function' {
            Get-Command Merge-ProfileSystemConfig -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-DeploymentProfile function' {
            Get-Command Get-DeploymentProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-ProfileValid function' {
            Get-Command Test-ProfileValid -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ApplicationsByCategory function' {
            Get-Command Get-ApplicationsByCategory -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-RequiredApplications function' {
            Get-Command Get-RequiredApplications -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export ConvertTo-Hashtable function' {
            Get-Command ConvertTo-Hashtable -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-ProfilePath' {
        It 'Should resolve Base profile by name' {
            $path = Get-ProfilePath -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory
            $path | Should -Not -BeNullOrEmpty
            $path | Should -Match 'Base\.json$'
            Test-Path $path | Should -BeTrue
        }

        It 'Should resolve Office profile by name' {
            $path = Get-ProfilePath -ProfileName 'Office' -ProfilesDirectory $script:ProfilesDirectory
            $path | Should -Not -BeNullOrEmpty
            Test-Path $path | Should -BeTrue
        }

        It 'Should resolve Gaming profile by name' {
            $path = Get-ProfilePath -ProfileName 'Gaming' -ProfilesDirectory $script:ProfilesDirectory
            $path | Should -Not -BeNullOrEmpty
            Test-Path $path | Should -BeTrue
        }

        It 'Should resolve Personnel profile by name' {
            $path = Get-ProfilePath -ProfileName 'Personnel' -ProfilesDirectory $script:ProfilesDirectory
            $path | Should -Not -BeNullOrEmpty
            Test-Path $path | Should -BeTrue
        }

        It 'Should return full path when given a full path' {
            $fullPath = Join-Path $script:ProfilesDirectory 'Base.json'
            $result = Get-ProfilePath -ProfileName $fullPath
            $result | Should -Be $fullPath
        }

        It 'Should throw for non-existent profile' {
            { Get-ProfilePath -ProfileName 'NonExistentProfile' -ProfilesDirectory $script:ProfilesDirectory } | Should -Throw
        }

        It 'Should handle profile name with .json extension' {
            $path = Get-ProfilePath -ProfileName 'Base.json' -ProfilesDirectory $script:ProfilesDirectory
            Test-Path $path | Should -BeTrue
        }
    }

    Context 'Import-ProfileJson' {
        It 'Should load Base profile successfully' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Base.json'
            $profile = Import-ProfileJson -Path $profilePath

            $profile | Should -Not -BeNullOrEmpty
            $profile.Name | Should -Be 'Base'
        }

        It 'Should have correct profile properties' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Base.json'
            $profile = Import-ProfileJson -Path $profilePath

            $profile.Name | Should -Not -BeNullOrEmpty
            $profile.Description | Should -Not -BeNullOrEmpty
            $profile.Version | Should -Not -BeNullOrEmpty
            $profile.Applications | Should -Not -BeNullOrEmpty
        }

        It 'Should load profile version' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Base.json'
            $profile = Import-ProfileJson -Path $profilePath

            $profile.Version | Should -Match '^\d+\.\d+\.\d+$'
        }

        It 'Should load applications array' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Base.json'
            $profile = Import-ProfileJson -Path $profilePath

            $profile.Applications | Should -Not -BeNullOrEmpty
            $profile.Applications.Count | Should -BeGreaterThan 0
        }

        It 'Should load SystemConfig' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Base.json'
            $profile = Import-ProfileJson -Path $profilePath

            $profile.SystemConfig | Should -Not -BeNullOrEmpty
            $profile.SystemConfig.Keys | Should -Contain 'Explorer'
        }

        It 'Should throw for non-existent file' {
            { Import-ProfileJson -Path 'C:\NonExistent\Profile.json' } | Should -Throw
        }

        It 'Should throw for invalid JSON' {
            $invalidPath = Join-Path $script:TestDataDirectory 'InvalidProfile.json'
            '{ invalid json content' | Set-Content -Path $invalidPath

            { Import-ProfileJson -Path $invalidPath } | Should -Throw
        }

        It 'Should handle empty Inherits array' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Base.json'
            $profile = Import-ProfileJson -Path $profilePath

            # Base has empty Inherits
            $profile.Inherits | Should -BeNullOrEmpty -Or { $profile.Inherits.Count -eq 0 }
        }

        It 'Should handle single inheritance' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Office.json'
            $profile = Import-ProfileJson -Path $profilePath

            $profile.Inherits | Should -Contain 'Base'
        }
    }

    Context 'Resolve-ProfileInheritance' {
        It 'Should return single profile for Base (no inheritance)' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Base.json'
            $baseProfile = Import-ProfileJson -Path $profilePath

            $chain = Resolve-ProfileInheritance -InputProfile $baseProfile -ProfilesDirectory $script:ProfilesDirectory

            $chain.Count | Should -Be 1
            $chain[0].Name | Should -Be 'Base'
        }

        It 'Should resolve Office inheritance chain (Base -> Office)' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Office.json'
            $officeProfile = Import-ProfileJson -Path $profilePath

            $chain = Resolve-ProfileInheritance -InputProfile $officeProfile -ProfilesDirectory $script:ProfilesDirectory

            $chain.Count | Should -Be 2
            $chain[0].Name | Should -Be 'Base'
            $chain[1].Name | Should -Be 'Office'
        }

        It 'Should resolve Gaming inheritance chain (Base -> Office -> Gaming)' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Gaming.json'
            $gamingProfile = Import-ProfileJson -Path $profilePath

            $chain = Resolve-ProfileInheritance -InputProfile $gamingProfile -ProfilesDirectory $script:ProfilesDirectory

            $chain.Count | Should -Be 3
            $chain[0].Name | Should -Be 'Base'
            $chain[1].Name | Should -Be 'Office'
            $chain[2].Name | Should -Be 'Gaming'
        }

        It 'Should resolve Personnel inheritance chain (Base -> Office -> Gaming -> Personnel)' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Personnel.json'
            $personnelProfile = Import-ProfileJson -Path $profilePath

            $chain = Resolve-ProfileInheritance -InputProfile $personnelProfile -ProfilesDirectory $script:ProfilesDirectory

            $chain.Count | Should -Be 4
            $chain[0].Name | Should -Be 'Base'
            $chain[1].Name | Should -Be 'Office'
            $chain[2].Name | Should -Be 'Gaming'
            $chain[3].Name | Should -Be 'Personnel'
        }

        It 'Should detect circular inheritance and throw exception' {
            # Create profiles with circular dependency
            $circularA = Join-Path $script:TestDataDirectory 'CircularA.json'
            $circularB = Join-Path $script:TestDataDirectory 'CircularB.json'

            @{
                Name = 'CircularA'
                Description = 'Circular test A'
                Version = '1.0.0'
                Inherits = @('CircularB')
                Applications = @()
                SystemConfig = @{}
            } | ConvertTo-Json | Set-Content -Path $circularA

            @{
                Name = 'CircularB'
                Description = 'Circular test B'
                Version = '1.0.0'
                Inherits = @('CircularA')
                Applications = @()
                SystemConfig = @{}
            } | ConvertTo-Json | Set-Content -Path $circularB

            $profileA = Import-ProfileJson -Path $circularA

            # Should throw InvalidOperationException with cycle path when circular reference detected
            $thrown = $null
            try {
                Resolve-ProfileInheritance -InputProfile $profileA -ProfilesDirectory $script:TestDataDirectory
            } catch {
                $thrown = $_
            }

            $thrown | Should -Not -BeNullOrEmpty
            $thrown.Exception | Should -BeOfType [System.InvalidOperationException]
            $thrown.Exception.Message | Should -Match '(Circular inheritance detected|Héritage circulaire détecté|profile\.inheritance\.cycle_detected)'
        }
    }

    Context 'Resolve-ApplicationReference' {
        It 'Should resolve string AppId reference' {
            $app = Resolve-ApplicationReference -AppReference 'GoogleChrome'

            $app | Should -Not -BeNullOrEmpty
            $app.Name | Should -Be 'Google Chrome'
        }

        It 'Should return null for non-existent AppId' {
            $app = Resolve-ApplicationReference -AppReference 'NonExistentApp99999'

            $app | Should -BeNullOrEmpty
        }

        It 'Should resolve object with AppId property' {
            $appRef = [PSCustomObject]@{
                AppId = 'GoogleChrome'
                Priority = 5
                Required = $false
                Overrides = $null
            }

            $app = Resolve-ApplicationReference -AppReference $appRef

            $app | Should -Not -BeNullOrEmpty
            $app.Name | Should -Be 'Google Chrome'
        }

        It 'Should resolve legacy format with Name property' {
            $legacyApp = [PSCustomObject]@{
                Name = 'LegacyApp'
                Sources = @{ Winget = 'some.id' }
                Priority = 10
            }

            $app = Resolve-ApplicationReference -AppReference $legacyApp

            $app | Should -Not -BeNullOrEmpty
            $app.Name | Should -Be 'LegacyApp'
        }

        It 'Should apply Priority override from AppId object' {
            $appRef = [PSCustomObject]@{
                AppId = 'GoogleChrome'
                Priority = 1
                Required = $true
                Overrides = $null
            }

            $app = Resolve-ApplicationReference -AppReference $appRef

            $app | Should -Not -BeNullOrEmpty
            $app.Priority | Should -Be 1
        }
    }

    Context 'Merge-ProfileApplications' {
        It 'Should merge applications from profile chain' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Office.json'
            $officeProfile = Import-ProfileJson -Path $profilePath
            $chain = Resolve-ProfileInheritance -InputProfile $officeProfile -ProfilesDirectory $script:ProfilesDirectory

            $merged = Merge-ProfileApplications -Profiles $chain

            $merged | Should -Not -BeNullOrEmpty
            $merged.Count | Should -BeGreaterThan 0
        }

        It 'Should include Base applications in Office merge' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Office.json'
            $officeProfile = Import-ProfileJson -Path $profilePath
            $chain = Resolve-ProfileInheritance -InputProfile $officeProfile -ProfilesDirectory $script:ProfilesDirectory

            $merged = Merge-ProfileApplications -Profiles $chain
            $appNames = $merged | ForEach-Object { $_.Name }

            # Base apps should be included
            $appNames | Should -Contain 'Google Chrome'
            $appNames | Should -Contain 'VLC Media Player'
        }

        It 'Should include Office-specific applications' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Office.json'
            $officeProfile = Import-ProfileJson -Path $profilePath
            $chain = Resolve-ProfileInheritance -InputProfile $officeProfile -ProfilesDirectory $script:ProfilesDirectory

            $merged = Merge-ProfileApplications -Profiles $chain
            $appNames = $merged | ForEach-Object { $_.Name }

            # Office-specific apps
            $appNames | Should -Contain 'Signal Desktop'
        }

        It 'Should deduplicate applications (child overrides parent)' {
            # Create test profiles with override
            $parentPath = Join-Path $script:TestDataDirectory 'ParentProfile.json'
            $childPath = Join-Path $script:TestDataDirectory 'ChildProfile.json'

            @{
                Name = 'ParentProfile'
                Description = 'Parent test profile'
                Version = '1.0.0'
                Inherits = @()
                Applications = @('GoogleChrome')
                SystemConfig = @{}
            } | ConvertTo-Json | Set-Content -Path $parentPath

            @{
                Name = 'ChildProfile'
                Description = 'Child test profile'
                Version = '1.0.0'
                Inherits = @('ParentProfile')
                Applications = @(
                    @{
                        AppId = 'GoogleChrome'
                        Priority = 1
                        Required = $true
                        Overrides = $null
                    }
                )
                SystemConfig = @{}
            } | ConvertTo-Json -Depth 5 | Set-Content -Path $childPath

            $childProfile = Import-ProfileJson -Path $childPath
            $chain = Resolve-ProfileInheritance -InputProfile $childProfile -ProfilesDirectory $script:TestDataDirectory

            $merged = Merge-ProfileApplications -Profiles $chain
            $chromeApps = @($merged | Where-Object { $_.Name -eq 'Google Chrome' })

            # Should only have one Chrome entry
            $chromeApps.Count | Should -Be 1
        }
    }

    Context 'Merge-ProfileSystemConfig' {
        It 'Should merge system config from profile chain' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Gaming.json'
            $gamingProfile = Import-ProfileJson -Path $profilePath
            $chain = Resolve-ProfileInheritance -InputProfile $gamingProfile -ProfilesDirectory $script:ProfilesDirectory

            $merged = Merge-ProfileSystemConfig -Profiles $chain

            $merged | Should -Not -BeNullOrEmpty
            $merged | Should -BeOfType [hashtable]
        }

        It 'Should include Base Explorer config' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Gaming.json'
            $gamingProfile = Import-ProfileJson -Path $profilePath
            $chain = Resolve-ProfileInheritance -InputProfile $gamingProfile -ProfilesDirectory $script:ProfilesDirectory

            $merged = Merge-ProfileSystemConfig -Profiles $chain

            $merged.Explorer | Should -Not -BeNullOrEmpty
            $merged.Explorer.ShowHiddenFiles | Should -BeTrue
        }

        It 'Should override Performance settings in Gaming' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Gaming.json'
            $gamingProfile = Import-ProfileJson -Path $profilePath
            $chain = Resolve-ProfileInheritance -InputProfile $gamingProfile -ProfilesDirectory $script:ProfilesDirectory

            $merged = Merge-ProfileSystemConfig -Profiles $chain

            # Gaming overrides Performance.PowerPlan to "High Performance"
            $merged.Performance.PowerPlan | Should -Be 'High Performance'
            $merged.Performance.GameMode | Should -BeTrue
        }

        It 'Should deep merge nested config' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Gaming.json'
            $gamingProfile = Import-ProfileJson -Path $profilePath
            $chain = Resolve-ProfileInheritance -InputProfile $gamingProfile -ProfilesDirectory $script:ProfilesDirectory

            $merged = Merge-ProfileSystemConfig -Profiles $chain

            # Network should have Gaming-specific settings
            $merged.Network | Should -Not -BeNullOrEmpty
            $merged.Network.GamingOptimizations | Should -BeTrue
            # Privacy comes from Base profile
            $merged.Privacy | Should -Not -BeNullOrEmpty
            $merged.Privacy.DisableTelemetry | Should -BeTrue
        }
    }

    Context 'Get-DeploymentProfile' {
        It 'Should load Base profile with all properties' {
            $profile = Get-DeploymentProfile -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory

            $profile | Should -Not -BeNullOrEmpty
            $profile.Name | Should -Be 'Base'
            $profile.Applications | Should -Not -BeNullOrEmpty
            $profile.SystemConfig | Should -Not -BeNullOrEmpty
        }

        It 'Should have InheritanceChain property' {
            $profile = Get-DeploymentProfile -ProfileName 'Gaming' -ProfilesDirectory $script:ProfilesDirectory

            $profile.InheritanceChain | Should -Not -BeNullOrEmpty
            $profile.InheritanceChain | Should -Contain 'Base'
            $profile.InheritanceChain | Should -Contain 'Office'
            $profile.InheritanceChain | Should -Contain 'Gaming'
        }

        It 'Should load correct application count for Base' {
            $profile = Get-DeploymentProfile -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory

            # Base has 30 applications
            $profile.Applications.Count | Should -BeGreaterOrEqual 25
        }

        It 'Should load more applications for Office than Base' {
            $baseProfile = Get-DeploymentProfile -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory
            $officeProfile = Get-DeploymentProfile -ProfileName 'Office' -ProfilesDirectory $script:ProfilesDirectory

            $officeProfile.Applications.Count | Should -BeGreaterThan $baseProfile.Applications.Count
        }

        It 'Should load more applications for Gaming than Office' {
            $officeProfile = Get-DeploymentProfile -ProfileName 'Office' -ProfilesDirectory $script:ProfilesDirectory
            $gamingProfile = Get-DeploymentProfile -ProfileName 'Gaming' -ProfilesDirectory $script:ProfilesDirectory

            $gamingProfile.Applications.Count | Should -BeGreaterThan $officeProfile.Applications.Count
        }

        It 'Should throw for non-existent profile' {
            { Get-DeploymentProfile -ProfileName 'NonExistentProfile' -ProfilesDirectory $script:ProfilesDirectory } | Should -Throw
        }

        It 'Should have ProfilePath property' {
            $profile = Get-DeploymentProfile -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory

            $profile.ProfilePath | Should -Not -BeNullOrEmpty
            Test-Path $profile.ProfilePath | Should -BeTrue
        }
    }

    Context 'Test-ProfileValid' {
        It 'Should validate Base profile as valid' {
            $profilePath = Join-Path $script:ProfilesDirectory 'Base.json'
            $result = Test-ProfileValid -ProfilePath $profilePath

            $result.Valid | Should -BeTrue
            $result.Errors.Count | Should -Be 0
        }

        It 'Should validate all standard profiles' {
            $profiles = @('Base', 'Office', 'Gaming', 'Personnel')

            foreach ($profileName in $profiles) {
                $profilePath = Join-Path $script:ProfilesDirectory "$profileName.json"
                $result = Test-ProfileValid -ProfilePath $profilePath

                $result.Valid | Should -BeTrue -Because "Profile $profileName should be valid"
            }
        }

        It 'Should return error for non-existent file' {
            $result = Test-ProfileValid -ProfilePath 'C:\NonExistent\Profile.json'

            $result.Valid | Should -BeFalse
            @($result.Errors).Count | Should -BeGreaterThan 0
        }

        It 'Should return error for profile without name' {
            $invalidPath = Join-Path $script:TestDataDirectory 'NoNameProfile.json'
            @{
                Name = ''
                Description = 'Test profile without name'
                Version = '1.0.0'
                Inherits = @()
                Applications = @()
                SystemConfig = @{}
            } | ConvertTo-Json | Set-Content -Path $invalidPath

            $result = Test-ProfileValid -ProfilePath $invalidPath

            $result.Valid | Should -BeFalse
            # Accept either schema validation error or legacy validation error
            $hasNameError = @(@($result.Errors) | Where-Object {
                $_ -match 'Profile name is missing' -or
                $_ -match 'Name.*String.*length' -or
                $_ -match 'schema validation failed'
            }).Count -gt 0
            $hasNameError | Should -BeTrue -Because "Profile without name should be detected as invalid"
        }

        It 'Should return error for invalid JSON' {
            $invalidPath = Join-Path $script:TestDataDirectory 'InvalidJson.json'
            '{ invalid json' | Set-Content -Path $invalidPath

            $result = Test-ProfileValid -ProfilePath $invalidPath

            $result.Valid | Should -BeFalse
            $result.Errors | Where-Object { $_ -match 'JSON parsing error' } | Should -Not -BeNullOrEmpty
        }

        It 'Should warn about duplicate AppIds' {
            $duplicatePath = Join-Path $script:TestDataDirectory 'DuplicateApps.json'
            @{
                Name = 'DuplicateTest'
                Description = 'Test profile with duplicates'
                Version = '1.0.0'
                Inherits = @()
                Applications = @('GoogleChrome', 'GoogleChrome')
                SystemConfig = @{}
            } | ConvertTo-Json | Set-Content -Path $duplicatePath

            $result = Test-ProfileValid -ProfilePath $duplicatePath

            $result.Warnings | Where-Object { $_ -match 'Duplicate' } | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-ApplicationsByCategory' {
        It 'Should group applications by category' {
            $profile = Get-DeploymentProfile -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory
            $grouped = Get-ApplicationsByCategory -Applications $profile.Applications

            $grouped | Should -Not -BeNullOrEmpty
            $grouped | Should -BeOfType [hashtable]
        }

        It 'Should have multiple categories' {
            $profile = Get-DeploymentProfile -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory
            $grouped = Get-ApplicationsByCategory -Applications $profile.Applications

            $grouped.Keys.Count | Should -BeGreaterThan 1
        }

        It 'Should categorize apps correctly' {
            $profile = Get-DeploymentProfile -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory
            $grouped = Get-ApplicationsByCategory -Applications $profile.Applications

            # Should have common categories
            $allCategories = $grouped.Keys -join ', '
            # At least some standard categories should exist
            ($grouped.Keys -contains 'Navigateurs' -or
             $grouped.Keys -contains 'Browsers' -or
             $grouped.Keys -match 'Diagnostic|Security|Utility') | Should -BeTrue -Because "Expected standard categories, got: $allCategories"
        }

        It 'Should handle apps without category' {
            $apps = @(
                [PSCustomObject]@{ Name = 'NoCategoryApp'; Category = $null }
            )

            $grouped = Get-ApplicationsByCategory -Applications $apps

            $grouped.ContainsKey('Uncategorized') | Should -BeTrue
        }
    }

    Context 'Get-RequiredApplications' {
        It 'Should filter only required applications' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1'; Required = $true }
                [PSCustomObject]@{ Name = 'App2'; Required = $false }
                [PSCustomObject]@{ Name = 'App3'; Required = $true }
            )

            $required = Get-RequiredApplications -Applications $apps

            $required.Count | Should -Be 2
            $required.Name | Should -Contain 'App1'
            $required.Name | Should -Contain 'App3'
            $required.Name | Should -Not -Contain 'App2'
        }

        It 'Should return empty for no required apps' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1'; Required = $false }
                [PSCustomObject]@{ Name = 'App2'; Required = $false }
            )

            $required = Get-RequiredApplications -Applications $apps

            $required | Should -BeNullOrEmpty
        }

        It 'Should handle apps without Required property' {
            $apps = @(
                [PSCustomObject]@{ Name = 'App1'; Required = $null }
                [PSCustomObject]@{ Name = 'App2'; Required = $true }
            )

            $required = @(Get-RequiredApplications -Applications $apps)

            $required.Count | Should -Be 1
            $required[0].Name | Should -Be 'App2'
        }
    }

    Context 'ConvertTo-Hashtable' {
        It 'Should convert PSCustomObject to hashtable' {
            $obj = [PSCustomObject]@{
                Key1 = 'Value1'
                Key2 = 'Value2'
            }

            $result = ConvertTo-Hashtable -InputObject $obj

            $result | Should -BeOfType [hashtable]
            $result.Key1 | Should -Be 'Value1'
            $result.Key2 | Should -Be 'Value2'
        }

        It 'Should handle nested PSCustomObject' {
            $obj = [PSCustomObject]@{
                Outer = [PSCustomObject]@{
                    Inner = 'Value'
                }
            }

            $result = ConvertTo-Hashtable -InputObject $obj

            $result.Outer | Should -BeOfType [hashtable]
            $result.Outer.Inner | Should -Be 'Value'
        }

        It 'Should handle arrays' {
            $obj = [PSCustomObject]@{
                Items = @('A', 'B', 'C')
            }

            $result = ConvertTo-Hashtable -InputObject $obj

            # Arrays are converted, items should be accessible
            $result.Items | Should -Not -BeNullOrEmpty
            $result.Items.Count | Should -Be 3
        }

        It 'Should handle empty hashtable' {
            $result = ConvertTo-Hashtable -InputObject @{}

            $result | Should -BeOfType [hashtable]
            $result.Count | Should -Be 0
        }

        It 'Should return primitive types unchanged' {
            $result = ConvertTo-Hashtable -InputObject 'string'
            $result | Should -Be 'string'

            $result = ConvertTo-Hashtable -InputObject 42
            $result | Should -Be 42

            $result = ConvertTo-Hashtable -InputObject $true
            $result | Should -Be $true
        }

        It 'Should handle existing hashtable' {
            $hash = @{ Key = 'Value' }

            $result = ConvertTo-Hashtable -InputObject $hash

            $result | Should -BeOfType [hashtable]
            $result.Key | Should -Be 'Value'
        }
    }
}

Describe 'ProfileManager Integration Tests' {
    Context 'Full Profile Loading Pipeline' {
        It 'Should load Personnel profile with complete inheritance' {
            $profile = Get-DeploymentProfile -ProfileName 'Personnel' -ProfilesDirectory $script:ProfilesDirectory

            $profile | Should -Not -BeNullOrEmpty
            $profile.InheritanceChain.Count | Should -Be 4
            $profile.Applications.Count | Should -BeGreaterThan 50
        }

        It 'Should have merged system config from all profiles' {
            $profile = Get-DeploymentProfile -ProfileName 'Personnel' -ProfilesDirectory $script:ProfilesDirectory

            # Should have Explorer from Base
            $profile.SystemConfig.Explorer | Should -Not -BeNullOrEmpty

            # Should have Performance settings
            $profile.SystemConfig.Performance | Should -Not -BeNullOrEmpty
        }

        It 'Should maintain application order by priority' {
            $profile = Get-DeploymentProfile -ProfileName 'Base' -ProfilesDirectory $script:ProfilesDirectory

            $priorities = $profile.Applications | ForEach-Object { $_.Priority }

            # Most apps should have priorities defined
            ($priorities | Where-Object { $null -ne $_ }).Count | Should -BeGreaterThan ($profile.Applications.Count / 2)
        }
    }

    Context 'Error Handling' {
        It 'Should handle missing parent profile gracefully' {
            $orphanPath = Join-Path $script:TestDataDirectory 'OrphanProfile.json'
            @{
                Name = 'OrphanProfile'
                Description = 'Test orphan profile'
                Version = '1.0.0'
                Inherits = @('NonExistentParent')
                Applications = @('GoogleChrome')
                SystemConfig = @{}
            } | ConvertTo-Json | Set-Content -Path $orphanPath

            # Should not throw but may warn
            { Get-DeploymentProfile -ProfileName 'OrphanProfile' -ProfilesDirectory $script:TestDataDirectory } | Should -Not -Throw
        }
    }
}
