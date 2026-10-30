param([string]$DalamudHome = $env:DALAMUD_HOME)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$DalamudHome -and (Test-Path "$env:APPDATA/XIVLauncherCN/addon/Hooks/dev/Dalamud.dll")) { $DalamudHome = "$env:APPDATA/XIVLauncherCN/addon/Hooks/dev" }
if (!$DalamudHome -or !(Test-Path "$DalamudHome/Dalamud.dll")) { throw 'Set DALAMUD_HOME to the API 15 assembly directory.' }
$env:DALAMUD_HOME = $DalamudHome
dotnet build "$root/src/JiZhang/JiZhang.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
dotnet test "$root/tests/JiZhang.Tests/JiZhang.Tests.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
python "$PSScriptRoot/package.py"
if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
