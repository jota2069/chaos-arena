using Godot;
using System.Collections.Generic;
using ChaosArena.entities.enemies;

namespace ChaosArena.systems
{
    public partial class EnemySpawner : Node2D
    {
        [Export] public PackedScene EnemyScene;
        [Export] public int MinPerWave = 3;
        [Export] public int MaxPerWave = 7;
        [Export] public float WaveInterval = 12f;
        [Export] public int MaxEnemies = 15;

        private List<Vector2> _spawnPositions = new();
        private int _activeEnemies = 0;
        private float _timer = 0f;
        private bool _spawning = false;

        public void SetSpawnPoints(List<Vector2> positions)
        {
            _spawnPositions = positions;
            _timer = WaveInterval;
            _spawning = _spawnPositions.Count > 0;
            
            GD.Print($"[EnemySpawner] Точки получены ({_spawnPositions.Count}). Активация.");
        }

        public override void _Process(double delta)
        {
            if (!_spawning || _spawnPositions.Count == 0) return;

            _timer += (float)delta;
            if (_timer >= WaveInterval)
            {
                _timer = 0f;
                SpawnWave();
            }
        }

        private void SpawnWave()
        {
            if (_activeEnemies >= MaxEnemies || EnemyScene == null) return;

            int count = GD.RandRange(MinPerWave, MaxPerWave);
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                if (_activeEnemies >= MaxEnemies) break;

                Vector2 basePos = _spawnPositions[GD.RandRange(0, _spawnPositions.Count - 1)];
                
                var enemy = EnemyScene.Instantiate<EnemyBase>();
                
                Vector2 offset = new Vector2(GD.RandRange(-24, 24), GD.RandRange(-24, 24));
                enemy.GlobalPosition = basePos + offset;

                enemy.TreeExited += () => _activeEnemies = Mathf.Max(0, _activeEnemies - 1);
                
                GetTree().CurrentScene.AddChild(enemy);
                
                _activeEnemies++;
                spawned++;
            }

            GD.Print($"[EnemySpawner] Волна: +{spawned} мобов. Всего на карте: {_activeEnemies}");
        }

        public void Stop()
        {
            _spawning = false;
        }
    }
}