using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using CreateYourTile.Models;
using CreateYourTile.Services;
using Microsoft.Win32;

namespace CreateYourTile;

public partial class MainWindow : Window
{
    private const double CompactLayoutThreshold = 980;
    private BitmapSource? _sourceImage;
    private string? _sourceImagePath;
    private bool _isCompactLayout;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.Text = "我的磁贴";
        UpdateResponsiveLayout(ActualWidth);
        UpdatePreview();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateResponsiveLayout(e.NewSize.Width);
        }
    }

    private void UpdateResponsiveLayout(double width)
    {
        var useCompactLayout = width < CompactLayoutThreshold;
        if (_isCompactLayout == useCompactLayout)
        {
            return;
        }

        _isCompactLayout = useCompactLayout;
        if (useCompactLayout)
        {
            PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
            LeftGapColumn.Width = new GridLength(0);
            DividerColumn.Width = new GridLength(0);
            RightGapColumn.Width = new GridLength(0);
            SettingsColumn.Width = new GridLength(0);

            PreviewContentRow.Height = new GridLength(1, GridUnitType.Star);
            ResponsiveGapRow.Height = new GridLength(25);
            SettingsContentRow.Height = new GridLength(1.15, GridUnitType.Star);

            Grid.SetRow(PreviewPanel, 0);
            Grid.SetColumn(PreviewPanel, 0);
            Grid.SetRow(ContentDivider, 1);
            Grid.SetColumn(ContentDivider, 0);
            Grid.SetRow(SettingsPanel, 2);
            Grid.SetColumn(SettingsPanel, 0);
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

            Grid.SetRow(PreviewPanel, 0);
            Grid.SetColumn(PreviewPanel, 0);
            Grid.SetRow(ContentDivider, 0);
            Grid.SetColumn(ContentDivider, 2);
            Grid.SetRow(SettingsPanel, 0);
            Grid.SetColumn(SettingsPanel, 4);
            ContentDivider.Width = 1;
            ContentDivider.Height = double.NaN;
            ContentDivider.HorizontalAlignment = HorizontalAlignment.Center;
            ContentDivider.VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    private void ChooseImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择磁贴图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有文件|*.*"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _sourceImage = ImageCropper.Load(dialog.FileName);
            _sourceImagePath = dialog.FileName;
            ImagePathText.Text = Path.GetFileName(dialog.FileName);
            ImagePathText.ToolTip = dialog.FileName;
            EmptyImageHint.Visibility = Visibility.Collapsed;
            ZoomSlider.Value = 1;
            OffsetXSlider.Value = 0;
            OffsetYSlider.Value = 0;
            UpdatePreview();
            SetStatus("图片已载入。可用缩放、左右和上下滑块调整裁剪。", false);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"无法读取图片：{exception.Message}", "图片错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BrowseTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedTag(TargetKindComboBox) != "File")
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "选择要打开的程序或快捷方式",
            Filter = "程序和快捷方式|*.exe;*.lnk;*.bat;*.cmd;*.com;*.url|所有文件|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            TargetTextBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NameTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }

            var icon = await Task.Run(() => ShellIconService.TryGetIcon(dialog.FileName, "File"));
            ApplyInitialTileImage(icon, dialog.FileName, $"{Path.GetFileNameWithoutExtension(dialog.FileName)} 的图标");
        }
    }

    private async void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedTag(TargetKindComboBox) != "File")
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "选择要打开的文件夹",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            TargetTextBox.Text = dialog.FolderName;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                NameTextBox.Text = GetFolderDisplayName(dialog.FolderName);
            }

            var icon = await Task.Run(() => ShellIconService.TryGetIcon(dialog.FolderName, "File"));
            ApplyInitialTileImage(icon, dialog.FolderName, $"{GetFolderDisplayName(dialog.FolderName)} 的图标");
        }
    }

    private void ChooseInstalledAppButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new InstalledAppPickerWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedApp is null)
        {
            return;
        }

        var app = picker.SelectedApp;
        SelectComboBoxTag(TargetKindComboBox, app.TargetKind);
        TargetTextBox.Text = app.Target;
        ArgumentsTextBox.Text = string.Empty;
        NameTextBox.Text = app.Name;
        ApplyInitialTileImage(app.Icon as BitmapSource, app.Target, $"{app.Name} 的应用图标");
        SetStatus(
            app.Icon is null
                ? $"已选择应用：{app.Name}；未能取得图标，请手动选择图片。"
                : $"已选择应用：{app.Name}；其图标已作为初始磁贴图片。",
            false);
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryValidate(out var error))
        {
            SetStatus(error, true);
            return;
        }

        RegisterButton.IsEnabled = false;
        RegisterButton.Content = "正在准备…";
        try
        {
            var targetKind = GetSelectedTag(TargetKindComboBox);
            var preferredSize = GetSelectedTag(TileSizeComboBox);
            var name = NameTextBox.Text.Trim();
            var target = NormalizeTarget(TargetTextBox.Text.Trim(), targetKind);
            TargetTextBox.Text = target;
            var id = TileRegistrar.CreateStableId(name, target);
            var tileDirectory = AppStorage.GetTileDirectory(id);
            Directory.CreateDirectory(tileDirectory);

            var square = ImageCropper.Render(_sourceImage!, 512, 512, ZoomSlider.Value, OffsetXSlider.Value, OffsetYSlider.Value);
            var wide = ImageCropper.Render(_sourceImage!, 1024, 512, ZoomSlider.Value, OffsetXSlider.Value, OffsetYSlider.Value);
            var small = ImageCropper.Render(_sourceImage!, 256, 256, ZoomSlider.Value, OffsetXSlider.Value, OffsetYSlider.Value);
            ImageCropper.SavePng(square, Path.Combine(tileDirectory, "Square.png"));
            ImageCropper.SavePng(wide, Path.Combine(tileDirectory, "Wide.png"));
            ImageCropper.SavePng(small, Path.Combine(tileDirectory, "Small.png"));
            ImageCropper.SavePngAsIcon(small, Path.Combine(tileDirectory, "Icon.ico"));

            var definition = new TileDefinition
            {
                Id = id,
                Name = name,
                TargetKind = targetKind,
                Target = target,
                Arguments = ArgumentsTextBox.Text,
                PreferredSize = preferredSize,
                ShowName = ShowNameCheckBox.IsChecked == true
            };

            RegisterButton.Content = "等待系统确认…";
            var owner = new WindowInteropHelper(this).Handle;
            var result = await new TileRegistrar().RegisterAsync(definition, owner);
            SetStatus(result.Message, !result.Success);
        }
        catch (Exception exception)
        {
            SetStatus($"注册失败：{exception.Message}", true);
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            RegisterButton.Content = "注册并固定到“开始”";
        }
    }

    private bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            error = "请填写磁贴名称。";
            return false;
        }

        if (_sourceImage is null || string.IsNullOrWhiteSpace(_sourceImagePath))
        {
            error = "请先选择一张磁贴图片。";
            return false;
        }

        var targetKind = GetSelectedTag(TargetKindComboBox);
        var target = NormalizeTarget(TargetTextBox.Text.Trim(), targetKind);
        if (string.IsNullOrWhiteSpace(target))
        {
            error = "请填写要打开的目标。";
            return false;
        }

        switch (targetKind)
        {
            case "File" when !File.Exists(target) && !Directory.Exists(target):
                error = "所选磁盘、文件夹、程序或快捷方式不存在。";
                return false;
            case "Uri" when !Uri.TryCreate(target, UriKind.Absolute, out _):
                error = "请输入完整网址或 URI，例如 https://example.com。";
                return false;
        }

        error = string.Empty;
        return true;
    }

    private void CropControl_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || ZoomValue is null)
        {
            return;
        }

        ZoomValue.Text = $"{ZoomSlider.Value:0.0}×";
        UpdatePreview();
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PreviewName is not null)
        {
            PreviewName.Text = string.IsNullOrWhiteSpace(NameTextBox.Text) ? "磁贴名称" : NameTextBox.Text.Trim();
        }
    }

    private void TileSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdatePreview();
    }

    private void TargetKindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        switch (GetSelectedTag(TargetKindComboBox))
        {
            case "Uri":
                BrowseTargetButton.Visibility = Visibility.Collapsed;
                BrowseFolderButton.Visibility = Visibility.Collapsed;
                TargetHelpText.Text = "支持 https://、mailto:、steam: 等已注册的 URI 协议。";
                break;
            case "AppId":
                BrowseTargetButton.Visibility = Visibility.Collapsed;
                BrowseFolderButton.Visibility = Visibility.Collapsed;
                TargetHelpText.Text = "填写应用的 AUMID，例如 Microsoft.WindowsCalculator_8wekyb3d8bbwe!App。";
                break;
            default:
                BrowseTargetButton.Visibility = Visibility.Visible;
                BrowseFolderButton.Visibility = Visibility.Visible;
                TargetHelpText.Text = "支持 C:、C:\\、普通文件夹、文件、.exe、.lnk、.bat、.cmd 等本地目标。";
                break;
        }
    }

    private void ShowNameCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (PreviewNamePanel is not null)
        {
            PreviewNamePanel.Visibility = ShowNameCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdatePreview()
    {
        if (!IsLoaded)
        {
            return;
        }

        var isWide = GetSelectedTag(TileSizeComboBox) == "Wide";
        PreviewFrame.Width = isWide ? 340 : 300;
        PreviewFrame.Height = isWide ? 164 : 300;
        if (_sourceImage is null)
        {
            PreviewImage.Source = null;
            return;
        }

        var width = isWide ? 720 : 512;
        var height = isWide ? 348 : 512;
        PreviewImage.Source = ImageCropper.Render(
            _sourceImage,
            width,
            height,
            ZoomSlider.Value,
            OffsetXSlider.Value,
            OffsetYSlider.Value);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            isError ? "SystemControlErrorTextForegroundBrush" : "SystemControlPageTextBaseMediumBrush");
    }

    private static string GetSelectedTag(ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;

    private static string NormalizeTarget(string target, string targetKind)
    {
        if (targetKind == "File" && target.Length == 2 && char.IsLetter(target[0]) && target[1] == ':')
        {
            return target + "\\";
        }

        return target;
    }

    private static string GetFolderDisplayName(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.Equals(root, path, StringComparison.OrdinalIgnoreCase))
        {
            return path.TrimEnd('\\');
        }

        return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static void SelectComboBoxTag(ComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void ApplyInitialTileImage(BitmapSource? icon, string target, string label)
    {
        if (icon is null)
        {
            _sourceImage = null;
            _sourceImagePath = null;
            PreviewImage.Source = null;
            EmptyImageHint.Visibility = Visibility.Visible;
            ImagePathText.Text = "未能取得图标，请选择图片";
            return;
        }

        _sourceImage = icon;
        _sourceImagePath = $"shell-icon:{target}";
        ImagePathText.Text = label;
        ImagePathText.ToolTip = target;
        EmptyImageHint.Visibility = Visibility.Collapsed;
        ZoomSlider.Value = 1;
        OffsetXSlider.Value = 0;
        OffsetYSlider.Value = 0;
        UpdatePreview();
    }
}
