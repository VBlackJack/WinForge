<#
.SYNOPSIS
    Pester tests for Win11Forge launchers

.DESCRIPTION
    Static validation for launcher scripts that should not start the GUI during tests.

.NOTES
    Author: Julien Bombled
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
    $script:LauncherPath = Join-Path $PSScriptRoot '..\Start-Win11ForgeGUI.ps1'
    $script:LauncherContent = Get-Content -Path $script:LauncherPath -Raw -Encoding UTF8
}

Describe 'Start-Win11ForgeGUI launcher selection' {
    It 'Should prefer source Release before source Debug' {
        $releaseIndex = $script:LauncherContent.IndexOf("New-WpfGuiCandidate -Name 'source Release'")
        $debugIndex = $script:LauncherContent.IndexOf("New-WpfGuiCandidate -Name 'source Debug'")

        $releaseIndex | Should -BeGreaterOrEqual 0
        $debugIndex | Should -BeGreaterOrEqual 0
        $releaseIndex | Should -BeLessThan $debugIndex
    }

    It 'Should keep Debug as the last source fallback' {
        $script:LauncherContent | Should -Match "source Release'.*-Priority 10"
        $script:LauncherContent | Should -Match "source publish'.*-Priority 20"
        $script:LauncherContent | Should -Match "source Debug'.*-Priority 30"
    }

    It 'Should warn when the selected executable is older than GUI source files' {
        $script:LauncherContent | Should -Match 'Get-NewestGuiSourceWriteTimeUtc'
        $script:LauncherContent | Should -Match 'selected WPF executable is older than the newest GUI source file'
    }
}
