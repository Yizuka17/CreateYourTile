using Windows.Storage;
using CreateYourTile.Uwp.Models;

namespace CreateYourTile.Uwp.Services
{
    internal static class TileStorage
    {
        private const string ContainerName = "Tiles";

        public static void Save(TileDefinition definition)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings.CreateContainer(
                ContainerName,
                ApplicationDataCreateDisposition.Always);
            ApplicationDataCompositeValue value = new ApplicationDataCompositeValue();
            value["Name"] = definition.Name;
            value["TargetKind"] = definition.TargetKind;
            value["Target"] = definition.Target;
            value["Arguments"] = definition.Arguments ?? string.Empty;
            value["PreferredSize"] = definition.PreferredSize;
            value["ShowName"] = definition.ShowName;
            container.Values[definition.Id] = value;
        }

        public static TileDefinition Load(string tileId)
        {
            ApplicationDataContainer container;
            if (!ApplicationData.Current.LocalSettings.Containers.TryGetValue(ContainerName, out container))
            {
                return null;
            }

            ApplicationDataCompositeValue value = container.Values[tileId] as ApplicationDataCompositeValue;
            if (value == null)
            {
                return null;
            }

            return new TileDefinition
            {
                Id = tileId,
                Name = value["Name"] as string ?? string.Empty,
                TargetKind = value["TargetKind"] as string ?? "File",
                Target = value["Target"] as string ?? string.Empty,
                Arguments = value["Arguments"] as string ?? string.Empty,
                PreferredSize = value["PreferredSize"] as string ?? "Medium",
                ShowName = value["ShowName"] is bool && (bool)value["ShowName"]
            };
        }
    }
}
