using System;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using CreateYourTile.Uwp.Models;

namespace CreateYourTile.Uwp.Services
{
    internal static class TileLaunchService
    {
        public static async Task<bool> TryLaunchAsync(string tileId)
        {
            TileDefinition definition = TileStorage.Load(tileId);
            if (definition == null || string.IsNullOrWhiteSpace(definition.Target))
            {
                return false;
            }

            try
            {
                string target = Convert.ToBase64String(Encoding.UTF8.GetBytes(definition.Target));
                string arguments = Convert.ToBase64String(Encoding.UTF8.GetBytes(definition.Arguments ?? string.Empty));
                // The terminator preserves an intentionally empty third record.
                string request = definition.TargetKind + "\n" + target + "\n" + arguments + "\n";
                StorageFile requestFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    "launch-request.txt",
                    CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(requestFile, request, Windows.Storage.Streams.UnicodeEncoding.Utf8);
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
