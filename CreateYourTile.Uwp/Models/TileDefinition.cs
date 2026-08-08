namespace CreateYourTile.Uwp.Models
{
    internal sealed class TileDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TargetKind { get; set; }
        public string Target { get; set; }
        public string Arguments { get; set; }
        public string PreferredSize { get; set; }
        public bool ShowName { get; set; }
    }
}
