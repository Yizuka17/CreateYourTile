using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CreateYourTile;

internal static class PackageAssetGenerator
{
    public static void Generate(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        SaveLogo(outputDirectory, "Square44x44Logo.png", 44, 44);
        SaveLogo(outputDirectory, "Square150x150Logo.png", 150, 150);
        SaveLogo(outputDirectory, "Wide310x150Logo.png", 310, 150);
        SaveLogo(outputDirectory, "Square310x310Logo.png", 310, 310);
        SaveLogo(outputDirectory, "StoreLogo.png", 50, 50);
    }

    private static void SaveLogo(string outputDirectory, string name, int width, int height)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var iconSize = Math.Min(width, height);
            var iconBounds = new Rect((width - iconSize) / 2d, (height - iconSize) / 2d, iconSize, iconSize);
            context.DrawImage(ProductBranding.Icon, iconBounds);
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        ImageCropper.SavePng(bitmap, Path.Combine(outputDirectory, name));
    }
}
