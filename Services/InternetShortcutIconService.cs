using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace CreateYourTile;

internal static class InternetShortcutIconService
{
    public static IReadOnlyDictionary<string, InternetShortcutIcon> BuildIndex()
    {
        var result = new Dictionary<string, InternetShortcutIcon>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in GetShortcutRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (var shortcutPath in Directory.EnumerateFiles(root, "*.url", SearchOption.AllDirectories))
                {
                    var url = ReadIniValue(shortcutPath, "InternetShortcut", "URL");
                    var iconFile = ReadIniValue(shortcutPath, "InternetShortcut", "IconFile");
                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(iconFile))
                    {
                        continue;
                    }

                    iconFile = Environment.ExpandEnvironmentVariables(iconFile.Trim().Trim('"'));
                    var iconIndexText = ReadIniValue(shortcutPath, "InternetShortcut", "IconIndex");
                    _ = int.TryParse(iconIndexText, out var iconIndex);
                    result.TryAdd(url.Trim(), new InternetShortcutIcon(shortcutPath, iconFile, iconIndex));
                }
            }
            catch
            {
                // A stale or inaccessible Start menu directory should not block the app list.
            }
        }

        return result;
    }

    public static BitmapSource? TryGetIcon(
        string target,
        IReadOnlyDictionary<string, InternetShortcutIcon> shortcutIndex)
    {
        if (!shortcutIndex.TryGetValue(target.Trim(), out var shortcut) || !File.Exists(shortcut.IconFile))
        {
            return null;
        }

        var fromIco = TryLoadIconFile(shortcut.IconFile);
        return fromIco ?? TryExtractIndexedIcon(shortcut.IconFile, shortcut.IconIndex);
    }

    private static BitmapSource? TryLoadIconFile(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var decoder = new IconBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var bestFrame = decoder.Frames
                .OrderByDescending(frame => frame.PixelWidth * frame.PixelHeight)
                .ThenByDescending(frame => frame.Format.BitsPerPixel)
                .FirstOrDefault();
            if (bestFrame is null)
            {
                return null;
            }

            var result = BitmapFrame.Create(bestFrame);
            result.Freeze();
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? TryExtractIndexedIcon(string path, int iconIndex)
    {
        IntPtr iconHandle = IntPtr.Zero;
        try
        {
            var extracted = PrivateExtractIcons(
                path,
                iconIndex,
                256,
                256,
                out iconHandle,
                out _,
                1,
                0);
            if (extracted == 0 || iconHandle == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (iconHandle != IntPtr.Zero)
            {
                DestroyIcon(iconHandle);
            }
        }
    }

    private static IEnumerable<string> GetShortcutRoots()
    {
        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadIniValue(string filePath, string section, string key)
    {
        var buffer = new StringBuilder(32768);
        var length = GetPrivateProfileString(section, key, string.Empty, buffer, (uint)buffer.Capacity, filePath);
        return length == 0 ? string.Empty : buffer.ToString(0, (int)length);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetPrivateProfileString(
        string section,
        string key,
        string defaultValue,
        StringBuilder returnedString,
        uint size,
        string filePath);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint PrivateExtractIcons(
        string fileName,
        int iconIndex,
        int iconWidth,
        int iconHeight,
        out IntPtr iconHandle,
        out uint iconId,
        uint iconCount,
        uint flags);
}

internal sealed record InternetShortcutIcon(string ShortcutPath, string IconFile, int IconIndex);
