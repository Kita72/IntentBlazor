#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'This local credential helper uses Windows current-user protection.' }
$directory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'IntentBlazor/Publishing'
$path = Join-Path $directory 'Wilson72-IntentBlazor-preview1.dpapi'
if (Test-Path -LiteralPath $path) { throw 'A preview credential already exists. Review it before replacing it.' }
[IO.Directory]::CreateDirectory($directory) | Out-Null
$acl = Get-Acl -LiteralPath $directory
$acl.SetAccessRuleProtection($true, $false)
foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
}
Set-Acl -LiteralPath $directory -AclObject $acl
Write-Host 'Paste only the new IntentBlazor NuGet API key. Input is hidden; do not enter your Microsoft password.'
$key = Read-Host -AsSecureString -Prompt 'NuGet API key'
try {
    if ($key.Length -lt 20 -or $key.Length -gt 512) { throw 'The key length is unexpected. Nothing was saved.' }
    $protected = ConvertFrom-SecureString -SecureString $key
    [IO.File]::WriteAllText($path, $protected)
    [ordered]@{ owner='Wilson72'; packagePattern='IntentBlazor.*'; scope='push-new-packages-and-versions';
        method='local-nuget-api-key'; capturedAt=[DateTimeOffset]::UtcNow.ToString('O'); expectedExpiresAt=[DateTimeOffset]::UtcNow.AddDays(1).ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'Wilson72-IntentBlazor-preview1.json') -Encoding utf8
    Write-Host 'Saved the key encrypted for this Windows account, outside the repository. Tell AIMIE the secure setup is done.'
}
finally { $key.Dispose(); $protected = $null }
