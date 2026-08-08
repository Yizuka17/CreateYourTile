`v1.6.0` 是 CreateYourTile! 的首个正式 UWP 版本，面向 Windows 10 的 Live Tile 创建与固定场景。

### 主要功能

- 原生 UWP XAML、Fluent 控件、触摸直接操作、惯性滚动与响应式双栏布局。
- 跟随 Windows 系统强调色和应用深浅模式。
- 支持在 `0.1×–4.0×` 范围内缩放、移动和裁剪磁贴图片，并保留透明 PNG 的 Alpha 通道。
- 从完整 Windows AppsFolder 读取 Store 应用、桌面程序和系统工具，并按 `# / A–Z`（中文按拼音首字母）分组。
- 支持程序、文件、文件夹、网址、URI、AUMID 和 `.lnk` 快捷方式；保留快捷方式参数、工作目录及“以管理员身份运行”选项。
- 使用 Windows `SecondaryTile` 接口生成小、中、宽、大磁贴资源，并固定受系统支持的中/宽默认尺寸。

### 1.6 修复与完善

- 修复缩小默认应用图标后外围被写成不透明深色的问题。
- 移除产品图标中误带的灰白网格，所有图标资源改为真实透明背景。
- 增加完整的 `targetsize`、深色 `altform-unplated` 和浅色 `altform-lightunplated` 资源，任务栏、Alt+Tab 和开始菜单应用列表不再出现系统底板。
- 修复小尺寸应用图标处理时“分配的缓冲区不够”以及错误默认磁贴尺寸引发的“参数错误”。
- 文件选择器启用 Per-Monitor V2 高 DPI 与 Common Controls v6，避免高缩放屏幕上的模糊旧式外观。

### 安装

1. 下载并解压 `CreateYourTile-v1.6.0-win-x64.zip`。
2. 运行其中的 `Install-Package.ps1`。
3. 接受 UAC 管理员权限提示；脚本会导入随附的自签名证书，并安装 UWP 依赖与 MSIX。

> 本版本使用有效期 10 年、带 RFC 3161 时间戳的自签名证书。请仅安装本 Release 附带的原始文件，并可使用 `SHA256SUMS.txt` 校验下载内容。
