<#
.SYNOPSIS
    Pester tests for JsonSchemaValidation module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge JsonSchemaValidation v3.5.0
    Tests JSON Schema validation functionality

.NOTES
    Author: Julien Bombled
    Version: 3.5.0
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
    $script:JsonSchemaValidationPath = Join-Path $script:ModuleRoot 'JsonSchemaValidation.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'
    $script:TestDataDirectory = Join-Path $PSScriptRoot 'TestData'
    $script:SchemasDirectory = Join-Path $PSScriptRoot '..\Schemas'
    $script:ProfilesDirectory = Join-Path $PSScriptRoot '..\Profiles'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    # Import JsonSchemaValidation
    Import-Module $script:JsonSchemaValidationPath -Force -ErrorAction Stop

    # Create test data directory if needed
    if (-not (Test-Path $script:TestDataDirectory)) {
        New-Item -Path $script:TestDataDirectory -ItemType Directory -Force | Out-Null
    }
}

AfterAll {
    # Cleanup test data
    if (Test-Path $script:TestDataDirectory) {
        Get-ChildItem -Path $script:TestDataDirectory -Filter 'TestSchema*' | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

Describe 'JsonSchemaValidation Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:JsonSchemaValidationPath -Force } | Should -Not -Throw
        }

        It 'Should export Test-JsonSyntax function' {
            Get-Command Test-JsonSyntax -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-JsonAgainstSchema function' {
            Get-Command Test-JsonAgainstSchema -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-DeploymentProfile function' {
            Get-Command Test-DeploymentProfile -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-ApplicationsDatabase function' {
            Get-Command Test-ApplicationsDatabase -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-AllProfiles function' {
            Get-Command Test-AllProfiles -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-JsonSchemaValidation function' {
            Get-Command Invoke-JsonSchemaValidation -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-JsonSyntax' {
        It 'Should return valid for valid JSON' {
            $validJsonPath = Join-Path $script:TestDataDirectory 'TestSchemaValid.json'
            '{ "name": "test", "value": 123 }' | Set-Content -Path $validJsonPath

            $result = Test-JsonSyntax -Path $validJsonPath
            $result.IsValid | Should -BeTrue
            $result.Errors.Count | Should -Be 0
        }

        It 'Should return invalid for invalid JSON' {
            $invalidJsonPath = Join-Path $script:TestDataDirectory 'TestSchemaInvalid.json'
            '{ invalid json content' | Set-Content -Path $invalidJsonPath

            $result = Test-JsonSyntax -Path $invalidJsonPath
            $result.IsValid | Should -BeFalse
            $result.Errors.Count | Should -BeGreaterThan 0
        }

        It 'Should return invalid for non-existent file' {
            $result = Test-JsonSyntax -Path 'C:\NonExistent\file.json'
            $result.IsValid | Should -BeFalse
            $result.Errors | Should -Contain 'File not found: C:\NonExistent\file.json'
        }
    }

    Context 'Test-JsonAgainstSchema' {
        BeforeAll {
            # Create a simple test schema
            $testSchema = @{
                '$schema' = 'https://json-schema.org/draft-07/schema#'
                type = 'object'
                required = @('name', 'version')
                properties = @{
                    name = @{
                        type = 'string'
                        minLength = 1
                    }
                    version = @{
                        type = 'string'
                        pattern = '^\d+\.\d+\.\d+$'
                    }
                    count = @{
                        type = 'integer'
                        minimum = 0
                        maximum = 100
                    }
                }
            }
            $script:TestSchemaPath = Join-Path $script:TestDataDirectory 'TestSchema.schema.json'
            $testSchema | ConvertTo-Json -Depth 10 | Set-Content -Path $script:TestSchemaPath
        }

        It 'Should validate valid JSON against schema' {
            $validJsonPath = Join-Path $script:TestDataDirectory 'TestSchemaValid2.json'
            @{ name = 'Test'; version = '1.0.0'; count = 50 } | ConvertTo-Json | Set-Content -Path $validJsonPath

            $result = Test-JsonAgainstSchema -JsonPath $validJsonPath -SchemaPath $script:TestSchemaPath
            $result.IsValid | Should -BeTrue
        }

        It 'Should detect missing required property' {
            $invalidJsonPath = Join-Path $script:TestDataDirectory 'TestSchemaMissing.json'
            @{ name = 'Test' } | ConvertTo-Json | Set-Content -Path $invalidJsonPath

            $result = Test-JsonAgainstSchema -JsonPath $invalidJsonPath -SchemaPath $script:TestSchemaPath
            $result.IsValid | Should -BeFalse
            $result.Errors | Where-Object { $_ -match 'Missing required property: version' } | Should -Not -BeNullOrEmpty
        }

        It 'Should detect invalid pattern' {
            $invalidJsonPath = Join-Path $script:TestDataDirectory 'TestSchemaPattern.json'
            @{ name = 'Test'; version = 'invalid' } | ConvertTo-Json | Set-Content -Path $invalidJsonPath

            $result = Test-JsonAgainstSchema -JsonPath $invalidJsonPath -SchemaPath $script:TestSchemaPath
            $result.IsValid | Should -BeFalse
            $result.Errors | Where-Object { $_ -match 'does not match pattern' } | Should -Not -BeNullOrEmpty
        }

        It 'Should detect value out of range' {
            $invalidJsonPath = Join-Path $script:TestDataDirectory 'TestSchemaRange.json'
            @{ name = 'Test'; version = '1.0.0'; count = 200 } | ConvertTo-Json | Set-Content -Path $invalidJsonPath

            $result = Test-JsonAgainstSchema -JsonPath $invalidJsonPath -SchemaPath $script:TestSchemaPath
            $result.IsValid | Should -BeFalse
            $result.Errors | Where-Object { $_ -match 'exceeds maximum' } | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-DeploymentProfile' {
        It 'Should validate Base profile successfully' {
            $basePath = Join-Path $script:ProfilesDirectory 'Base.json'
            if (Test-Path $basePath) {
                $result = Test-DeploymentProfile -ProfilePath $basePath
                $result.IsValid | Should -BeTrue -Because "Base profile should be valid"
            }
        }

        It 'Should validate Office profile successfully' {
            $officePath = Join-Path $script:ProfilesDirectory 'Office.json'
            if (Test-Path $officePath) {
                $result = Test-DeploymentProfile -ProfilePath $officePath
                $result.IsValid | Should -BeTrue -Because "Office profile should be valid"
            }
        }

        It 'Should validate Gaming profile successfully' {
            $gamingPath = Join-Path $script:ProfilesDirectory 'Gaming.json'
            if (Test-Path $gamingPath) {
                $result = Test-DeploymentProfile -ProfilePath $gamingPath
                $result.IsValid | Should -BeTrue -Because "Gaming profile should be valid"
            }
        }

        It 'Should validate Personnel profile successfully' {
            $personnelPath = Join-Path $script:ProfilesDirectory 'Personnel.json'
            if (Test-Path $personnelPath) {
                $result = Test-DeploymentProfile -ProfilePath $personnelPath
                $result.IsValid | Should -BeTrue -Because "Personnel profile should be valid"
            }
        }
    }

    Context 'Test-ApplicationsDatabase' {
        It 'Should validate applications database successfully' {
            $result = Test-ApplicationsDatabase
            $result.IsValid | Should -BeTrue -Because "Applications database should be valid"
        }
    }

    Context 'Test-AllProfiles' {
        It 'Should return results for all profiles' {
            $results = Test-AllProfiles

            $results | Should -Not -BeNullOrEmpty
            $results.Count | Should -BeGreaterOrEqual 1
        }

        It 'Should validate all standard profiles' {
            $results = Test-AllProfiles
            $invalidProfiles = $results | Where-Object { -not $_.IsValid }

            @($invalidProfiles).Count | Should -Be 0 -Because "All standard profiles should be valid"
        }
    }

    Context 'Invoke-JsonSchemaValidation' {
        It 'Should return summary hashtable' {
            $summary = Invoke-JsonSchemaValidation

            $summary | Should -BeOfType [hashtable]
            $summary.Keys | Should -Contain 'TotalFiles'
            $summary.Keys | Should -Contain 'ValidFiles'
            $summary.Keys | Should -Contain 'InvalidFiles'
            $summary.Keys | Should -Contain 'TotalErrors'
        }

        It 'Should validate all files without errors' {
            $summary = Invoke-JsonSchemaValidation

            $summary.InvalidFiles | Should -Be 0 -Because "All configuration files should be valid"
            $summary.TotalErrors | Should -Be 0
        }
    }
}
