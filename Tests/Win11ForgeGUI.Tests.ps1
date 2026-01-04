<#
.SYNOPSIS
    Pester tests for Win11ForgeGUI module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge GUI v2.5.0
    Tests GUI initialization and exported functions

.NOTES
    Author: Julien Bombled
    Version: 2.5.0
    Requires: Pester v5+
#>

BeforeAll {
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:GUIPath = Join-Path $script:ModuleRoot 'Win11ForgeGUI.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    Import-Module $script:GUIPath -Force -ErrorAction Stop
}

Describe 'Win11ForgeGUI Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:GUIPath -Force } | Should -Not -Throw
        }

        It 'Should export Initialize-GUIModules function' {
            Get-Command Initialize-GUIModules -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Show-MainMenu function' {
            Get-Command Show-MainMenu -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Show-Header function' {
            Get-Command Show-Header -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Show-Footer function' {
            Get-Command Show-Footer -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Read-Choice function' {
            Get-Command Read-Choice -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Initialize-GUIModules' {
        It 'Should return boolean' {
            $result = Initialize-GUIModules
            $result | Should -BeOfType [bool]
        }

        It 'Should complete without throwing' {
            { Initialize-GUIModules } | Should -Not -Throw
        }

        It 'Should return true on success' {
            $result = Initialize-GUIModules
            $result | Should -BeTrue
        }

        It 'Should load Core module' {
            Initialize-GUIModules | Out-Null
            Get-Command Write-Status -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Show-Header' {
        It 'Should have Title parameter' {
            $cmd = Get-Command Show-Header
            $cmd.Parameters.ContainsKey('Title') | Should -BeTrue
        }

        It 'Should be a function' {
            $cmd = Get-Command Show-Header
            $cmd.CommandType | Should -Be 'Function'
        }
    }

    Context 'Show-Footer' {
        It 'Should be available as function' {
            Get-Command Show-Footer | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Read-Choice' {
        It 'Should be available as function' {
            Get-Command Read-Choice | Should -Not -BeNullOrEmpty
        }

        It 'Should have Prompt parameter' {
            $cmd = Get-Command Read-Choice
            $cmd.Parameters.ContainsKey('Prompt') | Should -BeTrue
        }
    }

    Context 'Show-MainMenu' {
        It 'Should be available as function' {
            Get-Command Show-MainMenu | Should -Not -BeNullOrEmpty
        }
    }
}

Describe 'Win11ForgeGUI Integration Tests' {
    Context 'Module Dependencies' {
        It 'Should load all required modules via Initialize-GUIModules' {
            $result = Initialize-GUIModules
            $result | Should -BeTrue
        }

        It 'Should make Core functions available' {
            Initialize-GUIModules | Out-Null
            Get-Command Write-Status -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should be idempotent (multiple initializations safe)' {
            {
                Initialize-GUIModules | Out-Null
                Initialize-GUIModules | Out-Null
                Initialize-GUIModules | Out-Null
            } | Should -Not -Throw
        }
    }

    Context 'Function Availability' {
        It 'All exported functions should be available' {
            Get-Command Initialize-GUIModules | Should -Not -BeNullOrEmpty
            Get-Command Show-MainMenu | Should -Not -BeNullOrEmpty
            Get-Command Show-Header | Should -Not -BeNullOrEmpty
            Get-Command Show-Footer | Should -Not -BeNullOrEmpty
            Get-Command Read-Choice | Should -Not -BeNullOrEmpty
        }
    }
}
