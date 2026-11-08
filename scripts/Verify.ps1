#requires -Version 7.0
[CmdletBinding()]
param([switch] $InstallBrowserDependencies)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    $evidence = Join-Path $repositoryRoot 'artifacts/phase7/evidence'
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    # Invalidate the previous full-suite receipt before any subsequent verification can fail.
    [IO.File]::WriteAllText((Join-Path $evidence 'repository.json'), '{"status":"incomplete"}')
    [IO.File]::WriteAllText((Join-Path $evidence 'readiness.json'), '{"status":"incomplete"}')
    Invoke-DotNet -Arguments @('restore', 'IntentBlazor.slnx', '--locked-mode')
    Invoke-DotNet -Arguments @('build', 'IntentBlazor.slnx', '--no-restore', '--configuration', 'Release')
    Invoke-DotNet -Arguments @('format', 'IntentBlazor.slnx', '--verify-no-changes', '--no-restore')

    $testProjects = @(Get-ChildItem -LiteralPath 'tests' -Recurse -Filter '*.csproj' -File | Where-Object {
        [xml] $projectXml = Get-Content -Raw -LiteralPath $_.FullName
        $testNode = $projectXml.SelectSingleNode('//IsTestProject')
        $null -ne $testNode -and $testNode.InnerText -eq 'true'
    })
    foreach ($testProject in $testProjects) {
        if ($testProject.Name -eq 'IntentBlazor.BrowserTests.csproj') { continue }
        Invoke-DotNet -Arguments @('test', $testProject.FullName, '--no-build', '--no-restore', '--configuration', 'Release',
            '--logger', 'trx;LogFileName=unit.trx', '--results-directory', $evidence)
    }
    if ($testProjects.Count -eq 0) {
        Write-Host 'No behaviour test projects exist yet; contract tests begin in Phase 1.'
    }

    & (Join-Path $PSScriptRoot 'Verify-Documentation.ps1')
    $libraryProjects = Get-ChildItem -LiteralPath 'src' -Recurse -Filter '*.csproj' -File
    foreach ($libraryProject in $libraryProjects) {
        Invoke-DotNet -Arguments @('pack', $libraryProject.FullName, '--no-build', '--no-restore', '--configuration', 'Release', '--output', 'artifacts/packages')
    }
    & (Join-Path $PSScriptRoot 'Verify-Quickstart.ps1')
    & (Join-Path $PSScriptRoot 'Verify-Packages.ps1')
    & (Join-Path $PSScriptRoot 'Verify-Examples.ps1')
    & (Join-Path $PSScriptRoot 'Verify-ProductionHost.ps1')
    & (Join-Path $PSScriptRoot 'Verify-Browser.ps1') -NoBuild -InstallDependencies:$InstallBrowserDependencies
    & (Join-Path $PSScriptRoot 'Record-HostEvidence.ps1') -PrepareOnly
    $tool = Join-Path $repositoryRoot 'tools/IntentBlazor.Verification/bin/Release/net10.0/IntentBlazor.Verification.dll'
    if ($IsWindows -and [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [Runtime.InteropServices.Architecture]::X64) {
        $bundle = [IO.File]::ReadAllText((Join-Path $repositoryRoot 'artifacts/phase7-docs-path.txt')).Trim()
        Invoke-DotNet -Arguments @($tool, 'repository', $repositoryRoot, (Join-Path $evidence 'unit.trx'), (Join-Path $repositoryRoot 'artifacts/browser/browser.trx'), $bundle, (Join-Path $evidence 'repository.json'))
    }
    else {
        Remove-Item -LiteralPath (Join-Path $evidence 'repository.json')
        Write-Host 'Portable checks completed. Current IIS native/bundle acceptance and the full release receipt require Windows x64 and remain unverified here.'
    }
    & (Join-Path $PSScriptRoot 'Verify-Readiness.ps1')
    Write-Host 'Repository verification completed.'
}
finally {
    Pop-Location
}
