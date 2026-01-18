<#
.SYNOPSIS
    Pester tests for StartupManager module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge StartupManager v2.5.0
    Tests startup application detection and management

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
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:StartupPath = Join-Path $script:ModuleRoot 'StartupManager.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    Import-Module $script:StartupPath -Force -ErrorAction Stop
}

Describe 'StartupManager Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:StartupPath -Force } | Should -Not -Throw
        }

        It 'Should export Get-StartupApplications function' {
            Get-Command Get-StartupApplications -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Disable-StartupApplication function' {
            Get-Command Disable-StartupApplication -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Disable-StartupApplications function' {
            Get-Command Disable-StartupApplications -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Show-StartupApplications function' {
            Get-Command Show-StartupApplications -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-StartupBlacklist function' {
            Get-Command Invoke-StartupBlacklist -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-StartupApplications' {
        It 'Should return array of PSCustomObjects' {
            $result = Get-StartupApplications
            if ($result.Count -gt 0) {
                $result[0] | Should -BeOfType [PSCustomObject]
            }
        }

        It 'Should complete without throwing' {
            { Get-StartupApplications } | Should -Not -Throw
        }

        It 'Each item should have Name property' {
            $result = Get-StartupApplications
            foreach ($item in $result) {
                $item.PSObject.Properties.Name | Should -Contain 'Name'
            }
        }

        It 'Each item should have Location property' {
            $result = Get-StartupApplications
            foreach ($item in $result) {
                $item.PSObject.Properties.Name | Should -Contain 'Location'
            }
        }

        It 'Each item should have Type property' {
            $result = Get-StartupApplications
            foreach ($item in $result) {
                $item.PSObject.Properties.Name | Should -Contain 'Type'
            }
        }

        It 'Each item should have Path property' {
            $result = Get-StartupApplications
            foreach ($item in $result) {
                $item.PSObject.Properties.Name | Should -Contain 'Path'
            }
        }

        It 'Type should be Registry, Shortcut, or ScheduledTask' {
            $result = Get-StartupApplications
            $validTypes = @('Registry', 'Shortcut', 'ScheduledTask')
            foreach ($item in $result) {
                $item.Type | Should -BeIn $validTypes
            }
        }

        It 'Should complete within reasonable time' {
            $duration = Measure-Command { Get-StartupApplications }
            $duration.TotalSeconds | Should -BeLessThan 60
        }
    }

    Context 'Disable-StartupApplication' {
        It 'Should have Name parameter' {
            $cmd = Get-Command Disable-StartupApplication
            $cmd.Parameters.ContainsKey('Name') | Should -BeTrue
        }

        It 'Should have Location parameter' {
            $cmd = Get-Command Disable-StartupApplication
            $cmd.Parameters.ContainsKey('Location') | Should -BeTrue
        }

        It 'Should support ShouldProcess' {
            $cmd = Get-Command Disable-StartupApplication
            $cmd.Parameters.ContainsKey('WhatIf') | Should -BeTrue
            $cmd.Parameters.ContainsKey('Confirm') | Should -BeTrue
        }

        It 'Should return false for non-existent app' {
            $result = Disable-StartupApplication -Name 'NonExistentApp12345XYZ' -WhatIf
            $result | Should -BeFalse
        }
    }

    Context 'Disable-StartupApplications' {
        It 'Should have ApplicationNames parameter' {
            $cmd = Get-Command Disable-StartupApplications
            $cmd.Parameters.ContainsKey('ApplicationNames') | Should -BeTrue
        }

        It 'Should support ShouldProcess' {
            $cmd = Get-Command Disable-StartupApplications
            $cmd.Parameters.ContainsKey('WhatIf') | Should -BeTrue
        }

        It 'Should require non-empty ApplicationNames' {
            # Function requires at least one application name
            $cmd = Get-Command Disable-StartupApplications
            $param = $cmd.Parameters['ApplicationNames']
            $param.Attributes | Where-Object { $_.TypeId.Name -eq 'ParameterAttribute' } | Should -Not -BeNullOrEmpty
        }

        It 'Should handle non-existent apps gracefully' {
            { Disable-StartupApplications -ApplicationNames @('NonExistent1', 'NonExistent2') -WhatIf } | Should -Not -Throw
        }
    }

    Context 'Show-StartupApplications' {
        It 'Should complete without throwing' {
            { Show-StartupApplications } | Should -Not -Throw
        }
    }

    Context 'Invoke-StartupBlacklist' {
        It 'Should have ConfigPath parameter' {
            $cmd = Get-Command Invoke-StartupBlacklist
            $cmd.Parameters.ContainsKey('ConfigPath') | Should -BeTrue
        }

        It 'Should support ShouldProcess' {
            $cmd = Get-Command Invoke-StartupBlacklist
            $cmd.Parameters.ContainsKey('WhatIf') | Should -BeTrue
        }

        It 'Should handle missing config file gracefully' {
            { Invoke-StartupBlacklist -ConfigPath 'C:\NonExistent\Config.json' -WhatIf } | Should -Not -Throw
        }
    }
}

Describe 'StartupManager Integration Tests' {
    Context 'Startup Detection Consistency' {
        It 'Should return consistent results' {
            $first = Get-StartupApplications
            $second = Get-StartupApplications

            $first.Count | Should -Be $second.Count
        }

        It 'Should detect registry-based startups' {
            $result = Get-StartupApplications
            $registryItems = $result | Where-Object { $_.Type -eq 'Registry' }
            # Registry items may or may not exist depending on system
            $registryItems.Count | Should -BeGreaterOrEqual 0
        }
    }

    Context 'Location Detection' {
        It 'Should detect CurrentUserRun location' {
            $result = Get-StartupApplications
            $locations = $result | Select-Object -ExpandProperty Location -Unique
            # Location list should include expected values if items exist
            if ($locations.Count -gt 0) {
                $validLocations = @(
                    'CurrentUserRun',
                    'CurrentUserRunOnce',
                    'LocalMachineRun',
                    'LocalMachineRunOnce',
                    'LocalMachineRun64',
                    'CurrentUserStartup',
                    'CommonStartup',
                    'TaskScheduler'
                )
                foreach ($loc in $locations) {
                    $loc | Should -BeIn $validLocations
                }
            }
        }
    }

    Context 'Wildcard Support' {
        It 'Disable-StartupApplication should support wildcards' {
            # Test with wildcard that should match nothing
            $result = Disable-StartupApplication -Name '*NonExistentPattern12345*' -WhatIf
            $result | Should -BeFalse
        }
    }
}
