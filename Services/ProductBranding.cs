using System.Windows.Media.Imaging;

namespace CreateYourTile;

internal static class ProductBranding
{
    public const string DisplayName = "CreateYourTile!";

    private const string IconResourceName = "CreateYourTile.ProductIcon.png.base64";
    private static readonly Lazy<BitmapSource> IconSource = new(LoadIcon);

    public static BitmapSource Icon => IconSource.Value;

    private static BitmapSource LoadIcon()
    {
        using var resource = typeof(ProductBranding).Assembly.GetManifestResourceStream(IconResourceName)
            ?? throw new InvalidOperationException($"找不到产品图标资源：{IconResourceName}");
        using var reader = new StreamReader(resource);
        var bytes = Convert.FromBase64String(reader.ReadToEnd());
        using var imageStream = new MemoryStream(bytes, writable: false);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = imageStream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
