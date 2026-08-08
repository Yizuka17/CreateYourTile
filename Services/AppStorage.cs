using System.Runtime.InteropServices;
using System.Text.Json;
using CreateYourTile.Models;
using Windows.Storage;

namespace CreateYourTile;

internal static class AppStorage
{
    private const int AppModelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;

    public static bool HasPackageIdentity { get; } = DetectPackageIdentity();

    public static string RootPath { get; } = GetRootPath();

    public static string TilesPath => Path.Combine(RootPath, "Tiles");

    public static string GetTileDirectory(string tileId) => Path.Combine(TilesPath, tileId);

    public static string GetDefinitionPath(string tileId) => Path.Combine(GetTileDirectory(tileId), "tile.json");

    public static void SaveDefinition(TileDefinition definition)
    {
        var directory = GetTileDirectory(definition.Id);
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(definition, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetDefinitionPath(definition.Id), json);
    }

    public static TileDefinition? LoadDefinition(string tileId)
    {
        var path = GetDefinitionPath(tileId);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TileDefinition>(File.ReadAllText(path));
    }

    private static string GetRootPath()
    {
        string root;
        if (HasPackageIdentity)
        {
            root = ApplicationData.Current.LocalFolder.Path;
        }
        else
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CreateYourTile");
        }

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Tiles"));
        return root;
    }

    private static bool DetectPackageIdentity()
    {
        var length = 0;
        var result = GetCurrentPackageFullName(ref length, null);
        return result == ErrorInsufficientBuffer || result == 0;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
