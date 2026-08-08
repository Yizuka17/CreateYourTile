using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using CreateYourTile.Uwp.Models;
using CreateYourTile.Uwp.Services;

namespace CreateYourTile.Uwp
{
    public sealed partial class MainPage : Page
    {
        private const double CompactLayoutThreshold = 900;
        private StorageFile _sourceImageFile;
        private BitmapImage _sourceBitmap;
        private double _sourcePixelWidth;
        private double _sourcePixelHeight;
        private bool? _isCompactLayout;
        private bool _ready;
        private IReadOnlyList<InstalledAppInfo> _installedApps;
        private string _selectedInstalledAppTarget;
        private string _selectedInstalledAppTargetKind;

        public MainPage()
        {
            InitializeComponent();
            Loaded += MainPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string argument = e.Parameter as string;
            if (!string.IsNullOrWhiteSpace(argument) && argument.StartsWith("tile:", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("无法读取该磁贴的本地启动信息，请重新创建磁贴。", true);
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            NameTextBox.Text = "我的磁贴";
            UpdateResponsiveLayout(ActualWidth);
            _ready = true;
            UpdateTargetKindUi();
            UpdatePreview();
            PreviewScroller.ChangeView(null, 0, null, true);
            SettingsScroller.ChangeView(null, 0, null, true);
        }

        private async void ChooseImageButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".tif");
            picker.FileTypeFilter.Add(".tiff");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            await ApplySourceImageAsync(file, file.Name);
            SetStatus("图片已载入。可以直接用手指拖动下面的滑块调整裁剪。", false);
        }

        private async void BrowseTargetButton_Click(object sender, RoutedEventArgs e)
        {
            if (GetSelectedTag(TargetKindComboBox) != "File")
            {
                return;
            }

            FileOpenPicker picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add("*");
            StorageFile file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            TargetTextBox.Text = file.Path;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NameTextBox.Text = file.DisplayName;
            }
            await TryUseStorageItemThumbnailAsync(file, file.DisplayName + " 的图标");
        }

        private async void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (GetSelectedTag(TargetKindComboBox) != "File")
            {
                return;
            }

