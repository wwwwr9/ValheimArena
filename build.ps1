<#
  Builds Valheim Arena.
    .\build.ps1                                  - Release build and Harmony patch check against the installed game
    .\build.ps1 -Deploy                          - the same, then copy the plugin to BepInEx\plugins\ValheimArena
    .\build.ps1 -ValheimDir "D:\Games\Valheim"   - game installed in another folder
#>
param(
    [string]$ValheimDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [switch]$Deploy,
    [switch]$SkipVerify
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'src\ValheimArena.csproj'

Write-Host '== Build' -ForegroundColor Cyan
dotnet build $proj -c Release -nologo -v q "-p:ValheimDir=$ValheimDir"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$dll = Join-Path $root 'src\bin\Release\ValheimArena.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "Not found: $dll" }

if (-not $SkipVerify) {
    Write-Host '== Checking Harmony patch targets against the game and UI translation coverage' -ForegroundColor Cyan
    $pcProj = Join-Path $root 'tools\PatchCheck\PatchCheck.csproj'
    dotnet build $pcProj -c Release -nologo -v q "-p:ValheimDir=$ValheimDir"
    if ($LASTEXITCODE -ne 0) { throw 'PatchCheck build failed' }
    dotnet (Join-Path $root 'tools\PatchCheck\bin\Release\net8.0\PatchCheck.dll') $dll $ValheimDir
    if ($LASTEXITCODE -ne 0) { throw 'Check failed: a game update changed patched members, or UI strings lack a translation in En.cs (see above)' }
}

if ($Deploy) {
    Write-Host '== Deploy' -ForegroundColor Cyan
    $plugins = Join-Path $ValheimDir 'BepInEx\plugins'
    if (-not (Test-Path -LiteralPath $plugins)) { throw "BepInEx not found: $plugins" }
    $target = Join-Path $plugins 'ValheimArena'
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item -LiteralPath $dll -Destination $target -Force
    Write-Host "   $target"
}
Write-Host 'Done.' -ForegroundColor Green
