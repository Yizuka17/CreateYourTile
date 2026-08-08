using System.Security.Cryptography;
using System.Text;
using System.Windows.Interop;
using CreateYourTile.Models;
using Windows.UI.StartScreen;

namespace CreateYourTile;

internal sealed class TileRegistrar
{
    public static string CreateStableId(string name, string target)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{name.Trim()}\n{target.Trim()}"));
        return "localtile-" + Convert.ToHexString(bytes)[..24].ToLowerInvariant();
    }

    public async Task<RegistrationResult> RegisterAsync(TileDefinition definition, IntPtr ownerWindow)
    {
        AppStorage.SaveDefinition(definition);

        if (!AppStorage.HasPackageIdentity)
        {
            var iconPath = Path.Combine(AppStorage.GetTileDirectory(definition.Id), "Icon.ico");
            var shortcut = ShortcutService.Create(new TileDefinitionData(definition.Id, definition.Name), iconPath);
            return new RegistrationResult(
                true,
                false,
                $"已创建开始菜单快捷方式：{shortcut}\n当前运行的是未打包开发版，请在“所有应用”中右键它并选择“固定到开始屏幕”。安装 MSIX 版后可直接弹出系统固定确认框。");
        }

        var tile = BuildTile(definition);
        var alreadyPinned = SecondaryTile.Exists(definition.Id);
        if (alreadyPinned)
        {
            var updated = await tile.UpdateAsync();
            return updated
                ? new RegistrationResult(true, true, "磁贴图片、名称和启动目标已更新。")
                : new RegistrationResult(false, true, "Windows 拒绝更新该磁贴。请先从开始菜单取消固定后重试。");
        }

        WinRT.Interop.InitializeWithWindow.Initialize(tile, ownerWindow);
        var created = await tile.RequestCreateAsync();
        return created
            ? new RegistrationResult(true, true, "磁贴已固定。资源全部保存在本机，无需服务器或后台进程。")
            : new RegistrationResult(false, true, "已取消固定；Windows 只允许用户在系统确认框中完成此操作。");
    }

    private static SecondaryTile BuildTile(TileDefinition definition)
    {
        var tileSize = definition.PreferredSize switch
        {
            "Small" => TileSize.Square71x71,
            "Wide" => TileSize.Wide310x150,
            "Large" => TileSize.Square310x310,
            _ => TileSize.Square150x150
        };
        var baseUri = $"ms-appdata:///local/Tiles/{definition.Id}";
        var tile = new SecondaryTile(
            definition.Id,
            definition.Name,
            $"--launch-tile={definition.Id}",
            new Uri($"{baseUri}/Square.png"),
            tileSize);

        tile.VisualElements.Square44x44Logo = new Uri($"{baseUri}/Small.png");
        tile.VisualElements.Wide310x150Logo = new Uri($"{baseUri}/Wide.png");
        tile.VisualElements.Square310x310Logo = new Uri($"{baseUri}/Square.png");
        tile.VisualElements.ShowNameOnSquare150x150Logo = definition.ShowName;
        tile.VisualElements.ShowNameOnWide310x150Logo = definition.ShowName;
        tile.VisualElements.ShowNameOnSquare310x310Logo = definition.ShowName;
        tile.VisualElements.ForegroundText = ForegroundText.Light;
        tile.VisualElements.BackgroundColor = Windows.UI.Color.FromArgb(255, 20, 24, 30);
        return tile;
    }
}

internal sealed record RegistrationResult(bool Success, bool UsedWindowsTileApi, string Message);
