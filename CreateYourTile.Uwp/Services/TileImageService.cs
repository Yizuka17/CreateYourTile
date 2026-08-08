using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CreateYourTile.Uwp.Services
{
    internal static class TileImageService
    {
        public static async Task<StorageFile> RenderAsync(
            StorageFile sourceFile,
            StorageFolder outputFolder,
            string outputName,
            uint outputWidth,
            uint outputHeight,
            double zoom,
            double offsetX,
            double offsetY)
        {
            zoom = Limit(zoom, 1, 4);
            offsetX = Limit(offsetX, -1, 1);
            offsetY = Limit(offsetY, -1, 1);

            using (IRandomAccessStream input = await sourceFile.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(input);
                double sourceWidth = decoder.PixelWidth;
                double sourceHeight = decoder.PixelHeight;
                double outputAspect = (double)outputWidth / outputHeight;
                double sourceAspect = sourceWidth / sourceHeight;

                double baseCropWidth;
                double baseCropHeight;
                if (sourceAspect > outputAspect)
                {
                    baseCropHeight = sourceHeight;
                    baseCropWidth = sourceHeight * outputAspect;
                }
                else
                {
                    baseCropWidth = sourceWidth;
                    baseCropHeight = sourceWidth / outputAspect;
                }

                double cropWidth = Math.Max(1, baseCropWidth / zoom);
                double cropHeight = Math.Max(1, baseCropHeight / zoom);
                double maxLeft = Math.Max(0, sourceWidth - cropWidth);
                double maxTop = Math.Max(0, sourceHeight - cropHeight);
                uint left = (uint)Math.Round(maxLeft * (offsetX + 1) / 2);
                uint top = (uint)Math.Round(maxTop * (offsetY + 1) / 2);

                BitmapTransform transform = new BitmapTransform
                {
                    Bounds = new BitmapBounds
                    {
                        X = left,
                        Y = top,
                        Width = Math.Min((uint)Math.Round(cropWidth), decoder.PixelWidth - left),
                        Height = Math.Min((uint)Math.Round(cropHeight), decoder.PixelHeight - top)
                    },
                    ScaledWidth = outputWidth,
                    ScaledHeight = outputHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant
                };

                PixelDataProvider provider = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);

                StorageFile outputFile = await outputFolder.CreateFileAsync(
                    outputName,
                    CreationCollisionOption.ReplaceExisting);
                using (IRandomAccessStream output = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        outputWidth,
                        outputHeight,
                        96,
                        96,
                        provider.DetachPixelData());
                    await encoder.FlushAsync();
                }

                return outputFile;
            }
        }

        public static async Task<StorageFile> CopyStreamToTemporaryFileAsync(
            IRandomAccessStreamWithContentType source,
            string fileName)
        {
            StorageFile file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                fileName,
                CreationCollisionOption.ReplaceExisting);
            using (IRandomAccessStream output = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                source.Seek(0);
                await RandomAccessStream.CopyAsync(source, output);
                await output.FlushAsync();
            }
            return file;
        }

        private static double Limit(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
