using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CreateYourTile;

internal static class ImageCropper
{
    public static BitmapSource Load(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapSource Render(BitmapSource source, int width, int height, double zoom, double offsetX, double offsetY)
    {
        zoom = Math.Clamp(zoom, 1, 4);
        offsetX = Math.Clamp(offsetX, -1, 1);
        offsetY = Math.Clamp(offsetY, -1, 1);

        var coverScale = Math.Max((double)width / source.PixelWidth, (double)height / source.PixelHeight);
        var scale = coverScale * zoom;
        var drawWidth = source.PixelWidth * scale;
        var drawHeight = source.PixelHeight * scale;
        var overflowX = Math.Max(0, drawWidth - width);
        var overflowY = Math.Max(0, drawHeight - height);
        var x = -overflowX / 2 + offsetX * overflowX / 2;
        var y = -overflowY / 2 + offsetY * overflowY / 2;

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
            context.DrawImage(source, new Rect(x, y, drawWidth, drawHeight));
        }

        var result = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    public static void SavePng(BitmapSource bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    public static void SavePngAsIcon(BitmapSource bitmap, string path)
    {
        using var pngStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(pngStream);
        var png = pngStream.ToArray();

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(png.Length);
        writer.Write(22);
        writer.Write(png);
    }
}
