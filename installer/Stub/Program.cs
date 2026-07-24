using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

const string AppName = "IcyFre Codex Studio";
const string AppId = "IcyFreCodexStudio";
const string ExeName = "IcyFreCodexStudio.exe";
const string Publisher = "IcyFre";
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion ?? "0.0.0";
const string MarkerText = "ICYFRE_PAYLOAD_V1";

try
{
    var quiet = args.Any(arg => string.Equals(arg, "/S", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase));

    var installerPath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate installer path.");
    var tempRoot = Path.Combine(Path.GetTempPath(), $"{AppId}-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);

    try
    {
        var payloadZip = Path.Combine(tempRoot, "payload.zip");
        ExtractAppendedPayload(installerPath, payloadZip);

        var installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppName);

        if (Directory.Exists(installDir))
        {
            Directory.Delete(installDir, true);
        }

        Directory.CreateDirectory(installDir);
        ZipFile.ExtractToDirectory(payloadZip, installDir, true);

        var exePath = Path.Combine(installDir, ExeName);
        CreateStartMenuShortcut(exePath, installDir);
        WriteUninstaller(installDir);
        WriteUninstallRegistry(installDir, exePath, version);

        if (!quiet && File.Exists(exePath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = installDir,
                UseShellExecute = true
            });
        }
    }
    finally
    {
        try { Directory.Delete(tempRoot, true); } catch { }
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{AppName} setup failed:");
    Console.Error.WriteLine(ex);
    return 1;
}

static void ExtractAppendedPayload(string installerPath, string outputZip)
{
    var marker = System.Text.Encoding.UTF8.GetBytes(MarkerText);
    using var input = File.OpenRead(installerPath);
    if (input.Length < marker.Length + sizeof(long))
    {
        throw new InvalidOperationException("Installer payload footer is missing.");
    }

    input.Seek(-sizeof(long), SeekOrigin.End);
    Span<byte> lengthBytes = stackalloc byte[sizeof(long)];
    if (input.Read(lengthBytes) != sizeof(long))
    {
        throw new InvalidOperationException("Cannot read payload length.");
    }

    var payloadLength = BitConverter.ToInt64(lengthBytes);
    var markerOffset = input.Length - sizeof(long) - marker.Length;
    var payloadOffset = markerOffset - payloadLength;
    if (payloadLength <= 0 || payloadOffset < 0)
    {
        throw new InvalidOperationException("Installer payload length is invalid.");
    }

    input.Seek(markerOffset, SeekOrigin.Begin);
    var actualMarker = new byte[marker.Length];
    if (input.Read(actualMarker) != marker.Length || !actualMarker.SequenceEqual(marker))
    {
        throw new InvalidOperationException("Installer payload marker is invalid.");
    }

    input.Seek(payloadOffset, SeekOrigin.Begin);
    using var output = File.Create(outputZip);
    CopyExactly(input, output, payloadLength);
}

static void CopyExactly(Stream input, Stream output, long bytesToCopy)
{
    var buffer = new byte[1024 * 1024];
    while (bytesToCopy > 0)
    {
        var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, bytesToCopy));
        if (read <= 0)
        {
            throw new EndOfStreamException("Unexpected end of installer payload.");
        }

        output.Write(buffer, 0, read);
        bytesToCopy -= read;
    }
}

static void CreateStartMenuShortcut(string exePath, string installDir)
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return;
    }

    var startMenuDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "Windows",
        "Start Menu",
        "Programs",
        Publisher);
    Directory.CreateDirectory(startMenuDir);

    var shortcutPath = Path.Combine(startMenuDir, $"{AppName}.lnk");
    var shellType = Type.GetTypeFromProgID("WScript.Shell");
    if (shellType is null)
    {
        return;
    }

    dynamic shell = Activator.CreateInstance(shellType)!;
    dynamic shortcut = shell.CreateShortcut(shortcutPath);
    shortcut.TargetPath = exePath;
    shortcut.WorkingDirectory = installDir;
    shortcut.Description = AppName;
    shortcut.Save();
}

static void WriteUninstaller(string installDir)
{
    var uninstallScript = Path.Combine(installDir, "uninstall.ps1");
    var startMenuLink = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "Windows",
        "Start Menu",
        "Programs",
        Publisher,
        $"{AppName}.lnk");

    var script = string.Join(Environment.NewLine, new[]
    {
        "$ErrorActionPreference = \"Stop\"",
        $"$StartMenuLink = \"{EscapePowerShellString(startMenuLink)}\"",
        "if (Test-Path -LiteralPath $StartMenuLink) { Remove-Item -LiteralPath $StartMenuLink -Force }",
        $@"Remove-Item -LiteralPath ""HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppId}"" -Recurse -Force -ErrorAction SilentlyContinue",
        "Start-Sleep -Milliseconds 300",
        $"Remove-Item -LiteralPath \"{EscapePowerShellString(installDir)}\" -Recurse -Force",
        string.Empty
    });

    File.WriteAllText(uninstallScript, script, System.Text.Encoding.UTF8);
}

static void WriteUninstallRegistry(string installDir, string exePath, string version)
{
    using var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppId}");
    if (key is null)
    {
        return;
    }

    var uninstallScript = Path.Combine(installDir, "uninstall.ps1");
    key.SetValue("DisplayName", AppName);
    key.SetValue("DisplayVersion", version);
    key.SetValue("Publisher", Publisher);
    key.SetValue("InstallLocation", installDir);
    key.SetValue("DisplayIcon", exePath);
    key.SetValue("UninstallString", $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{uninstallScript}\"");
}

static string EscapePowerShellString(string value) => value.Replace("`", "``").Replace("\"", "`\"");
