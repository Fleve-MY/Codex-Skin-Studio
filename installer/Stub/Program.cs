using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
    private const string AppName = "IcyFre Codex Studio";
    private const string AppId = "IcyFreCodexStudio";
    private const string ExeName = "IcyFreCodexStudio.exe";
    private const string Publisher = "IcyFre";
    private const string MarkerText = "ICYFRE_PAYLOAD_V1";

    [STAThread]
    private static int Main(string[] args)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        var quiet = args.Any(arg => string.Equals(arg, "/S", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase));

        try
        {
            var defaultInstallDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                AppName);

            if (quiet)
            {
                Install(defaultInstallDir, launchAfterInstall: false, version);
                return 0;
            }

            ApplicationConfiguration.Initialize();
            using var form = new InstallForm(defaultInstallDir, version);
            Application.Run(form);
            return form.ExitCode;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{AppName} setup failed:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                $"{AppName} Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void Install(string installDir, bool launchAfterInstall, string version)
    {
        var installerPath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate installer path.");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"{AppId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var payloadZip = Path.Combine(tempRoot, "payload.zip");
            ExtractAppendedPayload(installerPath, payloadZip);

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

            if (launchAfterInstall && File.Exists(exePath))
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
    }

    private static void ExtractAppendedPayload(string installerPath, string outputZip)
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

    private static void CopyExactly(Stream input, Stream output, long bytesToCopy)
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

    private static void CreateStartMenuShortcut(string exePath, string installDir)
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

    private static void WriteUninstaller(string installDir)
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

    private static void WriteUninstallRegistry(string installDir, string exePath, string version)
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
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static string EscapePowerShellString(string value) => value.Replace("`", "``").Replace("\"", "`\"");

    private sealed class InstallForm : Form
    {
        private readonly TextBox _installPath;
        private readonly Button _installButton;
        private readonly Button _cancelButton;
        private readonly CheckBox _launchAfterInstall;
        private readonly ProgressBar _progress;
        private readonly Label _status;
        private readonly string _version;

        public int ExitCode { get; private set; }

        public InstallForm(string defaultInstallDir, string version)
        {
            _version = version;
            Text = "IcyFre Codex Studio Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 300);
            Font = new Font("Microsoft YaHei UI", 9F);

            var title = new Label
            {
                Text = $"安装 IcyFre Codex Studio {version}",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
                Location = new Point(24, 22)
            };

            var summary = new Label
            {
                Text = "请选择安装位置。安装程序会创建开始菜单快捷方式，并支持从系统设置中卸载。",
                AutoSize = false,
                Size = new Size(510, 42),
                Location = new Point(24, 62)
            };

            var pathLabel = new Label
            {
                Text = "安装位置",
                AutoSize = true,
                Location = new Point(24, 122)
            };

            _installPath = new TextBox
            {
                Text = defaultInstallDir,
                Location = new Point(24, 146),
                Size = new Size(410, 26)
            };

            var browseButton = new Button
            {
                Text = "浏览...",
                Location = new Point(446, 144),
                Size = new Size(88, 30)
            };
            browseButton.Click += (_, _) => BrowseInstallPath();

            _launchAfterInstall = new CheckBox
            {
                Text = "安装完成后启动 IcyFre Codex Studio",
                Checked = true,
                AutoSize = true,
                Location = new Point(24, 190)
            };

            _progress = new ProgressBar
            {
                Location = new Point(24, 222),
                Size = new Size(510, 14),
                Style = ProgressBarStyle.Continuous
            };

            _status = new Label
            {
                Text = "准备安装",
                AutoSize = false,
                Size = new Size(330, 28),
                Location = new Point(24, 252)
            };

            _installButton = new Button
            {
                Text = "安装",
                Location = new Point(356, 248),
                Size = new Size(82, 32)
            };
            _installButton.Click += (_, _) => InstallNow();

            _cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(452, 248),
                Size = new Size(82, 32)
            };
            _cancelButton.Click += (_, _) => Close();

            Controls.AddRange(new Control[]
            {
                title,
                summary,
                pathLabel,
                _installPath,
                browseButton,
                _launchAfterInstall,
                _progress,
                _status,
                _installButton,
                _cancelButton
            });
        }

        private void BrowseInstallPath()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择 IcyFre Codex Studio 的安装位置",
                SelectedPath = _installPath.Text,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _installPath.Text = dialog.SelectedPath;
            }
        }

        private void InstallNow()
        {
            var path = _installPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this, "请选择安装位置。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _installButton.Enabled = false;
                _cancelButton.Enabled = false;
                _progress.Style = ProgressBarStyle.Marquee;
                _status.Text = "正在安装...";
                Application.DoEvents();

                Install(path, _launchAfterInstall.Checked, _version);

                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = 100;
                _status.Text = "安装完成";
                MessageBox.Show(this, "安装完成。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExitCode = 0;
                Close();
            }
            catch (Exception ex)
            {
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = 0;
                _status.Text = "安装失败";
                _installButton.Enabled = true;
                _cancelButton.Enabled = true;
                ExitCode = 1;
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
