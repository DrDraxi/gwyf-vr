# Builds the mod and copies it into a local game install for testing.
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Gamble With Your Friends",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

dotnet build "$root\GWYFVR.slnx" -c $Configuration -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$plugin = Join-Path $GameDir "BepInEx\plugins\GWYFVR"
$patcher = Join-Path $GameDir "BepInEx\patchers\GWYFVR"
New-Item -ItemType Directory -Force "$plugin\RuntimeDeps", $patcher | Out-Null

Copy-Item "$root\src\GWYFVR\bin\$Configuration\netstandard2.1\GWYFVR.dll" $plugin -Force
Copy-Item "$root\lib\RuntimeDeps\*" "$plugin\RuntimeDeps" -Recurse -Force
New-Item -ItemType Directory -Force "$plugin\Prompts" | Out-Null
Copy-Item "$root\assets\prompts\*" "$plugin\Prompts" -Force
Copy-Item "$root\src\GWYFVR.Preload\bin\$Configuration\netstandard2.1\GWYFVR.Preload.dll" $patcher -Force

Write-Host "Deployed to $GameDir"
