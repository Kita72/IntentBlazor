#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string] $PublishSettingsPath,
    [Parameter(Mandatory)][string] $PublishDirectory,
    [Parameter(Mandatory)][string] $ExpectedSiteHost,
    [string] $MSDeployPath = 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$source = [IO.Path]::GetFullPath($PublishDirectory)
if ($ExpectedSiteHost -notmatch '^[a-z0-9][a-z0-9.-]*\.runasp\.net$') { throw 'ExpectedSiteHost must be the public MonsterASP DNS host, without a scheme or path.' }
$toolFramework = Get-IntentTargetFramework -ProjectPath (Join-Path $repositoryRoot 'tools/IntentBlazor.Verification/IntentBlazor.Verification.csproj')
$tool = Join-Path $repositoryRoot "tools/IntentBlazor.Verification/bin/Release/$toolFramework/IntentBlazor.Verification.dll"
# Validate every sealed payload byte before opening any credential file. -WhatIf is entirely offline.
Invoke-IntentDotNet -Arguments @($tool, 'bundle-verify', $source)
$manifest = Get-Content -Raw -LiteralPath (Join-Path $source 'deployment-manifest.json') | ConvertFrom-Json
if (-not $PSCmdlet.ShouldProcess(('https://' + $ExpectedSiteHost), 'Deploy the sealed Production docs bundle with Web Deploy (no remote deletions)')) { return }
if (-not (Test-Path -LiteralPath $MSDeployPath -PathType Leaf)) { throw 'Install Microsoft Web Deploy V3 before deploying.' }

# Credentials stay in memory. Never echo the profile, native arguments or raw native output.
$settings = [Xml.XmlReaderSettings]::new()
$settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
$settings.XmlResolver = $null
$reader = [Xml.XmlReader]::Create([IO.Path]::GetFullPath($PublishSettingsPath), $settings)
try {
    $document = [Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    $document.Load($reader)
}
finally { $reader.Dispose() }
$profiles = @($document.SelectNodes('//publishProfile[@publishMethod="MSDeploy"]'))
if ($profiles.Count -ne 1) { throw 'Expected exactly one MSDeploy publishing profile.' }
$profile = $profiles[0]
$destination = [Uri]$profile.GetAttribute('destinationAppUrl')
if (-not $destination.IsAbsoluteUri -or $destination.Host -ne $ExpectedSiteHost -or
    $destination.UserInfo -or $destination.Query -or $destination.Fragment) { throw 'The profile does not match the expected public site.' }
$site = $profile.GetAttribute('msdeploySite')
$server = $profile.GetAttribute('publishUrl')
if ($site -notmatch '^site[0-9]+$' -or $server -ne ($site + '.siteasp.net')) {
    throw 'This helper requires a MonsterASP site profile with a matching publishing server.'
}
$endpoint = 'https://' + $server + ':8172/msdeploy.axd?site=' + $site
$username = $profile.GetAttribute('userName')
$password = $profile.GetAttribute('userPWD')
if ([string]::IsNullOrWhiteSpace($username) -or [string]::IsNullOrWhiteSpace($password)) { throw 'Publishing credentials are missing.' }
foreach ($value in @($username, $password)) {
    if ($value.Contains('"') -or $value.Contains("`r") -or $value.Contains("`n")) { throw 'The publishing profile requires unsupported option quoting.' }
}
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $MSDeployPath
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
# MSDeploy parses the raw Windows command line itself; ArgumentList would escape its
# option-value quotes a second time. These quoted values never pass through a shell.
$start.Arguments = @(
    '-verb:sync', ('-source:contentPath="' + $source + '"'),
    ('-dest:contentPath="' + $site + '",computerName="' + $endpoint + '",userName="' + $username + '",password="' + $password + '",authType=Basic,includeAcls=False'),
    '-enableRule:AppOffline', '-enableRule:DoNotDeleteRule',
    '-disableLink:AppPoolExtension', '-disableLink:ContentExtension', '-disableLink:CertificateExtension'
) -join ' '
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
try {
    if (-not $process.Start()) { throw 'Web Deploy could not be started.' }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(300000)) { $process.Kill($true); throw 'Web Deploy timed out. Inspect the host before retrying or rolling back.' }
    $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) {
        $codes = @([regex]::Matches($output, '\bERROR_[A-Z0-9_]+\b') | ForEach-Object Value | Sort-Object -Unique)
        throw ('Web Deploy failed (exit ' + $process.ExitCode + '). ' + ($codes -join ', ') + ' Raw output was suppressed to protect publishing credentials.')
    }
    $changes = [regex]::Match($output, 'Total changes: [0-9]+ \([0-9]+ added, [0-9]+ deleted, [0-9]+ updated, [0-9]+ parameters changed, [0-9]+ bytes copied\)')
    if ($changes.Success) { Write-Host $changes.Value }
    Write-Host ('Deployed ' + $manifest.identity.version + ' to https://' + $ExpectedSiteHost + '. Verify public health, routes and an interactive circuit next. Remote files were not pruned.')
}
finally {
    $process.Dispose()
    $start.Arguments = ''
    $username = $null; $password = $null; $output = $null; $document = $null; $profile = $null; $profiles = $null
}
