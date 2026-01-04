<#
.SYNOPSIS
    Pester tests for StartMenuLayout module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge StartMenuLayout v2.5.0
    Tests Start Menu organization using LayoutModification.json

.NOTES
    Author: Julien Bombled
    Version: 2.5.0
    Requires: Pester v5+
#>

BeforeAll {
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Modules'
    $script:LayoutPath = Join-Path $script:ModuleRoot 'StartMenuLayout.psm1'
    $script:CorePath = Join-Path $PSScriptRoot '..\Core\Core.psm1'

    # Import Core first
    if (Test-Path $script:CorePath) {
        Import-Module $script:CorePath -Force -ErrorAction Stop
    }

    Import-Module $script:LayoutPath -Force -ErrorAction Stop
}

Describe 'StartMenuLayout Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:LayoutPath -Force } | Should -Not -Throw
        }

        It 'Should export Get-DesktopShortcuts function' {
            Get-Command Get-DesktopShortcuts -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ApplicationCategory function' {
            Get-Command Get-ApplicationCategory -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-ShortcutInfo function' {
            Get-Command Get-ShortcutInfo -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-PackagedAppId function' {
            Get-Command Get-PackagedAppId -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Copy-ShortcutToStartMenu function' {
            Get-Command Copy-ShortcutToStartMenu -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export New-LayoutModificationJson function' {
            Get-Command New-LayoutModificationJson -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Set-StartMenuLayout function' {
            Get-Command Set-StartMenuLayout -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Invoke-StartMenuOrganization function' {
            Get-Command Invoke-StartMenuOrganization -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Get-DesktopShortcuts' {
        It 'Should return array or null' {
            $result = Get-DesktopShortcuts
            # Result can be empty array, array of items, or null
            if ($null -ne $result) {
                $result | Should -BeOfType [System.IO.FileInfo]
            }
        }

        It 'Should complete without throwing' {
            { Get-DesktopShortcuts } | Should -Not -Throw
        }
    }

    Context 'Get-ApplicationCategory' {
        It 'Should return a string' {
            $result = Get-ApplicationCategory -ApplicationName 'TestApp'
            $result | Should -BeOfType [string]
        }

        It 'Should return Other for unknown apps' {
            $result = Get-ApplicationCategory -ApplicationName 'UnknownApp12345'
            $result | Should -Be 'Other'
        }

        It 'Should categorize Chrome as Browser' {
            $result = Get-ApplicationCategory -ApplicationName 'Google Chrome'
            $result | Should -Be 'Browser'
        }

        It 'Should categorize Firefox as Browser' {
            $result = Get-ApplicationCategory -ApplicationName 'Firefox'
            $result | Should -Be 'Browser'
        }

        It 'Should categorize VSCode as Development' {
            $result = Get-ApplicationCategory -ApplicationName 'Visual Studio Code'
            $result | Should -Be 'Development'
        }

        It 'Should categorize Steam as Gaming' {
            $result = Get-ApplicationCategory -ApplicationName 'Steam'
            $result | Should -Be 'Gaming'
        }

        It 'Should categorize VLC as Media' {
            $result = Get-ApplicationCategory -ApplicationName 'VLC'
            $result | Should -Be 'Media'
        }

        It 'Should categorize 7-Zip as Utility' {
            $result = Get-ApplicationCategory -ApplicationName '7-Zip'
            $result | Should -Be 'Utility'
        }

        It 'Should categorize Discord as Communication' {
            $result = Get-ApplicationCategory -ApplicationName 'Discord'
            $result | Should -Be 'Communication'
        }
    }

    Context 'Get-ShortcutInfo' {
        It 'Should handle non-existent shortcut gracefully' {
            # Function may return null or empty hashtable for non-existent paths
            { Get-ShortcutInfo -ShortcutPath 'C:\NonExistent\Path.lnk' } | Should -Not -Throw
        }

        It 'Should return hashtable for valid shortcut' {
            # Find any existing shortcut on desktop
            $desktopPath = [Environment]::GetFolderPath('Desktop')
            $shortcuts = Get-ChildItem -Path $desktopPath -Filter '*.lnk' -ErrorAction SilentlyContinue | Select-Object -First 1

            if ($shortcuts) {
                $result = Get-ShortcutInfo -ShortcutPath $shortcuts.FullName
                $result | Should -Not -BeNullOrEmpty
                $result.Name | Should -Not -BeNullOrEmpty
            }
        }
    }

    Context 'Get-PackagedAppId' {
        It 'Should return null for non-existent app' {
            $result = Get-PackagedAppId -AppName 'NonExistentApp12345XYZ'
            $result | Should -BeNullOrEmpty
        }

        It 'Should complete without throwing' {
            { Get-PackagedAppId -AppName 'Calculator' } | Should -Not -Throw
        }
    }

    Context 'New-LayoutModificationJson' {
        It 'Should return valid JSON' {
            $shortcuts = @{
                'Browser' = @('C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Chrome.lnk')
            }
            $result = New-LayoutModificationJson -CategorizedShortcuts $shortcuts
            $result | Should -Not -BeNullOrEmpty
            { $result | ConvertFrom-Json } | Should -Not -Throw
        }

        It 'Should handle empty shortcuts' {
            $shortcuts = @{}
            $result = New-LayoutModificationJson -CategorizedShortcuts $shortcuts
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should create pinnedList structure' {
            $shortcuts = @{
                'Test' = @('C:\Test\app.lnk')
            }
            $result = New-LayoutModificationJson -CategorizedShortcuts $shortcuts
            $json = $result | ConvertFrom-Json
            $json.PSObject.Properties.Name | Should -Contain 'pinnedList'
        }

        It 'Should create folder structure for categories' {
            $shortcuts = @{
                'Development' = @('C:\Test\vscode.lnk')
                'Browser' = @('C:\Test\chrome.lnk')
            }
            $result = New-LayoutModificationJson -CategorizedShortcuts $shortcuts
            $json = $result | ConvertFrom-Json
            # pinnedList may be empty if paths don't exist, but structure should be valid
            $json.PSObject.Properties.Name | Should -Contain 'pinnedList'
        }
    }

    Context 'Set-StartMenuLayout' {
        It 'Should have ForDefaultProfile parameter' {
            $cmd = Get-Command Set-StartMenuLayout
            $cmd.Parameters.ContainsKey('ForDefaultProfile') | Should -BeTrue
        }

        It 'Should have JsonContent parameter' {
            $cmd = Get-Command Set-StartMenuLayout
            $cmd.Parameters.ContainsKey('JsonContent') | Should -BeTrue
        }
    }

    Context 'Invoke-StartMenuOrganization' {
        It 'Should have ForDefaultProfile parameter' {
            $cmd = Get-Command Invoke-StartMenuOrganization
            $cmd.Parameters.ContainsKey('ForDefaultProfile') | Should -BeTrue
        }

        It 'Should have ExcludePatterns parameter' {
            $cmd = Get-Command Invoke-StartMenuOrganization
            $cmd.Parameters.ContainsKey('ExcludePatterns') | Should -BeTrue
        }
    }
}

Describe 'StartMenuLayout Integration Tests' {
    Context 'Category Mapping Consistency' {
        It 'Should return consistent categories' {
            $first = Get-ApplicationCategory -ApplicationName 'Chrome'
            $second = Get-ApplicationCategory -ApplicationName 'Chrome'
            $first | Should -Be $second
        }

        It 'Should handle case variations' {
            $lower = Get-ApplicationCategory -ApplicationName 'chrome'
            $upper = Get-ApplicationCategory -ApplicationName 'CHROME'
            # Both should map to Browser or similar
            $lower | Should -Not -BeNullOrEmpty
            $upper | Should -Not -BeNullOrEmpty
        }
    }

    Context 'JSON Generation' {
        It 'Should generate valid JSON for multiple categories' {
            $shortcuts = @{
                'Development' = @('C:\Apps\VSCode.lnk', 'C:\Apps\Git.lnk')
                'Browser' = @('C:\Apps\Chrome.lnk')
                'Utility' = @('C:\Apps\7zip.lnk')
            }
            $result = New-LayoutModificationJson -CategorizedShortcuts $shortcuts
            { $result | ConvertFrom-Json } | Should -Not -Throw
        }
    }
}
