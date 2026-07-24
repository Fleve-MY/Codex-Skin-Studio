# IcyFre Codex Studio

给 Windows 版 Codex 桌面端套一层可恢复的本机皮肤，并提供主题管理、热更新注入和后续在线更新能力。

本项目通过本机 Chrome DevTools Protocol 注入 CSS 和装饰 DOM，不修改 `WindowsApps`、`app.asar` 或官方签名。真实侧栏、输入框、任务内容仍是 Codex 原生控件。

## 当前状态

- 桌面应用名：`IcyFre Codex Studio`
- GitHub 仓库：`git@github.com:Fleve-MY/Codex-Skin-Studio.git`
- 本地 `app/` 目录是开发时发布副本，不作为源码提交内容
- 正式分发应使用 GitHub Release 中的安装包和更新包

## 开发构建

```powershell
dotnet build ".\src\HarleySkinStudio\HarleySkinStudio.csproj" -c Release
```

发布本地运行副本：

```powershell
dotnet publish ".\src\HarleySkinStudio\HarleySkinStudio.csproj" -c Release -o ".\app"
```

## 目录结构

```text
src/HarleySkinStudio/      WPF 桌面程序源码
runtime/assets/            当前注入使用的 CSS、JS、背景图和主题配置
runtime/scripts/           CDP 启动、注入、验证和背景生成脚本
runtime/themes/            主题库，每个子目录是一套导入主题
docs/images/               调试截图和效果图
app/                       本地发布副本，已被 .gitignore 忽略
```

## 高级脚本

如果需要绕过桌面程序，也可以直接使用底层脚本。

先关闭正在运行的 Codex，然后执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\runtime\scripts\start-harley-skin.ps1"
```

如果 Codex 已经开着，并且你希望脚本帮你重启它以挂上调试端口：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\runtime\scripts\start-harley-skin.ps1" -RestartExisting
```

验证注入状态：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\runtime\scripts\verify-harley-skin.ps1"
```

移除当前窗口里的皮肤层：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\runtime\scripts\restore-harley-skin.ps1"
```

## 换自己的图

推荐把一张图交给脚本自动生成浅色/深色配套背景：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\runtime\scripts\generate-theme-backgrounds.ps1" -InputPath "D:\your-image.jpg"
```

它会生成：

```text
runtime/assets/background-light.jpg
runtime/assets/background-dark.jpg
```

推荐尺寸是 `2560 x 1440`。左侧约一半保持低信息、低对比，右侧放主体。不要把 Codex 窗口、侧栏、输入框、文字、Logo 或水印画进背景图。

## 主题配置

编辑 `runtime/assets/theme.json`：

```json
{
  "appearance": "auto",
  "art": {
    "focusX": 0.72,
    "focusY": 0.45,
    "safeArea": "left",
    "taskMode": "ambient"
  },
  "welcome": {
    "title": "Happy everyday~",
    "cardsImage": "welcome-cards.png"
  }
}
```

- `appearance`: `auto`, `light`, `dark`
- `focusX` / `focusY`: 背景焦点，范围 `0..1`
- `safeArea`: `left`, `right`, `center`, `none`
- `taskMode`: `ambient`, `banner`, `off`
- `welcome.title`: 新对话欢迎标题
- `welcome.cardsImage`: 新对话快捷卡片背景图

## 发布策略

源码仓库只提交源码、脚本、默认主题资源和文档。正式安装包、更新包、压缩包放到 GitHub Release，不直接提交到 `main`。

后续在线更新可以基于 GitHub Release API 做版本检查：应用启动时读取最新 Release，比较版本号，再下载正式安装包或更新包。

## 安全边界

- CDP 只绑定 `127.0.0.1`
- 主题层使用 `pointer-events: none`
- 不读取或修改 API Key、登录态、任务数据
- 不修改官方 Codex 安装目录
