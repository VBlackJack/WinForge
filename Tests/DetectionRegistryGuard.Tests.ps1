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
    $script:RegistryGuardModulePath = (Resolve-Path (Join-Path $PSScriptRoot '..\Modules\DetectionRegistryGuard.psm1')).Path
    Import-Module $script:RegistryGuardModulePath -Force -ErrorAction Stop
}

Describe 'DetectionRegistryGuard - Test-RegistryPathAllowed' {
    Context 'Module loading' {
        It 'Exports Test-RegistryPathAllowed' {
            Get-Command Test-RegistryPathAllowed -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Allowed paths' {
        It 'Allows standard software hives' {
            Test-RegistryPathAllowed -Path 'HKLM:\SOFTWARE' | Should -BeTrue
            Test-RegistryPathAllowed -Path 'HKCU:\SOFTWARE\Vendor\App' | Should -BeTrue
        }

        It 'Allows uninstall keys' {
            Test-RegistryPathAllowed -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall' | Should -BeTrue
        }
    }

    Context 'Blocked and disallowed paths' {
        It 'Blocks sensitive hives' {
            Test-RegistryPathAllowed -Path 'HKLM:\SYSTEM\CurrentControlSet' | Should -BeFalse
            Test-RegistryPathAllowed -Path 'HKLM:\SAM\Domains' | Should -BeFalse
        }

        It 'Blocks startup persistence keys even under SOFTWARE' {
            Test-RegistryPathAllowed -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce' | Should -BeFalse
        }

        It 'Rejects paths outside the whitelist' {
            Test-RegistryPathAllowed -Path 'HKLM:\HARDWARE\Description' | Should -BeFalse
        }

        It 'Rejects excessively long paths' {
            $longPath = 'HKLM:\SOFTWARE\' + ('A' * 600)
            Test-RegistryPathAllowed -Path $longPath | Should -BeFalse
        }
    }
}
