#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Export-ErrorReference.ps1') -Verify
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$markdownFiles = @(Get-ChildItem -LiteralPath $repositoryRoot -Recurse -Filter '*.md' -File |
    Where-Object {
        $repositoryPath = [IO.Path]::GetRelativePath($repositoryRoot, $_.FullName)
        $repositoryPath -notmatch '(^|[\\/])(bin|obj|artifacts|\.git)[\\/]'
    })
if ($markdownFiles.Count -eq 0) {
    throw 'No Markdown files were found; documentation verification cannot succeed without checking files.'
}
$failures = [Collections.Generic.List[string]]::new()
$linkPattern = '\[[^\]]+\]\((?<target>[^\s)]+)(?:\s+"[^"]*")?\)'

foreach ($markdownFile in $markdownFiles) {
    $content = Get-Content -Raw -LiteralPath $markdownFile.FullName
    foreach ($match in [regex]::Matches($content, $linkPattern)) {
        $linkTarget = $match.Groups['target'].Value.Trim('<', '>')
        if ($linkTarget -match '^[a-zA-Z][a-zA-Z0-9+.-]*:' -or $linkTarget.StartsWith('#')) { continue }
        $relativeTarget = [Uri]::UnescapeDataString(($linkTarget -split '#', 2)[0])
        $baseDirectory = $markdownFile.DirectoryName
        if ($relativeTarget.StartsWith('/')) {
            $baseDirectory = $repositoryRoot
            $relativeTarget = $relativeTarget.TrimStart('/')
        }
        $resolvedTarget = [IO.Path]::GetFullPath((Join-Path $baseDirectory $relativeTarget))
        if (-not (Test-Path -LiteralPath $resolvedTarget)) {
            $sourcePath = [IO.Path]::GetRelativePath($repositoryRoot, $markdownFile.FullName)
            $failures.Add("$sourcePath -> $linkTarget")
        }
    }
}
if ($failures.Count -gt 0) {
    throw ("Broken local documentation links:" + [Environment]::NewLine + ($failures -join [Environment]::NewLine))
}
Write-Host "Checked local links in $($markdownFiles.Count) Markdown files."
