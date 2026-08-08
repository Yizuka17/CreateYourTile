using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using CreateYourTile.Uwp.Services;

namespace CreateYourTile.Uwp
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Arguments) &&
                e.Arguments.StartsWith("tile:", StringComparison.OrdinalIgnoreCase))
            {
                string tileId = e.Arguments.Substring("tile:".Length);
                if (await TileLaunchService.TryLaunchAsync(tileId))
                {
                    return;
                }
            }

            Frame rootFrame = EnsureRootFrame();
            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }

            Window.Current.Activate();
        }

        private static Frame EnsureRootFrame()
        {
            Frame frame = Window.Current.Content as Frame;
            if (frame != null)
            {
                return frame;
            }

            frame = new Frame();
            frame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = frame;
            return frame;
        }

        private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("无法打开页面：" + e.SourcePageType.FullName);
        }

        private static void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
