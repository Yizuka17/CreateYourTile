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
            zoom = Limit(zoom, 0.1, 4);
            offsetX = Limit(offsetX, -1, 1);
            offsetY = Limit(offsetY, -1, 1);

            using (IRandomAccessStream input = await sourceFile.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(input);
                double sourceWidth = decoder.PixelWidth;
                double sourceHeight = decoder.PixelHeight;
                if (zoom < 1)
                {
                    byte[] zoomedOutPixels = await RenderZoomedOutPixelsAsync(
                        decoder,
                        outputWidth,
                        outputHeight,
                        zoom,
                        offsetX,
                        offsetY);
                    return await EncodePngAsync(
                        outputFolder,
                        outputName,
                        outputWidth,
                        outputHeight,
                        zoomedOutPixels);
                }

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
                double left = maxLeft * (offsetX + 1) / 2;
                double top = maxTop * (offsetY + 1) / 2;

                // BitmapTransform applies scaling before Bounds. Bounds therefore
                // uses coordinates in the scaled bitmap, not the source bitmap.
                // Keeping source-space bounds here makes small Store icons return
                // fewer pixels than BitmapEncoder.SetPixelData expects.
                double scale = Math.Max(outputWidth / cropWidth, outputHeight / cropHeight);
                uint scaledWidth = Math.Max(outputWidth, (uint)Math.Ceiling(sourceWidth * scale));
                uint scaledHeight = Math.Max(outputHeight, (uint)Math.Ceiling(sourceHeight * scale));
                uint boundsX = Math.Min((uint)Math.Round(left * scale), scaledWidth - outputWidth);
                uint boundsY = Math.Min((uint)Math.Round(top * scale), scaledHeight - outputHeight);

                BitmapTransform transform = new BitmapTransform
                {
                    Bounds = new BitmapBounds
                    {
                        X = boundsX,
                        Y = boundsY,
                        Width = outputWidth,
                        Height = outputHeight
                    },
                    ScaledWidth = scaledWidth,
                    ScaledHeight = scaledHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant
                };

                PixelDataProvider provider = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);

                byte[] pixels = provider.DetachPixelData();
                int expectedLength = checked((int)(outputWidth * outputHeight * 4));
                if (pixels.Length != expectedLength)
                {
                    throw new InvalidOperationException("图片解码后的像素缓冲区尺寸不正确。");
                }
                return await EncodePngAsync(
                    outputFolder,
                    outputName,
                    outputWidth,
                    outputHeight,
                    pixels);
            }
        }

        private static async Task<byte[]> RenderZoomedOutPixelsAsync(
            BitmapDecoder decoder,
            uint outputWidth,
            uint outputHeight,
            double zoom,
            double offsetX,
            double offsetY)
        {
            double scale = Math.Max(
                outputWidth / (double)decoder.PixelWidth,
                outputHeight / (double)decoder.PixelHeight) * zoom;
            uint scaledWidth = ToScaledDimension(decoder.PixelWidth * scale);
            uint scaledHeight = ToScaledDimension(decoder.PixelHeight * scale);
            long imageLeft = (long)Math.Round(
                (outputWidth - (double)scaledWidth) / 2 -
                offsetX * Math.Max(0, scaledWidth - (double)outputWidth) / 2);
            long imageTop = (long)Math.Round(
                (outputHeight - (double)scaledHeight) / 2 -
                offsetY * Math.Max(0, scaledHeight - (double)outputHeight) / 2);

            uint destinationX = (uint)Math.Max(0, imageLeft);
            uint destinationY = (uint)Math.Max(0, imageTop);
            uint sourceX = (uint)Math.Max(0, -imageLeft);
            uint sourceY = (uint)Math.Max(0, -imageTop);
            uint copyWidth = (uint)Math.Min(
                (long)scaledWidth - sourceX,
                (long)outputWidth - destinationX);
            uint copyHeight = (uint)Math.Min(
                (long)scaledHeight - sourceY,
                (long)outputHeight - destinationY);

            BitmapTransform transform = new BitmapTransform
            {
                Bounds = new BitmapBounds
                {
                    X = sourceX,
                    Y = sourceY,
                    Width = copyWidth,
                    Height = copyHeight
                },
                ScaledWidth = scaledWidth,
                ScaledHeight = scaledHeight,
                InterpolationMode = BitmapInterpolationMode.Fant
            };
            PixelDataProvider provider = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);
            byte[] sourcePixels = provider.DetachPixelData();
            int expectedSourceLength = checked((int)(copyWidth * copyHeight * 4));
            if (sourcePixels.Length != expectedSourceLength)
            {
                throw new InvalidOperationException("缩小图片后的像素缓冲区尺寸不正确。");
            }

            // A new BGRA buffer is fully transparent. Keep the area outside the
            // scaled image transparent so Windows can composite it with the
            // tile's BackgroundColor instead of baking in an opaque dark box.
            byte[] outputPixels = new byte[checked((int)(outputWidth * outputHeight * 4))];
            int sourceStride = checked((int)(copyWidth * 4));
            for (uint row = 0; row < copyHeight; row++)
            {
                System.Buffer.BlockCopy(
                    sourcePixels,
                    checked((int)(row * copyWidth * 4)),
                    outputPixels,
                    checked((int)((destinationY + row) * outputWidth * 4 + destinationX * 4)),
                    sourceStride);
            }
            return outputPixels;
        }

        private static uint ToScaledDimension(double value)
        {
            return (uint)Math.Max(1, Math.Min(uint.MaxValue, Math.Ceiling(value)));
        }

        private static async Task<StorageFile> EncodePngAsync(
            StorageFolder outputFolder,
            string outputName,
            uint outputWidth,
            uint outputHeight,
            byte[] pixels)
        {
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
                    pixels);
                await encoder.FlushAsync();
            }
            return outputFile;
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
