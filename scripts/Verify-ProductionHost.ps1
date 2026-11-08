#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$version = Get-IntentVersion -RepositoryRoot $repositoryRoot
$feed = Join-Path $repositoryRoot 'artifacts/packages'
if (-not (Test-Path -LiteralPath (Join-Path $feed "IntentBlazor.Blazor.AI.$version.nupkg"))) { throw 'Pack the framework first, or run scripts/Verify.ps1.' }
$consumerRoot = Join-Path ([IO.Path]::GetTempPath()) ('IntentBlazor-ProductionHost-' + [Guid]::NewGuid().ToString('N'))
Copy-IntentSourceTree -Source (Join-Path $repositoryRoot 'examples/ProductionHost') -Destination $consumerRoot
$projectPath = Join-Path $consumerRoot 'IntentBlazor.ProductionHost.csproj'
Convert-IntentFrameworkReferences -ProjectPath $projectPath -Version $version
[xml] $project = Get-Content -Raw -LiteralPath $projectPath
if ($project.SelectNodes('//ProjectReference').Count -ne 0) { throw 'The independent host consumer still references source projects.' }
$warnings = $project.CreateElement('TreatWarningsAsErrors'); $warnings.InnerText = 'true'; [void] $project.Project.PropertyGroup.AppendChild($warnings)
$project.Save($projectPath)
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/ConsumerChecks/ProductionHostCheck.cs.txt') -Destination (Join-Path $consumerRoot 'ProductionHostCheck.cs')
$programPath = Join-Path $consumerRoot 'Program.cs'
$program = Get-Content -Raw -LiteralPath $programPath
$anchor = 'var app = ProductionHostApp.Build('
if (-not $program.Contains($anchor)) { throw 'The host bootstrap changed; update the package-consumer entry point.' }
[IO.File]::WriteAllText($programPath, $program.Replace($anchor, 'if (args.Contains("--verify-host-package")) { await ProductionHostCheck.RunAsync(); return; }' + "`n" + $anchor))
$configPath = Join-Path $consumerRoot 'NuGet.Config'; Write-IntentPackageSources -Path $configPath -Feed $feed
Invoke-IntentDotNet -Arguments @('restore', $projectPath, '--configfile', $configPath, '--packages', (Join-Path $consumerRoot '.packages'), '--no-cache')
Invoke-IntentDotNet -Arguments @('build', $projectPath, '--no-restore', '-c', 'Release')
$framework = Get-IntentTargetFramework -ProjectPath $projectPath
Invoke-IntentDotNet -Arguments @((Join-Path $consumerRoot "bin/Release/$framework/IntentBlazor.ProductionHost.dll"), '--verify-host-package')
$results = Join-Path $repositoryRoot 'artifacts/step4'; New-Item -ItemType Directory -Path $results -Force | Out-Null
[ordered]@{ mode='offline-package-consumer'; version=$version; sourceCommit=(& git -C $repositoryRoot rev-parse HEAD); consumerRoot=$consumerRoot; sourceProjectReferences=0; passed=$true } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $results 'production-host-consumer.json') -Encoding utf8
Write-Host "Verified independent package-only production host recipe: $consumerRoot"
