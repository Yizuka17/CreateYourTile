using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace CreateYourTile.Uwp.Models
{
    public sealed class InstalledAppInfo
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public BitmapImage Icon { get; set; }
        public IRandomAccessStreamReference LogoReference { get; set; }
    }
}
