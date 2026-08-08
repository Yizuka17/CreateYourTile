using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;
using Windows.UI.Xaml.Media.Imaging;
using CreateYourTile.Uwp.Models;

namespace CreateYourTile.Uwp.Services
{
    internal static class InstalledAppCatalog
    {
        public static async Task<IReadOnlyList<InstalledAppInfo>> GetAppsAsync()
        {
            List<InstalledAppInfo> result = new List<InstalledAppInfo>();
            PackageManager manager = new PackageManager();

            foreach (Package package in manager.FindPackagesForUser(string.Empty))
            {
                if (package.IsFramework || package.IsResourcePackage)
                {
                    continue;
                }

                IReadOnlyList<AppListEntry> entries;
                try
                {
                    entries = await package.GetAppListEntriesAsync();
                }
                catch
                {
                    continue;
                }

                foreach (AppListEntry entry in entries)
                {
                    BitmapImage icon = null;
                    Windows.Storage.Streams.IRandomAccessStreamReference logoReference = null;
                    try
                    {
                        logoReference = entry.DisplayInfo.GetLogo(new Windows.Foundation.Size(96, 96));
                        using (Windows.Storage.Streams.IRandomAccessStreamWithContentType stream =
                            await logoReference.OpenReadAsync())
                        {
                            icon = new BitmapImage();
                            await icon.SetSourceAsync(stream);
                        }
                    }
                    catch
                    {
                        icon = null;
                    }

                    result.Add(new InstalledAppInfo
                    {
                        Name = entry.DisplayInfo.DisplayName,
                        Target = entry.AppUserModelId,
                        Icon = icon,
                        LogoReference = logoReference
                    });
                }
            }

            return result
                .Where(app => !string.IsNullOrWhiteSpace(app.Name) && !string.IsNullOrWhiteSpace(app.Target))
                .GroupBy(app => app.Target, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
