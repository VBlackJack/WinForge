[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = 'GUI\Win11Forge.GUI\Resources\Resources.fr.resx'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resourcePath = if ([System.IO.Path]::IsPathRooted($Path)) {
    $Path
}
else {
    Join-Path -Path (Get-Location) -ChildPath $Path
}

if (-not (Test-Path -LiteralPath $resourcePath)) {
    Write-Error "French resource file not found: $resourcePath"
    exit 1
}

[xml]$resources = Get-Content -LiteralPath $resourcePath -Raw

$patterns = @(
    @{ Stem = 'Echec'; Pattern = '\b[Ee]chec(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'echoue'; Pattern = '\b[Ee]choue(?:e|es|s)?\b'; Expected = 'accented form' }
    @{ Stem = 'Parametre'; Pattern = '\b[Pp]arametre(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'Selection'; Pattern = '\b[Ss]election(?:ner|nez|ne|nes|nee|nees|s)?\b'; Expected = 'accented form' }
    @{ Stem = 'Desinstall'; Pattern = '\b[Dd]esinstall(?:er|e|es|ee|ees|ation)?\b'; Expected = 'accented form' }
    @{ Stem = 'Detection'; Pattern = '\b[Dd]etection\b'; Expected = 'accented form' }
    @{ Stem = 'detecter'; Pattern = '\b[Dd]etecter\b'; Expected = 'accented form' }
    @{ Stem = 'Verification'; Pattern = '\b[Vv]erification\b'; Expected = 'accented form' }
    @{ Stem = 'verifier'; Pattern = '\b[Vv]erifier\b'; Expected = 'accented form' }
    @{ Stem = 'Operation'; Pattern = '\b[Oo]peration(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'Reessayer'; Pattern = '\b[Rr]eessayer\b'; Expected = 'accented form' }
    @{ Stem = 'Activite'; Pattern = '\b[Aa]ctivite\b'; Expected = 'accented form' }
    @{ Stem = 'recent'; Pattern = '\b[Rr]ecent(?:e|es|s)?\b'; Expected = 'accented form' }
    @{ Stem = 'Editeur'; Pattern = '\b[Ee]diteur\b'; Expected = 'accented form' }
    @{ Stem = 'Deploiement'; Pattern = '\b[Dd]eploiement(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'Prerequis'; Pattern = '\b[Pp]rerequis\b'; Expected = 'accented form' }
    @{ Stem = 'succes'; Pattern = '\b[Ss]ucces\b'; Expected = 'accented form' }
    @{ Stem = 'a jour'; Pattern = '\ba jour\b'; Expected = 'accented form' }
    @{ Stem = 'etre'; Pattern = '\b[Ee]tre\b'; Expected = 'accented form' }
    @{ Stem = 'ete'; Pattern = "\b[Ee]te\b"; Expected = 'accented form' }
    @{ Stem = 'resultat'; Pattern = '\b[Rr]esultat(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'element'; Pattern = '\b[Ee]lement(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'securite'; Pattern = '\b[Ss]ecurite\b'; Expected = 'accented form' }
    @{ Stem = 'delai'; Pattern = '\b[Dd]elai(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'fleche'; Pattern = '\b[Ff]leche(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'Entree'; Pattern = '\b[Ee]ntree(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'Reference'; Pattern = '\b[Rr]eference(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'methode'; Pattern = '\b[Mm]ethode(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'deja'; Pattern = '\b[Dd]eja\b'; Expected = 'accented form' }
    @{ Stem = 'irreversible'; Pattern = '\b[Ii]rreversible\b'; Expected = 'accented form' }
    @{ Stem = 'systeme'; Pattern = '\b[Ss]ysteme(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'planifie'; Pattern = '\b[Pp]lanifie(?:e|es|s)?\b'; Expected = 'accented form' }
    @{ Stem = 'cree'; Pattern = '\b[Cc]ree(?:e|es|s)?\b'; Expected = 'accented form' }
    @{ Stem = 'demarr'; Pattern = '\b[Dd]emarr(?:er|age|e|es|ee|ees)?\b'; Expected = 'accented form' }
    @{ Stem = 'categorie'; Pattern = '\b[Cc]ategorie(?:s)?\b'; Expected = 'accented form' }
    @{ Stem = 'telechargement'; Pattern = '\b[Tt]elechargement\b'; Expected = 'accented form' }
    @{ Stem = 'depot'; Pattern = '\b[Dd]epot\b'; Expected = 'accented form' }
    @{ Stem = 'peut-etre'; Pattern = '\b[Pp]eut-etre\b'; Expected = 'accented form' }
)

$failures = [System.Collections.Generic.List[object]]::new()

foreach ($data in $resources.root.data) {
    $value = [string]$data.value
    if ([string]::IsNullOrWhiteSpace($value)) {
        continue
    }

    foreach ($pattern in $patterns) {
        if ($value -match $pattern.Pattern) {
            $failures.Add([pscustomobject]@{
                    Key      = [string]$data.name
                    Stem     = $pattern.Stem
                    Expected = $pattern.Expected
                    Value    = $value
                })
        }
    }
}

if ($failures.Count -gt 0) {
    $details = $failures | Format-Table -AutoSize -Wrap | Out-String
    Write-Error "French diacritics lint found $($failures.Count) issue(s):`n$details"
    exit 1
}

Write-Output "French diacritics lint passed for $resourcePath"
