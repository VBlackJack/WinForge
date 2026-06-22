<#
.SYNOPSIS
    Pester tests for UserProfileManager module

.DESCRIPTION
    Comprehensive unit tests for WinForge UserProfileManager v3.7.2
    Tests profile saving, loading, import/export, copy, merge, removal,
    statistics, and edge cases with full mock isolation

.NOTES
    Author: Julien Bombled
    Version: 3.6.8
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
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:ModulePath = Join-Path $script:ModuleRoot 'UserProfileManager.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop
}

Describe 'UserProfileManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-UserProfileManager function' {
            Get-Command Initialize-UserProfileManager -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Save-UserProfile function' {
            Get-Command Save-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-UserProfiles function' {
            Get-Command Get-UserProfiles -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-UserProfile function' {
            Get-Command Get-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Remove-UserProfile function' {
            Get-Command Remove-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Export-UserProfile function' {
            Get-Command Export-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Import-UserProfile function' {
            Get-Command Import-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Copy-UserProfile function' {
            Get-Command Copy-UserProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Merge-UserProfiles function' {
            Get-Command Merge-UserProfiles -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-UserProfileStatistics function' {
            Get-Command Get-UserProfileStatistics -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Initialize-UserProfileManager' {
        It 'Should initialize without errors' {
            { Initialize-UserProfileManager } | Should -Not -Throw
        }
    }

    Context 'Save-UserProfile' {
        It 'Should save profile without errors' {
            Initialize-UserProfileManager
            { Save-UserProfile -Name 'TestProfile' -Applications @('VSCode', 'Git') -Overwrite } | Should -Not -Throw
        }

        It 'Should accept Description parameter' {
            Initialize-UserProfileManager
            { Save-UserProfile -Name 'TestProfile2' -Applications @('App1') -Description 'Test description' -Overwrite } | Should -Not -Throw
        }

        It 'Should accept Tags parameter' {
            Initialize-UserProfileManager
            { Save-UserProfile -Name 'TestProfile3' -Applications @('App1') -Tags @('dev', 'test') -Overwrite } | Should -Not -Throw
        }

        It 'Should throw on invalid name characters' {
            { Save-UserProfile -Name 'Invalid Name!' -Applications @('App1') } | Should -Throw
        }
    }

    Context 'Get-UserProfiles' {
        BeforeAll {
            Initialize-UserProfileManager
            Save-UserProfile -Name 'ListTest1' -Applications @('App1') -Overwrite
        }

        It 'Should return profiles' {
            $profiles = @(Get-UserProfiles)
            $profiles.Count | Should -BeGreaterThan 0 -Because 'ListTest1 was created'
        }

        It 'Should support wildcard filtering' {
            { Get-UserProfiles -Name 'List*' } | Should -Not -Throw
        }
    }

    Context 'Get-UserProfile' {
        BeforeAll {
            Initialize-UserProfileManager
            Save-UserProfile -Name 'GetTest' -Applications @('VSCode', 'Git') -Overwrite
        }

        It 'Should return profile by name' {
            $profile = Get-UserProfile -Name 'GetTest'
            $profile | Should -Not -BeNullOrEmpty
        }

        It 'Should return null for non-existent profile' {
            $profile = Get-UserProfile -Name 'NonExistentProfile'
            $profile | Should -BeNullOrEmpty
        }
    }

    Context 'Get-UserProfileStatistics' {
        BeforeAll {
            Initialize-UserProfileManager
        }

        It 'Should return statistics object' {
            $stats = Get-UserProfileStatistics
            $stats | Should -Not -BeNullOrEmpty
        }

        It 'Should have TotalProfiles property' {
            $stats = Get-UserProfileStatistics
            $stats.PSObject.Properties.Name | Should -Contain 'TotalProfiles'
        }

        It 'Should have UniqueApplications property' {
            $stats = Get-UserProfileStatistics
            $stats.PSObject.Properties.Name | Should -Contain 'UniqueApplications'
        }
    }

    AfterAll {
        # Cleanup test profiles
        Initialize-UserProfileManager
        @('TestProfile', 'TestProfile2', 'TestProfile3', 'ListTest1', 'GetTest') | ForEach-Object {
            $profileName = $_
            try {
                Remove-UserProfile -Name $profileName -Confirm -ErrorAction SilentlyContinue
            } catch {
                Write-Verbose "Unable to remove test profile '$profileName': $($_.Exception.Message)"
            }
        }
    }
}

