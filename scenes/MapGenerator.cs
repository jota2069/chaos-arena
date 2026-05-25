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
        // Сдвигаем карту так чтобы центр арены был в (0,0)
        Position = new Vector2(-(Width / 2f) * 16f, -(Height / 2f) * 16f);
        GD.Print($"Map позиция: {Position}, тайлов: {GetUsedCells().Count}");
    }
}