@{
    # PSScriptAnalyzer configuration for Win11Forge v2.5.0
    # https://github.com/PowerShell/PSScriptAnalyzer

    # === SEVERITY ===
    Severity = @('Error', 'Warning', 'Information')

    # === INCLUDE RULES ===
    IncludeRules = @(
        # Best Practices
        'PSAvoidDefaultValueForMandatoryParameter',
        'PSAvoidDefaultValueSwitchParameter',
        'PSAvoidGlobalVars',
        'PSAvoidInvokingEmptyMembers',
        'PSAvoidNullOrEmptyHelpMessageAttribute',
        'PSAvoidOverwritingBuiltInCmdlets',
        'PSAvoidShouldContinueWithoutForce',
        'PSAvoidTrailingWhitespace',
        'PSAvoidUsingCmdletAliases',
        'PSAvoidUsingComputerNameHardcoded',
        'PSAvoidUsingConvertToSecureStringWithPlainText',
        'PSAvoidUsingDeprecatedManifestFields',
        'PSAvoidUsingEmptyCatchBlock',
        'PSAvoidUsingInvokeExpression',
        'PSAvoidUsingPlainTextForPassword',
        'PSAvoidUsingPositionalParameters',
        'PSAvoidUsingWMICmdlet',
        'PSAvoidUsingWriteHost',
        'PSDSCDscExamplesPresent',
        'PSDSCDscTestsPresent',
        'PSDSCReturnCorrectTypesForDSCFunctions',
        'PSDSCStandardDSCFunctionsInResource',
        'PSDSCUseIdenticalMandatoryParametersForDSC',
        'PSDSCUseIdenticalParametersForDSC',
        'PSDSCUseVerboseMessageInDSCResource',
        'PSMisleadingBacktick',
        'PSMissingModuleManifestField',
        'PSPlaceCloseBrace',
        'PSPlaceOpenBrace',
        'PSPossibleIncorrectComparisonWithNull',
        'PSPossibleIncorrectUsageOfAssignmentOperator',
        'PSPossibleIncorrectUsageOfRedirectionOperator',
        'PSProvideCommentHelp',
        'PSReservedCmdletChar',
        'PSReservedParams',
        'PSReviewUnusedParameter',
        'PSShouldProcess',
        'PSUseApprovedVerbs',
        'PSUseBOMForUnicodeEncodedFile',
        'PSUseCmdletCorrectly',
        'PSUseCompatibleCmdlets',
        'PSUseCompatibleCommands',
        'PSUseCompatibleSyntax',
        'PSUseCompatibleTypes',
        'PSUseConsistentIndentation',
        'PSUseConsistentWhitespace',
        'PSUseCorrectCasing',
        'PSUseDeclaredVarsMoreThanAssignments',
        'PSUseLiteralInitializerForHashtable',
        'PSUseOutputTypeCorrectly',
        'PSUseProcessBlockForPipelineCommand',
        'PSUsePSCredentialType',
        'PSUseShouldProcessForStateChangingFunctions',
        'PSUseSingularNouns',
        'PSUseSupportsShouldProcess',
        'PSUseToExportFieldsInManifest',
        'PSUseUsingScopeModifierInNewRunspaces',
        'PSUseUTF8EncodingForHelpFile'
    )

    # === EXCLUDE RULES ===
    # Rules we intentionally disable for Win11Forge
    ExcludeRules = @(
        # We use Write-Host extensively for UI (GUI.ps1, Deploy-Win11Environment.ps1)
        'PSAvoidUsingWriteHost',

        # We use positional parameters in some internal functions for brevity
        'PSAvoidUsingPositionalParameters',

        # Comment help is provided but may not match strict format
        'PSProvideCommentHelp',

        # Some unused parameters are intentional for interface compatibility
        'PSReviewUnusedParameter'
    )

    # === CODE FORMATTING ===
    Rules = @{
        # Indentation: 4 spaces
        PSUseConsistentIndentation = @{
            Enable = $true
            Kind = 'space'
            IndentationSize = 4
        }

        # Whitespace
        PSUseConsistentWhitespace = @{
            Enable = $true
            CheckOpenBrace = $true
            CheckOpenParen = $true
            CheckOperator = $true
            CheckSeparator = $true
        }

        # Brace placement
        PSPlaceOpenBrace = @{
            Enable = $true
            OnSameLine = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $true
        }

        PSPlaceCloseBrace = @{
            Enable = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $true
            NoEmptyLineBefore = $false
        }

        # Cmdlet aliases
        PSAvoidUsingCmdletAliases = @{
            Enable = $true
        }

        # Correct casing
        PSUseCorrectCasing = @{
            Enable = $true
        }

        # Compatible syntax (PowerShell 5.1+)
        PSUseCompatibleSyntax = @{
            Enable = $true
            TargetVersions = @(
                '5.1',
                '7.0',
                '7.1',
                '7.2',
                '7.3',
                '7.4'
            )
        }

        # Compatible cmdlets
        PSUseCompatibleCmdlets = @{
            Enable = $true
            Compatibility = @(
                'desktop-5.1.14393.206-windows',
                'core-7.4.0-windows'
            )
        }
    }
}
