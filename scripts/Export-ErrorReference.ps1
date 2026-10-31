#requires -Version 7.0
[CmdletBinding()]
param([switch] $Verify)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$entries = [Collections.Generic.Dictionary[string, Collections.Generic.HashSet[string]]]::new([StringComparer]::Ordinal)
$patterns = @(
    '(?s)\bNeed\((?:(?!;).)*?,\s*"(?<code>[a-z][a-z0-9-]+)"\s*,',
    '\b\w*(?:Fail|Failure)\(\s*"(?<code>[a-z][a-z0-9-]+)"'
)
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Filter '*.cs' -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    $path = [IO.Path]::GetRelativePath($repositoryRoot, $file.FullName).Replace('\', '/')
    foreach ($pattern in $patterns) {
        foreach ($match in [regex]::Matches($content, $pattern)) {
            $code = $match.Groups['code'].Value
            if (-not $entries.ContainsKey($code)) { $entries[$code] = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal) }
            [void] $entries[$code].Add($path)
        }
    }
}
if ($entries.Count -lt 50) { throw 'Error extraction found too few codes; review the source patterns.' }
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Framework error code index')
$lines.Add('')
$lines.Add('Generated from literal validator, session, and setup failure call sites by [Export-ErrorReference.ps1](../../scripts/Export-ErrorReference.ps1). Run it after changing a code; verification detects drift. Codes identify a failure category; the returned path and message explain the specific condition. Application-defined actions and renderer contracts may add their own messages/codes.')
$lines.Add('')
$lines.Add('See [errors and recovery](errors.md) for recovery rules, action outcomes, and normalized provider/content failures. Links point to the canonical source for each code, including its current bounds and message. This is a source index, not a promise of every possible host exception.')
$lines.Add('')
$lines.Add('| Code | Canonical source |')
$lines.Add('| --- | --- |')
foreach ($code in $entries.Keys | Sort-Object -CaseSensitive) {
    $links = @($entries[$code] | Sort-Object -CaseSensitive | ForEach-Object { "[$([IO.Path]::GetFileName($_))](../../$_)" }) -join ', '
    $lines.Add('| `' + $code + '` | ' + $links + ' |')
}
$expected = ($lines -join "`n") + "`n"
$target = Join-Path $repositoryRoot 'docs/site/error-codes.md'
if ($Verify) {
    if (-not (Test-Path -LiteralPath $target) -or (Get-Content -Raw -LiteralPath $target).Replace("`r`n", "`n") -cne $expected) {
        throw 'The error reference is stale. Run pwsh -File scripts/Export-ErrorReference.ps1.'
    }
    Write-Host "Verified $($entries.Count) documented framework error codes."
}
else { [IO.File]::WriteAllText($target, $expected) }
