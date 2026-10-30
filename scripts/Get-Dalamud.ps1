$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$reference = Get-Content "$root/dalamud-reference.json" | ConvertFrom-Json
$dir = Join-Path ([IO.Path]::GetTempPath()) ("jizhang-dalamud-" + $reference.sourceCommit)
$zip = "$dir.zip"
if (!(Test-Path "$dir/Dalamud.dll")) {
    Invoke-WebRequest $reference.url -OutFile $zip
    if ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $reference.sha256) { throw 'Dalamud reference checksum mismatch.' }
    Expand-Archive $zip $dir -Force
}
$major = [Diagnostics.FileVersionInfo]::GetVersionInfo("$dir/Dalamud.dll").FileMajorPart
if ($major -ne $reference.apiLevel) { throw "Expected API $($reference.apiLevel), got $major" }
$env:DALAMUD_HOME = $dir
if ($env:GITHUB_ENV) { "DALAMUD_HOME=$dir" >> $env:GITHUB_ENV }
Write-Host "Using pinned API $major references at $dir"
