# Contributing to Win11Forge

Thank you for your interest in contributing to Win11Forge! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Development Setup](#development-setup)
- [Code Style Guidelines](#code-style-guidelines)
- [Commit Message Format](#commit-message-format)
- [Pull Request Process](#pull-request-process)
- [Testing Requirements](#testing-requirements)
- [Documentation](#documentation)

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow

## Development Setup

### Prerequisites

- **PowerShell 5.1+** (Windows built-in) or **PowerShell 7+** (recommended for parallel features)
- **.NET 8 SDK** (for GUI development)
- **Git** for version control
- **VS Code** with PowerShell extension (recommended IDE)

### Quick Start

```powershell
# Clone the repository
git clone https://github.com/your-org/Win11Forge.git
cd Win11Forge

# Install test dependencies
Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -Scope CurrentUser
Install-Module -Name PSScriptAnalyzer -Force -Scope CurrentUser

# Run tests to verify setup
Invoke-Pester -Path ./Tests -Output Detailed

# Build GUI (optional)
dotnet build ./GUI/Win11Forge.slnx
```

### Project Structure

```
Win11Forge/
├── Core/                    # Core infrastructure modules
│   ├── Core.psm1           # Logging, utilities
│   ├── Localization.psm1   # i18n support
│   ├── PluginManager.psm1  # Plugin system
│   └── RestApiServer.psm1  # REST API
├── Modules/                 # Feature modules
│   ├── InstallationOrchestrator.psm1
│   ├── ApplicationDetection.psm1
│   └── ...
├── Config/                  # JSON configuration files
├── Tests/                   # Pester test files
├── GUI/                     # WPF GUI application
└── Profiles/                # Deployment profiles
```

## Code Style Guidelines

### PowerShell Standards

1. **Use Strict Mode**
   ```powershell
   Set-StrictMode -Version Latest
   ```

2. **Function Naming** - Use approved PowerShell verbs with PascalCase
   ```powershell
   # Good
   function Get-ApplicationStatus { }
   function Test-ApplicationInstalled { }
   function Install-Application { }

   # Bad
   function getAppStatus { }
   function CheckIfInstalled { }
   ```

3. **Parameter Validation**
   ```powershell
   function Get-Application {
       [CmdletBinding()]
       [OutputType([PSCustomObject])]
       param(
           [Parameter(Mandatory)]
           [ValidateNotNullOrEmpty()]
           [string]$AppId
       )
   }
   ```

4. **Comment-Based Help** - Required for all public functions
   ```powershell
   function Get-Example {
       <#
       .SYNOPSIS
           Brief description.
       .DESCRIPTION
           Detailed description.
       .PARAMETER Name
           Parameter description.
       .OUTPUTS
           Output type description.
       .EXAMPLE
           Get-Example -Name 'Test'
       #>
   }
   ```

5. **Error Handling**
   ```powershell
   try {
       # Code that may fail
   } catch {
       Write-Status -Message "Error: $($_.Exception.Message)" -Level 'Error'
       throw  # Re-throw if caller should handle
   }
   ```

### Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Functions | Verb-Noun (PascalCase) | `Get-ApplicationStatus` |
| Parameters | PascalCase | `$AppName` |
| Variables | camelCase | `$appCount` |
| Script Variables | $script:camelCase | `$script:cacheData` |
| Constants | UPPER_SNAKE_CASE | `$MAX_RETRIES` |

### Zero Hardcoding Mandate

**Never hardcode:**
- User-facing strings (use i18n)
- URLs or endpoints (use Config/)
- Magic numbers (use constants or Config/)
- File paths (use environment variables)

```powershell
# Bad
Write-Host "Installation complete"
$url = "https://example.com/download"
$timeout = 30000

# Good
Write-Status -Message (Get-LocalizedString -Key 'install.complete') -Level 'Success'
$url = $config.DownloadUrl
$timeout = Get-TimeoutSetting -Operation 'Download'
```

## Commit Message Format

Use conventional commits format:

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Types

- `feat`: New feature
- `fix`: Bug fix
- `perf`: Performance improvement
- `refactor`: Code refactoring
- `test`: Adding tests
- `docs`: Documentation
- `style`: Code style (formatting, etc.)
- `chore`: Maintenance tasks

### Examples

```
feat(detection): add parallel registry scanning for PS7+

- Implement ForEach-Object -Parallel for registry paths
- Add graceful fallback for PS5.1
- ~30% performance improvement on multi-core systems

Closes #123
```

```
fix(security): sanitize API error messages

Prevent information disclosure by sanitizing exception
messages before sending to API clients.

BREAKING CHANGE: Error responses now use generic messages
```

## Pull Request Process

### Before Submitting

1. **Run Tests**
   ```powershell
   Invoke-Pester -Path ./Tests -Output Detailed
   ```

2. **Run Linter**
   ```powershell
   Invoke-ScriptAnalyzer -Path . -Recurse -ExcludeRule PSAvoidUsingWriteHost
   ```

3. **Update Documentation** if adding new features

4. **Add Tests** for new functionality

### PR Requirements

- [ ] All tests pass
- [ ] No new PSScriptAnalyzer warnings
- [ ] Code follows style guidelines
- [ ] Commit messages follow convention
- [ ] Documentation updated (if applicable)
- [ ] i18n keys added for new user-facing strings

### PR Template

```markdown
## Summary
Brief description of changes.

## Changes
- Change 1
- Change 2

## Test Plan
- [ ] Tested locally
- [ ] New tests added
- [ ] Existing tests pass

## Related Issues
Closes #XX
```

## Testing Requirements

### Test File Structure

```
Tests/
├── ModuleName.Tests.ps1    # Unit tests for ModuleName.psm1
├── TestData/               # Test fixtures
└── README.md               # Testing documentation
```

### Writing Tests

```powershell
Describe 'ModuleName' {
    Context 'Function-Name' {
        It 'Should do expected behavior' {
            $result = Get-Something -Input 'test'
            $result | Should -Be 'expected'
        }

        It 'Should handle errors gracefully' {
            { Get-Something -Input $null } | Should -Not -Throw
        }
    }
}
```

### Test Categories

1. **Unit Tests** - Test individual functions in isolation
2. **Integration Tests** - Test module interactions
3. **Performance Tests** - Validate performance requirements

### Coverage Goals

- All exported functions must have tests
- Edge cases (null, empty, invalid input)
- Error conditions
- Security validations (path traversal, injection)

## Documentation

### Adding i18n Keys

1. Add key to `Config/Locales/en.json`:
   ```json
   {
     "module": {
       "feature": {
         "message": "English message"
       }
     }
   }
   ```

2. Add translation to `Config/Locales/fr.json`:
   ```json
   {
     "module": {
       "feature": {
         "message": "Message en francais"
       }
     }
   }
   ```

3. Use in code:
   ```powershell
   Write-Status -Message (Get-LocalizedString -Key 'module.feature.message')
   ```

### Key Naming Convention

```
<module>.<feature>.<element>.<action>
```

Examples:
- `install.app.starting`
- `detection.registry.scanning`
- `errors.network.timeout`

## Questions?

- Open an issue for questions
- Check existing issues and documentation
- Review ARCHITECTURE.md for technical details

---

Thank you for contributing to Win11Forge!
