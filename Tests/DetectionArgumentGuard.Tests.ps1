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
    $script:GuardModulePath = (Resolve-Path (Join-Path $PSScriptRoot '..\Modules\DetectionArgumentGuard.psm1')).Path
    Import-Module $script:GuardModulePath -Force -ErrorAction Stop
}

Describe 'DetectionArgumentGuard - Test-DetectionArgumentDangerous' {
    Context 'Module loading' {
        It 'Exports Test-DetectionArgumentDangerous' {
            Get-Command Test-DetectionArgumentDangerous -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Safe arguments' {
        It 'Treats an empty argument string as safe' {
            Test-DetectionArgumentDangerous -Arguments '' | Should -BeFalse
        }

        It 'Treats a typical version flag as safe' {
            Test-DetectionArgumentDangerous -Arguments '--version' | Should -BeFalse
            Test-DetectionArgumentDangerous -Arguments 'list --runtimes' | Should -BeFalse
        }
    }

    Context 'Dangerous arguments' {
        It 'Blocks shell metacharacters' {
            foreach ($payload in @('a; calc', 'a & calc', 'a | calc', 'a `whoami`', 'a $(calc)', 'a (b)')) {
                Test-DetectionArgumentDangerous -Arguments $payload | Should -BeTrue
            }
        }

        It 'Blocks redirection operators' {
            Test-DetectionArgumentDangerous -Arguments 'a >> b' | Should -BeTrue
            Test-DetectionArgumentDangerous -Arguments 'a << b' | Should -BeTrue
        }

        It 'Blocks newlines and control characters' {
            Test-DetectionArgumentDangerous -Arguments "a`r`nb" | Should -BeTrue
            Test-DetectionArgumentDangerous -Arguments "a`tb".Replace("`t", [char]1) | Should -BeTrue
        }
    }
}
