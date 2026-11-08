#requires -Version 7.0
[CmdletBinding()]
param([switch] $AllowPublicHost, [switch] $PrepareOnly)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $AllowPublicHost -and -not $PrepareOnly) { throw 'Public network verification is separate from offline tests. Invoke -AllowPublicHost only after Tory explicitly requests it.' }
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$tool = Join-Path $repositoryRoot 'tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll'
$evidence = Join-Path $repositoryRoot 'artifacts/phase7/evidence'; New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$candidatePath = Join-Path $evidence 'host-candidate.json'
Invoke-IntentDotNet -Arguments @($tool, 'candidate', $repositoryRoot, (Get-IntentVersion -RepositoryRoot $repositoryRoot), $candidatePath)
$candidate = Get-Content -Raw -LiteralPath $candidatePath | ConvertFrom-Json
if (-not $PrepareOnly -and -not $candidate.clean) { throw 'Commit the exact candidate before recording public-host evidence.' }
$scratch = Join-Path $repositoryRoot ('artifacts/host-smoke/' + [Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$reference = [Security.SecurityElement]::Escape((Join-Path $repositoryRoot 'tools/IntentBlazor.Verification/IntentBlazor.Verification.csproj'))
# Central versions and SDK are inherited from this ignored repository-local tool copy.
[IO.File]::WriteAllText((Join-Path $scratch 'HostSmoke.csproj'), @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><IsPackable>false</IsPackable></PropertyGroup><ItemGroup><ProjectReference Include="$reference" /><PackageReference Include="Microsoft.Playwright" /></ItemGroup></Project>
"@)
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/HostSmoke/DocsHostSmoke.cs') -Destination (Join-Path $scratch 'DocsHostSmoke.cs')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/HostSmoke/Program.cs.txt') -Destination (Join-Path $scratch 'Program.cs')
Invoke-IntentDotNet -Arguments @('build', (Join-Path $scratch 'HostSmoke.csproj'), '-c', 'Release')
if ($PrepareOnly) { Write-Host "Compiled the recorder without public requests: $scratch"; return }
& (Join-Path $scratch 'bin/Release/net10.0/playwright.ps1') install --only-shell chromium
if ($LASTEXITCODE -ne 0) { throw 'Chromium installation failed.' }
Invoke-IntentDotNet -Arguments @((Join-Path $scratch 'bin/Release/net10.0/HostSmoke.dll'), $candidatePath, (Join-Path $evidence 'host.json'), '--allow-public-host')
