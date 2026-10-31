#requires -Version 7.0
[CmdletBinding()]
param([switch] $RequireReady)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$tool = Join-Path $repositoryRoot 'tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll'
$evidence = Join-Path $repositoryRoot 'artifacts/phase7/evidence'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
foreach ($provider in @('ollama', 'lmstudio', 'openai')) {
    $recorded = Join-Path $repositoryRoot ('verification/providers/' + $provider + '.json')
    $local = Join-Path $evidence ($provider + '.json')
    if ((Test-Path -LiteralPath $recorded) -and -not (Test-Path -LiteralPath $local)) { Copy-Item -LiteralPath $recorded -Destination $local }
}
Invoke-IntentDotNet -Arguments @($tool, 'runtime', $repositoryRoot, (Join-Path $evidence 'runtime.json'))
$arguments = @($tool, 'readiness', $repositoryRoot, (Get-IntentVersion -RepositoryRoot $repositoryRoot), $evidence, (Join-Path $evidence 'readiness.json'))
if ($RequireReady) { $arguments += 'require-ready' }
Invoke-IntentDotNet -Arguments $arguments
