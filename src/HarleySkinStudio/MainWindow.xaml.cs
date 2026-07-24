using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HarleySkinStudio;

public partial class MainWindow : Window
{
    private readonly string _root;
    private readonly string _runtime;
    private readonly string _assets;
    private readonly string _scripts;
    private readonly string _themes;

    private readonly ObservableCollection<ThemeItem> _allThemes = new();
    private readonly ObservableCollection<ThemeItem> _filteredThemes = new();
    private readonly ObservableCollection<ThemeItem> _primaryItems = new();

    private bool _english;
    private string? _appliedThemeSlug;
    private string? _welcomeCardsImagePath;
    private bool _isUpdatingSelection;

    // 主题模式：0=跟随系统, 1=浅色, 2=深色
    private int _appThemeMode = 0;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += OnSourceInitialized;

        _root = FindRoot();
        _runtime = FindRuntime(_root);
        _assets = Path.Combine(_runtime, "assets");
        _scripts = Path.Combine(_runtime, "scripts");
        _themes = Path.Combine(_runtime, "themes");
        Directory.CreateDirectory(_themes);

        ThemeList.ItemsSource = _primaryItems;
        ModalThemeList.ItemsSource = _filteredThemes;

        ThemeList.SelectionChanged += (_, _) => OnThemeSelectionChanged(ThemeList.SelectedItem as ThemeItem);
        ModalThemeList.SelectionChanged += (_, _) =>
        {
            if (ModalThemeList.SelectedItem is ThemeItem selected)
            {
                SelectAndPromoteTheme(selected);
                LibraryModal.Visibility = Visibility.Collapsed;
            }
        };

        SearchBox.TextChanged += (_, _) => ApplyFilter(SearchBox.Text);

        OpenLibraryModalButton.Click += (_, _) =>
        {
            SearchBox.Text = "";
            ApplyFilter("");
            LibraryModal.Visibility = Visibility.Visible;
        };
        CloseModalButton.Click += (_, _) => LibraryModal.Visibility = Visibility.Collapsed;

        ImportButton.Click += async (_, _) => await ImportTheme();
        ApplyButton.Click += async (_, _) => await ApplySelected();
        LaunchButton.Click += async (_, _) => await LaunchSelected();
        DeleteButton.Click += (_, _) => DeleteSelected();
        ChooseCardsImageButton.Click += (_, _) => ChooseWelcomeCardsImage();
        ClearCardsImageButton.Click += (_, _) =>
        {
            _welcomeCardsImagePath = null;
            UpdateWelcomeCardsImageUi();
            UpdateWelcomeCardsPreview();
        };

        LanguageButton.Click += (_, _) =>
        {
            _english = !_english;
            ApplyLanguage();
            UpdateSelection();
        };

        // 软件深浅主题切换按钮
        ThemeModeButton.Click += (_, _) =>
        {
            _appThemeMode = (_appThemeMode + 1) % 3;
            ApplyAppTheme();
        };

