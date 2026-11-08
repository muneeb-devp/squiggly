# Packs Squiggly into a local folder feed so manual IDE testing runs against the real package,
# not project references. Each run gets a unique version, so NuGet's cache never serves a stale build.
#
#   eng/pack-local.ps1                         pack only
#   eng/pack-local.ps1 -Playground C:\ArcadeTest   pack, then create (or update) a console project there that uses it
param([string]$Playground)
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$feed = Join-Path $root 'artifacts/local-feed'
$version = '0.0.0-local.' + (Get-Date -Format 'yyyyMMddHHmmss')

dotnet pack (Join-Path $root 'src/Squiggly.Package') --configuration Release --output $feed "-p:MinVerVersionOverride=$version"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Packed Squiggly $version into $feed"

if ($Playground) {
    if (-not (Get-ChildItem -Path $Playground -Filter *.csproj -ErrorAction SilentlyContinue)) {
        dotnet new console --output $Playground --name (Split-Path $Playground -Leaf)
    }
    dotnet add $Playground package Squiggly --version $version --source $feed
    $game = Join-Path $Playground 'Game.cs'
    if (-not (Test-Path $game)) { Set-Content -Path $game -Value "// squiggly`nnamespace Arcade;" -NoNewline }
    Write-Host "Ready: open $game, put the caret on line 1 and press Ctrl+. (Alt+Enter in Rider)."
}
