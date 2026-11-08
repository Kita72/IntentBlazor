#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $AssetsPath, [Parameter(Mandatory)][string] $Destination)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$assets = Get-Content -Raw -LiteralPath $AssetsPath | ConvertFrom-Json -AsHashtable
$destinationRoot = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null
$entries = [Collections.Generic.List[object]]::new()
foreach ($entry in $assets.libraries.GetEnumerator() | Sort-Object Key) {
    if ($entry.Value.type -ne 'package') { continue }
    $parts = $entry.Key.Split('/')
    if ($parts.Count -ne 2 -or $parts[0] -notmatch '^[A-Za-z0-9_.-]+$' -or $parts[1] -notmatch '^[A-Za-z0-9_.+-]+$') { throw 'Invalid dependency identity.' }
    $packageRoot = $null
    foreach ($folder in $assets.packageFolders.Keys) {
        $candidate = Join-Path $folder $entry.Value.path
        if (Test-Path -LiteralPath $candidate -PathType Container) { $packageRoot = [IO.Path]::GetFullPath($candidate); break }
    }
    if ($null -eq $packageRoot) { throw 'A restored dependency package is missing.' }
    $nuspec = @(Get-ChildItem -LiteralPath $packageRoot -Filter '*.nuspec' -File)
    if ($nuspec.Count -ne 1) { throw 'A dependency package has ambiguous metadata.' }
    $settings = [Xml.XmlReaderSettings]::new(); $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit; $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($nuspec[0].FullName, $settings)
    try { $xml = [Xml.XmlDocument]::new(); $xml.XmlResolver = $null; $xml.Load($reader) } finally { $reader.Dispose() }
    $license = $xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="license"]')
    $url = $xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="licenseUrl"]')
    if ($null -eq $license -and $null -eq $url) { throw ('Review the missing license metadata for ' + $entry.Key) }
    $notices = [Collections.Generic.List[object]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Where-Object { $_.Name -match '^(license|licence|copying|copyright|notice|third[._ -]?party[._ -]?notices)([._ -].*)?$' }) {
        if ($file.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) -or $file.Length -gt 4194304) { throw 'A dependency notice is linked or excessive.' }
        $relative = [IO.Path]::GetRelativePath($packageRoot, $file.FullName).Replace('\', '/')
        $targetRelative = 'licenses/' + $parts[0] + '/' + $parts[1] + '/' + $relative
        $target = [IO.Path]::GetFullPath((Join-Path $destinationRoot $targetRelative))
        if (-not $target.StartsWith($destinationRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'A notice path escaped the bundle.' }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
        $notices.Add(@{ path = $targetRelative; sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash })
    }
    $entries.Add([ordered]@{ package = $parts[0]; version = $parts[1]; licenseType = $(if ($null -ne $license) { $license.GetAttribute('type') } else { 'url' });
        license = $(if ($null -ne $license) { $license.InnerText } else { $url.InnerText }); notices = @($notices.ToArray()) })
}
[ordered]@{ scope = 'exact restored docs graph; includes transitive/build dependencies'; packages = @($entries.ToArray()) } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destinationRoot 'dependency-notices.json') -Encoding utf8
Write-Host "Recorded license metadata for $($entries.Count) restored dependencies and copied their bundled notices."
