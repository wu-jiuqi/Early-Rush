#requires -Version 7
[CmdletBinding()]
param([string]$Root = (Join-Path (Split-Path -Parent $PSScriptRoot) 'EarlyRush_Art_v002'))
$ErrorActionPreference='Stop'
$packagePath=[IO.Path]::GetFullPath($Root)
$assets=@()
foreach($part in @('root','street','metro','office')) {
    $path=Join-Path $packagePath "metadata/$part-export-config.json"
    if(-not (Test-Path -LiteralPath $path)){throw "Missing asset group: $path"}
    $assets += @((Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).assets)
}
$config=[ordered]@{
    schema='early-rush-art-export-1'
    batch='v002-map-art'
    layout='Existing V11 seed 91026 snapshots; no level or collision edits.'
    assets=$assets
}
$json=($config | ConvertTo-Json -Depth 30).Replace("`r`n","`n")+"`n"
[IO.File]::WriteAllText((Join-Path $packagePath 'metadata/export-config.json'),$json,[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Export-ArtV001.ps1') -Root $packagePath
