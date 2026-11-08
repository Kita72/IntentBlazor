#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SdkDirectory,
    [Parameter(Mandatory)][string] $SdkVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Repository.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sdkRoot = [IO.Path]::GetFullPath($SdkDirectory)
$dotnet = Join-Path $sdkRoot $(if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' })
if ($SdkVersion -notmatch '^11\.0\.' -or -not (Test-Path -LiteralPath (Join-Path $sdkRoot "sdk/$SdkVersion"))) { throw 'Supply an installed isolated .NET 11 SDK and its exact version.' }
if ($sdkRoot -eq [IO.Path]::GetDirectoryName((Get-Command dotnet).Source)) { throw 'Use an isolated SDK, not the system installation.' }
$runRoot = Join-Path $repositoryRoot ('artifacts/compatibility/run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$tool = Join-Path $repositoryRoot 'tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll'
Invoke-IntentDotNet -Arguments @($tool, 'candidate', $repositoryRoot, (Get-IntentVersion -RepositoryRoot $repositoryRoot), (Join-Path $runRoot 'source.json'))
$source = Get-Content -Raw -LiteralPath (Join-Path $runRoot 'source.json') | ConvertFrom-Json
$originalGlobal = Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'global.json') -Algorithm SHA256
$savedEnvironment = @{}
foreach ($name in @('PATH', 'DOTNET_ROOT', 'DOTNET_ROLL_FORWARD', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_NOLOGO')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$cases = [Collections.Generic.List[object]]::new()
$files = @(& git -C $repositoryRoot ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source inputs.' }
try {
    $env:PATH = $sdkRoot + [IO.Path]::PathSeparator + $env:PATH
    $env:DOTNET_ROOT = $sdkRoot
    $env:DOTNET_ROLL_FORWARD = 'LatestPatch'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    foreach ($framework in @('net10.0', 'net11.0')) {
        $snapshot = Join-Path $runRoot $framework
        & git clone --quiet --no-hardlinks --single-branch --branch main $repositoryRoot $snapshot
        if ($LASTEXITCODE -ne 0) { throw 'The isolated source copy could not be created.' }
        & git -C $snapshot remote set-url origin https://github.com/Kita72/IntentBlazor.git
        if ($LASTEXITCODE -ne 0) { throw 'The isolated source metadata could not be set.' }
        # Overlay the reviewed working tree, including uncommitted Step 5 inputs; never copy local credentials or outputs.
        foreach ($relative in $files) {
            if ($relative -match '(^|/)(bin|obj|artifacts|node_modules)(/|$)|(^|/)\.env|\.Local\.json$|\.(publishsettings|pubxml\.user|pfx|p12|db|sqlite|sqlite3)(-(wal|shm|journal))?$') { continue }
            $original = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $relative))
            $target = [IO.Path]::GetFullPath((Join-Path $snapshot $relative))
            if (-not $original.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                -not $target.StartsWith($snapshot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'A source path escaped the isolated copy.' }
            if (-not (Test-Path -LiteralPath $original -PathType Leaf)) { if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target }; continue }
            if ((Get-Item -LiteralPath $original).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Linked source inputs are not supported.' }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Copy-Item -LiteralPath $original -Destination $target -Force
        }
        [ordered]@{ sdk = @{ version = $SdkVersion; rollForward = 'disable'; allowPrerelease = $true; paths = @($sdkRoot) } } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $snapshot 'global.json') -Encoding utf8
        if ($framework -eq 'net11.0') {
            foreach ($relative in $files | Where-Object { $_ -like '*.csproj' }) {
                $project = Join-Path $snapshot $relative
                if (Test-Path -LiteralPath $project) {
                    $text = [IO.File]::ReadAllText($project)
                    [IO.File]::WriteAllText($project, $text.Replace('<TargetFramework>net10.0</TargetFramework>', '<TargetFramework>net11.0</TargetFramework>'))
                }
            }
            # SDK 11 reports NU1510 as an error for this package, now supplied by net11 itself.
            # This is an explicit scratch-only retarget adjustment, not a baseline dependency change.
            $runtimeProject = Join-Path $snapshot 'src/IntentBlazor.Runtime/IntentBlazor.Runtime.csproj'
            [xml] $runtimeXml = Get-Content -Raw -LiteralPath $runtimeProject
            $redundant = $runtimeXml.SelectSingleNode('//PackageReference[@Include="Microsoft.Extensions.DependencyInjection.Abstractions"]')
            if ($null -eq $redundant) { throw 'Review the net11 retarget adjustment: the runtime package reference changed.' }
            [void] $redundant.ParentNode.RemoveChild($redundant); $runtimeXml.Save($runtimeProject)
        }
        Push-Location $snapshot
        $stage = 'sdk-selection'
        $caseLog = Join-Path $runRoot ($framework + '.log')
        [IO.File]::WriteAllText($caseLog, '')
        try {
            . {
                $selected = (& $dotnet --version).Trim()
                if ($LASTEXITCODE -ne 0 -or $selected -ne $SdkVersion) { throw 'The snapshot selected an unexpected SDK.' }
                $restore = @('restore', 'IntentBlazor.slnx', '--verbosity', 'quiet')
                # net11 is a disposable retarget experiment; only its copy gets regenerated lock files.
                if ($framework -eq 'net10.0') { $restore += '--locked-mode' } else { $restore += '--force-evaluate' }
                $stage = 'restore'; Invoke-IntentDotNet -Arguments $restore
                $stage = 'build'
                Invoke-IntentDotNet -Arguments @('build', 'IntentBlazor.slnx', '--no-restore', '-c', 'Release', '--verbosity', 'quiet')
                $stage = 'tests'
                $results = Join-Path $runRoot ($framework + '-tests')
                Invoke-IntentDotNet -Arguments @('test', 'tests/IntentBlazor.Tests/IntentBlazor.Tests.csproj', '--no-build', '--no-restore', '-c', 'Release',
                    '--logger', 'trx;LogFileName=compatibility.trx', '--results-directory', $results, '--verbosity', 'quiet')
                [xml] $trx = Get-Content -Raw -LiteralPath (Join-Path $results 'compatibility.trx')
                $counts = $trx.SelectSingleNode('/*[local-name()="TestRun"]/*[local-name()="ResultSummary"]/*[local-name()="Counters"]')
                if ($null -eq $counts -or [int] $counts.total -le 0 -or [int] $counts.passed -ne [int] $counts.total -or [int] $counts.executed -ne [int] $counts.total) { throw 'Compatibility tests are incomplete.' }
                Invoke-IntentDotNet -Arguments @("tools/IntentBlazor.Verification/bin/Release/$framework/IntentBlazor.Verification.dll", 'environment', (Join-Path $results 'environment.json'))
                $runtime = Get-Content -Raw -LiteralPath (Join-Path $results 'environment.json') | ConvertFrom-Json
                if ($runtime.runtime -notlike $(if ($framework -eq 'net10.0') { '.NET 10.*' } else { '.NET 11.*' })) { throw 'The snapshot ran on an unexpected runtime.' }
                $stage = 'package-consumers'
                foreach ($library in Get-ChildItem -LiteralPath src -Recurse -Filter '*.csproj' -File) {
                    Invoke-IntentDotNet -Arguments @('pack', $library.FullName, '--no-build', '--no-restore', '-c', 'Release', '--output', 'artifacts/packages', '--verbosity', 'quiet')
                }
                & (Join-Path $snapshot 'scripts/Verify-Quickstart.ps1')
                & (Join-Path $snapshot 'scripts/Verify-Examples.ps1')
                & (Join-Path $snapshot 'scripts/Verify-ProductionHost.ps1')
                $cases.Add([ordered]@{ status = 'passed'; sdk = $selected; targetFramework = $framework; runtime = $runtime.runtime; architecture = $runtime.architecture; os = $runtime.os;
                    build = 'passed'; tests = [int] $counts.total; passed = [int] $counts.passed; skipped = [int] $counts.notExecuted;
                    packageConsumers = @('Quickstart', 'GamingNews', 'StaffBusiness', 'AssetWorkbench', 'ProductionHost');
                    adjustments = $(if ($framework -eq 'net11.0') { @('Retarget project files; refresh scratch locks; remove Runtime DI abstractions PackageReference supplied by net11 (NU1510).') } else { @() }); snapshot = $snapshot })
            } *>&1 | Tee-Object -FilePath $caseLog
        }
        catch {
            Write-Host ("Isolated $framework check failed at $stage; retaining the exact diagnostic and marking dependent checks unverified.")
            $cases.Add([ordered]@{ status = 'failed'; sdk = $SdkVersion; targetFramework = $framework; failedStage = $stage; runtime = $null;
                tests = $null; packageConsumers = @(); log = $caseLog; snapshot = $snapshot;
                diagnostics = @([regex]::Matches([IO.File]::ReadAllText($caseLog), '\b(?:BL|CS|NETSDK|NU)[0-9]{4,5}\b') | ForEach-Object Value | Sort-Object -Unique);
                adjustments = $(if ($framework -eq 'net11.0') { @('Retarget project files; refresh scratch locks; remove Runtime DI abstractions PackageReference supplied by net11 (NU1510).') } else { @() }) })
        }
        finally { Pop-Location }
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    if ((Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'global.json') -Algorithm SHA256).Hash -ne $originalGlobal.Hash) { throw 'The baseline SDK selection changed.' }
    [ordered]@{ identity = $source; checkedAt = [DateTimeOffset]::UtcNow.ToString('O'); mode = 'offline-isolated-sdk'; complete = $cases.Count -eq 2;
        passed = @($cases | Where-Object { $_.status -ne 'passed' }).Count -eq 0 -and $cases.Count -eq 2; cases = @($cases.ToArray()) } |
        ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runRoot 'compatibility.json') -Encoding utf8
    Write-Host "Isolated compatibility evidence: $runRoot"
}
if (@($cases | Where-Object { $_.status -ne 'passed' }).Count -gt 0) { throw 'Isolated compatibility checks recorded a failure. The net10 baseline and system SDK are unchanged; review compatibility.json and the retained logs.' }
