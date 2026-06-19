using Godot;
using ChaosArena.autoload;
using ChaosArena.entities.player;

namespace ChaosArena.ui
{
    /// <summary>
    /// HUD дуэли — отдельный от PvE HUD. Сверху по центру: счёт раундов и секундомер.
    /// По краям: по 3 сердца жизней и полоса HP на каждого игрока.
    /// HP слушает EventBus.PlayerHealthChanged; жизни/счёт/время обновляет PvpArena
    /// прямыми вызовами SetLives / SetScore / SetTime.
    /// </summary>
    public partial class PvpHud : CanvasLayer
    {
        public const int MaxLives = 3;

        private const float BarWidth = 220f;
        private const float BarHeight = 14f;
        private const float HeartSize = 36f;
        private const float Margin = 20f;

        private static readonly Color Gold = new(1f, 0.843f, 0f);
        private static readonly Color BarBg = new(0.08f, 0.08f, 0.08f, 0.85f);
        private static readonly Texture2D HeartFull = GD.Load<Texture2D>("res://assets/ui/hud/heart_full.png");
        private static readonly Texture2D HeartEmpty = GD.Load<Texture2D>("res://assets/ui/hud/heart_empty.png");

        private readonly TextureRect[][] _hearts = { new TextureRect[MaxLives], new TextureRect[MaxLives] };
        private readonly ColorRect[] _hpFill = new ColorRect[2];

        private Control _root;
        private Label _scoreLabel;
        private Label _timeLabel;
        private EventBus _eventBus;

        public override void _Ready()
        {
            Layer = 50;
            _eventBus = GetNode<EventBus>("/root/EventBus");

            BuildUi();

            _eventBus.PlayerHealthChanged += OnHealthChanged;
            CallDeferred(nameof(InitHealth));
        }

        public override void _ExitTree()
        {
            if (_eventBus != null && GodotObject.IsInstanceValid(_eventBus))
                _eventBus.PlayerHealthChanged -= OnHealthChanged;
        }

        // --- Публичный API (вызывает PvpArena) ---

        public void SetLives(int playerId, int lives)
        {
            if (playerId is < 0 or > 1) return;
            for (int i = 0; i < MaxLives; i++)
                _hearts[playerId][i].Texture = i < lives ? HeartFull : HeartEmpty;
        }

        public void SetScore(int w0, int w1) => _scoreLabel.Text = $"🔵 {w0} : {w1} 🔴";

        public void SetTime(double seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt((float)seconds));
            _timeLabel.Text = $"{s / 60:00}:{s % 60:00}";
        }

        // --- HP через EventBus ---

        private void InitHealth()
        {
            foreach (var node in GetTree().GetNodesInGroup("players"))
                if (node is PlayerBase p)
                    UpdateHp(p.PlayerId, p.CurrentHealth, p.MaxHealth);
        }

        private void OnHealthChanged(int playerId, float newHealth)
        {
            UpdateHp(playerId, newHealth, FindMaxHealth(playerId));
        }

        private float FindMaxHealth(int playerId)
        {
            foreach (var node in GetTree().GetNodesInGroup("players"))
                if (node is PlayerBase p && p.PlayerId == playerId) return p.MaxHealth;
            return 100f;
        }

        private void UpdateHp(int playerId, float health, float max)
        {
            if (playerId is < 0 or > 1) return;
            float ratio = max > 0f ? Mathf.Clamp(health / max, 0f, 1f) : 0f;
            var fill = _hpFill[playerId];

            // Игрок 0 — заполнение слева направо; игрок 1 — зеркально, от правого края.
            if (playerId == 0)
                fill.OffsetRight = fill.OffsetLeft + BarWidth * ratio;
            else
                fill.OffsetLeft = fill.OffsetRight - BarWidth * ratio;

            fill.Color = ratio > 0.5f ? new Color(0.2f, 0.8f, 0.25f)
                       : ratio > 0.25f ? new Color(0.95f, 0.6f, 0.1f)
                       : new Color(0.9f, 0.15f, 0.15f);
        }

        // --- Построение UI ---

        private void BuildUi()
        {
            _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            _scoreLabel = MakeCenterLabel("🔵 0 : 0 🔴", 26, Gold, top: 10f, bottom: 44f);
            _timeLabel = MakeCenterLabel("00:00", 18, Colors.White, top: 46f, bottom: 72f);

            BuildSide(0);
            BuildSide(1);
        }

        private Label MakeCenterLabel(string text, int fontSize, Color color, float top, float bottom)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeFontSizeOverride("font_size", fontSize);
            _root.AddChild(label);

            label.AnchorLeft = 0f; label.AnchorRight = 1f;
            label.AnchorTop = 0f; label.AnchorBottom = 0f;
            label.OffsetLeft = 0f; label.OffsetRight = 0f;
            label.OffsetTop = top; label.OffsetBottom = bottom;
            return label;
        }

        // Сердца + полоса HP одного игрока. 0 — левый верхний угол, 1 — правый верхний.
        private void BuildSide(int playerId)
        {
            bool right = playerId == 1;

            for (int i = 0; i < MaxLives; i++)
            {
                var heart = new TextureRect
                {
                    Texture = HeartFull,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                _root.AddChild(heart);
                heart.OffsetTop = 14f;
                heart.OffsetBottom = 14f + HeartSize;

                if (!right)
                {
                    heart.OffsetLeft = Margin + i * (HeartSize + 4f);
                    heart.OffsetRight = heart.OffsetLeft + HeartSize;
                }
                else
                {
                    heart.AnchorLeft = 1f; heart.AnchorRight = 1f;
                    heart.OffsetRight = -(Margin + i * (HeartSize + 4f));
                    heart.OffsetLeft = heart.OffsetRight - HeartSize;
                }
                _hearts[playerId][i] = heart;
            }

            float barTop = 14f + HeartSize + 6f;

            var bg = new ColorRect { Color = BarBg, MouseFilter = Control.MouseFilterEnum.Ignore };
            var fill = new ColorRect { Color = new Color(0.2f, 0.8f, 0.25f), MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.AddChild(bg);
            _root.AddChild(fill);

            foreach (var rect in new[] { bg, fill })
            {
                rect.OffsetTop = barTop;
                rect.OffsetBottom = barTop + BarHeight;
                if (!right)
                {
                    rect.OffsetLeft = Margin;
                    rect.OffsetRight = Margin + BarWidth;
                }
                else
                {
                    rect.AnchorLeft = 1f; rect.AnchorRight = 1f;
                    rect.OffsetRight = -Margin;
                    rect.OffsetLeft = -Margin - BarWidth;
                }
            }
            _hpFill[playerId] = fill;
        }
    }
}
