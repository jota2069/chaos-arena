using Godot;
using ChaosArena.scenes;

namespace ChaosArena
{
    /// <summary>
    /// Корневой узел игровой сцены. Связывает карту и игрока: после генерации
    /// подземелья телепортирует игрока в центр первой комнаты.
    /// </summary>
    public partial class Main : Node2D
    {
        private MapGenerator _map;
        private CharacterBody2D _player;

        public override void _Ready()
        {
            _map = GetNode<MapGenerator>("Map");
            _player = GetNode<CharacterBody2D>("LocalPlayer");

            CallDeferred(nameof(SpawnPlayerOnMap));
        }

        private void SpawnPlayerOnMap()
        {
            if (_map == null || _player == null) return;

            Vector2I spawnCell = _map.PlayerSpawnCell;
            Vector2 localPos = _map.MapToLocal(spawnCell);
            _player.GlobalPosition = _map.ToGlobal(localPos);
        }
    }
}
