#requires -Version 7.0
[CmdletBinding()]
param([switch] $NoBuild, [switch] $InstallDependencies)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$project = Join-Path $repositoryRoot 'tests/IntentBlazor.BrowserTests/IntentBlazor.BrowserTests.csproj'
$output = Join-Path $repositoryRoot 'tests/IntentBlazor.BrowserTests/bin/Release/net10.0'
$results = Join-Path $repositoryRoot 'artifacts/browser'
New-Item -ItemType Directory -Path $results -Force | Out-Null
if (-not $NoBuild) {
    Invoke-IntentDotNet -Arguments @('restore', (Join-Path $repositoryRoot 'IntentBlazor.slnx'), '--locked-mode')
    Invoke-IntentDotNet -Arguments @('build', (Join-Path $repositoryRoot 'IntentBlazor.slnx'), '--no-restore', '-c', 'Release')
    foreach ($library in Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Filter '*.csproj' -Recurse -File) {
        Invoke-IntentDotNet -Arguments @('pack', $library.FullName, '--no-build', '--no-restore', '-c', 'Release', '--output', (Join-Path $repositoryRoot 'artifacts/packages'))
    }
    & (Join-Path $PSScriptRoot 'Verify-Quickstart.ps1')
}
$installer = Join-Path $output 'playwright.ps1'
if (-not (Test-Path -LiteralPath $installer)) { throw 'Build the browser project before using -NoBuild.' }
$installArguments = @('install', '--only-shell', 'chromium')
if ($InstallDependencies -and $IsLinux) { $installArguments += '--with-deps' }
& $installer @installArguments
if ($LASTEXITCODE -ne 0) { throw 'Playwright browser/dependency installation failed.' }
Invoke-IntentDotNet -Arguments @('test', $project, '--no-build', '--no-restore', '--configuration', 'Release',
    '--logger', 'trx;LogFileName=browser.trx', '--results-directory', $results)
[xml] $trx = Get-Content -Raw -LiteralPath (Join-Path $results 'browser.trx')
$counters = $trx.SelectSingleNode('/*[local-name()="TestRun"]/*[local-name()="ResultSummary"]/*[local-name()="Counters"]')
if ($null -eq $counters -or [int] $counters.total -le 0 -or [int] $counters.passed -ne [int] $counters.total -or
    [int] $counters.executed -ne [int] $counters.total -or [int] $counters.failed -ne 0 -or [int] $counters.notExecuted -ne 0) {
    throw 'Browser TRX is missing, incomplete, failed, or contains skipped cases.'
}
$revision = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'The browser evidence source revision could not be established.' }
$workingChanges = & git -C $repositoryRoot status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'The browser evidence working-tree state could not be established.' }
$summary = [ordered]@{
    commit = $revision
    workingTreeDirty = -not [string]::IsNullOrWhiteSpace(($workingChanges -join "`n"))
    checkedAt = [DateTimeOffset]::UtcNow.ToString('O')
    total = [int] $counters.total
    passed = [int] $counters.passed
    failed = [int] $counters.failed
    skipped = [int] $counters.notExecuted
    mode = 'offline-fixtures'
    browser = 'Chromium headless; GPU and WebGL disabled'
}
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $results 'summary.json') -Encoding utf8
Write-Host "Browser regression verification completed: $($counters.passed) passed, 0 failed, 0 skipped."
