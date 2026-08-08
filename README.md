# CreateYourTile!

一个面向 Windows 10 的本地磁贴工具，也可在 Windows 11 上作为静态开始菜单固定项使用。

## 已实现

- 自选 PNG、JPEG、BMP、GIF 或 TIFF 图片；支持缩放和横纵移动裁剪。
- 自定义磁贴名称，以及小、中、宽、大四种默认尺寸。
- 打开本地程序、快捷方式、脚本、网址、URI 协议或 Microsoft Store 应用 AUMID。
- 直接打开 `C:`、磁盘根目录或任意文件夹，并可从 Windows 已安装应用列表中选择目标。
- 已安装应用列表显示 Windows Shell 原生图标；选择应用时自动把该图标作为初始磁贴图片。
- Steam 等 `.url` URI 快捷方式会读取 `IconFile`/`IconIndex`，优先使用游戏或网站的专属图标。
- 界面使用 ModernWpf 提供的 Windows 10 Fluent/UWP 控件样式；强调色跟随 Windows 系统强调色，深浅模式跟随“选择默认应用模式”，运行中修改设置也会自动刷新。
- 面向触屏提供不小于 40 像素的操作目标、可触摸拖动和惯性滚动、易触控滑块与下拉项，并在较窄窗口中自动切换为上下布局。
- 图片与启动配置全部存放在当前用户的本地应用数据中；无网络请求、无服务器、无常驻后台。
- 对同一“名称 + 目标”再次注册时更新已有磁贴。
- 未打包开发版自动创建开始菜单快捷方式；安装 MSIX 后使用 Windows 官方 SecondaryTile 接口。

## 构建与安装

要求 Windows 10 1809 或更高版本，以及 .NET 9 SDK。

```powershell
dotnet build .\CreateYourTile.csproj
.\scripts\Build-Package.ps1
.\scripts\Install-Package.ps1
```

`Build-Package.ps1` 默认生成有效期 10 年的自签名本地开发证书，并使用 SHA-256 RFC 3161 时间戳签名 MSIX，再把产物放在 `artifacts\msix`。可通过 `-CertificateValidityYears` 调整证书年限。安装脚本会通过 UAC 请求管理员权限，把该公钥证书加入本机 `TrustedPeople` 后安装 MSIX；不需要服务器。

## 系统限制

- Windows 10 支持 Live Tile 视觉和四种尺寸；Windows 11 已取消 Live Tiles，因此只能显示静态图标/固定项，尺寸选择会被系统忽略。
- Windows 规定只有用户可以完成固定。点击“注册并固定”后，必须在系统确认框中同意；应用不能静默固定。
- “永久”表示无需后台、重启后仍保留。用户取消固定、重置/卸载注册器、清理本地应用数据，或删除目标程序后，磁贴会失效。
- 固定项通过这个很小的本地启动桥打开目标，因此注册器本体必须保持安装，但平时不会驻留后台。

微软文档：

- [从桌面应用固定 Secondary Tile](https://learn.microsoft.com/windows/apps/design/shell/tiles-and-notifications/secondary-tiles-desktop-pinning)
- [Secondary Tile 固定流程](https://learn.microsoft.com/windows/uwp/launch-resume/secondary-tiles-pinning)
- [Windows 11 开始菜单布局](https://learn.microsoft.com/windows-hardware/customize/desktop/customize-the-windows-11-start-menu)
