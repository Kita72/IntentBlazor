#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$version = Get-IntentVersion -RepositoryRoot $repositoryRoot
$revision = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source revision could not be established.' }
$output = Join-Path $repositoryRoot ('artifacts/deploy/intentblazor-docs-' + $version + '-' + [Guid]::NewGuid().ToString('N'))
Invoke-IntentDotNet -Arguments @('publish', (Join-Path $repositoryRoot 'apps/IntentBlazor.Docs'), '--no-restore', '--configuration', 'Release', '--output', $output,
    '--self-contained', 'false', '-p:EnvironmentName=Production')
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
[pscustomobject]@{
    formatVersion = 1; version = $version; sourceRevision = $revision; target = 'MonsterASP / IIS';
    runtime = 'net10.0'; selfContained = $false; environment = 'Production'; mode = 'fixture'; createdAt = [DateTimeOffset]::UtcNow
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'deployment-manifest.json')
$archive = $output + '.zip'
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $output -Force | ForEach-Object FullName) -DestinationPath $archive
[IO.File]::WriteAllText((Join-Path $repositoryRoot 'artifacts/phase7-docs-path.txt'), $output)
Write-Host "IIS publish folder: $output"
Write-Host "Folder bundle: $archive"
Write-Host 'Use the MonsterASP Web Deploy profile with the docs project when the host is configured. This bundle is an IIS folder artifact; publication has not occurred.'
