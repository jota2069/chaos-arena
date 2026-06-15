using Godot;
using ChaosArena.autoload;
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

        // Временные dev-клавиши, пока нет лобби: F1 — поднять хост, F2 — подключиться
        // к localhost. Будут заменены на ShopUI/Лобби.
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            var net = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (net == null || net.IsNetworked) return;

            if (key.Keycode == Key.F1)
                net.HostGame();
            else if (key.Keycode == Key.F2)
                net.JoinGame();
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
