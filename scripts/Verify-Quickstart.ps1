#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$version = Get-IntentVersion -RepositoryRoot $repositoryRoot
$sampleRoot = Join-Path $repositoryRoot 'examples/Quickstart'
$feed = Join-Path $repositoryRoot 'artifacts/packages'
if (-not (Test-Path -LiteralPath (Join-Path $feed "IntentBlazor.Blazor.$version.nupkg"))) {
    throw 'Pack the framework first, or run scripts/Verify.ps1.'
}
$consumerRoot = Join-Path ([IO.Path]::GetTempPath()) ('IntentBlazor-Quickstart-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $consumerRoot | Out-Null
Copy-IntentSourceTree -Source $sampleRoot -Destination $consumerRoot
$projectPath = Join-Path $consumerRoot 'IntentBlazor.Quickstart.csproj'
[xml] $project = Get-Content -Raw -LiteralPath $projectPath
foreach ($reference in @($project.SelectNodes('//ProjectReference'))) {
    $package = $project.CreateElement('PackageReference')
    $package.SetAttribute('Include', [IO.Path]::GetFileNameWithoutExtension($reference.GetAttribute('Include')))
    $package.SetAttribute('Version', $version)
    [void] $reference.ParentNode.ReplaceChild($package, $reference)
}
foreach ($setting in @{ Nullable = 'enable'; ImplicitUsings = 'enable'; TreatWarningsAsErrors = 'true' }.GetEnumerator()) {
    $element = $project.CreateElement($setting.Key)
    $element.InnerText = $setting.Value
    [void] $project.Project.PropertyGroup.AppendChild($element)
}
$project.Save($projectPath)
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/ConsumerChecks/QuickstartCheck.cs.txt') -Destination (Join-Path $consumerRoot 'QuickstartCheck.cs')
$programPath = Join-Path $consumerRoot 'Program.cs'
$program = Get-Content -Raw -LiteralPath $programPath
$program = $program.Replace('var builder = WebApplication.CreateBuilder(args);', 'if (args.Contains("--verify-package-fixture")) { await IntentBlazor.Quickstart.QuickstartCheck.RunAsync(); return; }' + "`n" + 'var builder = WebApplication.CreateBuilder(args);')
[IO.File]::WriteAllText($programPath, $program)
$configPath = Join-Path $consumerRoot 'NuGet.Config'
Write-IntentPackageSources -Path $configPath -Feed $feed
& dotnet restore $projectPath --configfile $configPath --packages (Join-Path $consumerRoot '.packages') --force --no-cache
if ($LASTEXITCODE -ne 0) { throw 'The package-only quickstart restore failed.' }
& dotnet build $projectPath --no-restore --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'The package-only quickstart build failed.' }
Invoke-IntentDotNet -Arguments @((Join-Path $consumerRoot 'bin/Release/net10.0/IntentBlazor.Quickstart.dll'), '--verify-package-fixture')
[IO.File]::WriteAllText((Join-Path $repositoryRoot 'artifacts/phase7-quickstart-path.txt'), $consumerRoot)
Write-Host "Verified independent package-only quickstart: $consumerRoot"
