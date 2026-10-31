#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$version = Get-IntentVersion -RepositoryRoot $repositoryRoot
$revision = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source revision could not be established.' }
$tool = Join-Path $repositoryRoot 'tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll'
Invoke-IntentDotNet -Arguments @($tool, 'packages', (Join-Path $repositoryRoot 'artifacts/packages'), $version, $revision,
    (Join-Path $repositoryRoot 'artifacts/phase7/evidence/packages.json'))
