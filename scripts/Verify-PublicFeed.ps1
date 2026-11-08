#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Repository.ps1')
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$version = Get-IntentVersion -RepositoryRoot $root
$results = Join-Path $root 'artifacts/step6'; New-Item -ItemType Directory -Path $results -Force | Out-Null
$consumers = [Collections.Generic.List[object]]::new()
foreach ($name in @('Quickstart', 'Examples', 'ProductionHost')) {
    $log = Join-Path $results ($name + '-public-consumer.log')
    & (Join-Path $PSScriptRoot ('Verify-' + $name + '.ps1')) -PublicFeed *>&1 | Tee-Object -FilePath $log
    $content = [IO.File]::ReadAllText($log)
    $match = [regex]::Match($content, 'Verified (?:independent package-only quickstart \(public feed: True\)|three independent package-backed examples \(public feed: True\)|independent package-only production host recipe): (.+)')
    if (-not $match.Success) { throw 'The public consumer did not report a completed execution.' }
    $consumerRoot = $match.Groups[1].Value.Trim()
    $frameworkPackages = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($assetFile in Get-ChildItem -LiteralPath $consumerRoot -Filter project.assets.json -Recurse -File) {
        $assets = Get-Content -Raw -LiteralPath $assetFile.FullName | ConvertFrom-Json -AsHashtable
        foreach ($entry in $assets.libraries.GetEnumerator()) {
            $id = $entry.Key.Split('/')[0]
            if ($id -notin @('IntentBlazor.Core','IntentBlazor.Runtime','IntentBlazor.Blazor','IntentBlazor.AI','IntentBlazor.Storage.Sqlite','IntentBlazor.Content','IntentBlazor.Packs.ImageEditor','IntentBlazor.Blazor.AI')) { continue }
            if ($entry.Value.type -ne 'package' -or $entry.Key.Split('/')[1] -ne $version) { throw 'A framework reference bypassed the exact public package version.' }
            [void] $frameworkPackages.Add($id)
            $packagePath = Join-Path $consumerRoot ('.packages/' + $id.ToLowerInvariant() + '/' + $version + '/.nupkg.metadata')
            $metadata = Get-Content -Raw -LiteralPath $packagePath | ConvertFrom-Json
            if ($metadata.source -ne 'https://api.nuget.org/v3/index.json') { throw 'A framework package was not resolved from the public source.' }
        }
    }
    if ($frameworkPackages.Count -lt 3) { throw 'The public consumer has incomplete framework package evidence.' }
    $consumers.Add(@{ consumer=$name; root=$consumerRoot; frameworkPackages=@($frameworkPackages | Sort-Object); source='https://api.nuget.org/v3/index.json'; passed=$true })
}
[ordered]@{ version=$version; checkedAt=[DateTimeOffset]::UtcNow.ToString('O'); mode='public-feed/offline-application-fixtures'; consumers=@($consumers.ToArray()) } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $results 'public-consumers.json') -Encoding utf8
Write-Host 'Fresh public-only quickstart, three example apps and production host consumers passed without model inference.'