            FolderPicker picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            TargetTextBox.Text = folder.Path;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NameTextBox.Text = folder.DisplayName;
            }
            await TryUseStorageItemThumbnailAsync(folder, folder.DisplayName + " 的图标");
        }

        private async void ChooseInstalledAppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetStatus("正在读取已安装应用…", false);
                _installedApps = await InstalledAppCatalog.GetAppsAsync();

                AutoSuggestBox searchBox = new AutoSuggestBox
                {
                    PlaceholderText = "搜索应用",
                    QueryIcon = new SymbolIcon(Symbol.Find),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                ListView listView = new ListView
                {
                    Height = 420,
                    SelectionMode = ListViewSelectionMode.Single,
                    IsItemClickEnabled = true,
                    ItemTemplate = (DataTemplate)Resources["InstalledAppTemplate"]
                };
                listView.GroupStyle.Add(new GroupStyle
                {
                    HeaderTemplate = (DataTemplate)Resources["InstalledAppGroupHeaderTemplate"],
                    HidesIfEmpty = true
                });
                TextBlock countText = new TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = (Brush)Application.Current.Resources[
                        "SystemControlForegroundBaseMediumBrush"]
                };
                Action<IEnumerable<InstalledAppInfo>> updateList = apps =>
                {
                    List<InstalledAppInfo> filtered = apps.ToList();
                    CollectionViewSource source = new CollectionViewSource
                    {
                        IsSourceGrouped = true,
                        Source = InstalledAppCatalog.GroupApps(filtered),
                        ItemsPath = new PropertyPath("Items")
                    };
                    listView.ItemsSource = source.View;
                    countText.Text = filtered.Count + " 个已安装应用，按首字母分组";
                };
                updateList(_installedApps);
                searchBox.TextChanged += delegate
                {
                    string query = searchBox.Text == null ? string.Empty : searchBox.Text.Trim();
                    IEnumerable<InstalledAppInfo> filtered = string.IsNullOrWhiteSpace(query)
                        ? _installedApps
                        : _installedApps.Where(app =>
                            app.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                            app.Target.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                    updateList(filtered);
                };
                listView.ItemClick += delegate(object itemSender, ItemClickEventArgs itemArgs)
                {
                    listView.SelectedItem = itemArgs.ClickedItem;
                };

                StackPanel content = new StackPanel();
                content.Children.Add(searchBox);
                content.Children.Add(countText);
                content.Children.Add(listView);
                ContentDialog dialog = new ContentDialog
                {
                    Title = "选择已安装应用",
                    Content = content,
                    PrimaryButtonText = "选择",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary
                };

                ContentDialogResult result = await dialog.ShowAsync();
                InstalledAppInfo selected = listView.SelectedItem as InstalledAppInfo;
                if (result != ContentDialogResult.Primary || selected == null)
                {
                    SetStatus("未选择应用。", false);
                    return;
                }

                _selectedInstalledAppTarget = selected.Target;
                _selectedInstalledAppTargetKind = selected.TargetKind;
                SelectComboBoxTag(TargetKindComboBox, "AppId");
                TargetTextBox.Text = selected.Target;
                ArgumentsTextBox.Text = string.Empty;
                NameTextBox.Text = selected.Name;

                string iconError = null;
                if (!string.IsNullOrWhiteSpace(selected.IconFileName))
                {
                    try
                    {
                        StorageFolder iconFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(
                            "AppCatalog");
                        StorageFile logoFile = await iconFolder.GetFileAsync(selected.IconFileName);
                        await ApplySourceImageAsync(logoFile, selected.Name + " 的应用图标");
                    }
                    catch (Exception exception)
                    {
                        iconError = exception.Message;
                    }
                }
                SetStatus(iconError == null
                    ? "已选择应用：" + selected.Name + "。"
                    : "已选择应用，但无法读取其图标：" + iconError,
                    iconError != null);
            }
            catch (Exception exception)
            {
                SetStatus("无法读取已安装应用：" + exception.Message, true);
            }
        }

        private async Task TryUseStorageItemThumbnailAsync(IStorageItem item, string label)
        {
            try
            {
                StorageItemThumbnail thumbnail;
                StorageFile file = item as StorageFile;
                if (file != null)
                {
                    thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 256, ThumbnailOptions.UseCurrentScale);
                }
                else
                {
                    StorageFolder folder = item as StorageFolder;
                    thumbnail = await folder.GetThumbnailAsync(ThumbnailMode.SingleItem, 256, ThumbnailOptions.UseCurrentScale);
                }

                if (thumbnail == null)
                {
                    return;
                }

                using (thumbnail)
                {
                    StorageFile thumbnailFile = await TileImageService.CopyStreamToTemporaryFileAsync(
                        thumbnail,
                        "selected-target-thumbnail.img");
                    await ApplySourceImageAsync(thumbnailFile, label);
                }
            }
            catch
            {
                // A target can still be used when Windows cannot provide a thumbnail.
            }
        }

        private async Task ApplySourceImageAsync(StorageFile file, string label)
        {
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                _sourcePixelWidth = decoder.PixelWidth;
                _sourcePixelHeight = decoder.PixelHeight;
                stream.Seek(0);
                BitmapImage bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                _sourceBitmap = bitmap;
            }

            _sourceImageFile = file;
            PreviewImage.Source = _sourceBitmap;
            ImagePathText.Text = label;
            EmptyImageHint.Visibility = Visibility.Collapsed;
            ZoomSlider.Value = 1;
            OffsetXSlider.Value = 0;
            OffsetYSlider.Value = 0;
            UpdatePreview();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string validationError = ValidateInput();
            if (!string.IsNullOrEmpty(validationError))
            {
                SetStatus(validationError, true);
                return;
            }

            RegisterButton.IsEnabled = false;
            RegisterButton.Content = "正在生成磁贴…";
            try
            {
                string name = NameTextBox.Text.Trim();
                string targetKind = GetSelectedTag(TargetKindComboBox);
                string rawTarget = TargetTextBox.Text.Trim();
                if (targetKind == "AppId" &&
                    !string.IsNullOrWhiteSpace(_selectedInstalledAppTargetKind) &&
                    string.Equals(rawTarget, _selectedInstalledAppTarget, StringComparison.OrdinalIgnoreCase))
                {
                    targetKind = _selectedInstalledAppTargetKind;
                }
                string target = NormalizeTarget(rawTarget, targetKind);
                string id = CreateStableId(name, target);
                StorageFolder tilesFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    "Tiles",
                    CreationCollisionOption.OpenIfExists);
                StorageFolder tileFolder = await tilesFolder.CreateFolderAsync(
                    id,
                    CreationCollisionOption.OpenIfExists);

                await TileImageService.RenderAsync(_sourceImageFile, tileFolder, "Square.png", 512, 512,
                    ZoomSlider.Value, OffsetXSlider.Value, OffsetYSlider.Value);
                await TileImageService.RenderAsync(_sourceImageFile, tileFolder, "Wide.png", 1024, 512,
                    ZoomSlider.Value, OffsetXSlider.Value, OffsetYSlider.Value);
                await TileImageService.RenderAsync(_sourceImageFile, tileFolder, "Small.png", 256, 256,
                    ZoomSlider.Value, OffsetXSlider.Value, OffsetYSlider.Value);

                TileDefinition definition = new TileDefinition
                {
                    Id = id,
                    Name = name,
                    TargetKind = targetKind,
                    Target = target,
                    Arguments = ArgumentsTextBox.Text ?? string.Empty,
                    PreferredSize = GetSelectedTag(TileSizeComboBox),
                    ShowName = ShowNameCheckBox.IsChecked == true
                };
                TileStorage.Save(definition);

                SecondaryTile tile = BuildTile(definition);
                bool success;
                if (SecondaryTile.Exists(id))
                {
                    success = await tile.UpdateAsync();
                    SetStatus(success ? "磁贴图片、名称和启动目标已更新。" : "Windows 拒绝更新该磁贴。", !success);
                }
                else
                {
                    RegisterButton.Content = "等待系统确认…";
                    success = await tile.RequestCreateAsync();
                    SetStatus(success ? "磁贴已固定。" : "已取消固定。", false);
                }
            }
            catch (Exception exception)
            {
                SetStatus("注册失败：" + exception.Message, true);
            }
            finally
            {
                RegisterButton.Content = "注册并固定到“开始”";
                RegisterButton.IsEnabled = true;
            }
        }

        private string ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                return "请填写磁贴名称。";
            }
            if (_sourceImageFile == null)
            {
                return "请先选择一张磁贴图片。";
            }
            if (string.IsNullOrWhiteSpace(TargetTextBox.Text))
            {
                return "请填写要打开的目标。";
            }
            if (GetSelectedTag(TargetKindComboBox) == "Uri")
            {
                Uri parsed;
                if (!Uri.TryCreate(TargetTextBox.Text.Trim(), UriKind.Absolute, out parsed))
                {
                    return "请输入完整网址或 URI，例如 https://example.com。";
                }
            }
            return string.Empty;
        }

        private static SecondaryTile BuildTile(TileDefinition definition)
        {
            // Windows 10 only accepts medium, wide, or Default as the desired
            // size passed to the SecondaryTile constructor. Small and large
            // remain available after pinning because their logo assets are set
            // below, but passing either size here raises E_INVALIDARG.
            TileSize tileSize = definition.PreferredSize == "Wide"
                ? TileSize.Wide310x150
                : TileSize.Square150x150;

            string baseUri = "ms-appdata:///local/Tiles/" + definition.Id;
            SecondaryTile tile = new SecondaryTile(
                definition.Id,
                definition.Name,
                "tile:" + definition.Id,
                new Uri(baseUri + "/Square.png"),
                tileSize);
            tile.VisualElements.Square44x44Logo = new Uri(baseUri + "/Small.png");
            tile.VisualElements.Square70x70Logo = new Uri(baseUri + "/Small.png");
            tile.VisualElements.Wide310x150Logo = new Uri(baseUri + "/Wide.png");
            tile.VisualElements.Square310x310Logo = new Uri(baseUri + "/Square.png");
            tile.VisualElements.ShowNameOnSquare150x150Logo = definition.ShowName;
            tile.VisualElements.ShowNameOnWide310x150Logo = definition.ShowName;
            tile.VisualElements.ShowNameOnSquare310x310Logo = definition.ShowName;
            tile.VisualElements.ForegroundText = ForegroundText.Light;
            tile.VisualElements.BackgroundColor = Color.FromArgb(255, 20, 24, 30);
            return tile;
        }

        private static string CreateStableId(string name, string target)
        {
            byte[] bytes;
            using (SHA256 algorithm = SHA256.Create())
            {
                bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(name.Trim() + "\n" + target.Trim()));
            }
            StringBuilder builder = new StringBuilder(24);
            for (int index = 0; index < 12; index++)
            {
                builder.Append(bytes[index].ToString("x2"));
            }
            return "localtile-" + builder;
        }

        private void CropControl_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_ready)
            {
                return;
            }
            ZoomValueText.Text = ZoomSlider.Value.ToString("0.0") + "×";
            UpdatePreview();
        }

        private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PreviewName != null)
            {
                PreviewName.Text = string.IsNullOrWhiteSpace(NameTextBox.Text)
                    ? "磁贴名称"
                    : NameTextBox.Text.Trim();
            }
        }

        private void TileSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ready)
            {
                UpdatePreview();
            }
        }

        private void TargetKindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ready)
            {
                UpdateTargetKindUi();
            }
        }

        private void UpdateTargetKindUi()
        {
            string targetKind = GetSelectedTag(TargetKindComboBox);
            bool isFile = targetKind == "File";
            BrowseTargetButton.Visibility = isFile ? Visibility.Visible : Visibility.Collapsed;
            BrowseFolderButton.Visibility = isFile ? Visibility.Visible : Visibility.Collapsed;
            if (targetKind == "Uri")
            {
                TargetHelpText.Text = "支持 https://、mailto:、steam: 等已注册的 URI 协议。";
            }
            else if (targetKind == "AppId")
            {
                TargetHelpText.Text = "可从已安装应用列表选择，也可以手动填写 AUMID。";
            }
            else
            {
                TargetHelpText.Text = "支持磁盘、文件夹、文件、程序和快捷方式。";
            }
        }

        private void ShowNameCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (PreviewNamePanel != null)
            {
                PreviewNamePanel.Visibility = ShowNameCheckBox.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdatePreview()
        {
            if (!_ready)
            {
                return;
            }

            bool isWide = GetSelectedTag(TileSizeComboBox) == "Wide";
            double frameWidth = isWide ? 340 : 300;
            double frameHeight = isWide ? 164 : 300;
            PreviewFrame.Width = frameWidth;
            PreviewFrame.Height = frameHeight;
            PreviewCanvas.Width = frameWidth;
            PreviewCanvas.Height = frameHeight;

            if (_sourceBitmap == null || _sourcePixelWidth <= 0 || _sourcePixelHeight <= 0)
            {
                return;
            }

            double scale = Math.Max(frameWidth / _sourcePixelWidth, frameHeight / _sourcePixelHeight) * ZoomSlider.Value;
            double imageWidth = _sourcePixelWidth * scale;
            double imageHeight = _sourcePixelHeight * scale;
            double overflowX = Math.Max(0, imageWidth - frameWidth);
            double overflowY = Math.Max(0, imageHeight - frameHeight);
            double left = -overflowX / 2 - OffsetXSlider.Value * overflowX / 2;
            double top = -overflowY / 2 - OffsetYSlider.Value * overflowY / 2;

            PreviewImage.Width = imageWidth;
            PreviewImage.Height = imageHeight;
            Canvas.SetLeft(PreviewImage, left);
            Canvas.SetTop(PreviewImage, top);
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout(e.NewSize.Width);
        }

        private void UpdateResponsiveLayout(double width)
        {
            bool compact = width < CompactLayoutThreshold;
            if (_isCompactLayout.HasValue && _isCompactLayout.Value == compact)
            {
                return;
            }
            _isCompactLayout = compact;

            if (compact)
            {
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                LeftGapColumn.Width = new GridLength(0);
                DividerColumn.Width = new GridLength(0);
                RightGapColumn.Width = new GridLength(0);
                SettingsColumn.Width = new GridLength(0);
                PreviewContentRow.Height = new GridLength(1, GridUnitType.Star);
                ResponsiveGapRow.Height = new GridLength(25);
                SettingsContentRow.Height = new GridLength(1.15, GridUnitType.Star);
                Grid.SetRow(PreviewPane, 0);
                Grid.SetColumn(PreviewPane, 0);
                Grid.SetRow(ContentDivider, 1);
                Grid.SetColumn(ContentDivider, 0);
                Grid.SetRow(SettingsPane, 2);
                Grid.SetColumn(SettingsPane, 0);
                ContentDivider.Width = double.NaN;
                ContentDivider.Height = 1;
                ContentDivider.HorizontalAlignment = HorizontalAlignment.Stretch;
                ContentDivider.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                PreviewColumn.Width = new GridLength(390);
                LeftGapColumn.Width = new GridLength(30);
                DividerColumn.Width = new GridLength(1);
                RightGapColumn.Width = new GridLength(30);
                SettingsColumn.Width = new GridLength(1, GridUnitType.Star);
                PreviewContentRow.Height = new GridLength(1, GridUnitType.Star);
                ResponsiveGapRow.Height = new GridLength(0);
                SettingsContentRow.Height = new GridLength(0);
                Grid.SetRow(PreviewPane, 0);
                Grid.SetColumn(PreviewPane, 0);
                Grid.SetRow(ContentDivider, 0);
                Grid.SetColumn(ContentDivider, 2);
                Grid.SetRow(SettingsPane, 0);
                Grid.SetColumn(SettingsPane, 4);
                ContentDivider.Width = 1;
                ContentDivider.Height = double.NaN;
                ContentDivider.HorizontalAlignment = HorizontalAlignment.Center;
                ContentDivider.VerticalAlignment = VerticalAlignment.Stretch;
            }
        }

        private void SetStatus(string message, bool error)
        {
            if (StatusText == null)
            {
                return;
            }
            StatusText.Text = message;
            string resourceName = error
                ? "SystemControlErrorTextForegroundBrush"
                : "SystemControlForegroundBaseMediumBrush";
            StatusText.Foreground = (Brush)Application.Current.Resources[resourceName];
        }

        private static string GetSelectedTag(ComboBox comboBox)
        {
            ComboBoxItem item = comboBox.SelectedItem as ComboBoxItem;
            return item == null || item.Tag == null ? string.Empty : item.Tag.ToString();
        }

        private static void SelectComboBoxTag(ComboBox comboBox, string tag)
        {
            foreach (object value in comboBox.Items)
            {
                ComboBoxItem item = value as ComboBoxItem;
                if (item != null && item.Tag != null && item.Tag.ToString() == tag)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private static string NormalizeTarget(string target, string targetKind)
        {
            if (targetKind == "File" && target.Length == 2 && char.IsLetter(target[0]) && target[1] == ':')
            {
                return target + "\\";
            }
            return target;
        }
    }
}
