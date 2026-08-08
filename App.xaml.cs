using System.Windows;
using CreateYourTile.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace CreateYourTile;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        var activationArguments = GetActivationArguments(e.Args);

        var generateArgument = activationArguments.FirstOrDefault(arg =>
            arg.StartsWith("--generate-package-assets=", StringComparison.OrdinalIgnoreCase));
        if (generateArgument is not null)
        {
            var outputDirectory = generateArgument[(generateArgument.IndexOf('=') + 1)..].Trim('"');
            PackageAssetGenerator.Generate(outputDirectory);
            Shutdown();
            return;
        }

        var launchArgument = activationArguments.FirstOrDefault(arg =>
            arg.StartsWith("--launch-tile=", StringComparison.OrdinalIgnoreCase));
        if (launchArgument is not null)
        {
            var tileId = launchArgument[(launchArgument.IndexOf('=') + 1)..];
            if (TileLaunchService.TryLaunch(tileId, out var error))
            {
                Shutdown();
                return;
            }

            MessageBox.Show(error, "磁贴启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        SystemThemeService.Initialize();
        var window = new MainWindow();
        window.Icon = ProductBranding.Icon;
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    private static IReadOnlyList<string> GetActivationArguments(IEnumerable<string> commandLineArguments)
    {
        var arguments = commandLineArguments.ToList();
        if (!AppStorage.HasPackageIdentity)
        {
            return arguments;
        }

        try
        {
            if (AppInstance.GetActivatedEventArgs() is LaunchActivatedEventArgs launch &&
                !string.IsNullOrWhiteSpace(launch.Arguments))
            {
                arguments.Add(launch.Arguments.Trim());
            }
        }
        catch
        {
            // Command-line activation still works as a fallback on packaged desktop builds.
        }

        return arguments;
    }
}
