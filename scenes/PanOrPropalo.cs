using Godot;
using System;

namespace ChaosArena.scenes
{
    /// <summary>
    /// Экран «Либо пан, либо пропал» — показывается победителю при счёте 3:0.
    /// ДА → последний 4й раунд с гандикапом (обрабатывает ComebackSystem),
    /// НЕТ или истёкший 15-сек таймер → обычная победа.
    /// Инстанцируется ComebackSystem и живёт на его CanvasLayer.
    /// </summary>
    public partial class PanOrPropalo : CanvasLayer
    {
        private const float DecisionTime = 15f;
        private static readonly Color Gold = new(1f, 0.843f, 0f);

        private Action _onYes;
        private Action _onNo;
        private Label _timerLabel;
        private double _timeLeft = DecisionTime;
        private bool _resolved;

        public override void _Ready() => Layer = 100;

        public void Show(Action onYes, Action onNo)
        {
            _onYes = onYes;
            _onNo = onNo;
            BuildUi();
        }

        public override void _Process(double delta)
        {
            if (_resolved) return;

            _timeLeft -= delta;
            if (_timerLabel != null)
                _timerLabel.Text = $"Таймер: {Mathf.Max(0, Mathf.CeilToInt((float)_timeLeft))} сек";

            if (_timeLeft <= 0) Resolve(false); // по умолчанию — НЕТ
        }

        private void BuildUi()
        {
            var root = new Control();
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(root);

            var bg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://assets/ui/screens/pan_or_propalo_bg.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                // MouseFilter=Stop по умолчанию — перехватывает клики из-под оверлея.
            };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(bg);

            root.AddChild(MakeLabel("ЛИБО ПАН, ЛИБО ПРОПАЛ", 44, Gold, 0.12f, 0.24f));
            root.AddChild(MakeLabel("Дать сопернику последний шанс?", 24, Colors.White, 0.30f, 0.40f));

            var yes = new Button { Text = "ДА — Рискнуть" };
            yes.AddThemeFontSizeOverride("font_size", 24);
            yes.AnchorLeft = 0.5f; yes.AnchorRight = 0.5f; yes.AnchorTop = 0.55f; yes.AnchorBottom = 0.55f;
            yes.OffsetLeft = -320; yes.OffsetRight = -40; yes.OffsetTop = -30; yes.OffsetBottom = 30;
            yes.Pressed += () => Resolve(true);
            root.AddChild(yes);

            var no = new Button { Text = "НЕТ — Победить" };
            no.AddThemeFontSizeOverride("font_size", 24);
            no.AnchorLeft = 0.5f; no.AnchorRight = 0.5f; no.AnchorTop = 0.55f; no.AnchorBottom = 0.55f;
            no.OffsetLeft = 40; no.OffsetRight = 320; no.OffsetTop = -30; no.OffsetBottom = 30;
            no.Pressed += () => Resolve(false);
            root.AddChild(no);

            _timerLabel = MakeLabel($"Таймер: {(int)DecisionTime} сек", 20, Gold, 0.68f, 0.76f);
            root.AddChild(_timerLabel);
        }

        private Label MakeLabel(string text, int size, Color color, float anchorTop, float anchorBottom)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeFontSizeOverride("font_size", size);
            label.AnchorLeft = 0; label.AnchorRight = 1;
            label.AnchorTop = anchorTop; label.AnchorBottom = anchorBottom;
            return label;
        }

        private void Resolve(bool yes)
        {
            if (_resolved) return;
            _resolved = true;

            if (yes) _onYes?.Invoke();
            else _onNo?.Invoke();

            QueueFree();
        }
    }
}
