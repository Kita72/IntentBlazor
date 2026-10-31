#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sampleRoot = Join-Path $repositoryRoot 'examples/Quickstart'
$feed = Join-Path $repositoryRoot 'artifacts/packages'
if (-not (Test-Path -LiteralPath (Join-Path $feed 'IntentBlazor.Blazor.0.1.0-alpha.0.nupkg'))) {
    throw 'Pack the framework first, or run scripts/Verify.ps1.'
}
$consumerRoot = Join-Path ([IO.Path]::GetTempPath()) ('IntentBlazor-Quickstart-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $consumerRoot | Out-Null
Get-ChildItem -LiteralPath $sampleRoot | Where-Object { $_.Name -notin @('bin', 'obj', 'packages.lock.json', 'README.md') } |
    Copy-Item -Destination $consumerRoot -Recurse
$projectPath = Join-Path $consumerRoot 'IntentBlazor.Quickstart.csproj'
[xml] $project = Get-Content -Raw -LiteralPath $projectPath
foreach ($reference in @($project.SelectNodes('//ProjectReference'))) {
    $package = $project.CreateElement('PackageReference')
    $package.SetAttribute('Include', [IO.Path]::GetFileNameWithoutExtension($reference.GetAttribute('Include')))
    $package.SetAttribute('Version', '0.1.0-alpha.0')
    [void] $reference.ParentNode.ReplaceChild($package, $reference)
}
foreach ($setting in @{ Nullable = 'enable'; ImplicitUsings = 'enable'; TreatWarningsAsErrors = 'true' }.GetEnumerator()) {
    $element = $project.CreateElement($setting.Key)
    $element.InnerText = $setting.Value
    [void] $project.Project.PropertyGroup.AppendChild($element)
}
$project.Save($projectPath)
$configPath = Join-Path $consumerRoot 'NuGet.Config'
$escapedFeed = [Security.SecurityElement]::Escape($feed)
[IO.File]::WriteAllText($configPath, @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="IntentBlazorLocal" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="IntentBlazorLocal"><package pattern="IntentBlazor.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@)
& dotnet restore $projectPath --configfile $configPath --packages (Join-Path $consumerRoot '.packages') --force --no-cache
if ($LASTEXITCODE -ne 0) { throw 'The package-only quickstart restore failed.' }
& dotnet build $projectPath --no-restore --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'The package-only quickstart build failed.' }
Write-Host "Verified independent package-only quickstart: $consumerRoot"
