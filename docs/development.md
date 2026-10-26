# 开发指南

## 环境

- Windows 10 1809 或更高版本，推荐 Windows 11
- .NET SDK 8 或更高版本
- 可选：Visual Studio 2022 17.12 或更高版本，安装“WinUI 应用程序开发”工作负载。用于 XAML 热重载和调试。
- 可选：[Inno Setup 6](https://jrsoftware.org/isinfo.php)，只在生成安装程序时需要：`winget install JRSoftware.InnoSetup`

Windows App SDK、Windows SDK 构建工具和 `winapp` 打包工具都通过 NuGet 还原，不需要另外安装。

## 目录结构

```text
OtpClipboard/
├─ src/
│  ├─ OtpClipboard/           WinUI 3 应用（MSIX 打包）
│  │  ├─ Api/                 后台快照、控制协议与命名管道
│  │  ├─ Views/               主窗口与主界面
│  │  ├─ Services/            通知监听、设置、开机自启、手机连接、剪贴板、记录存储
│  │  ├─ Models/              后台记录与 UI 数据项
│  │  ├─ Desktop/             托盘、验证码浮窗、全局快捷键（Win32 原生实现）
│  │  └─ Assets/              图标与磁贴图片，AppIcon.svg 是源文件
│  └─ OtpClipboard.Core/      与界面无关的纯逻辑：验证码提取、来源判断、错误描述
├─ tests/
│  └─ OtpClipboard.Core.Tests/  Core 的 xUnit 测试
├─ packaging/                 安装程序：Inno Setup 脚本、安装/卸载脚本、签名公钥证书
├─ scripts/                   开发与发布脚本
└─ docs/                      文档
```

各模块的职责和关键设计见 [architecture.md](architecture.md)。

## 构建与测试

```powershell
# 运行测试（只依赖 .NET，不需要 WinUI 环境）
dotnet test tests\OtpClipboard.Core.Tests

# 构建应用
dotnet build src\OtpClipboard\OtpClipboard.csproj -c Debug -p:Platform=x64
```

改动 `OtpClipboard.Core` 时请同时补充测试。界面和 Win32 部分目前没有自动化测试，需要按下文“手动验证”的步骤确认。

## 运行与调试

应用必须带 MSIX 包身份运行，否则读不到 Windows 通知。所以不能直接双击 `bin` 里的 exe，要用下面两种方式之一启动。

### Visual Studio（支持热重载）

1. 打开 `OtpClipboard.slnx`，选择 `Debug | x64`，启动配置选 `OtpClipboard (Package)`。
2. 按 `F5` 启动。修改 XAML 或 C# 方法体后保存，XAML 热重载和 C# 热重载会直接更新正在运行的窗口。
3. 如果改动涉及类型结构、应用清单、包权限、NuGet 依赖或启动代码，需要按 `Shift+F5` 停止后重新 `F5`。

### 命令行快速重启

```powershell
.\scripts\Restart-Dev.ps1
```

脚本会做这些事：

- 只关闭本仓库构建出的旧开发实例，不影响已安装的正式版，也不改通知权限。
- 增量构建 Debug 版，将输出复制到被 Git 忽略的 `.local/development-layout/`。
- 以独立的 Development 包身份启动后台，然后显式启动 `--ui` 界面并等待真实窗口句柄。
- 如果检测到其他位置安装的“验证码剪贴板”正在运行，会停下来提示你先手动关闭。

开发包身份必须与正式包 `B11E1D98-24B3-4D84-AE8C-5CFD2E423AC2` 分开，避免覆盖或卸载用户已安装版本。清单位于 `src/OtpClipboard/Package.Development.appxmanifest`。

后台和 UI 进程都使用同一个应用入口。默认运行原生后台消息泵；传 `--ui` 时启动 Windows App SDK 与 WinUI。这样关闭界面只会结束 UI 进程，后台托盘与通知监听仍会继续。

`dotnet watch` 在这个项目里不可靠：它在打包运行时重建 `AppX\OtpClipboard.exe`，会被正在运行的实例占用文件。

## 手动验证

### 不接手机，发一条测试通知

```powershell
# 以“手机连接”的身份弹出英文验证码通知，默认验证码 246810
powershell -ExecutionPolicy Bypass -File .\scripts\Send-TestToast.ps1

# 中文验证码通知，指定验证码
powershell -ExecutionPolicy Bypass -File .\scripts\Send-TestToast.ps1 -Chinese -Code 135790

# 换成其他来源应用，验证“已过滤”记录
powershell -ExecutionPolicy Bypass -File .\scripts\Send-TestToast.ps1 -AppUserModelId 'Microsoft.WindowsStore_8wekyb3d8bbwe!App'
```

这条通知会在弹出后从通知中心清掉，加 `-KeepInActionCenter` 可以保留。脚本只发本地测试通知，不修改通知权限。

### 浮窗截图

```powershell
.\scripts\Verify-Popup.ps1 -ProcessId <PID> -OutputPath popup.png
```

脚本通过 UI 自动化点击应用里的浮窗预览按钮，然后截取浮窗及周围的阴影和圆角，并输出浮窗位置和尺寸。可以用来对比界面改动前后的效果。

### 资源占用

```powershell
.\scripts\Measure-Idle.ps1 -ProcessId <PID> -Seconds 30
```

每秒输出一次 CPU、内存、线程数和句柄数，用来确认空闲时没有轮询或泄漏。v1.0.5 的回归基线为 30 秒平均 `0.00%` CPU、约 16 MiB 私有内存和 14–22 个线程；这些是本机测量值，不是跨设备性能承诺。

## 修改图标

图标源文件是 `src/OtpClipboard/Assets/AppIcon.svg`。修改后运行下面的命令，重新生成所有尺寸的 PNG 和 ICO：

```powershell
python -m pip install Pillow
python scripts\generate-icons.py
```

脚本依赖本机的 Microsoft Edge 来渲染 SVG，全程离线。

## 代码约定

- 编码风格以 `.editorconfig` 为准：文件范围命名空间，私有字段用 `_camelCase`，缩进 4 个空格。
- 用户可见的文字、注释和文档使用简体中文。
- 不依赖 WinUI 的逻辑放进 `OtpClipboard.Core` 并写测试。`MainPage` 只负责界面状态。
- 后台状态经快照和纯数据记录传递。不要在后台创建 `SolidColorBrush`、`Visibility` 或其他 WinUI 对象。
- WinRT 通知回调、异步续体和控制命令必须回到 `BackgroundDispatcher`；不要从线程池直接操作桌面窗口。
- 原生消息泵在等待新消息前要处理完已排队的 `WM_*` 消息。新增隐藏窗口或绘制逻辑后，需用 `Measure-Idle.ps1` 验证没有忙等回归。
- PowerShell 脚本保存为带 BOM 的 UTF-8，Windows PowerShell 5.1 才能正确读取中文。
- 测试数据、文档示例和截图里不要出现真实的短信、验证码、手机号或个人路径。示例号码请使用 `1065900000000000001` 这类明显虚构的值。

## 发布前检查

运行 `powershell -ExecutionPolicy Bypass -File .\scripts\Check-Privacy.ps1`，检查 Git 候选文件中是否包含常见凭据、个人绝对路径或本机数据文件。检查只报告文件名，不打印匹配内容。它是基础规则检查，不能代替人工审阅图片、任意格式的密钥或 Git 历史。

历史文件的隐私边界由 `OtpClipboard.Core/HistoryArchive.cs` 统一控制：只允许处理分类和固定格式时间。涉及持久化的修改必须保持旧格式脱敏回归测试通过。用于 Issue 或 Pull Request 的截图必须先移除真实通知、验证码和个人信息；本机原始截图放在已忽略的 `output/`。

### 开发包与已安装版身份冲突

若 `dotnet run` 提示同身份的非开发包已安装，说明本机已装正式 MSIX。不要把启动器退出码误认为构建失败。可以先自行卸载已安装版，或者在独立工作副本中给 `Package.appxmanifest` 的 `Identity.Name` 设置一个不同的开发名称，再构建运行。后者会使用独立的本地存储。不要为此修改正式发布的包身份，否则已安装用户无法正常升级。

`Restart-Dev.ps1` 的启动日志保存在已忽略的 `.local/`，失败时会打印末尾诊断。
