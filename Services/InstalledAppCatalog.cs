using System.Runtime.InteropServices;
using CreateYourTile.Models;

namespace CreateYourTile;

internal static class InstalledAppCatalog
{
    public static IReadOnlyList<InstalledAppInfo> GetApps()
    {
        var result = new List<InstalledAppInfo>();
        var internetShortcutIcons = InternetShortcutIconService.BuildIndex();
        object? shell = null;
        object? folder = null;
        object? items = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application")
                ?? throw new InvalidOperationException("Windows Shell.Application is unavailable.");
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Unable to create Windows Shell.Application.");
            folder = shellType.InvokeMember(
                "NameSpace",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                ["shell:AppsFolder"]);
            if (folder is null)
            {
                throw new InvalidOperationException("The installed applications folder is unavailable.");
            }

            var folderType = folder.GetType();
            items = folderType.InvokeMember(
                "Items",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                folder,
                null);
            if (items is null)
            {
                return result;
            }

            var itemsType = items.GetType();
            var count = Convert.ToInt32(itemsType.InvokeMember(
                "Count",
                System.Reflection.BindingFlags.GetProperty,
                null,
                items,
                null));

            for (var index = 0; index < count; index++)
            {
                object? item = null;
                try
                {
                    item = itemsType.InvokeMember(
                        "Item",
                        System.Reflection.BindingFlags.InvokeMethod,
                        null,
                        items,
                        [index]);
                    if (item is null)
                    {
                        continue;
                    }

                    var itemType = item.GetType();
                    var name = itemType.InvokeMember(
                        "Name",
                        System.Reflection.BindingFlags.GetProperty,
                        null,
                        item,
                        null)?.ToString()?.Trim();
                    var target = itemType.InvokeMember(
                        "Path",
                        System.Reflection.BindingFlags.GetProperty,
                        null,
                        item,
                        null)?.ToString()?.Trim();

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target))
                    {
                        continue;
                    }

                    var targetKind = GetTargetKind(target);
                    result.Add(new InstalledAppInfo(name, target, targetKind, null));
                }
                catch
                {
                    // Ignore individual stale Shell entries and continue loading the list.
                }
                finally
                {
                    ReleaseComObject(item);
                }
            }
        }
        finally
        {
            ReleaseComObject(items);
            ReleaseComObject(folder);
            ReleaseComObject(shell);
        }

        var apps = result
            .GroupBy(app => app.Target, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        Parallel.For(
            0,
            apps.Length,
            new ParallelOptions { MaxDegreeOfParallelism = 6 },
            index =>
            {
                var app = apps[index];
                var shortcutIcon = app.TargetKind == "Uri"
                    ? InternetShortcutIconService.TryGetIcon(app.Target, internetShortcutIcons)
                    : null;
                apps[index] = app with
                {
                    Icon = shortcutIcon ?? ShellIconService.TryGetIcon(app.Target, app.TargetKind)
                };
            });
        return apps;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static string GetTargetKind(string target)
    {
        if (Path.IsPathFullyQualified(target))
        {
            return "File";
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme.Length > 1)
        {
            return "Uri";
        }

        return "AppId";
    }
}
