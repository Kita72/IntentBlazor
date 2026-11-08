#requires -Version 7.0
[CmdletBinding()]
param([switch] $SkipArchive)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne [Runtime.InteropServices.Architecture]::X64) {
    throw 'The IIS publisher includes real Windows x64 native checks. Run it on a suitable Windows x64 verification host.'
}
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$version = Get-IntentVersion -RepositoryRoot $repositoryRoot
$revision = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source revision could not be established.' }
$output = Join-Path $repositoryRoot ('artifacts/deploy/intentblazor-docs-' + $version + '-' + [Guid]::NewGuid().ToString('N'))
$tool = Join-Path $repositoryRoot 'tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll'
$candidatePath = Join-Path $repositoryRoot 'artifacts/step5/publish-candidate.json'
Invoke-IntentDotNet -Arguments @($tool, 'candidate', $repositoryRoot, $version, $candidatePath)
$candidate = Get-Content -Raw -LiteralPath $candidatePath | ConvertFrom-Json
$buildRoot = Join-Path $repositoryRoot ('artifacts/deploy/build-' + [Guid]::NewGuid().ToString('N'))
# Separate intermediates prevent host-specific publishing from changing portable package outputs.
Invoke-IntentDotNet -Arguments @('restore', (Join-Path $repositoryRoot 'apps/IntentBlazor.Docs'), '--locked-mode', '--artifacts-path', $buildRoot)
Invoke-IntentDotNet -Arguments @('publish', (Join-Path $repositoryRoot 'apps/IntentBlazor.Docs'), '--no-restore', '--configuration', 'Release', '--output', $output,
    '--runtime', 'win-x64', '--self-contained', 'false', '--artifacts-path', $buildRoot, '-p:UseAppHost=false', '-p:EnvironmentName=Production', ('-p:IntentSourceFingerprint=' + $candidate.sourceFingerprint))
& (Join-Path $PSScriptRoot 'Export-DependencyNotices.ps1') -AssetsPath (Join-Path $buildRoot 'obj/IntentBlazor.Docs/project.assets.json') -Destination $output
foreach ($notice in @('LICENSE', 'THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $repositoryRoot $notice) -Destination $output }
foreach ($required in @('web.config', 'IntentBlazor.Docs.dll', 'IntentBlazor.Docs.runtimeconfig.json', 'wwwroot')) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $required))) { throw "Published output is missing $required." }
}
$privateFiles = @(Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {
    $_.Name -like '.env*' -or $_.Name -like '*.Local.json' -or $_.Name -match '\.(db|sqlite|sqlite3)(-(wal|shm|journal))?$' -or
    $_.Name -like '*.pfx' -or $_.Name -like '*.publishsettings' -or $_.Name -like '*.pubxml.user'
})
if ($privateFiles.Count -gt 0) { throw 'Private/configuration artifacts were found in the publish output; no deployment bundle was created.' }
[xml] $config = Get-Content -Raw -LiteralPath (Join-Path $output 'web.config')
$aspnet = $config.SelectSingleNode('//aspNetCore')
if ($null -eq $aspnet -or $aspnet.GetAttribute('hostingModel') -ne 'inprocess') { throw 'The expected IIS ASP.NET Core Module configuration is missing.' }
Invoke-IntentDotNet -Arguments @($tool, 'bundle', $output, $repositoryRoot, $version)
Invoke-IntentDotNet -Arguments @((Join-Path $output 'IntentBlazor.Docs.dll'), '--verify-offline-publish')
$archive = $output + '.zip'
if (-not $SkipArchive) { Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $output -Force | ForEach-Object FullName) -DestinationPath $archive }
[IO.File]::WriteAllText((Join-Path $repositoryRoot 'artifacts/phase7-docs-path.txt'), $output)
Write-Host "IIS publish folder: $output"
if (-not $SkipArchive) { Write-Host "Folder bundle: $archive" }
Write-Host 'Use the MonsterASP Web Deploy profile with the docs project when the host is configured. This bundle is an IIS folder artifact; publication has not occurred.'
