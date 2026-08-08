using System.Diagnostics;

namespace CreateYourTile;

internal static class TileLaunchService
{
    public static bool TryLaunch(string tileId, out string error)
    {
        try
        {
            var definition = AppStorage.LoadDefinition(tileId);
            if (definition is null)
            {
                error = "找不到该磁贴的本地启动信息。它可能已被清理或应用已被重置。";
                return false;
            }

            ProcessStartInfo startInfo;
            switch (definition.TargetKind)
            {
                case "AppId":
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"shell:AppsFolder\\{definition.Target}\"",
                        UseShellExecute = true
                    };
                    break;
                case "Uri":
                    startInfo = new ProcessStartInfo
                    {
                        FileName = definition.Target,
                        UseShellExecute = true
                    };
                    break;
                default:
                    startInfo = new ProcessStartInfo
                    {
                        FileName = definition.Target,
                        Arguments = definition.Arguments,
                        WorkingDirectory = GetWorkingDirectory(definition.Target),
                        UseShellExecute = true
                    };
                    break;
            }

            Process.Start(startInfo);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"无法打开目标：{exception.Message}";
            return false;
        }
    }

    private static string GetWorkingDirectory(string target)
    {
        if (Directory.Exists(target))
        {
            return target;
        }

        if (!Path.IsPathFullyQualified(target))
        {
            return string.Empty;
        }

        return Path.GetDirectoryName(target) ?? string.Empty;
    }
}
