using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using CreateYourTile.Uwp.Models;

namespace CreateYourTile.Uwp.Services
{
    internal enum DesktopTilePinOutcome
    {
        Created,
        Updated,
        Cancelled
    }

    internal static class DesktopTileService
    {
        private const string PinRequestPrefix = "tile-pin-";
        private const string PinResultPrefix = "tile-pin-result-";

        public static async Task<DesktopTilePinOutcome> PinAsync(TileDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (!IsSafeTileId(definition.Id))
            {
                throw new ArgumentException("磁贴 ID 无效。", nameof(definition));
            }

            await SaveLaunchDefinitionAsync(definition);

            string token = Guid.NewGuid().ToString("N");
            string pinRequestName = PinRequestPrefix + token + ".txt";
            string resultName = PinResultPrefix + token + ".txt";
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile pinRequestFile = await localFolder.CreateFileAsync(
                pinRequestName,
                CreationCollisionOption.ReplaceExisting);
            string pinRequest =
                EncodeBase64(definition.Id) + "\n" +
                EncodeBase64(definition.Name) + "\n" +
                (definition.PreferredSize == "Wide" ? "Wide" : "Medium") + "\n" +
                (definition.ShowName ? "1" : "0") + "\n";
            await FileIO.WriteTextAsync(
                pinRequestFile,
                pinRequest,
                Windows.Storage.Streams.UnicodeEncoding.Utf8);

            string launchRequest =
                "PinTile\n" +
                EncodeBase64(pinRequestName) + "\n" +
                EncodeBase64(resultName) + "\n";
            StorageFile launchRequestFile = await localFolder.CreateFileAsync(
                "launch-request.txt",
                CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(
                launchRequestFile,
                launchRequest,
                Windows.Storage.Streams.UnicodeEncoding.Utf8);

            StorageFile resultFile = null;
            try
            {
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                for (int attempt = 0; attempt < 12000 && resultFile == null; attempt++)
                {
                    try
                    {
                        resultFile = await localFolder.GetFileAsync(resultName);
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        await Task.Delay(50);
                    }
                }
                if (resultFile == null)
                {
                    throw new TimeoutException("等待 Windows 固定磁贴结果超时。");
                }

                IList<string> lines = await FileIO.ReadLinesAsync(
                    resultFile,
                    Windows.Storage.Streams.UnicodeEncoding.Utf8);
                string result = lines.Count > 0 ? lines[0] : string.Empty;
                string[] fields = result.Split('\t');
                if (fields.Length > 0 && fields[0] == "success")
                {
                    return fields.Length > 1 && fields[1] == "updated"
                        ? DesktopTilePinOutcome.Updated
                        : DesktopTilePinOutcome.Created;
                }
                if (fields.Length > 0 && fields[0] == "cancelled")
                {
                    return DesktopTilePinOutcome.Cancelled;
                }

                string detail = fields.Length > 1 ? fields[1] : "桌面磁贴启动器返回未知错误。";
                throw new InvalidOperationException(detail);
            }
            finally
            {
                if (resultFile != null)
                {
                    await resultFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                await TryDeleteAsync(localFolder, pinRequestName);
            }
        }

        private static async Task SaveLaunchDefinitionAsync(TileDefinition definition)
        {
            StorageFolder definitionsFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "TileDefinitions",
                CreationCollisionOption.OpenIfExists);
            StorageFile definitionFile = await definitionsFolder.CreateFileAsync(
                definition.Id + ".txt",
                CreationCollisionOption.ReplaceExisting);
            string content =
                definition.TargetKind + "\n" +
                EncodeBase64(definition.Target) + "\n" +
                EncodeBase64(definition.Arguments ?? string.Empty) + "\n";
            await FileIO.WriteTextAsync(
                definitionFile,
                content,
                Windows.Storage.Streams.UnicodeEncoding.Utf8);
        }

        private static async Task TryDeleteAsync(StorageFolder folder, string name)
        {
            try
            {
                StorageFile file = await folder.GetFileAsync(name);
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (System.IO.FileNotFoundException)
            {
            }
        }

        private static string EncodeBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static bool IsSafeTileId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !value.StartsWith("localtile-", StringComparison.Ordinal) ||
                value.Length != "localtile-".Length + 24)
            {
                return false;
            }
            for (int index = "localtile-".Length; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= 'a' && character <= 'f') ||
                      (character >= '0' && character <= '9')))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
