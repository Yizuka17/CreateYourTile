using System.Collections.Generic;
using Windows.UI.Xaml.Media.Imaging;

namespace CreateYourTile.Uwp.Models
{
    public sealed class InstalledAppInfo
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public string TargetKind { get; set; }
        public string GroupKey { get; set; }
        public int GroupOrder { get; set; }
        public BitmapImage Icon { get; set; }
        public string IconFileName { get; set; }
    }

    public sealed class InstalledAppGroup
    {
        public string Key { get; set; }
        public IReadOnlyList<InstalledAppInfo> Items { get; set; }
    }
}
