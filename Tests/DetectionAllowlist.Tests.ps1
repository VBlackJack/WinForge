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
    $script:DetectionAllowlistModulePath = (Resolve-Path (Join-Path $PSScriptRoot '..\Modules\DetectionAllowlist.psm1')).Path
    Import-Module $script:DetectionAllowlistModulePath -Force -ErrorAction Stop
}

Describe 'DetectionAllowlist - Get-DetectionAllowlist' {
    Context 'Module loading' {
        It 'Exports Get-DetectionAllowlist' {
            Get-Command Get-DetectionAllowlist -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Loading the canonical allowlist' {
        It 'Loads known dev tools from the shared JSON' {
            $allow = Get-DetectionAllowlist -Force
            $allow | Should -Contain 'java'
            $allow | Should -Contain 'git'
            $allow | Should -Contain 'dotnet'
        }

        It 'Does not include arbitrary executables' {
            Get-DetectionAllowlist | Should -Not -Contain 'powershell.exe'
            Get-DetectionAllowlist | Should -Not -Contain 'cmd.exe'
        }

        It 'Recognizes the AI CLI tools the legacy copies omitted (I3 closed)' {
            $allow = Get-DetectionAllowlist
            foreach ($tool in @('codex', 'claude', 'agy', 'ollama', 'aish')) {
                $allow | Should -Contain $tool
            }
        }
    }

    Context 'Fail-closed behavior' {
        It 'Returns an empty list (deny-all) when the allowlist file is missing' {
            InModuleScope DetectionAllowlist {
                $originalPath = $script:DetectionAllowlistPath
                try {
                    $script:DetectionAllowlistPath = Join-Path ([System.IO.Path]::GetTempPath()) 'win11forge-no-such-allowlist.json'
                    $script:DetectionAllowlistLoaded = $false
                    @(Get-DetectionAllowlist -Force).Count | Should -Be 0
                } finally {
                    $script:DetectionAllowlistPath = $originalPath
                    $script:DetectionAllowlistLoaded = $false
                }
            }
        }
    }

    Context 'Parallel runspace loading' {
        It 'Resolves the allowlist inside a ForEach-Object -Parallel runspace via Import-Module by path' {
            $modulePath = $script:DetectionAllowlistModulePath
            $result = @(1) | ForEach-Object -Parallel {
                Import-Module $using:modulePath -Force
                Get-DetectionAllowlist
            }
            # Proves the runspace resolves the JSON (incl. the I3 entries), so parallel
            # Command detection does not silently fail closed.
            $result | Should -Contain 'codex'
            $result | Should -Contain 'java'
        }
    }
}
