#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Tag)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = Get-IntentVersion -RepositoryRoot $repositoryRoot
if ($Tag -cne ('v' + $version) -or $version -notmatch '-preview\.[0-9]+$') { throw 'The release tag must exactly match the shared preview version.' }
$head = & git -C $repositoryRoot rev-parse HEAD
$tagRevision = & git -C $repositoryRoot rev-parse --verify ('refs/tags/' + $Tag + '^{commit}')
if ($LASTEXITCODE -ne 0 -or $head -ne $tagRevision) { throw 'The checkout must be the exact tagged commit.' }
& git -C $repositoryRoot merge-base --is-ancestor $head origin/main
if ($LASTEXITCODE -ne 0) { throw 'The release commit must be included on main.' }
$changes = & git -C $repositoryRoot status --porcelain
if ($LASTEXITCODE -ne 0 -or $changes) { throw 'A release requires a clean source checkout.' }
Write-Host ('Validated exact preview tag ' + $Tag + ' on main.')
