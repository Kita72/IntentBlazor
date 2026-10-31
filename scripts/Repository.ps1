#requires -Version 7.0
Set-StrictMode -Version Latest

function Get-IntentVersion {
    param([Parameter(Mandatory)][string] $RepositoryRoot)
    [xml] $properties = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Directory.Build.props')
    $version = [string] $properties.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$') { throw 'The shared package version is invalid.' }
    return $version
}

function Invoke-IntentDotNet {
    param([Parameter(Mandatory)][string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed with exit code $LASTEXITCODE." }
}

function Write-IntentPackageSources {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string] $Feed)
    $escapedFeed = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($Feed))
    [IO.File]::WriteAllText($Path, @"
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
}

function Copy-IntentSourceTree {
    param([Parameter(Mandatory)][string] $Source, [Parameter(Mandatory)][string] $Destination)
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) {
        if ($item.Name -in @('bin', 'obj', 'artifacts', 'packages.lock.json', 'Properties') -or
            $item.Name -like '.env*' -or $item.Name -like '*.Local.json' -or $item.Name -like '*.user' -or $item.Name -like '*.publishsettings') { continue }
        $target = Join-Path $Destination $item.Name
        if ($item.PSIsContainer) { Copy-IntentSourceTree -Source $item.FullName -Destination $target }
        else { Copy-Item -LiteralPath $item.FullName -Destination $target }
    }
}

function Convert-IntentFrameworkReferences {
    param([Parameter(Mandatory)][string] $ProjectPath, [Parameter(Mandatory)][string] $Version)
    [xml] $project = Get-Content -Raw -LiteralPath $ProjectPath
    foreach ($reference in @($project.SelectNodes('//ProjectReference'))) {
        $include = $reference.GetAttribute('Include').Replace('\', '/')
        if ($include -notmatch '(^|/)src/') { continue }
        $package = $project.CreateElement('PackageReference')
        $package.SetAttribute('Include', [IO.Path]::GetFileNameWithoutExtension($include))
        $package.SetAttribute('Version', $Version)
        [void] $reference.ParentNode.ReplaceChild($package, $reference)
    }
    $project.Save($ProjectPath)
}
