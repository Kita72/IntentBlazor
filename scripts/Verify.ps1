#requires -Version 7.0
[CmdletBinding()]
param()

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
    Invoke-DotNet -Arguments @('restore', 'IntentBlazor.slnx', '--locked-mode')
    Invoke-DotNet -Arguments @('build', 'IntentBlazor.slnx', '--no-restore', '--configuration', 'Release')
    Invoke-DotNet -Arguments @('format', 'IntentBlazor.slnx', '--verify-no-changes', '--no-restore')

    $testProjects = @(Get-ChildItem -LiteralPath 'tests' -Recurse -Filter '*.csproj' -File | Where-Object {
        [xml] $projectXml = Get-Content -Raw -LiteralPath $_.FullName
        $testNode = $projectXml.SelectSingleNode('//IsTestProject')
        $null -ne $testNode -and $testNode.InnerText -eq 'true'
    })
    foreach ($testProject in $testProjects) {
        Invoke-DotNet -Arguments @('test', $testProject.FullName, '--no-build', '--no-restore', '--configuration', 'Release')
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
    Write-Host 'Repository verification completed.'
}
finally {
    Pop-Location
}
