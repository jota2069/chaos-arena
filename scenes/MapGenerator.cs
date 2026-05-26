using Godot;
using System.Collections.Generic;
using ChaosArena.systems;

namespace ChaosArena.scenes
{
    public partial class MapGenerator : TileMapLayer
    {
        [Export] public int MinRooms = 4;
        [Export] public int MaxRooms = 6;
        [Export] public int MinRoomSize = 20;
        [Export] public int MaxRoomSize = 30;

        private readonly Vector2I _floorTile = new Vector2I(1, 0);
        private readonly Vector2I[] _wallTiles = new Vector2I[]
        {
            new Vector2I(4, 3),
            new Vector2I(9, 4),
            new Vector2I(10, 4),
            new Vector2I(11, 4)
        };

        private enum CellType { Empty, Floor, Wall }

        private CellType[,] _grid;
        private const int GridSize = 120;
        private readonly List<Room> _rooms = new List<Room>();

        public Vector2I PlayerSpawnCell { get; private set; }

        public override void _Ready()
        {
            GD.Randomize();
            GenerateDungeon();
        }
        
        private void NotifySpawner()
        {
            var spawner = GetNodeOrNull<EnemySpawner>("/root/Main/EnemySpawner");
            if (spawner == null) return;

            List<Vector2> roomCenters = new();
    
            for (int i = 1; i < _rooms.Count; i++)
            {
                Vector2 worldPos = ToGlobal(MapToLocal(_rooms[i].Center));
                roomCenters.Add(worldPos);
            }

            spawner.SetSpawnPoints(roomCenters);
        }

        public void GenerateDungeon()
        {
            Clear();
            _rooms.Clear();
            _grid = new CellType[GridSize, GridSize];

            int targetRooms = GD.RandRange(MinRooms, MaxRooms);
            int attempts = 0;
            int maxAttempts = 500;

            while (_rooms.Count < targetRooms && attempts < maxAttempts)
            {
                attempts++;
                if (TryCreateRoom(out Room room))
                {
                    _rooms.Add(room);
                }
            }

            if (_rooms.Count < 2)
            {
                GD.PrintErr("MapGenerator: failed to create enough rooms!");
                return;
            }

            for (int i = 0; i < _rooms.Count - 1; i++)
                ConnectRooms(_rooms[i], _rooms[i + 1]);

            BuildWalls();
            RenderGrid();

            Position = new Vector2(-(GridSize / 2f) * 16f, -(GridSize / 2f) * 16f);
            PlayerSpawnCell = _rooms[0].Center;

            GD.Print($"Dungeon: {_rooms.Count} rooms, spawn: {PlayerSpawnCell}");
            NotifySpawner();
        }

        private bool TryCreateRoom(out Room room)
        {
            int w = GD.RandRange(MinRoomSize, MaxRoomSize);
            int h = GD.RandRange(MinRoomSize, MaxRoomSize);

            int margin = 12;
            int x = GD.RandRange(margin, GridSize - w - margin);
            int y = GD.RandRange(margin, GridSize - h - margin);

            room = new Room(x, y, w, h);

            foreach (var existing in _rooms)
            {
                if (room.Intersects(existing, padding: 5))
                    return false;
            }

            for (int rx = x; rx < x + w; rx++)
                for (int ry = y; ry < y + h; ry++)
                    _grid[rx, ry] = CellType.Floor;

            return true;
        }

        private void ConnectRooms(Room a, Room b)
        {
            Vector2I start = a.Center;
            Vector2I end = b.Center;

            if (GD.Randf() > 0.5f)
            {
                CarveHorizontal(start.X, end.X, start.Y);
                CarveVertical(start.Y, end.Y, end.X);
            }
            else
            {
                CarveVertical(start.Y, end.Y, start.X);
                CarveHorizontal(start.X, end.X, end.Y);
            }
        }

        // Широкий коридор: 3 тайла (center ± 1)
        private void CarveHorizontal(int fromX, int toX, int y)
        {
            int min = Mathf.Min(fromX, toX);
            int max = Mathf.Max(fromX, toX);
            for (int x = min; x <= max; x++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny >= 0 && ny < GridSize && x >= 0 && x < GridSize)
                        _grid[x, ny] = CellType.Floor;
                }
            }
        }

        private void CarveVertical(int fromY, int toY, int x)
        {
            int min = Mathf.Min(fromY, toY);
            int max = Mathf.Max(fromY, toY);
            for (int y = min; y <= max; y++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    if (nx >= 0 && nx < GridSize && y >= 0 && y < GridSize)
                        _grid[nx, y] = CellType.Floor;
                }
            }
        }

        private void BuildWalls()
        {
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    if (_grid[x, y] == CellType.Empty && HasFloorNeighbor(x, y))
                        _grid[x, y] = CellType.Wall;
                }
            }
        }

        private bool HasFloorNeighbor(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < GridSize && ny >= 0 && ny < GridSize)
                        if (_grid[nx, ny] == CellType.Floor)
                            return true;
                }
            }
            return false;
        }

        private void RenderGrid()
        {
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    Vector2I cell = new Vector2I(x, y);
                    if (_grid[x, y] == CellType.Floor)
                        SetCell(cell, 0, _floorTile);
                    else if (_grid[x, y] == CellType.Wall)
                        SetCell(cell, 0, _wallTiles[GD.RandRange(0, _wallTiles.Length - 1)]);
                }
            }
        }

        private readonly struct Room
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;

            public Room(int x, int y, int w, int h)
            {
                X = x;
                Y = y;
                Width = w;
                Height = h;
            }

            public Vector2I Center => new Vector2I(X + Width / 2, Y + Height / 2);

            public bool Intersects(Room other, int padding)
            {
                return X - padding < other.X + other.Width + padding &&
                       X + Width + padding > other.X - padding &&
                       Y - padding < other.Y + other.Height + padding &&
                       Y + Height + padding > other.Y - padding;
            }
        }
    }
}