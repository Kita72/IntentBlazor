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

function Test-IntentNuGetPushAcknowledgement {
    param([AllowEmptyString()][string] $Output, [int] $ExitCode, [string] $PackageId, [string] $Version)
    if ($ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($Output)) { return $false }
    $name = [regex]::Escape($PackageId + '.' + $Version)
    $packagePush = [regex]::Match($Output, ('(?m)^Pushing ' + $name + '\.nupkg to '))
    $symbolsPush = [regex]::Match($Output, ('(?m)^Pushing ' + $name + '\.snupkg to '))
    if (-not $packagePush.Success -or -not $symbolsPush.Success -or $symbolsPush.Index -le $packagePush.Index) { return $false }
    $packageSegment = $Output.Substring($packagePush.Index, $symbolsPush.Index - $packagePush.Index)
    $symbolsSegment = $Output.Substring($symbolsPush.Index)
    # NuGet 7.9 uses the generic acknowledgement for both uploads. Older clients
    # can name symbols explicitly; each artifact still needs its own success.
    $acknowledgement = '(?m)^Your (?:symbols )?package was pushed\.\s*$'
    return [regex]::Matches($packageSegment, $acknowledgement).Count -eq 1 -and
        [regex]::Matches($symbolsSegment, $acknowledgement).Count -eq 1
}

function Get-IntentTargetFramework {
    param([Parameter(Mandatory)][string] $ProjectPath)
    [xml] $project = Get-Content -Raw -LiteralPath $ProjectPath
    $nodes = $project.SelectNodes('//TargetFramework')
    if ($nodes.Count -ne 1) { throw 'Expected one explicit .NET target framework.' }
    $framework = [string] $nodes[0].InnerText
    if ($framework -notmatch '^net[0-9]+\.[0-9]+$') { throw 'Expected one explicit .NET target framework.' }
    return $framework
}

function Write-IntentPackageSources {
    param([Parameter(Mandatory)][string] $Path, [string] $Feed, [switch] $PublicFeed)
    if ($PublicFeed) {
        [IO.File]::WriteAllText($Path, @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
  <packageSourceMapping><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
'@)
        return
    }
    if ([string]::IsNullOrWhiteSpace($Feed)) { throw 'A local feed is required unless -PublicFeed is selected.' }
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
