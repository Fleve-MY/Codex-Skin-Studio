param(
  [string]$Version = "0.1.0",
  [string]$RuntimeIdentifier = "win-x64",
  [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$Root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$Project = Join-Path $Root "src/HarleySkinStudio/HarleySkinStudio.csproj"
$InstallerStubProject = Join-Path $Root "installer/Stub/InstallerStub.csproj"
$Artifacts = Join-Path $Root "artifacts"
$ReleaseRoot = Join-Path $Artifacts "release/v$Version"
$PublishDir = Join-Path $Artifacts "publish/IcyFreCodexStudio-$Version-$RuntimeIdentifier"
$StubPublishDir = Join-Path $Artifacts "installer-stub/$Version-$RuntimeIdentifier"
$PackageName = "IcyFreCodexStudio-v$Version-$RuntimeIdentifier"
$PortableZip = Join-Path $ReleaseRoot "$PackageName-portable.zip"
$InstallerExe = Join-Path $ReleaseRoot "$PackageName-setup.exe"

function Reset-Directory {
  param([string]$Path)
  if (Test-Path -LiteralPath $Path) {
    Remove-Item -LiteralPath $Path -Recurse -Force
  }
  New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

Reset-Directory $ReleaseRoot
Reset-Directory $PublishDir
Reset-Directory $StubPublishDir

dotnet publish $Project `
  -c Release `
  -r $RuntimeIdentifier `
  --self-contained true `
  -p:Version=$Version `
  -p:FileVersion=$Version `
  -p:AssemblyVersion=$Version `
  -p:PublishSingleFile=false `
  -o $PublishDir

$RuntimeTarget = Join-Path $PublishDir "runtime"
New-Item -ItemType Directory -Force -Path $RuntimeTarget | Out-Null
Copy-Item -LiteralPath (Join-Path $Root "runtime/assets") -Destination (Join-Path $RuntimeTarget "assets") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $Root "runtime/scripts") -Destination (Join-Path $RuntimeTarget "scripts") -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $RuntimeTarget "themes") | Out-Null
Copy-Item -LiteralPath (Join-Path $Root "runtime/themes/README.md") -Destination (Join-Path $RuntimeTarget "themes/README.md") -Force

Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $PortableZip -Force

$Manifest = [ordered]@{
  name = "IcyFre Codex Studio"
  version = $Version
  runtime = $RuntimeIdentifier
  portableZip = Split-Path -Leaf $PortableZip
  installer = Split-Path -Leaf $InstallerExe
  installerMode = "offline-self-contained"
  builtAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}
$Manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ReleaseRoot "release.json") -Encoding UTF8

if (-not $SkipInstaller) {
  dotnet publish $InstallerStubProject `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:Version=$Version `
    -p:FileVersion=$Version `
    -p:AssemblyVersion=$Version `
    -p:InformationalVersion=$Version `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -o $StubPublishDir

  $StubExe = Join-Path $StubPublishDir "IcyFreCodexStudioSetupStub.exe"
  if (-not (Test-Path -LiteralPath $StubExe)) {
    throw "Installer stub was not created: $StubExe"
  }

  Copy-Item -LiteralPath $StubExe -Destination $InstallerExe -Force

  $marker = [Text.Encoding]::UTF8.GetBytes("ICYFRE_PAYLOAD_V1")
  $payload = [IO.File]::OpenRead($PortableZip)
  try {
    $output = [IO.File]::Open($InstallerExe, [IO.FileMode]::Append, [IO.FileAccess]::Write)
    try {
      $payload.CopyTo($output)
      $output.Write($marker, 0, $marker.Length)
      $lengthBytes = [BitConverter]::GetBytes([Int64]$payload.Length)
      $output.Write($lengthBytes, 0, $lengthBytes.Length)
    }
    finally {
      $output.Dispose()
    }
  }
  finally {
    $payload.Dispose()
  }
}

Write-Host "Release artifacts:"
Write-Host "  $PortableZip"
if (-not $SkipInstaller) {
  Write-Host "  $InstallerExe"
}
Write-Host "  $(Join-Path $ReleaseRoot "release.json")"
