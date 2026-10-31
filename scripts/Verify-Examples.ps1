#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$version = Get-IntentVersion -RepositoryRoot $repositoryRoot
$consumerRoot = Join-Path ([IO.Path]::GetTempPath()) ('IntentBlazor-Examples-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $consumerRoot | Out-Null
$exampleNames = @('IntentBlazor.ExampleHost', 'IntentBlazor.Examples', 'GamingNews', 'StaffBusiness', 'AssetWorkbench')
foreach ($name in $exampleNames) { Copy-IntentSourceTree -Source (Join-Path $repositoryRoot "examples/$name") -Destination (Join-Path $consumerRoot "examples/$name") }
Copy-IntentSourceTree -Source (Join-Path $repositoryRoot 'specs/fixtures') -Destination (Join-Path $consumerRoot 'specs/fixtures')
[IO.File]::WriteAllText((Join-Path $consumerRoot 'Directory.Build.props'), '<Project><PropertyGroup><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>')
[xml] $versions = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Directory.Packages.props')
$central = $versions.CreateElement('CentralPackageVersionOverrideEnabled'); $central.InnerText = 'true'; [void] $versions.Project.PropertyGroup.AppendChild($central)
$versions.Save((Join-Path $consumerRoot 'Directory.Packages.props'))
foreach ($project in Get-ChildItem -LiteralPath (Join-Path $consumerRoot 'examples') -Filter '*.csproj' -Recurse -File) {
    Convert-IntentFrameworkReferences -ProjectPath $project.FullName -Version $version
    # Central third-party versions coexist with explicit versions for unpublished framework artifacts.
    [xml] $xml = Get-Content -Raw -LiteralPath $project.FullName
    foreach ($package in $xml.SelectNodes('//PackageReference[@Version]')) {
        $package.SetAttribute('VersionOverride', $package.GetAttribute('Version')); $package.RemoveAttribute('Version')
    }
    $xml.Save($project.FullName)
}
$checks = @(
    @{name='GamingNews';assembly='IntentBlazor.GamingNews';expected='Recent updates';kind='news'},
    @{name='StaffBusiness';assembly='IntentBlazor.StaffBusiness';expected='Jobs waiting on materials';kind='staff'},
    @{name='AssetWorkbench';assembly='IntentBlazor.AssetWorkbench';expected='Dimension inspection';kind='assets'}
)
foreach ($check in $checks) {
    $directory = Join-Path $consumerRoot ('examples/' + $check.name)
    $source = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'tools/ConsumerChecks/ExampleCheck.cs.txt')
    $initialize = if ($check.kind -eq 'staff') { 'await IntentBlazor.StaffBusiness.EfStaffRepository.InitializeAsync(app.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<IntentBlazor.StaffBusiness.BusinessDbContext>>());' } else { '' }
    [IO.File]::WriteAllText((Join-Path $directory 'ExampleCheck.cs'), $source.Replace('/* INITIALIZE_STAFF */', $initialize))
    $programPath = Join-Path $directory 'Program.cs'
    $program = Get-Content -Raw -LiteralPath $programPath
    $replacement = 'if (args.Contains("--verify-package-fixture")) IntentBlazor.PackageChecks.ExampleCheck.Configure(builder.Services);' + "`n" +
        'var app = builder.Build();' + "`n" + 'if (args.Contains("--verify-package-fixture")) { await IntentBlazor.PackageChecks.ExampleCheck.RunAsync(app, "' + $check.expected + '"); return; }'
    if (-not $program.Contains('var app = builder.Build();')) { throw 'The app bootstrap changed; update the package-consumer check.' }
    [IO.File]::WriteAllText($programPath, $program.Replace('var app = builder.Build();', $replacement))
}
$solution = '<Solution>' + ((Get-ChildItem -LiteralPath (Join-Path $consumerRoot 'examples') -Filter '*.csproj' -Recurse -File |
    ForEach-Object { '<Project Path="' + [IO.Path]::GetRelativePath($consumerRoot, $_.FullName).Replace('\', '/') + '" />' }) -join '') + '</Solution>'
$solutionPath = Join-Path $consumerRoot 'Examples.slnx'; [IO.File]::WriteAllText($solutionPath, $solution)
$config = Join-Path $consumerRoot 'NuGet.Config'; Write-IntentPackageSources -Path $config -Feed (Join-Path $repositoryRoot 'artifacts/packages')
Invoke-IntentDotNet -Arguments @('restore', $solutionPath, '--configfile', $config, '--packages', (Join-Path $consumerRoot '.packages'), '--no-cache')
Invoke-IntentDotNet -Arguments @('build', $solutionPath, '--no-restore', '--configuration', 'Release')
foreach ($check in $checks) {
    $assembly = Join-Path $consumerRoot ('examples/' + $check.name + '/bin/Release/net10.0/' + $check.assembly + '.dll')
    Invoke-IntentDotNet -Arguments @($assembly, '--verify-package-fixture', 'true', '--environment', 'Development', '--Ai:Provider', 'Fixture', '--Example:DataDirectory', (Join-Path $consumerRoot ('.fixture-data/' + $check.kind)))
}
Write-Host "Verified three independent package-backed examples: $consumerRoot"
