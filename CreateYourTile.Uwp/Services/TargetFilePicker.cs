using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace CreateYourTile.Uwp.Services
{
    internal sealed class TargetFilePickerResult
    {
        public string Path { get; set; }
        public StorageFile IconFile { get; set; }
    }

    internal static class TargetFilePicker
    {
        private const string OutputPrefix = "target-picker-";

        public static async Task<TargetFilePickerResult> PickAsync()
        {
            string outputName = OutputPrefix + Guid.NewGuid().ToString("N") + ".txt";
            string encodedOutputName = Convert.ToBase64String(Encoding.UTF8.GetBytes(outputName));
            string request = "PickFile\n" + encodedOutputName + "\n\n";
            StorageFile requestFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                "launch-request.txt",
                CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(
                requestFile,
                request,
                Windows.Storage.Streams.UnicodeEncoding.Utf8);

            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();

            StorageFile outputFile = null;
            for (int attempt = 0; attempt < 12000 && outputFile == null; attempt++)
            {
                try
                {
                    outputFile = await ApplicationData.Current.LocalFolder.GetFileAsync(outputName);
                }
                catch (System.IO.FileNotFoundException)
                {
                    await Task.Delay(50);
                }
            }
            if (outputFile == null)
            {
                throw new TimeoutException("等待文件选择结果超时。");
            }

            string line;
            try
            {
                var lines = await FileIO.ReadLinesAsync(
                    outputFile,
                    Windows.Storage.Streams.UnicodeEncoding.Utf8);
                line = lines.FirstOrDefault() ?? string.Empty;
            }
            finally
            {
                await outputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }

            string[] fields = line.Split('\t');
            if (fields.Length == 0 || string.IsNullOrWhiteSpace(fields[0]))
            {
                return null;
            }

            string path = Encoding.UTF8.GetString(Convert.FromBase64String(fields[0]));
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            StorageFile iconFile = null;
            if (fields.Length >= 2 && IsSafeIconFileName(fields[1]))
            {
                try
                {
                    iconFile = await ApplicationData.Current.LocalFolder.GetFileAsync(fields[1]);
                }
                catch (System.IO.FileNotFoundException)
                {
                }
            }

            return new TargetFilePickerResult
            {
                Path = path,
                IconFile = iconFile
            };
        }

        private static bool IsSafeIconFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !value.StartsWith(OutputPrefix, StringComparison.Ordinal) ||
                !value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string id = value.Substring(
                OutputPrefix.Length,
                value.Length - OutputPrefix.Length - ".png".Length);
            return id.Length == 32 && id.All(character =>
                (character >= 'a' && character <= 'f') ||
                (character >= '0' && character <= '9'));
        }
    }
}
