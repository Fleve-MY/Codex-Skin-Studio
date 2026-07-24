param(
  [int]$Port = 9335
)

$ErrorActionPreference = "Stop"

try {
  $Targets = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/json/list"
} catch {
  throw "Cannot connect to CDP on 127.0.0.1:$Port. Start the skin first, and make sure Codex was launched by start-harley-skin.ps1."
}
$Pages = @($Targets | Where-Object { $_.type -eq "page" -and $_.webSocketDebuggerUrl })

if ($Pages.Count -eq 0) {
  throw "No injectable Codex renderer target was found."
}

Write-Host "Found $($Pages.Count) renderer target(s)."

$Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$Node = Get-Command "node" -ErrorAction Stop
$StatusJson = & $Node.Source "$Root/scripts/injector.mjs" --root "$Root" --port "$Port" --mode status
$Status = $StatusJson | ConvertFrom-Json
$ActiveCount = @($Status | Where-Object { $_.status.active -and $_.status.rootExists -and $_.status.styleExists }).Count

if ($ActiveCount -eq 0) {
  throw "CDP is reachable, but Harley Codex Skin is not active. Run apply-harley-skin.ps1 or start-harley-skin.ps1."
}

Write-Host "Harley Codex Skin is active in $ActiveCount renderer target(s)."
