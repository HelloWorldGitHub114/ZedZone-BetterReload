param(
    [string]$GameDir = "E:\SteamLibrary\steamapps\common\ZED ZONE",
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $repoRoot "src\BetterReload.csproj"

$cleanGameDir = $GameDir.TrimEnd('\', '/')
dotnet build $proj -c Release -nologo "-p:ZedZoneDir=$cleanGameDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

$dll = Get-ChildItem -Path (Join-Path $repoRoot "src\bin") -Filter "BetterReload.dll" -Recurse |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $dll) { throw "BetterReload.dll not found in src\bin" }

$stageDir = Join-Path $repoRoot "content\BetterReload"
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $stageDir "BetterReload.dll") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "mod.json") -Destination (Join-Path $stageDir "mod.json") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination (Join-Path $stageDir "README.md") -Force

Write-Host "Staged Workshop package -> $stageDir" -ForegroundColor Green

if ($Install) {
    if (Get-Process ZEDZONE -ErrorAction SilentlyContinue) {
        throw "ZEDZONE.exe is running; close the game before installing."
    }
    $gameModsDir = Join-Path $GameDir "Mods\BetterReload"
    New-Item -ItemType Directory -Force -Path $gameModsDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $stageDir "*") -Destination $gameModsDir -Force
    Write-Host "Installed to game -> $gameModsDir" -ForegroundColor Cyan
}
