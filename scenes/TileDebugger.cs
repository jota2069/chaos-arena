using Godot;

/// <summary>
/// Временный скрипт — печатает все тайлы из тайлсета.
/// Запусти один раз, скопируй координаты, потом удали.
/// </summary>
public partial class TileDebugger : Node
{
    [Export] public TileMapLayer TileMap;

    public override void _Ready()
    {
        if (TileMap == null)
        {
            GD.PrintErr("TileDebugger: не назначен TileMap!");
            return;
        }

        var tileSet = TileMap.TileSet;
        if (tileSet == null)
        {
            GD.PrintErr("TileDebugger: нет TileSet!");
            return;
        }

        GD.Print("=== ТАЙЛЫ ===");
        for (int sourceId = 0; sourceId < tileSet.GetSourceCount(); sourceId++)
        {
            int realId = tileSet.GetSourceId(sourceId);
            var source = tileSet.GetSource(realId) as TileSetAtlasSource;
            if (source == null) continue;

            GD.Print($"Источник {realId}: {source.ResourceName}");

            Vector2I size = source.GetAtlasGridSize();
            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    Vector2I coords = new Vector2I(x, y);
                    if (source.HasTile(coords))
                        GD.Print($"  тайл ({x},{y})");
                }
            }
        }
        GD.Print("=== КОНЕЦ ===");
    }
}