#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$privateRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'IntentBlazor/Publishing'
$credentialPath = Join-Path $privateRoot 'Wilson72-IntentBlazor-preview1.dpapi'
$metadata = Get-Content -Raw -LiteralPath (Join-Path $privateRoot 'Wilson72-IntentBlazor-preview1.json') | ConvertFrom-Json
if ($metadata.owner -ne 'Wilson72' -or $metadata.packagePattern -ne 'IntentBlazor.*' -or
    $metadata.scope -ne 'push-new-packages-and-versions' -or $metadata.method -ne 'local-nuget-api-key' -or
    [DateTimeOffset]::Parse($metadata.expectedExpiresAt) -le [DateTimeOffset]::UtcNow) { throw 'The scoped publishing setup must be reviewed or renewed.' }
$savedPolicy = [Environment]::GetEnvironmentVariable('INTENT_NUGET_POLICY_CONFIRMED', 'Process')
try {
    $env:INTENT_NUGET_POLICY_CONFIRMED = 'true'
    # Recompute exact candidate readiness before opening/decrypting the credential.
    & (Join-Path $PSScriptRoot 'Verify-Readiness.ps1') -RequireReady
    $evidence = Join-Path $repositoryRoot 'artifacts/phase7/evidence'
    $packages = Get-Content -Raw -LiteralPath (Join-Path $evidence 'packages.json') | ConvertFrom-Json
    $ready = Get-Content -Raw -LiteralPath (Join-Path $evidence 'readiness.json') | ConvertFrom-Json
    if (-not $ready.ready -or -not $ready.identity.clean -or $packages.identity.sourceRevision -ne $ready.identity.sourceRevision) { throw 'A complete clean candidate is required.' }
    $version = Get-IntentVersion -RepositoryRoot $repositoryRoot
    $releaseRoot = Join-Path $repositoryRoot ('artifacts/releases/' + $version + '/' + $ready.identity.sourceRevision)
    if (Test-Path -LiteralPath $releaseRoot) { throw 'A release archive already exists. Inspect prior publication results before retrying.' }
    foreach ($package in $packages.packages) {
        $id = $package.package.ToLowerInvariant()
        $address = 'https://api.nuget.org/v3-flatcontainer/' + $id + '/index.json'
        $response = Invoke-WebRequest -Uri $address -SkipHttpErrorCheck -TimeoutSec 30
        if ($response.StatusCode -eq 200) {
            $versions = ($response.Content | ConvertFrom-Json).versions
            if ($versions -contains $version) { throw ('The immutable public version already exists: ' + $package.package) }
        }
        elseif ($response.StatusCode -ne 404) { throw 'The registry preflight could not complete.' }
    }
    if (-not $PSCmdlet.ShouldProcess(('NuGet.org / Wilson72 / ' + $version), 'Publish the eight audited packages and matching symbols')) { return }
    $nugetVersion = (& dotnet nuget --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $nugetVersion -notmatch '^(?<major>[0-9]+)\.(?<minor>[0-9]+)\.' -or
        ([int]$Matches.major -lt 7 -or ([int]$Matches.major -eq 7 -and [int]$Matches.minor -lt 6))) { throw 'Publishing requires NuGet 7.6+ for process-only API key environment variables.' }
    New-Item -ItemType Directory -Path (Join-Path $releaseRoot 'packages') -Force | Out-Null
    Copy-Item -LiteralPath $evidence -Destination (Join-Path $releaseRoot 'evidence') -Recurse
    foreach ($package in $packages.packages) {
        foreach ($extension in @('nupkg', 'snupkg')) {
            Copy-Item -LiteralPath (Join-Path $repositoryRoot ('artifacts/packages/' + $package.package + '.' + $version + '.' + $extension)) -Destination (Join-Path $releaseRoot 'packages')
        }
    }
    [IO.File]::WriteAllText((Join-Path $releaseRoot 'candidate.json'), ($ready.identity | ConvertTo-Json -Depth 6))
    $key = Get-Content -Raw -LiteralPath $credentialPath | ConvertTo-SecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($key)
    $results = [Collections.Generic.List[object]]::new()
    try {
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        foreach ($package in $packages.packages) {
            $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
            $start.WorkingDirectory = $repositoryRoot; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
            $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
            foreach ($argument in @('nuget', 'push', (Join-Path $releaseRoot ('packages/' + $package.package + '.' + $version + '.nupkg')), '--source', 'https://api.nuget.org/v3/index.json', '--force-english-output')) { $start.ArgumentList.Add($argument) }
            # NuGet >=7.6 reads these; nothing is placed in process arguments, source, logs or user NuGet.Config.
            $start.Environment['NUGET_API_KEY'] = $plain; $start.Environment['NUGET_SYMBOL_API_KEY'] = $plain
            $process = [Diagnostics.Process]::Start($start)
            try {
                $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
                if (-not $process.WaitForExit(180000)) { $process.Kill($true); throw 'Publishing timed out; inspect registry state before retrying.' }
                $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
                $accepted = Test-IntentNuGetPushAcknowledgement -Output $output -ExitCode $process.ExitCode -PackageId $package.package -Version $version
                $results.Add(@{ package=$package.package; version=$version; accepted=$accepted; checkedAt=[DateTimeOffset]::UtcNow.ToString('O') })
                [ordered]@{ identity=$ready.identity; owner='Wilson72'; results=@($results.ToArray()) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $releaseRoot 'publication.json') -Encoding utf8
                if (-not $accepted) { throw ('Publishing failed for ' + $package.package + '; raw output was suppressed. Inspect the account/registry before retrying; no duplicate is silently skipped.') }
                Write-Host ('NuGet accepted package/symbol push: ' + $package.package + ' ' + $version)
            }
            finally { $process.Dispose(); $start.Environment.Remove('NUGET_API_KEY') | Out-Null; $start.Environment.Remove('NUGET_SYMBOL_API_KEY') | Out-Null; $output=$null }
        }
    }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $key.Dispose(); $plain=$null }
    [IO.File]::WriteAllText((Join-Path $repositoryRoot 'artifacts/step6-release-path.txt'), $releaseRoot)
    Write-Host ('Retained exact release artifacts and acceptance results: ' + $releaseRoot + '. Verify indexing, public payloads, symbols and fresh consumers next.')
}
finally { [Environment]::SetEnvironmentVariable('INTENT_NUGET_POLICY_CONFIRMED', $savedPolicy, 'Process') }
