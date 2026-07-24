param(
  [int]$Port = 9335
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$Node = Get-Command "node" -ErrorAction Stop

& $Node.Source "$Root/scripts/injector.mjs" --root "$Root" --port "$Port" --mode apply
Write-Host "Harley Codex Skin was applied to the current renderer window."
