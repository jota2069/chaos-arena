using Godot;
using System;
using System.Collections.Generic;
using ChaosArena.autoload;
using ChaosArena.entities.enemies;

namespace ChaosArena.systems
{
    /// <summary>
    /// Спавнер врагов. Точки спавна задаёт MapGenerator, но сам спавн
    /// идёт только во время фазы PvE — спавнер слушает PhaseChanged.
    /// При выходе из PvE спавн останавливается и арена очищается.
    /// </summary>
    public partial class EnemySpawner : Node2D
    {
        // Легаси-поле (необязательное): враги теперь создаются из кода фабриками ниже.
        [Export] public PackedScene EnemyScene;

        // Все 5 типов мобов. Волна берёт случайный тип из списка.
        private static readonly Func<EnemyBase>[] EnemyFactories =
        {
            () => new SkeletonWarrior(),
            () => new ZombieBrute(),
            () => new Bat(),
            () => new GhostMage(),
            () => new GiantSpider(),
        };

        // Id игрока-владельца этой арены — передаётся каждому врагу.
        [Export] public int OwnerPlayerId { get; set; } = 0;

        // ПРАВКА: Временный баланс для тестов, чтобы не умирать мгновенно
        [Export] public int MinPerWave = 2;
        [Export] public int MaxPerWave = 4;
        [Export] public float WaveInterval = 12f;
        [Export] public int MaxEnemies = 8;

        private List<Vector2> _spawnPositions = new();
        private readonly List<EnemyBase> _activeEnemies = new();
        private float _timer = 0f;
        private bool _spawning = false;

        private EventBus _eventBus;

        public override void _Ready()
        {
            _eventBus = GetNodeOrNull<EventBus>("/root/EventBus");
            if (_eventBus == null)
            {
                GD.PrintErr("[EnemySpawner] EventBus не найден!");
                return;
            }

            // C#-стиль подписки (соглашение CLAUDE.md), отписка в _ExitTree.
            _eventBus.PhaseChanged += OnPhaseChanged;

            // Сцена арены грузится уже ПОСЛЕ PhaseChanged(PvE) (смена сцен по фазе
            // через SceneLoader), поэтому этот сигнал спавнер пропускает. Если на
            // момент готовности фаза уже PvE — стартуем спавн сами.
            var gm = GetNodeOrNull<GameManager>("/root/GameManager");
            if (gm != null && gm.CurrentPhase == GameManager.GamePhase.PvE)
                StartSpawning();
        }

        public override void _ExitTree()
        {
            if (_eventBus == null || !GodotObject.IsInstanceValid(_eventBus)) return;

            _eventBus.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>
        /// Сохраняет точки спавна. Сам спавн НЕ запускает — ждём фазу PvE.
        /// </summary>
        public void SetSpawnPoints(List<Vector2> positions)
        {
            _spawnPositions = positions;

            // Если уже идёт PvE (точки пришли позже смены фазы) — стартуем сразу.
            if (_spawning)
                StartSpawning();
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

        // Реакция на смену фазы: спавним только в PvE, иначе стоп + очистка.
        private void OnPhaseChanged(int newPhase)
        {
            var phase = (GameManager.GamePhase)newPhase;

            if (phase == GameManager.GamePhase.PvE)
            {
                StartSpawning();
            }
            else
            {
                Stop();
                ClearEnemies();
            }
        }

        private void StartSpawning()
        {
            _timer = 0f;
            _spawning = true;

            if (_spawnPositions.Count == 0)
                return;

            SpawnWave();
        }

        private void SpawnWave()
        {
            if (_activeEnemies.Count >= MaxEnemies) return;

            int count = GD.RandRange(MinPerWave, MaxPerWave);

            for (int i = 0; i < count; i++)
            {
                if (_activeEnemies.Count >= MaxEnemies) break;

                Vector2 basePos = _spawnPositions[GD.RandRange(0, _spawnPositions.Count - 1)];

                var enemy = EnemyFactories[GD.RandRange(0, EnemyFactories.Length - 1)]();

                // Владелец арены — чтобы награда ушла нужному игроку.
                enemy.OwnerPlayerId = OwnerPlayerId;

                Vector2 offset = new Vector2(GD.RandRange(-24, 24), GD.RandRange(-24, 24));
                enemy.GlobalPosition = basePos + offset;

                _activeEnemies.Add(enemy);
                enemy.TreeExited += () => _activeEnemies.Remove(enemy);

                GetTree().CurrentScene.AddChild(enemy);
            }
        }

        /// <summary>
        /// Останавливает спавн новых волн (живые враги остаются).
        /// </summary>
        public void Stop()
        {
            _spawning = false;
        }

        /// <summary>
        /// Удаляет всех живых врагов этой арены (при переходе из PvE).
        /// </summary>
        public void ClearEnemies()
        {
            // Копируем список: TreeExited изменяет _activeEnemies во время удаления.
            foreach (var enemy in new List<EnemyBase>(_activeEnemies))
            {
                if (GodotObject.IsInstanceValid(enemy))
                    enemy.QueueFree();
            }
            _activeEnemies.Clear();
        }
    }
}
