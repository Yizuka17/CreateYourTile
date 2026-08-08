using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CreateYourTile.Models;
using CreateYourTile.Services;

namespace CreateYourTile;

public partial class InstalledAppPickerWindow : Window
{
    private IReadOnlyList<InstalledAppInfo> _allApps = [];

    public InstalledAppPickerWindow()
    {
        InitializeComponent();
        Loaded += InstalledAppPickerWindow_Loaded;
    }

    public InstalledAppInfo? SelectedApp { get; private set; }

    private async void InstalledAppPickerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadStatusText.Text = "正在读取已安装应用…";
            SearchTextBox.IsEnabled = false;
            _allApps = await Task.Run(InstalledAppCatalog.GetApps);
            ApplyFilter();
            LoadStatusText.Text = string.Empty;
            SearchTextBox.IsEnabled = true;
            SearchTextBox.Focus();
        }
        catch (Exception exception)
        {
            LoadStatusText.Text = $"读取失败：{exception.Message}";
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        var query = SearchTextBox.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allApps
            : _allApps.Where(app =>
                    app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    app.Target.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        AppListBox.ItemsSource = filtered;
        CountText.Text = $"显示 {filtered.Count} 个应用";
    }

    private void AppListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectButton.IsEnabled = AppListBox.SelectedItem is InstalledAppInfo;
    }

    private void AppListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        CompleteSelection();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        CompleteSelection();
    }

    private void CompleteSelection()
    {
        if (AppListBox.SelectedItem is not InstalledAppInfo app)
        {
            return;
        }

        SelectedApp = app;
        DialogResult = true;
    }
}
