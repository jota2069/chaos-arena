using Godot;
using ChaosArena.scenes;

namespace ChaosArena
{
    // Исправлен namespace на корневой ChaosArena, чтобы Rider не ругался на структуру папок
    public partial class Main : Node2D
    {
        private MapGenerator _map;
        private CharacterBody2D _player;

        public override void _Ready()
        {
            _map = GetNode<MapGenerator>("Map");
            
            // ИСПРАВЛЕНИЕ: Убраны двойные скобки <<
            _player = GetNode<CharacterBody2D>("LocalPlayer");
            
            CallDeferred(nameof(SpawnPlayerOnMap));
        }

        private void SpawnPlayerOnMap()
        {
            if (_map == null || _player == null) return;

            Vector2I spawnCell = _map.PlayerSpawnCell;
            Vector2 localPos = _map.MapToLocal(spawnCell);
            _player.GlobalPosition = _map.ToGlobal(localPos);
            
            GD.Print($"[Main] Player teleported to room center: {_player.GlobalPosition}");
        }
    }
}