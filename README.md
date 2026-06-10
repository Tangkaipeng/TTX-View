# TTX View

TTX View 是一个 Windows 桌面悬浮行情盯盘工具，基于 WPF 原生实现。它以小卡片列表的形式展示现货黄金、现货白银、A 股和基金等标的，适合放在桌面边缘持续观察行情。

## Features

- 桌面悬浮窗口，支持置顶或取消置顶
- 圆角窗口和深色 / 浅色主题切换
- 底部透明度滑条，可调节整体窗口透明度
- 右下角拖动调整窗口大小
- 顶部搜索栏支持通过股票代码或名称添加标的
- 标的默认加入 `默认` 分类，可拖动到其他分类
- 支持分类新增、删除、移动
- 支持标的删除、跨分类拖动和同分类排序
- 支持刷新间隔选择：`1s`、`3s`、`5s`、`10s`
- A 股习惯配色：红色上涨，绿色下跌
- 默认支持现货黄金、现货白银、A 股、基金

## Download

当前推荐下载 Windows x64 便携压缩包：

```text
TTXView-v0.1.0-win-x64-portable.zip
```

解压后运行：

```text
TTXView.exe
```

便携包是 self-contained 发布，用户电脑无需额外安装 .NET。请保留 `TTXView.exe` 同目录下的 `config.json`，它用于保存默认分类、标的和界面配置。

## Default Config

默认配置文件是根目录下的 `config.json`，发布时会复制到 exe 同级目录。

默认分类：

- `默认`
- `贵金属`
- `A股`
- `基金`

默认标的：

| 分类 | 名称 | 代码 |
| --- | --- | --- |
| 贵金属 | 现货黄金 | `hf_XAU` |
| 贵金属 | 现货白银 | `hf_XAG` |
| A股 | 上证指数 | `sh000001` |
| A股 | 许继电气 | `sz000400` |
| A股 | 兴业银锡 | `sz000426` |
| A股 | 赤峰黄金 | `sh600988` |

默认界面：

- 主题：深色
- 透明度：`94%`
- 刷新间隔：`10s`
- 默认置顶：开启

## Usage

- 在顶部搜索栏输入股票名称或代码，按 `Enter` 或点击 `+` 添加。
- 新增标的会进入 `默认` 分类。
- 拖动单条标的右侧把手，可以调整顺序或移动到其他分类。
- 右键点击分类区域，可以管理分类和刷新间隔。
- 右上角按钮用于切换主题、切换置顶、最小化和关闭。

常见输入示例：

```text
XAU
XAG
600519
sh600519
000001
兴业银锡
f_000001
```

## Build

项目需要 .NET 8 SDK。当前工作区内如果存在 `.dotnet` 目录，会优先使用本地 SDK；否则可以使用系统安装的 `dotnet`。

PowerShell:

```powershell
$root = (Get-Location).Path
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet_home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:APPDATA = Join-Path $root '.appdata'
$env:NUGET_PACKAGES = Join-Path $root '.nuget_packages'

$dotnet = if (Test-Path '.\.dotnet\dotnet.exe') { '.\.dotnet\dotnet.exe' } else { 'dotnet' }

& $dotnet restore .\TTXView.Wpf\TTXView.Wpf.csproj --configfile .\NuGet.Config
& $dotnet build .\TTXView.Wpf\TTXView.Wpf.csproj -c Release --no-restore
```

## Publish

普通框架依赖发布：

PowerShell:

```powershell
$root = (Get-Location).Path
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet_home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:APPDATA = Join-Path $root '.appdata'
$env:NUGET_PACKAGES = Join-Path $root '.nuget_packages'

$dotnet = if (Test-Path '.\.dotnet\dotnet.exe') { '.\.dotnet\dotnet.exe' } else { 'dotnet' }

& $dotnet restore .\TTXView.Wpf\TTXView.Wpf.csproj --configfile .\NuGet.Config
& $dotnet publish .\TTXView.Wpf\TTXView.Wpf.csproj -c Release --no-restore -o .\dist\TTXView
```

发布完成后运行：

```text
dist\TTXView\TTXView.exe
```

## Package Portable Release

生成 Windows x64 便携压缩包：

```powershell
.\scripts\package-portable.ps1
```

脚本会生成：

```text
artifacts\TTXView-v0.1.0-win-x64-portable.zip
artifacts\TTXView-v0.1.0-win-x64-portable.zip.sha256
```

便携包使用 self-contained single-file 发布，不要求用户安装 .NET。压缩包内会包含：

- `TTXView.exe`
- `config.json`
- `README.md`

当前便携包目标平台是 `win-x64`。如果要发布给其他平台，需要按目标运行时重新打包。

## Project Structure

```text
TTXView.Wpf\        WPF 主项目
config.json         默认分类、标的和界面配置
NuGet.Config        NuGet 构建配置
README.md           项目说明文档
scripts\            发布与维护脚本
artifacts\          便携压缩包输出目录，重新打包后生成
dist\TTXView\       本地发布产物，重新发布后生成
```

## Notes

- 行情数据依赖网络请求，网络异常时可能无法刷新。
- 本工具只用于行情观察，不构成任何投资建议。
- 若删除 exe 同级目录的 `config.json`，程序会重新生成内置默认配置。
- 当前发布包暂未做代码签名，部分 Windows 设备可能出现 SmartScreen 提示。
