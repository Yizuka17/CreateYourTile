using System.Windows.Media;

namespace CreateYourTile.Models;

public sealed record InstalledAppInfo(string Name, string Target, string TargetKind, ImageSource? Icon)
{
    public string KindLabel => TargetKind switch
    {
        "File" => "桌面应用 / 本地目标",
        "Uri" => "网址 / URI",
        _ => "Microsoft Store / 已注册应用"
    };
}
