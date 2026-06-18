using Godot;
using System.Collections.Generic;
using ChaosArena.autoload;

namespace ChaosArena.systems
{
    /// <summary>
    /// Автозагрузчик сцен (ПЯТЫЙ автолоад, после NetworkManager). Слушает
    /// EventBus.PhaseChanged и подменяет корневую сцену под текущую фазу,
    /// с затемнением через собственный CanvasLayer (переживает смену сцены).
    /// Не меняет фазы и не трогает менеджеры — только реагирует на сигнал.
    /// </summary>
    public partial class SceneLoader : Node
    {
        // Длительность одного направления fade (затемнение/осветление), секунды.
        private const float FadeDuration = 0.3f;

        // Сцена под каждую фазу. Фазы без записи (Lobby) сцену не меняют.
        private static readonly Dictionary<GameManager.GamePhase, string> ScenePaths = new()
        {
            { GameManager.GamePhase.PvE, "res://scenes/PveArena.tscn" },
            { GameManager.GamePhase.Shop, "res://scenes/ShopArena.tscn" },
            { GameManager.GamePhase.Chaos, "res://scenes/ChaosOracle.tscn" },
            { GameManager.GamePhase.PvP, "res://scenes/PvpArena.tscn" },
            { GameManager.GamePhase.RoundEnd, "res://scenes/RoundEnd.tscn" },
            { GameManager.GamePhase.MatchEnd, "res://scenes/MatchEnd.tscn" },
        };

        private EventBus _eventBus;
        private ColorRect _fade;

        public override void _Ready()
        {
            _eventBus = GetNode<EventBus>("/root/EventBus");
            _eventBus.PhaseChanged += OnPhaseChanged;

            // Слой затемнения поверх всего. Это дети автолоада, поэтому они
            // не уничтожаются при ChangeSceneToFile.
            var layer = new CanvasLayer { Layer = 128 };
            AddChild(layer);

            _fade = new ColorRect
            {
                Color = new Color(0f, 0f, 0f, 0f),
                // Не перехватываем ввод — иначе оверлей заблокирует мышь/стрельбу.
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(_fade);
        }

        public override void _ExitTree()
        {
            if (_eventBus != null && GodotObject.IsInstanceValid(_eventBus))
                _eventBus.PhaseChanged -= OnPhaseChanged;
        }

        // Смена фазы -> подбираем сцену и запускаем переход с затемнением.
        private void OnPhaseChanged(int newPhase)
        {
            var phase = (GameManager.GamePhase)newPhase;
            if (ScenePaths.TryGetValue(phase, out string scenePath))
                TransitionTo(scenePath);
        }

        // Затемнение 0->1 -> смена сцены -> осветление 1->0.
        private void TransitionTo(string scenePath)
        {
            var tween = CreateTween();
            tween.TweenProperty(_fade, "color:a", 1f, FadeDuration);
            tween.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(scenePath)));
            tween.TweenProperty(_fade, "color:a", 0f, FadeDuration);
        }
    }
}
