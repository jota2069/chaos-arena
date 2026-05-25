using Godot;

public partial class MapGenerator : TileMapLayer
{
    [Export] public int Width = 30;
    [Export] public int Height = 20;

    private readonly Vector2I FloorTile = new Vector2I(1, 0);
    private readonly Vector2I WallTile  = new Vector2I(0, 0);

    public override void _Ready()
    {
        GenerateArena();
        AddWallCollision();
    }

    private void GenerateArena()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Vector2I cell = new Vector2I(x, y);
                bool isWall = x == 0 || x == Width - 1 || y == 0 || y == Height - 1;
                SetCell(cell, 0, isWall ? WallTile : FloorTile);
            }
        }
        Position = new Vector2(-(Width / 2f) * 16f, -(Height / 2f) * 16f);
        GD.Print($"Map позиция: {Position}, тайлов: {GetUsedCells().Count}");
    }

    private void AddWallCollision()
    {
        // Назначаем коллизию тайлу стены через TileData
        var tileSet = TileSet;
        if (tileSet == null) return;

        var source = tileSet.GetSource(0) as TileSetAtlasSource;
        if (source == null) return;

        // Проверяем есть ли физический слой
        if (tileSet.GetPhysicsLayersCount() == 0)
            tileSet.AddPhysicsLayer();

        // Добавляем прямоугольную коллизию к тайлу стены
        var tileData = source.GetTileData(WallTile, 0);
        if (tileData != null && tileData.GetCollisionPolygonsCount(0) == 0)
        {
            var polygon = new Vector2[]
            {
                new Vector2(-8, -8),
                new Vector2(8, -8),
                new Vector2(8, 8),
                new Vector2(-8, 8)
            };
            tileData.AddCollisionPolygon(0);
            tileData.SetCollisionPolygonPoints(0, 0, polygon);
            GD.Print("MapGenerator: коллизия стен добавлена");
        }
    }
}