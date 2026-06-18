using Godot;
using ChaosArena.entities.weapons;

namespace ChaosArena.scenes
{
    /// <summary>
    /// Тренировочная груша. Бесконечное HP — не умирает. Сама ловит пули своим
    /// Area2D (маска слоя пуль = 2), показывает всплывающие цифры урона и копит
    /// суммарный урон, сбрасывая счётчик каждые 3 сек. Bullet.cs не трогаем —
    /// пулю уничтожает сама груша.
    /// </summary>
    public partial class TrainingDummy : Area2D
    {
        private static readonly PackedScene FloatingDamageScene =
            GD.Load<PackedScene>("res://scenes/FloatingDamage.tscn");

        // Период сброса суммарного урона.
        private const float ResetInterval = 3f;

        private Label _totalLabel;
        private float _totalDamage;
        private readonly RandomNumberGenerator _rng = new();

        public override void _Ready()
        {
            _rng.Randomize();

            // Ловим только пули (слой 2); сами для пуль невидимы (слой 0).
            CollisionLayer = 0;
            CollisionMask = 2;
            Monitoring = true;

            AreaEntered += OnAreaEntered;

            // Табличка суммарного урона над грушей.
            _totalLabel = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _totalLabel.AddThemeFontSizeOverride("font_size", 14);
            _totalLabel.AddThemeColorOverride("font_color", new Color(1f, 0.843f, 0f)); // золотой
            _totalLabel.Position = new Vector2(-40f, -56f);
            AddChild(_totalLabel);

            // Сброс счётчика каждые 3 сек.
            var timer = new Timer { WaitTime = ResetInterval, Autostart = true };
            AddChild(timer);
            timer.Timeout += ResetTotal;
        }

        private void OnAreaEntered(Area2D area)
        {
            if (area is not Bullet bullet) return;

            SpawnFloatingDamage(bullet.Damage);

            _totalDamage += bullet.Damage;
            _totalLabel.Text = $"Урон: {Mathf.RoundToInt(_totalDamage)}";

            // Груша поглощает пулю (HP бесконечно, урон не получает).
            bullet.QueueFree();
        }

        private void SpawnFloatingDamage(float amount)
        {
            if (FloatingDamageScene == null) return;

            var label = FloatingDamageScene.Instantiate<FloatingDamage>();
            AddChild(label);
            // Небольшой разброс, чтобы цифры не наслаивались.
            label.Position = new Vector2(_rng.RandfRange(-18f, 18f), -24f);
            label.Setup(amount);
        }

        private void ResetTotal()
        {
            _totalDamage = 0f;
            _totalLabel.Text = "";
        }
    }
}
