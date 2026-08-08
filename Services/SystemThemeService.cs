using ModernWpf;

namespace CreateYourTile.Services;

internal static class SystemThemeService
{
    public static void Initialize()
    {
        // Null means "use Windows settings" in ModernWpf. The library also listens for
        // live changes to the default app mode and system accent color.
        ThemeManager.Current.ApplicationTheme = null;
        ThemeManager.Current.AccentColor = null;
    }
}