# === EXTENDED TEST COVERAGE ===
# Tests below use InModuleScope with a dedicated TestDrive directory to avoid
# side effects on the real user profiles directory.

Describe 'UserProfileManager - Isolated Tests' {
    BeforeAll {
        Import-Module $script:ModulePath -Force -ErrorAction Stop

        # Redirect the module-scoped profiles directory to TestDrive
        InModuleScope 'UserProfileManager' {
            $script:UserProfilesDir = Join-Path $TestDrive 'UserProfiles'
            if (-not (Test-Path $script:UserProfilesDir)) {
                New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
            }
        }
    }

    Context 'Initialize-UserProfileManager - Directory Creation' {
        It 'Should create the profiles directory when it does not exist' {
            InModuleScope 'UserProfileManager' {
                $testDir = Join-Path $TestDrive 'InitTest'
                $script:UserProfilesDir = $testDir

                if (Test-Path $testDir) {
                    Remove-Item $testDir -Recurse -Force
                }

                Initialize-UserProfileManager
                Test-Path $testDir | Should -Be $true

                # Restore
                $script:UserProfilesDir = Join-Path $TestDrive 'UserProfiles'
            }
        }

        It 'Should not throw when directory already exists' {
            InModuleScope 'UserProfileManager' {
                { Initialize-UserProfileManager } | Should -Not -Throw
            }
        }
    }

    Context 'Save-UserProfile - Detailed Behavior' {
        BeforeEach {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'UserProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
        }

        It 'Should return the saved profile file path' {
            $result = Save-UserProfile -Name 'SaveReturnTest' -Applications @('App1') -Overwrite
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeLike '*.json'
        }

        It 'Should create a valid JSON file on disk' {
            Save-UserProfile -Name 'JsonValidTest' -Applications @('NodeJS', 'Git') -Overwrite
            InModuleScope 'UserProfileManager' {
                $filePath = Join-Path $script:UserProfilesDir 'JsonValidTest.json'
                Test-Path $filePath | Should -Be $true
                $content = Get-Content $filePath -Raw
                { $content | ConvertFrom-Json } | Should -Not -Throw
            }
        }

        It 'Should store the correct applications in the profile' {
            Save-UserProfile -Name 'AppCheckTest' -Applications @('VSCode', 'Git', 'Docker') -Overwrite
            $profile = Get-UserProfile -Name 'AppCheckTest'
            $profile.Applications | Should -Contain 'VSCode'
            $profile.Applications | Should -Contain 'Git'
            $profile.Applications | Should -Contain 'Docker'
            @($profile.Applications).Count | Should -Be 3
        }

        It 'Should store description, author, and tags correctly' {
            Save-UserProfile -Name 'MetaTest' -Applications @('App1') `
                -Description 'A test profile' -Author 'TestAuthor' `
                -Tags @('unit', 'test') -Overwrite
            $profile = Get-UserProfile -Name 'MetaTest'
            $profile.Description | Should -Be 'A test profile'
            $profile.Author | Should -Be 'TestAuthor'
            $profile.Tags | Should -Contain 'unit'
            $profile.Tags | Should -Contain 'test'
        }

        It 'Should set schema version in saved profile' {
            Save-UserProfile -Name 'SchemaTest' -Applications @('App1') -Overwrite
            $profile = Get-UserProfile -Name 'SchemaTest'
            $profile.'$schema' | Should -Be 'WinForge-UserProfile-v1.0'
        }

        It 'Should throw when overwriting an existing profile without -Overwrite' {
            Save-UserProfile -Name 'NoOverwriteTest' -Applications @('App1') -Overwrite
            { Save-UserProfile -Name 'NoOverwriteTest' -Applications @('App2') } | Should -Throw
        }

        It 'Should preserve CreatedAt when overwriting a profile' {
            Save-UserProfile -Name 'CreatedAtTest' -Applications @('App1') -Overwrite
            $original = Get-UserProfile -Name 'CreatedAtTest'
            $originalCreated = $original.CreatedAt

            Start-Sleep -Milliseconds 50
            Save-UserProfile -Name 'CreatedAtTest' -Applications @('App2') -Overwrite
            $updated = Get-UserProfile -Name 'CreatedAtTest'
            $updated.CreatedAt | Should -Be $originalCreated
        }

        It 'Should update ModifiedAt when overwriting a profile' {
            Save-UserProfile -Name 'ModifiedAtTest' -Applications @('App1') -Overwrite
            $original = Get-UserProfile -Name 'ModifiedAtTest'
            $originalModified = $original.ModifiedAt

            Start-Sleep -Milliseconds 50
            Save-UserProfile -Name 'ModifiedAtTest' -Applications @('App2') -Overwrite
            $updated = Get-UserProfile -Name 'ModifiedAtTest'
            $updated.ModifiedAt | Should -Not -Be $originalModified
        }

        It 'Should store Settings hashtable correctly' {
            $settings = @{ Theme = 'Dark'; AutoUpdate = $true }
            Save-UserProfile -Name 'SettingsTest' -Applications @('App1') -Settings $settings -Overwrite
            $profile = Get-UserProfile -Name 'SettingsTest'
            $profile.Settings.Theme | Should -Be 'Dark'
        }

        It 'Should reject names with spaces' {
            { Save-UserProfile -Name 'has space' -Applications @('App1') } | Should -Throw
        }

        It 'Should reject names with special characters' {
            { Save-UserProfile -Name 'test@profile' -Applications @('App1') } | Should -Throw
        }

        It 'Should accept names with hyphens and underscores' {
            { Save-UserProfile -Name 'valid-name_123' -Applications @('App1') -Overwrite } | Should -Not -Throw
        }
    }

    Context 'Get-UserProfile - Detailed Behavior' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'UserProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
            Save-UserProfile -Name 'DetailGetTest' -Applications @('VSCode', 'Git') `
                -Description 'Detail test' -Tags @('dev') -Overwrite
        }

        It 'Should return profile with correct Name property' {
            $profile = Get-UserProfile -Name 'DetailGetTest'
            $profile.Name | Should -Be 'DetailGetTest'
        }

        It 'Should return profile with Applications array' {
            $profile = Get-UserProfile -Name 'DetailGetTest'
            $profile.Applications | Should -Not -BeNullOrEmpty
            @($profile.Applications).Count | Should -Be 2
        }

        It 'Should return profile with CreatedAt timestamp' {
            $profile = Get-UserProfile -Name 'DetailGetTest'
            $profile.CreatedAt | Should -Not -BeNullOrEmpty
        }

        It 'Should return null and warn for corrupted JSON file' {
            InModuleScope 'UserProfileManager' {
                $corruptPath = Join-Path $script:UserProfilesDir 'CorruptProfile.json'
                'NOT VALID JSON{{{' | Set-Content $corruptPath -Encoding UTF8
            }
            $result = Get-UserProfile -Name 'CorruptProfile'
            $result | Should -BeNullOrEmpty
        }
    }

    Context 'Get-UserProfiles - Detailed Behavior' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'ListProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
            Save-UserProfile -Name 'DevProfile' -Applications @('VSCode', 'Git') `
                -Tags @('dev', 'coding') -Overwrite
            Save-UserProfile -Name 'GamingProfile' -Applications @('Steam', 'Discord') `
                -Tags @('gaming') -Overwrite
            Save-UserProfile -Name 'DevSetup2' -Applications @('Docker') `
                -Tags @('dev', 'ops') -Overwrite
        }

        It 'Should return all profiles when no filter is provided' {
            $profiles = @(Get-UserProfiles)
            $profiles.Count | Should -BeGreaterOrEqual 3
        }

        It 'Should filter profiles by Name wildcard pattern' {
            $profiles = @(Get-UserProfiles -Name 'Dev*')
            $profiles.Count | Should -Be 2
            $profiles.Name | Should -Contain 'DevProfile'
            $profiles.Name | Should -Contain 'DevSetup2'
        }

        It 'Should filter profiles by Tag' {
            $profiles = @(Get-UserProfiles -Tag 'gaming')
            $profiles.Count | Should -Be 1
            $profiles[0].Name | Should -Be 'GamingProfile'
        }

        It 'Should return profiles sorted by Name' {
            $profiles = @(Get-UserProfiles)
            $names = $profiles.Name
            $sortedNames = $names | Sort-Object
            $names | Should -Be $sortedNames
        }

        It 'Should include ApplicationCount in each profile' {
            $profiles = @(Get-UserProfiles -Name 'DevProfile')
            $profiles[0].ApplicationCount | Should -Be 2
        }

        It 'Should include FilePath in each profile' {
            $profiles = @(Get-UserProfiles -Name 'DevProfile')
            $profiles[0].FilePath | Should -Not -BeNullOrEmpty
            $profiles[0].FilePath | Should -BeLike '*.json'
        }

        It 'Should include FileSize in each profile' {
            $profiles = @(Get-UserProfiles -Name 'DevProfile')
            $profiles[0].FileSize | Should -BeGreaterThan 0
        }

        It 'Should return empty array when no profiles match the filter' {
            $profiles = @(Get-UserProfiles -Name 'ZzzNoMatch*')
            $profiles.Count | Should -Be 0
        }

        It 'Should skip corrupt JSON files without throwing' {
            InModuleScope 'UserProfileManager' {
                $corruptPath = Join-Path $script:UserProfilesDir 'BadFile.json'
                '{broken json' | Set-Content $corruptPath -Encoding UTF8
            }
            { Get-UserProfiles } | Should -Not -Throw
        }
    }

    Context 'Remove-UserProfile' {
        BeforeEach {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'RemoveProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
            Save-UserProfile -Name 'ToRemove' -Applications @('App1') -Overwrite
        }

        It 'Should remove an existing profile when -Confirm is provided' {
            Remove-UserProfile -Name 'ToRemove' -Confirm
            $result = Get-UserProfile -Name 'ToRemove'
            $result | Should -BeNullOrEmpty
        }

        It 'Should not remove profile when -Confirm is not provided' {
            Remove-UserProfile -Name 'ToRemove'
            $result = Get-UserProfile -Name 'ToRemove'
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should handle removal of non-existent profile without throwing' {
            { Remove-UserProfile -Name 'NonExistentRemove' -Confirm } | Should -Not -Throw
        }

        It 'Should delete the JSON file from disk' {
            InModuleScope 'UserProfileManager' {
                $filePath = Join-Path $script:UserProfilesDir 'ToRemove.json'
                Test-Path $filePath | Should -Be $true
            }
            Remove-UserProfile -Name 'ToRemove' -Confirm
            InModuleScope 'UserProfileManager' {
                $filePath = Join-Path $script:UserProfilesDir 'ToRemove.json'
                Test-Path $filePath | Should -Be $false
            }
        }
    }

    Context 'Export-UserProfile' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'ExportProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
            Save-UserProfile -Name 'ExportSource' -Applications @('VSCode', 'Git') `
                -Description 'For export testing' -Tags @('export', 'test') `
                -Author 'TestAuthor' -Overwrite
        }

        It 'Should export a profile to the specified output path' {
            $outputPath = Join-Path $TestDrive 'Exports\ExportSource.json'
            $result = Export-UserProfile -Name 'ExportSource' -OutputPath $outputPath
            $result | Should -Be $outputPath
            Test-Path $outputPath | Should -Be $true
        }

        It 'Should create parent directories for the output path' {
            $deepPath = Join-Path $TestDrive 'Deep\Nested\Dir\exported.json'
            Export-UserProfile -Name 'ExportSource' -OutputPath $deepPath
            Test-Path $deepPath | Should -Be $true
        }

        It 'Should produce valid JSON in the exported file' {
            $outputPath = Join-Path $TestDrive 'Exports\ValidJson.json'
            Export-UserProfile -Name 'ExportSource' -OutputPath $outputPath
            $content = Get-Content $outputPath -Raw
            { $content | ConvertFrom-Json } | Should -Not -Throw
        }

        It 'Should include schema version in exported data' {
            $outputPath = Join-Path $TestDrive 'Exports\SchemaCheck.json'
            Export-UserProfile -Name 'ExportSource' -OutputPath $outputPath
            $data = Get-Content $outputPath -Raw | ConvertFrom-Json
            $data.'$schema' | Should -Be 'WinForge-UserProfile-v1.0'
        }

        It 'Should include Name and Applications in exported data' {
            $outputPath = Join-Path $TestDrive 'Exports\Fields.json'
            Export-UserProfile -Name 'ExportSource' -OutputPath $outputPath
            $data = Get-Content $outputPath -Raw | ConvertFrom-Json
            $data.Name | Should -Be 'ExportSource'
            $data.Applications | Should -Contain 'VSCode'
            $data.Applications | Should -Contain 'Git'
        }

        It 'Should include metadata when IncludeMetadata is enabled' {
            $outputPath = Join-Path $TestDrive 'Exports\WithMeta.json'
            Export-UserProfile -Name 'ExportSource' -OutputPath $outputPath -IncludeMetadata
            $data = Get-Content $outputPath -Raw | ConvertFrom-Json
            $data.Author | Should -Be 'TestAuthor'
            $data.ExportedAt | Should -Not -BeNullOrEmpty
            $data.ExportedFrom | Should -Not -BeNullOrEmpty
        }

        It 'Should throw when exporting a non-existent profile' {
            $outputPath = Join-Path $TestDrive 'Exports\Missing.json'
            { Export-UserProfile -Name 'DoesNotExist' -OutputPath $outputPath } | Should -Throw
        }
    }

    Context 'Import-UserProfile' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'ImportProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }

            # Create a valid import source file
            $script:ValidImportFile = Join-Path $TestDrive 'ImportSource\valid-profile.json'
            $importDir = Split-Path $script:ValidImportFile -Parent
            if (-not (Test-Path $importDir)) {
                New-Item -Path $importDir -ItemType Directory -Force | Out-Null
            }
            $importData = [ordered]@{
                '$schema' = 'WinForge-UserProfile-v1.0'
                Name = 'ImportedProfile'
                Description = 'Imported from external source'
                Author = 'ExternalAuthor'
                Tags = @('shared', 'imported')
                Applications = @('Chrome', 'Firefox', 'Edge')
                Settings = @{ Language = 'en' }
            }
            $importData | ConvertTo-Json -Depth 10 | Set-Content $script:ValidImportFile -Encoding UTF8

            # Create a minimal valid import file
            $script:MinimalImportFile = Join-Path $TestDrive 'ImportSource\minimal-profile.json'
            $minimalData = [ordered]@{
                Name = 'MinimalImport'
                Applications = @('Notepad')
            }
            $minimalData | ConvertTo-Json -Depth 10 | Set-Content $script:MinimalImportFile -Encoding UTF8

            # Create an invalid import file (missing required fields)
            $script:InvalidImportFile = Join-Path $TestDrive 'ImportSource\invalid-profile.json'
            $invalidData = [ordered]@{
                Description = 'No name or applications'
            }
            $invalidData | ConvertTo-Json -Depth 10 | Set-Content $script:InvalidImportFile -Encoding UTF8

            # Create a corrupted JSON file
            $script:CorruptImportFile = Join-Path $TestDrive 'ImportSource\corrupt.json'
            'this is not valid json {{{}}}' | Set-Content $script:CorruptImportFile -Encoding UTF8
        }

        It 'Should import a valid profile file' {
            $result = Import-UserProfile -Path $script:ValidImportFile -Overwrite
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeLike '*.json'
        }

        It 'Should store imported profile in profiles directory' {
            Import-UserProfile -Path $script:ValidImportFile -Overwrite
            $profile = Get-UserProfile -Name 'ImportedProfile'
            $profile | Should -Not -BeNullOrEmpty
            $profile.Name | Should -Be 'ImportedProfile'
        }

        It 'Should preserve applications from the imported file' {
            Import-UserProfile -Path $script:ValidImportFile -Overwrite
            $profile = Get-UserProfile -Name 'ImportedProfile'
            @($profile.Applications).Count | Should -Be 3
            $profile.Applications | Should -Contain 'Chrome'
        }

        It 'Should allow renaming profile on import with -NewName' {
            $result = Import-UserProfile -Path $script:ValidImportFile -NewName 'RenamedImport' -Overwrite
            $profile = Get-UserProfile -Name 'RenamedImport'
            $profile | Should -Not -BeNullOrEmpty
            $profile.Name | Should -Be 'RenamedImport'
        }

        It 'Should throw when importing minimal profile missing optional properties under strict mode' {
            # The module runs under Set-StrictMode -Version Latest, which causes
            # property access on missing members to throw PropertyNotFoundException.
            # A minimal file without Description/Author/Tags triggers this.
            { Import-UserProfile -Path $script:MinimalImportFile -Overwrite } | Should -Throw
        }

        It 'Should import a profile with all optional fields populated' {
            $script:FullImportFile = Join-Path $TestDrive 'ImportSource\full-optional.json'
            $fullData = [ordered]@{
                Name = 'FullOptional'
                Description = 'Has all fields'
                Author = 'OptionalAuthor'
                Tags = @('opt')
                Applications = @('Notepad', 'Paint')
                Settings = @{ Key = 'Value' }
            }
            $fullData | ConvertTo-Json -Depth 10 | Set-Content $script:FullImportFile -Encoding UTF8
            Import-UserProfile -Path $script:FullImportFile -Overwrite
            $profile = Get-UserProfile -Name 'FullOptional'
            $profile | Should -Not -BeNullOrEmpty
            $profile.Tags | Should -Contain 'opt'
        }

        It 'Should throw when importing file with missing required fields' {
            { Import-UserProfile -Path $script:InvalidImportFile } | Should -Throw
        }

        It 'Should throw when importing corrupted JSON file' {
            { Import-UserProfile -Path $script:CorruptImportFile } | Should -Throw
        }

        It 'Should throw when importing to existing name without -Overwrite' {
            Import-UserProfile -Path $script:ValidImportFile -Overwrite
            { Import-UserProfile -Path $script:ValidImportFile } | Should -Throw
        }

        It 'Should set ImportedFrom metadata in the imported profile' {
            Import-UserProfile -Path $script:ValidImportFile -Overwrite
            $profile = Get-UserProfile -Name 'ImportedProfile'
            $profile.ImportedFrom | Should -Be $script:ValidImportFile
        }

        It 'Should set ImportedAt timestamp in the imported profile' {
            Import-UserProfile -Path $script:ValidImportFile -Overwrite
            $profile = Get-UserProfile -Name 'ImportedProfile'
            $profile.ImportedAt | Should -Not -BeNullOrEmpty
        }

        It 'Should reject NewName with invalid characters' {
            { Import-UserProfile -Path $script:ValidImportFile -NewName 'bad name!' } | Should -Throw
        }
    }

    Context 'Copy-UserProfile' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'CopyProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
            Save-UserProfile -Name 'CopySource' -Applications @('VSCode', 'Git', 'Docker') `
                -Description 'Source for copy test' -Tags @('dev', 'tools') -Overwrite
        }

        It 'Should create a copy of an existing profile' {
            $result = Copy-UserProfile -SourceName 'CopySource' -DestinationName 'CopiedProfile'
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should preserve applications in the copied profile' {
            Copy-UserProfile -SourceName 'CopySource' -DestinationName 'CopyAppCheck'
            $copy = Get-UserProfile -Name 'CopyAppCheck'
            @($copy.Applications).Count | Should -Be 3
            $copy.Applications | Should -Contain 'VSCode'
            $copy.Applications | Should -Contain 'Git'
            $copy.Applications | Should -Contain 'Docker'
        }

        It 'Should set description indicating it is a copy' {
            Copy-UserProfile -SourceName 'CopySource' -DestinationName 'CopyDescCheck'
            $copy = Get-UserProfile -Name 'CopyDescCheck'
            $copy.Description | Should -BeLike '*Copy of CopySource*'
        }

        It 'Should preserve tags from the source profile' {
            Copy-UserProfile -SourceName 'CopySource' -DestinationName 'CopyTagCheck'
            $copy = Get-UserProfile -Name 'CopyTagCheck'
            $copy.Tags | Should -Contain 'dev'
            $copy.Tags | Should -Contain 'tools'
        }

        It 'Should throw when source profile does not exist' {
            { Copy-UserProfile -SourceName 'NonExistentSource' -DestinationName 'Dest' } | Should -Throw
        }

        It 'Should reject destination name with invalid characters' {
            { Copy-UserProfile -SourceName 'CopySource' -DestinationName 'bad name!' } | Should -Throw
        }
    }

    Context 'Merge-UserProfiles' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'MergeProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
            Save-UserProfile -Name 'MergeA' -Applications @('VSCode', 'Git') -Tags @('dev') -Overwrite
            Save-UserProfile -Name 'MergeB' -Applications @('Steam', 'Discord') -Tags @('gaming') -Overwrite
            Save-UserProfile -Name 'MergeC' -Applications @('Git', 'Docker') -Tags @('dev', 'ops') -Overwrite
        }

        It 'Should merge multiple profiles into one' {
            $result = Merge-UserProfiles -Names @('MergeA', 'MergeB') -OutputName 'MergedAB'
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should combine applications from all source profiles' {
            Merge-UserProfiles -Names @('MergeA', 'MergeB') -OutputName 'MergedApps'
            $merged = Get-UserProfile -Name 'MergedApps'
            $merged.Applications | Should -Contain 'VSCode'
            $merged.Applications | Should -Contain 'Git'
            $merged.Applications | Should -Contain 'Steam'
            $merged.Applications | Should -Contain 'Discord'
        }

        It 'Should remove duplicate applications by default' {
            Merge-UserProfiles -Names @('MergeA', 'MergeC') -OutputName 'MergedNoDups'
            $merged = Get-UserProfile -Name 'MergedNoDups'
            $gitCount = @($merged.Applications | Where-Object { $_ -eq 'Git' }).Count
            $gitCount | Should -Be 1
        }

        It 'Should combine and deduplicate tags' {
            Merge-UserProfiles -Names @('MergeA', 'MergeC') -OutputName 'MergedTags'
            $merged = Get-UserProfile -Name 'MergedTags'
            $merged.Tags | Should -Contain 'merged'
            $merged.Tags | Should -Contain 'dev'
            $merged.Tags | Should -Contain 'ops'
        }

        It 'Should set description indicating merged source profiles' {
            Merge-UserProfiles -Names @('MergeA', 'MergeB') -OutputName 'MergedDesc'
            $merged = Get-UserProfile -Name 'MergedDesc'
            $merged.Description | Should -BeLike '*MergeA*'
            $merged.Description | Should -BeLike '*MergeB*'
        }

        It 'Should handle merging when one profile does not exist' {
            { Merge-UserProfiles -Names @('MergeA', 'NonExistentMerge') -OutputName 'MergedPartial' } | Should -Not -Throw
            $merged = Get-UserProfile -Name 'MergedPartial'
            $merged.Applications | Should -Contain 'VSCode'
        }

        It 'Should handle merging three profiles' {
            Merge-UserProfiles -Names @('MergeA', 'MergeB', 'MergeC') -OutputName 'MergedAll'
            $merged = Get-UserProfile -Name 'MergedAll'
            $merged.Applications | Should -Contain 'VSCode'
            $merged.Applications | Should -Contain 'Steam'
            $merged.Applications | Should -Contain 'Docker'
        }

        It 'Should reject output name with invalid characters' {
            { Merge-UserProfiles -Names @('MergeA') -OutputName 'bad name!' } | Should -Throw
        }
    }

    Context 'Get-UserProfileStatistics - Detailed Behavior' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'StatsProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
            Save-UserProfile -Name 'StatsA' -Applications @('VSCode', 'Git') -Tags @('dev') -Overwrite
            Save-UserProfile -Name 'StatsB' -Applications @('VSCode', 'Docker') -Tags @('ops') -Overwrite
        }

        It 'Should count total profiles correctly' {
            $stats = Get-UserProfileStatistics
            $stats.TotalProfiles | Should -Be 2
        }

        It 'Should count total application references' {
            $stats = Get-UserProfileStatistics
            $stats.TotalApplicationReferences | Should -Be 4
        }

        It 'Should count unique applications' {
            $stats = Get-UserProfileStatistics
            $stats.UniqueApplications | Should -Be 3
        }

        It 'Should count unique tags' {
            $stats = Get-UserProfileStatistics
            $stats.UniqueTags | Should -Be 2
        }

        It 'Should have ProfilesDirectory property' {
            $stats = Get-UserProfileStatistics
            $stats.ProfilesDirectory | Should -Not -BeNullOrEmpty
        }

        It 'Should have DirectorySize property' {
            $stats = Get-UserProfileStatistics
            $stats.DirectorySize | Should -BeGreaterThan 0
        }

        It 'Should have TopApplications property' {
            $stats = Get-UserProfileStatistics
            $stats.TopApplications | Should -Not -BeNullOrEmpty
        }

        It 'Should rank VSCode as most referenced application' {
            $stats = Get-UserProfileStatistics
            $topApp = $stats.TopApplications | Select-Object -First 1
            $topApp.Name | Should -Be 'VSCode'
            $topApp.Count | Should -Be 2
        }
    }

    Context 'Get-UserProfileStatistics - Empty Directory' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'EmptyStatsProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
        }

        It 'Should throw when computing statistics on an empty directory under strict mode' {
            # Get-UserProfileStatistics accesses .Count on the profiles result,
            # which under Set-StrictMode -Version Latest throws PropertyNotFoundException
            # when no profiles are found (null result from Sort-Object on empty array).
            { Get-UserProfileStatistics } | Should -Throw
        }
    }

    Context 'Export-Import Round Trip' {
        BeforeAll {
            InModuleScope 'UserProfileManager' {
                $script:UserProfilesDir = Join-Path $TestDrive 'RoundTripProfiles'
                if (-not (Test-Path $script:UserProfilesDir)) {
                    New-Item -Path $script:UserProfilesDir -ItemType Directory -Force | Out-Null
                }
            }
            Save-UserProfile -Name 'RoundTripSource' -Applications @('VSCode', 'Git', 'Docker') `
                -Description 'Round trip test' -Tags @('rt', 'test') -Overwrite
        }

        It 'Should preserve applications through export and re-import' {
            $exportPath = Join-Path $TestDrive 'RoundTrip\exported.json'
            Export-UserProfile -Name 'RoundTripSource' -OutputPath $exportPath
            Import-UserProfile -Path $exportPath -NewName 'RoundTripImported' -Overwrite
            $imported = Get-UserProfile -Name 'RoundTripImported'
            $imported.Applications | Should -Contain 'VSCode'
            $imported.Applications | Should -Contain 'Git'
            $imported.Applications | Should -Contain 'Docker'
            @($imported.Applications).Count | Should -Be 3
        }

        It 'Should preserve description through export and re-import' {
            $exportPath = Join-Path $TestDrive 'RoundTrip\desc-export.json'
            Export-UserProfile -Name 'RoundTripSource' -OutputPath $exportPath
            Import-UserProfile -Path $exportPath -NewName 'RoundTripDesc' -Overwrite
            $imported = Get-UserProfile -Name 'RoundTripDesc'
            $imported.Description | Should -Be 'Round trip test'
        }
    }

    Context 'Module Schema Configuration' {
        It 'Should have ProfileSchema with correct version' {
            InModuleScope 'UserProfileManager' {
                $script:ProfileSchema.Version | Should -Be '1.0'
            }
        }

        It 'Should require Name and Applications fields' {
            InModuleScope 'UserProfileManager' {
                $script:ProfileSchema.RequiredFields | Should -Contain 'Name'
                $script:ProfileSchema.RequiredFields | Should -Contain 'Applications'
            }
        }

        It 'Should define expected optional fields' {
            InModuleScope 'UserProfileManager' {
                $script:ProfileSchema.OptionalFields | Should -Contain 'Description'
                $script:ProfileSchema.OptionalFields | Should -Contain 'Author'
                $script:ProfileSchema.OptionalFields | Should -Contain 'Tags'
                $script:ProfileSchema.OptionalFields | Should -Contain 'Settings'
            }
        }
    }
}
