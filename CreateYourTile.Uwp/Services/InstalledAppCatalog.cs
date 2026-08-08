using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Globalization.Collation;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using CreateYourTile.Uwp.Models;

namespace CreateYourTile.Uwp.Services
{
    internal static class InstalledAppCatalog
    {
        private const string CatalogPrefix = "app-catalog-";

        public static async Task<IReadOnlyList<InstalledAppInfo>> GetAppsAsync()
        {
            string outputName = CatalogPrefix + Guid.NewGuid().ToString("N") + ".txt";
            string encodedOutputName = Convert.ToBase64String(Encoding.UTF8.GetBytes(outputName));
            string request = "Catalog\n" + encodedOutputName + "\n\n";
            StorageFile requestFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                "launch-request.txt",
                CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(
                requestFile,
                request,
                Windows.Storage.Streams.UnicodeEncoding.Utf8);

            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();

            StorageFile catalogFile = null;
            for (int attempt = 0; attempt < 200 && catalogFile == null; attempt++)
            {
                try
                {
                    catalogFile = await ApplicationData.Current.LocalFolder.GetFileAsync(outputName);
                }
                catch (System.IO.FileNotFoundException)
                {
                    await Task.Delay(50);
                }
            }
            if (catalogFile == null)
            {
                throw new TimeoutException("读取 Windows 开始菜单超时。");
            }

            IList<string> lines;
            try
            {
                lines = await FileIO.ReadLinesAsync(
                    catalogFile,
                    Windows.Storage.Streams.UnicodeEncoding.Utf8);
            }
            finally
            {
                await catalogFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }

            CharacterGroupings characterGroupings = new CharacterGroupings();
            List<InstalledAppInfo> result = new List<InstalledAppInfo>();
            foreach (string line in lines)
            {
                string[] fields = line.Split('\t');
                if (fields.Length < 2)
                {
                    continue;
                }

                string name;
                string target;
                try
                {
                    name = DecodeBase64(fields[0]);
                    target = DecodeBase64(fields[1]);
                }
                catch (FormatException)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                string iconFileName = fields.Length >= 3 ? fields[2] : string.Empty;
                BitmapImage icon = null;
                if (IsSafeIconFileName(iconFileName))
                {
                    icon = new BitmapImage(new Uri(
                        "ms-appdata:///local/AppCatalog/" + iconFileName));
                }

                string groupKey = GetGroupKey(characterGroupings, name);
                result.Add(new InstalledAppInfo
                {
                    Name = name,
                    Target = target,
                    TargetKind = "ShellApp",
                    GroupKey = groupKey,
                    GroupOrder = GetGroupOrder(groupKey),
                    Icon = icon,
                    IconFileName = IsSafeIconFileName(iconFileName) ? iconFileName : string.Empty
                });
            }

            return result
                .GroupBy(app => app.Name + "\0" + app.Target, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(app => app.GroupOrder)
                .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<InstalledAppGroup> GroupApps(IEnumerable<InstalledAppInfo> apps)
        {
            return apps
                .GroupBy(app => app.GroupKey)
                .OrderBy(group => group.Min(app => app.GroupOrder))
                .Select(group => new InstalledAppGroup
                {
                    Key = group.Key,
                    Items = group
                        .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                })
                .ToList();
        }

        private static string DecodeBase64(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private static bool IsSafeIconFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string stem = value.Substring(0, value.Length - ".png".Length);
            return stem.Length == 16 && stem.All(character =>
                (character >= 'a' && character <= 'f') ||
                (character >= 'A' && character <= 'F') ||
                (character >= '0' && character <= '9'));
        }

        private static string GetGroupKey(CharacterGroupings groupings, string name)
        {
            string label = groupings.Lookup(name);
            if (string.IsNullOrWhiteSpace(label))
            {
                return "#";
            }

            label = label.Trim();
            if (label.StartsWith("拼音", StringComparison.CurrentCultureIgnoreCase))
            {
                label = label.Substring("拼音".Length);
            }
            label = label.ToUpperInvariant();
            if (label.Length == 1 && label[0] >= 'A' && label[0] <= 'Z')
            {
                return label;
            }
            return "#";
        }

        private static int GetGroupOrder(string groupKey)
        {
            return groupKey == "#" ? 0 : groupKey[0] - 'A' + 1;
        }
    }
}
