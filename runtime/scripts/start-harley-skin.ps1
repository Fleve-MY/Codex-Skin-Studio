param(
  [int]$Port = 9335,
  [switch]$RestartExisting
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$Package = Get-AppxPackage -Name "OpenAI.Codex" | Select-Object -First 1

if (-not $Package) {
  throw "OpenAI.Codex package was not found. Install Codex Desktop first."
}

$ExistingCodex = @(Get-Process -Name "ChatGPT" -ErrorAction SilentlyContinue | Where-Object {
  $_.Path -like "*\WindowsApps\$($Package.Name)_*\app\ChatGPT.exe"
})

if ($ExistingCodex.Count -gt 0 -and -not $RestartExisting) {
  throw "Codex is already running, so CDP flags cannot be added to the existing process. Close Codex first, or rerun with -RestartExisting."
}

if ($RestartExisting) {
  $ExistingCodex | Stop-Process -Force
  Start-Sleep -Milliseconds 600
}

function Test-PortOpen {
  param([int]$PortToCheck)
  $Client = [System.Net.Sockets.TcpClient]::new()
  try {
    $Async = $Client.BeginConnect("127.0.0.1", $PortToCheck, $null, $null)
    if ($Async.AsyncWaitHandle.WaitOne(150)) {
      $Client.EndConnect($Async)
      return $true
    }
    return $false
  } catch {
    return $false
  } finally {
    $Client.Dispose()
  }
}

while (Test-PortOpen -PortToCheck $Port) {
  $Port += 1
}

$TypeName = "HarleySkin.PackageLauncher"
if (-not ($TypeName -as [type])) {
  Add-Type @"
using System;
using System.Runtime.InteropServices;

namespace HarleySkin {
  [Flags]
  internal enum ActivateOptions : uint {
    None = 0
  }

  [ComImport]
  [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  internal interface IApplicationActivationManager {
    [PreserveSig]
    int ActivateApplication(
      [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
      [MarshalAs(UnmanagedType.LPWStr)] string arguments,
      ActivateOptions options,
      out uint processId);
    int ActivateForFile();
    int ActivateForProtocol();
  }

  [ComImport]
  [Guid("45ba127d-10a8-46ea-8ab7-56ea9078943c")]
  internal class ApplicationActivationManager {}

  public static class PackageLauncher {
    public static uint Launch(string appUserModelId, string arguments) {
      var manager = (IApplicationActivationManager)new ApplicationActivationManager();
      try {
        uint processId;
        int result = manager.ActivateApplication(
          appUserModelId,
          arguments ?? string.Empty,
          ActivateOptions.None,
          out processId);
        Marshal.ThrowExceptionForHR(result);
        return processId;
      } finally {
        if (Marshal.IsComObject(manager)) {
          Marshal.FinalReleaseComObject(manager);
        }
      }
    }
  }
}
"@
}

$Arguments = "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$Port"
$AppUserModelId = "$($Package.PackageFamilyName)!App"
$ProcessId = [HarleySkin.PackageLauncher]::Launch($AppUserModelId, $Arguments)

if ($ProcessId -le 0) {
  throw "Windows did not return a Codex process ID after package activation."
}

Write-Host "Codex launched as process $ProcessId. Waiting for CDP on 127.0.0.1:$Port..."
$Deadline = (Get-Date).AddSeconds(45)
while (-not (Test-PortOpen -PortToCheck $Port)) {
  if ((Get-Date) -ge $Deadline) {
    throw "Codex did not expose CDP on 127.0.0.1:$Port within 45 seconds."
  }
  Start-Sleep -Milliseconds 400
}

$Node = Get-Command "node" -ErrorAction Stop
& $Node.Source "$Root/scripts/injector.mjs" --root "$Root" --port "$Port" --mode apply

Write-Host "Harley Codex Skin started. CDP: http://127.0.0.1:$Port"