        // 监听 Windows 系统主题变更事件（跟随系统）
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (_appThemeMode == 0) ApplyAppTheme();
        };

        MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaximizeButton.Click += (_, _) =>
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        };
        CloseButton.Click += (_, _) => Close();

        SetComboByTag(QualityPresetBox, "balanced");
        SetComboByTag(EffectPresetBox, "balanced");
        SetComboByTag(AccentPresetBox, "natural");
        AccentPresetBox.SelectionChanged += (_, _) => UpdateAccentPreview();

        LoadThemes();
        LoadRuntimeSettings();
        ApplyAppTheme();
        ApplyLanguage();
    }

    private void ApplyAppTheme()
    {
        bool isDark = false;
        if (_appThemeMode == 0) // 跟随系统
        {
            ThemeModeButton.Content = _english ? "💻 System" : "💻 跟随系统";
            isDark = IsSystemInDarkMode();
        }
        else if (_appThemeMode == 1) // 强制浅色
        {
            ThemeModeButton.Content = _english ? "☀️ Light" : "☀️ 浅色";
            isDark = false;
        }
        else // 强制深色
        {
            ThemeModeButton.Content = _english ? "🌙 Dark" : "🌙 深色";
            isDark = true;
        }

        if (isDark)
        {
            Resources["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181E25"));
            Resources["PanelBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222A34"));
            Resources["PanelBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333D48"));
            Resources["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B3440"));
            Resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F4F8"));
            Resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            Resources["ModalBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E252E"));
            Resources["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B3440"));
            Resources["InputBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A4754"));
        }
        else
        {
            Resources["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA"));
            Resources["PanelBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#80FFFFFF"));
            Resources["PanelBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#99FFFFFF"));
            Resources["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            Resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#142538"));
            Resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#607789"));
            Resources["ModalBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA"));
            Resources["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#99FFFFFF"));
            Resources["InputBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#88C4D4E0"));
        }
    }

    private static bool IsSystemInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (Environment.OSVersion.Version.Build >= 22000)
        {
            var preference = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(handle, 33, ref preference, sizeof(int));
        }
    }

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "runtime", "assets")) && Directory.Exists(Path.Combine(dir, "runtime", "scripts")))
                return dir;
            if (Directory.Exists(Path.Combine(dir, "assets")) && Directory.Exists(Path.Combine(dir, "scripts")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    private static string FindRuntime(string root)
    {
        var runtime = Path.Combine(root, "runtime");
        if (Directory.Exists(Path.Combine(runtime, "assets")) && Directory.Exists(Path.Combine(runtime, "scripts")))
            return runtime;
        return root;
    }

    private void LoadThemes()
    {
        _allThemes.Clear();
        foreach (var dir in Directory.GetDirectories(_themes).OrderByDescending(Directory.GetLastWriteTime))
        {
            var metaPath = Path.Combine(dir, "theme.json");
            var name = Path.GetFileName(dir);
            var createdAt = "";
            if (File.Exists(metaPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                    if (doc.RootElement.TryGetProperty("name", out var n)) name = n.GetString() ?? name;
                    if (doc.RootElement.TryGetProperty("createdAt", out var c)) createdAt = c.GetString() ?? "";
                }
                catch { }
            }
            var lightImg = LoadImage(Path.Combine(dir, "background-light.jpg"));
            var darkImg = LoadImage(Path.Combine(dir, "background-dark.jpg"));
            _allThemes.Add(new ThemeItem(name, Path.GetFileName(dir), dir, createdAt, lightImg, darkImg));
        }

        ApplyFilter(SearchBox.Text);
        RefreshPrimaryList();

        if (_primaryItems.Count > 0)
            ThemeList.SelectedIndex = 0;
        else
            UpdateSelection();
    }

    private void ApplyFilter(string query)
    {
        _filteredThemes.Clear();
        var q = query?.Trim() ?? "";
        foreach (var t in _allThemes)
        {
            if (string.IsNullOrEmpty(q) || t.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                _filteredThemes.Add(t);
            }
        }
        ModalCountText.Text = $"({_filteredThemes.Count} 项)";
    }

    // 关键修复位置：选中并置顶时，显式触发 UpdateSelection() 更新右侧预览
    private void SelectAndPromoteTheme(ThemeItem item)
    {
        if (item is null || _isUpdatingSelection) return;
        _isUpdatingSelection = true;
        try
        {
            var index = _allThemes.IndexOf(item);
            if (index > 0)
            {
                _allThemes.Move(index, 0);
            }
            RefreshPrimaryList();
            ThemeList.SelectedItem = item;
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        // 强行刷新右侧卡片与图片预览
        UpdateSelection();
    }

    private void RefreshPrimaryList()
    {
        _primaryItems.Clear();
        foreach (var theme in _allThemes.Take(4))
        {
            _primaryItems.Add(theme);
        }
    }

    private void OnThemeSelectionChanged(ThemeItem? item)
    {
        if (_isUpdatingSelection) return;
        if (item != null)
        {
            SelectAndPromoteTheme(item);
        }
    }

    private void UpdateSelection()
    {
        if (ThemeList.SelectedItem is not ThemeItem item)
        {
            SelectedTitle.Text = _english ? "Choose a theme" : "未选择主题";
            LightPreview.Source = null;
            DarkPreview.Source = null;
            PreviewGrid.Visibility = Visibility.Collapsed;
            ApplyButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            UpdateApplyButtonState();
            return;
        }

        SelectedTitle.Text = item.Name;
        StatusText.Text = _english ? "Theme selected" : "已选择主题";
        PreviewGrid.Visibility = Visibility.Visible;
        ApplyButton.IsEnabled = true;
        DeleteButton.IsEnabled = true;
        LightPreview.Source = item.LightPreview;
        DarkPreview.Source = item.DarkPreview;
        LightEmpty.Visibility = LightPreview.Source == null ? Visibility.Visible : Visibility.Collapsed;
        DarkEmpty.Visibility = DarkPreview.Source == null ? Visibility.Visible : Visibility.Collapsed;
        UpdateAccentPreview();
        UpdateWelcomeCardsPreview();
        UpdateApplyButtonState();
    }

    private static BitmapImage? LoadImage(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void LoadRuntimeSettings()
    {
        var themePath = Path.Combine(_assets, "theme.json");
        if (!File.Exists(themePath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(themePath));
            if (doc.RootElement.TryGetProperty("welcome", out var welcome) && welcome.TryGetProperty("title", out var title))
            {
                WelcomeTitleBox.Text = title.GetString() ?? WelcomeTitleBox.Text;
            }
            if (doc.RootElement.TryGetProperty("welcome", out welcome) && welcome.TryGetProperty("cardsImage", out var cardsImage))
            {
                var value = cardsImage.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var path = Path.IsPathRooted(value) ? value : Path.Combine(_assets, value);
                    _welcomeCardsImagePath = File.Exists(path) ? path : null;
                }
            }
            if (doc.RootElement.TryGetProperty("generation", out var generation) && generation.TryGetProperty("qualityPreset", out var qualityPreset))
            {
                SetComboByTag(QualityPresetBox, qualityPreset.GetString() ?? "balanced");
            }
            if (doc.RootElement.TryGetProperty("effects", out var effects) && effects.TryGetProperty("material", out var material))
            {
                SetComboByTag(EffectPresetBox, material.GetString() ?? "balanced");
            }
            if (doc.RootElement.TryGetProperty("colors", out var colors) && colors.TryGetProperty("source", out var source))
            {
                SetComboByTag(AccentPresetBox, source.GetString() ?? "natural");
            }
            UpdateAccentPreview();
            UpdateWelcomeCardsImageUi();
            UpdateWelcomeCardsPreview();
        }
        catch { }
    }

    private void ChooseWelcomeCardsImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = _english ? "Choose welcome card image" : "选择快捷卡图片",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        _welcomeCardsImagePath = dialog.FileName;
        UpdateWelcomeCardsImageUi();
        UpdateWelcomeCardsPreview();
    }

    private void UpdateWelcomeCardsImageUi()
    {
        WelcomeCardsPathText.Text = string.IsNullOrWhiteSpace(_welcomeCardsImagePath)
            ? (_english ? "Use theme background slices" : "沿用主题背景切片")
            : Path.GetFileName(_welcomeCardsImagePath);
        ClearCardsImageButton.IsEnabled = !string.IsNullOrWhiteSpace(_welcomeCardsImagePath);
    }

    private void UpdateWelcomeCardsPreview()
    {
        var source = LoadImage(ResolveWelcomeCardsPreviewPath());
        SetWelcomeCardPreview(WelcomeCardPreview0, source, 0, 4);
        SetWelcomeCardPreview(WelcomeCardPreview1, source, 1, 4);
        SetWelcomeCardPreview(WelcomeCardPreview2, source, 2, 4);
        SetWelcomeCardPreview(WelcomeCardPreview3, source, 3, 4);
    }

    private string ResolveWelcomeCardsPreviewPath()
    {
        if (!string.IsNullOrWhiteSpace(_welcomeCardsImagePath) && File.Exists(_welcomeCardsImagePath))
            return _welcomeCardsImagePath;

        if (ThemeList.SelectedItem is ThemeItem item)
            return Path.Combine(item.Path, "background-light.jpg");

        return Path.Combine(_assets, "background-light.jpg");
    }

    private static void SetWelcomeCardPreview(Image image, BitmapSource? source, int index, int count)
    {
        if (source == null)
        {
            image.Source = null;
            return;
        }

        var sliceWidth = Math.Max(1, source.PixelWidth / count);
        var x = Math.Min(Math.Max(0, source.PixelWidth - sliceWidth), index * sliceWidth);
        var crop = new CroppedBitmap(source, new Int32Rect(x, 0, sliceWidth, source.PixelHeight));
        crop.Freeze();
        image.Source = crop;
    }

    private async Task ImportTheme()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择主题源图片",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            SetBusy(_english ? "Generating light and dark variants..." : "正在生成浅色 / 深色主题...");
            var generation = GetGenerationPreset();
            await RunScript(
                "generate-theme-backgrounds.ps1",
                "-InputPath", dialog.FileName,
                "-Width", generation.Width.ToString(),
                "-Height", generation.Height.ToString(),
                "-LightQuality", generation.LightQuality.ToString(),
                "-DarkQuality", generation.DarkQuality.ToString());

            var slug = UniqueSlug(Path.GetFileNameWithoutExtension(dialog.FileName));
            var dir = Path.Combine(_themes, slug);
            Directory.CreateDirectory(dir);
            File.Copy(Path.Combine(_assets, "background-light.jpg"), Path.Combine(dir, "background-light.jpg"), true);
            File.Copy(Path.Combine(_assets, "background-dark.jpg"), Path.Combine(dir, "background-dark.jpg"), true);
            File.Copy(dialog.FileName, Path.Combine(dir, "source" + Path.GetExtension(dialog.FileName)), true);
            var colors = GetThemeColors(dialog.FileName);
            File.WriteAllText(Path.Combine(dir, "theme.json"), JsonSerializer.Serialize(new
            {
                name = Path.GetFileNameWithoutExtension(dialog.FileName),
                slug,
                createdAt = DateTime.Now.ToString("s"),
                generation = new
                {
                    qualityPreset = GetSelectedTag(QualityPresetBox, "balanced"),
                    width = generation.Width,
                    height = generation.Height,
                    lightQuality = generation.LightQuality,
                    darkQuality = generation.DarkQuality
                },
                effects = new { material = GetSelectedTag(EffectPresetBox, "balanced") },
                colors
            }, new JsonSerializerOptions { WriteIndented = true }));

            LoadThemes();
            var imported = _allThemes.FirstOrDefault(x => x.Slug == slug);
            if (imported != null) SelectAndPromoteTheme(imported);
            StatusText.Text = _english ? "Theme imported" : "主题已导入";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, _english ? "Import failed" : "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = _english ? "Import failed" : "导入失败";
        }
        finally
        {
            SetReady();
        }
    }

    private async Task ApplySelected()
    {
        if (ThemeList.SelectedItem is not ThemeItem item) return;
        try
        {
            ApplyThemeToAssets(item);
            _appliedThemeSlug = item.Slug;
            await RunScript("apply-harley-skin.ps1");
            StatusText.Text = _english ? "Theme applied" : "主题已应用";
            UpdateApplyButtonState();
        }
        catch
        {
            StatusText.Text = _english ? "Theme selected. Restart Codex if hot apply is unavailable." : "主题已设为当前；Codex 未以 CDP 运行时需要重启";
        }
    }

    private async Task LaunchSelected()
    {
        if (ThemeList.SelectedItem is ThemeItem item)
        {
            ApplyThemeToAssets(item);
            _appliedThemeSlug = item.Slug;
        }
        try
        {
            SetBusy(_english ? "Closing and restarting Codex..." : "正在关闭并重启 Codex...");
            await RunScript("start-harley-skin.ps1", "-RestartExisting");
            StatusText.Text = _english ? "Codex launched with skin" : "Codex 已带皮肤启动";
            UpdateApplyButtonState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, _english ? "Launch failed" : "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = _english ? "Launch failed" : "启动失败";
        }
        finally
        {
            SetReady();
        }
    }

    private void DeleteSelected()
    {
        if (ThemeList.SelectedItem is not ThemeItem item) return;
        var prompt = _english ? $"Delete theme {item.Name}?" : $"删除主题 {item.Name}？";
        var title = _english ? "Confirm delete" : "确认删除";
        if (MessageBox.Show(this, prompt, title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        Directory.Delete(item.Path, true);
        LoadThemes();
        StatusText.Text = _english ? "Theme deleted" : "主题已删除";
    }

    private void ApplyThemeToAssets(ThemeItem item)
    {
        File.Copy(Path.Combine(item.Path, "background-light.jpg"), Path.Combine(_assets, "background-light.jpg"), true);
        File.Copy(Path.Combine(item.Path, "background-dark.jpg"), Path.Combine(_assets, "background-dark.jpg"), true);
        var meta = LoadThemeMeta(item);
        var generation = GetGenerationPreset();
        var colorSource = Directory.GetFiles(item.Path, "source.*").FirstOrDefault() ?? Path.Combine(item.Path, "background-light.jpg");
        var colors = GetThemeColors(colorSource);
        File.WriteAllText(Path.Combine(_assets, "theme.json"), JsonSerializer.Serialize(new
        {
            appearance = "light",
            backgrounds = new { light = "background-light.jpg", dark = "background-dark.jpg", fallback = "background-light.jpg" },
            art = new { focusX = 0.68, focusY = 0.45, safeArea = "left", taskMode = "ambient" },
            generation = new
            {
                qualityPreset = GetSelectedTag(QualityPresetBox, "balanced"),
                width = generation.Width,
                height = generation.Height,
                lightQuality = generation.LightQuality,
                darkQuality = generation.DarkQuality
            },
            effects = new { material = GetSelectedTag(EffectPresetBox, meta.EffectMaterial ?? "balanced") },
            colors,
            welcome = BuildWelcomeConfig()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private Dictionary<string, object> BuildWelcomeConfig()
    {
        var welcome = new Dictionary<string, object>
        {
            ["title"] = string.IsNullOrWhiteSpace(WelcomeTitleBox.Text)
                ? (_english ? "What should we build?" : "我们该构建什么？")
                : WelcomeTitleBox.Text.Trim()
        };

        var cardsImage = CopyWelcomeCardsImageToAssets();
        if (!string.IsNullOrWhiteSpace(cardsImage))
        {
            welcome["cardsImage"] = cardsImage;
        }

        return welcome;
    }

    private string? CopyWelcomeCardsImageToAssets()
    {
        if (string.IsNullOrWhiteSpace(_welcomeCardsImagePath) || !File.Exists(_welcomeCardsImagePath)) return null;

        var ext = Path.GetExtension(_welcomeCardsImagePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var fileName = "welcome-cards" + ext.ToLowerInvariant();
        var target = Path.Combine(_assets, fileName);
        if (!Path.GetFullPath(_welcomeCardsImagePath).Equals(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(_welcomeCardsImagePath, target, true);
        }
        _welcomeCardsImagePath = target;
        UpdateWelcomeCardsImageUi();
        return fileName;
    }

    private async Task RunScript(string scriptName, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = _root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(Path.Combine(_scripts, scriptName));
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 PowerShell。");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
    }

    private string UniqueSlug(string name)
    {
        var baseSlug = string.Join("-", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "theme";
        var slug = baseSlug;
        var index = 2;
        while (Directory.Exists(Path.Combine(_themes, slug)))
            slug = $"{baseSlug}-{index++}";
        return slug;
    }

    private void SetBusy(string text)
    {
        StatusText.Text = text;
        IsEnabled = false;
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void SetReady()
    {
        IsEnabled = true;
    }

    private void ApplyLanguage()
    {
        LanguageButton.Content = _english ? "中" : "EN";
        Title = "IcyFre Codex Studio";
        AppTitleText.Text = "IcyFre Codex Studio";
        AppSubtitleText.Text = _english ? "  /  Codex desktop theme lab" : "  /  Codex 桌面主题实验室";
        LibraryTitleText.Text = _english ? "Theme Library" : "主题库";
        LibraryDescriptionText.Text = _english
            ? "Import one image and generate personalized light and dark Codex skins."
            : "导入一张图，自动生成浅色与深色 Codex 皮肤。";
        ImportTitleText.Text = _english ? "Import image theme" : "导入图片生成主题";
        ThemeSectionText.Text = _english ? "Recent Themes" : "常用主题";
        OpenLibraryModalButton.Content = _english ? "All Themes ↗" : "全部主题库 ↗";
        ModeBadgeText.Text = _english ? "Auto Light / Dark" : "自动浅色 / 深色";
        LightEmptyTitleText.Text = _english ? "Light preview" : "浅色预览";
        LightEmptyHintText.Text = _english ? "Shown after importing a theme" : "导入主题后显示";
        DarkEmptyTitleText.Text = _english ? "Dark preview" : "深色预览";
        DarkEmptyHintText.Text = _english ? "Generated automatically" : "自动生成暗色版本";
        FooterPrimaryText.Text = _english
            ? "Launch will close existing Codex and restart it with CDP enabled."
            : "启动按钮会自动关闭已有 Codex，并以 CDP 模式重启。";
        FooterSecondaryText.Text = _english
            ? "Apply copies the selected theme to runtime/assets and tries to hot refresh Codex."
            : "应用主题会复制当前主题到 runtime/assets，并尝试热更新运行中的 Codex。";
        WelcomeTitleLabelText.Text = _english ? "Welcome title" : "欢迎标题";
        WelcomeCardsLabelText.Text = _english ? "Card image" : "快捷卡图";
        ChooseCardsImageButton.Content = _english ? "Choose" : "选择";
        ClearCardsImageButton.Content = _english ? "Clear" : "清除";
        UpdateWelcomeCardsImageUi();
        QualityLabelText.Text = _english ? "Image quality" : "图片质量";
        EffectLabelText.Text = _english ? "Visual effect" : "视觉效果";
        AccentLabelText.Text = _english ? "Accent" : "主题色";
        SetComboItemContent(QualityPresetBox, "performance", _english ? "Lite" : "轻量");
        SetComboItemContent(QualityPresetBox, "balanced", _english ? "Balanced" : "均衡");
        SetComboItemContent(QualityPresetBox, "fidelity", _english ? "High" : "高清");
        SetComboItemContent(EffectPresetBox, "lite", _english ? "Lite" : "轻量");
        SetComboItemContent(EffectPresetBox, "balanced", _english ? "Balanced" : "均衡");
        SetComboItemContent(EffectPresetBox, "rich", _english ? "Rich glass" : "丰富磨砂");
        SetComboItemContent(AccentPresetBox, "natural", _english ? "Natural" : "自然提取");
        SetComboItemContent(AccentPresetBox, "sky", _english ? "Sky blue" : "天空蓝");
        SetComboItemContent(AccentPresetBox, "mint", _english ? "Mint" : "薄荷绿");
        SetComboItemContent(AccentPresetBox, "peach", _english ? "Peach" : "蜜桃粉");
        SetComboItemContent(AccentPresetBox, "lilac", _english ? "Lilac" : "雾紫");
        DeleteButton.Content = _english ? "Delete" : "删除";
        LaunchButton.Content = _english ? "Launch / Restart Codex" : "启动 / 重启 Codex";
        ApplyButton.Content = _english ? "Apply Theme" : "应用主题";

        ApplyAppTheme();

        if (ThemeList.SelectedItem is null)
        {
            SelectedTitle.Text = _english ? "Choose a theme" : "未选择主题";
            StatusText.Text = _english ? "Ready" : "准备就绪";
        }
    }

    private void UpdateApplyButtonState()
    {
        var isApplied = ThemeList.SelectedItem is ThemeItem item && item.Slug == _appliedThemeSlug;
        ApplyButton.Content = isApplied ? (_english ? "✓ Applied" : "✓ 已应用") : (_english ? "Apply Theme" : "应用主题");
    }

    private static string GetSelectedTag(ComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
    }

    private static void SetComboByTag(ComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static void SetComboItemContent(ComboBox comboBox, string tag, string content)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                item.Content = content;
                return;
            }
        }
    }

    private GenerationPreset GetGenerationPreset()
    {
        return GetSelectedTag(QualityPresetBox, "balanced") switch
        {
            "performance" => new GenerationPreset(1600, 900, 84, 82),
            "fidelity" => new GenerationPreset(2560, 1440, 94, 92),
            _ => new GenerationPreset(1920, 1080, 90, 88)
        };
    }

    private object GetThemeColors(string imagePath)
    {
        var source = GetSelectedTag(AccentPresetBox, "natural");
        var accent = source switch
        {
            "sky" => "#47A9D8",
            "mint" => "#59BFA0",
            "peach" => "#E8A18E",
            "lilac" => "#9B8AE8",
            _ => ExtractComfortAccent(imagePath)
        };
        var palette = BuildPalette(accent);
        return new
        {
            source,
            accent,
            accentWarm = palette.Warm,
            accentCool = palette.Cool,
            accentDeep = palette.Deep,
            accentMist = HexToRgba(palette.Mist, 0.12),
            accentSoft = HexToRgba(accent, 0.22),
            accentText = ComfortTextColor(accent)
        };
    }

    private void UpdateAccentPreview()
    {
        var imagePath = ThemeList.SelectedItem is ThemeItem item
            ? Directory.GetFiles(item.Path, "source.*").FirstOrDefault() ?? Path.Combine(item.Path, "background-light.jpg")
            : Path.Combine(_assets, "background-light.jpg");
        var accent = GetSelectedTag(AccentPresetBox, "natural") switch
        {
            "sky" => "#47A9D8",
            "mint" => "#59BFA0",
            "peach" => "#E8A18E",
            "lilac" => "#9B8AE8",
            _ => ExtractComfortAccent(imagePath)
        };
        AccentPreviewSwatch.Background = (Brush)new BrushConverter().ConvertFromString(accent)!;
    }

    private ThemeMeta LoadThemeMeta(ThemeItem item)
    {
        var path = Path.Combine(item.Path, "theme.json");
        if (!File.Exists(path)) return new ThemeMeta(null, null, null);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            object? generation = null;
            object? colors = null;
            string? effect = null;

            if (doc.RootElement.TryGetProperty("generation", out var generationElement))
                generation = JsonSerializer.Deserialize<object>(generationElement.GetRawText());
            if (doc.RootElement.TryGetProperty("colors", out var colorsElement))
                colors = JsonSerializer.Deserialize<object>(colorsElement.GetRawText());
            if (doc.RootElement.TryGetProperty("effects", out var effects) &&
                effects.TryGetProperty("material", out var material))
                effect = material.GetString();

            return new ThemeMeta(generation, colors, effect);
        }
        catch
        {
            return new ThemeMeta(null, null, null);
        }
    }

    private static string ExtractComfortAccent(string imagePath)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 96;
            image.UriSource = new Uri(imagePath);
            image.EndInit();

            var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);

            var buckets = new Dictionary<int, AccentBucket>();
            for (var y = 0; y < converted.PixelHeight; y += 2)
            {
                for (var x = 0; x < converted.PixelWidth; x += 2)
                {
                    var i = y * stride + x * 4;
                    var b = pixels[i];
                    var g = pixels[i + 1];
                    var r = pixels[i + 2];
                    var hsl = ToHsl(r, g, b);
                    if (hsl.L < 0.24 || hsl.L > 0.9 || hsl.S < 0.08) continue;

                    var key = (int)Math.Round(hsl.H / 15.0);
                    buckets.TryGetValue(key, out var bucket);
                    var comfort = 1.0 - Math.Min(1.0, Math.Abs(hsl.L - 0.58) / 0.34);
                    var colorfulness = Math.Pow(Math.Clamp(hsl.S, 0, 1), 1.35);
                    var score = (0.24 + colorfulness) * (0.42 + comfort);
                    buckets[key] = new AccentBucket(
                        bucket.Count + 1,
                        bucket.R + r * score,
                        bucket.G + g * score,
                        bucket.B + b * score,
                        bucket.Weight + score);
                }
            }

            if (buckets.Count == 0) return "#47A9D8";
            var best = buckets
                .OrderByDescending(x => x.Value.Weight * Math.Log(x.Value.Count + 3))
                .First()
                .Value;
            var rr = best.R / best.Weight;
            var gg = best.G / best.Weight;
            var bb = best.B / best.Weight;
            var adjusted = ToComfortColor(rr, gg, bb);
            return $"#{adjusted.R:X2}{adjusted.G:X2}{adjusted.B:X2}";
        }
        catch
        {
            return "#47A9D8";
        }
    }

    private static (byte R, byte G, byte B) ToComfortColor(double r, double g, double b)
    {
        var hsl = ToHsl(r, g, b);
        var saturation = Math.Clamp(hsl.S, 0.28, 0.58);
        var lightness = Math.Clamp(hsl.L, 0.44, 0.64);
        return FromHsl(hsl.H, saturation, lightness);
    }

    private static ThemePalette BuildPalette(string accent)
    {
        var color = (Color)ColorConverter.ConvertFromString(accent);
        var hsl = ToHsl(color.R, color.G, color.B);
        var warmHue = NormalizeHue(hsl.H - 10);
        var coolHue = NormalizeHue(hsl.H + 13);
        var warm = FromHsl(warmHue, Math.Clamp(hsl.S * 0.92, 0.24, 0.52), Math.Clamp(hsl.L + 0.12, 0.54, 0.76));
        var cool = FromHsl(coolHue, Math.Clamp(hsl.S * 0.78, 0.2, 0.46), Math.Clamp(hsl.L + 0.18, 0.6, 0.82));
        var deep = FromHsl(hsl.H, Math.Clamp(hsl.S * 0.82, 0.26, 0.52), Math.Clamp(hsl.L - 0.22, 0.18, 0.38));
        var mist = FromHsl(coolHue, Math.Clamp(hsl.S * 0.38, 0.12, 0.28), Math.Clamp(hsl.L + 0.28, 0.74, 0.9));
        return new ThemePalette(ToHex(warm), ToHex(cool), ToHex(deep), ToHex(mist));
    }

    private static (double H, double S, double L) ToHsl(double r, double g, double b)
    {
        r /= 255.0;
        g /= 255.0;
        b /= 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var h = 0.0;
        var l = (max + min) / 2.0;
        var d = max - min;
        var s = d == 0 ? 0 : d / (1 - Math.Abs(2 * l - 1));
        if (d != 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * (((b - r) / d) + 2);
            else h = 60 * (((r - g) / d) + 4);
            if (h < 0) h += 360;
        }
        return (h, s, l);
    }

    private static double NormalizeHue(double hue)
    {
        hue %= 360;
        return hue < 0 ? hue + 360 : hue;
    }

    private static string ToHex((byte R, byte G, byte B) color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static (byte R, byte G, byte B) FromHsl(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        var m = l - c / 2;
        var (rp, gp, bp) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        return ((byte)Math.Round((rp + m) * 255), (byte)Math.Round((gp + m) * 255), (byte)Math.Round((bp + m) * 255));
    }

    private static string HexToRgba(string hex, double alpha)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        return $"rgba({color.R}, {color.G}, {color.B}, {alpha:0.##})";
    }

    private static string ComfortTextColor(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        return color.R * 0.299 + color.G * 0.587 + color.B * 0.114 > 156 ? "#132C38" : "#FFFFFF";
    }
}

public record ThemeItem(string Name, string Slug, string Path, string CreatedAt, BitmapImage? LightPreview, BitmapImage? DarkPreview);
public record GenerationPreset(int Width, int Height, int LightQuality, int DarkQuality);
public record ThemeMeta(object? Generation, object? Colors, string? EffectMaterial);
public readonly record struct AccentBucket(int Count, double R, double G, double B, double Weight);
public record ThemePalette(string Warm, string Cool, string Deep, string Mist);
