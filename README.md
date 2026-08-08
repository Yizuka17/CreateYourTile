# CreateYourTile!

一个真正使用 Windows 10 UWP XAML 实现的本地 Live Tile 创建工具。Windows 11 已取消 Live Tiles，因此在 Windows 11 上只能作为静态开始菜单固定项使用。

## 已实现

- UWP 原生 Fluent 控件、触摸直接操作、惯性滚动和系统焦点反馈。
- 强调色跟随 Windows 系统强调色，深浅模式跟随“选择默认应用模式”。
- 预览与磁贴设置分别使用 UWP `ScrollViewer`，窗口变窄时自动切换成上下布局。
- 自选 PNG、JPEG、BMP、GIF 或 TIFF 图片，并通过 UWP 图片 API 在 `0.1×–4.0×` 范围内缩放和移动裁剪。
- 缩小透明图标时保留外围 Alpha 通道，由 Windows 使用磁贴背景色进行合成。
- 产品图标使用真正的透明背景，并提供 Windows 深/浅外壳所需的无底板任务栏资源。
- 自定义名称，以及小、中、宽、大四种默认尺寸。
- 打开本地程序、快捷方式、脚本、文件夹、网址、URI 或 Microsoft Store 应用 AUMID。
- 保留 `.lnk` 快捷方式的内置参数、工作目录及“以管理员身份运行”高级选项。
- 高 DPI 原生文件选择器会保留 `.lnk` 本身而不是将其替换为目标 EXE。
- 从“已安装应用”列表选择开始菜单中的 Store 应用、桌面程序或系统工具，按 `# / A–Z`（中文按拼音首字母）分组，并自动读取应用图标。
- 使用 Windows 官方 `SecondaryTile` 接口固定和更新磁贴。
- 所有图片和启动配置存放在当前包的本地应用数据中，无服务器、无网络请求、无后台常驻。

## 架构

- `CreateYourTile.Uwp`：真正的 UWP 前端，负责触摸界面、图片处理、应用选择与 SecondaryTile。
- `CreateYourTile.Launcher`：小型原生 C++ 全信任启动桥，仅在点击磁贴时打开桌面目标，完成后立即退出。
- `CreateYourTile.Package`：Windows Application Packaging Project，将两者组合成一个 MSIX。

## 构建与安装

要求 Windows 10 1809 或更高版本、Visual Studio 2022，以及“通用 Windows 平台开发”和“使用 C++ 的桌面开发”组件。

```powershell
.\scripts\Build-Package.ps1
.\scripts\Install-Package.ps1
```

`Build-Package.ps1` 会执行 UWP Release/.NET Native 构建，生成有效期 10 年的自签名开发证书，并使用 SHA-256 RFC 3161 时间戳签名。产物位于 `artifacts\msix`，其中包括 MSIX、证书及 x64 UWP 依赖。

安装脚本会通过 UAC 请求管理员权限，将公钥证书加入本机 `TrustedPeople`，再安装依赖和 MSIX。

## 系统限制

- Windows 10 支持 Live Tile 视觉和四种尺寸；Windows 11 只能显示静态固定项。
- Windows 只允许用户在系统确认框中完成固定，应用不能静默固定。
- UWP 沙箱不能直接启动任意桌面路径，所以包内包含一个只在点击磁贴时运行的原生全信任桥。
- “永久固定”表示无需后台且重启后保留；用户取消固定、卸载/重置应用或删除目标后，磁贴会失效。

微软文档：

- [UWP Secondary Tiles](https://learn.microsoft.com/windows/uwp/launch-resume/secondary-tiles-pinning)
- [桌面扩展与 FullTrustProcessLauncher](https://learn.microsoft.com/uwp/api/windows.applicationmodel.fulltrustprocesslauncher)
- [MSIX 签名与时间戳](https://learn.microsoft.com/windows/msix/package/signing-package-overview)
