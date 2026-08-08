namespace CreateYourTile.Models;

public sealed class TileDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string TargetKind { get; init; }
    public required string Target { get; init; }
    public string Arguments { get; init; } = string.Empty;
    public required string PreferredSize { get; init; }
    public bool ShowName { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
