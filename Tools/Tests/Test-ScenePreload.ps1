$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Add-Type -CompilerOptions '/nowarn:0649' -Path @(
    (Join-Path $PSScriptRoot 'ScenePreloadFixture.cs'),
    (Join-Path $root 'Assets/Scripts/BountyHunter/ScenePreload.cs'),
    (Join-Path $root 'Assets/Scripts/BountyHunter/Boss/BossSheet.cs')
)
[ScenePreloadChecks]::Run()
