using Godot;
using System;

namespace ChaosArena.ui
{
    /// <summary>
    /// Главное меню — первый экран игры. Фон + логотип + 4 кнопки (Играть, Профиль,
    /// Настройки, Выйти). Кнопки на текстуре menu_button.png, золотой текст, scale 1.05
    /// при наведении. При загрузке логотип плавно проявляется, кнопки появляются по
    /// очереди снизу вверх. Навигация — сменой сцены (фазы игры тут не участвуют).
    /// Шрифт Press Start 2P в проект не добавлен — как и в остальном UI, используем
    /// размер/цвет поверх системного шрифта.
    /// </summary>
    public partial class MainMenu : Control
    {
        private const string GameVersion = "v1.0.0";

        private static readonly Color Gold = new(1f, 0.843f, 0f);
        private static readonly Color Bg = new(0.101961f, 0.039216f, 0.180392f);
        private static readonly Texture2D ButtonTex = GD.Load<Texture2D>("res://assets/ui/menu/menu_button.png");
        private static readonly Texture2D BgTex = GD.Load<Texture2D>("res://assets/ui/menu/main_menu_bg.png");
        private static readonly Texture2D LogoTex = GD.Load<Texture2D>("res://assets/ui/menu/menu_logo.png");

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            BuildBackground();
            var logo = BuildLogo();
            BuildButtons();
            BuildVersion();

            // Анимация появления: логотип alpha 0->1 за 0.5 сек.
            logo.Modulate = new Color(1, 1, 1, 0);
            var t = logo.CreateTween();
            t.TweenProperty(logo, "modulate:a", 1f, 0.5f);
        }

        private void BuildBackground()
        {
            var fill = new ColorRect { Color = Bg, MouseFilter = MouseFilterEnum.Ignore };
            fill.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(fill);

            var bg = new TextureRect
            {
                Texture = BgTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(bg);
        }

        private TextureRect BuildLogo()
        {
            var logo = new TextureRect
            {
                Texture = LogoTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            logo.AnchorLeft = 0.5f; logo.AnchorRight = 0.5f;
            logo.AnchorTop = 0f; logo.AnchorBottom = 0f;
            logo.OffsetLeft = -280f; logo.OffsetRight = 280f;
            logo.OffsetTop = 40f; logo.OffsetBottom = 230f;
            AddChild(logo);
            return logo;
        }

        private void BuildButtons()
        {
            (string text, Action action)[] items =
            {
                ("⚔️  ИГРАТЬ", () => GoTo("res://scenes/LobbyScreen.tscn")),
                ("👤  ПРОФИЛЬ", () => GoTo("res://scenes/ProfileScreen.tscn")),
                ("⚙️  НАСТРОЙКИ", () => GoTo("res://scenes/SettingsScreen.tscn")),
                ("🚪  ВЫЙТИ", () => GetTree().Quit()),
            };

            const float h = 64f, gap = 18f;
            for (int i = 0; i < items.Length; i++)
            {
                var holder = MakeMenuButton(items[i].text, items[i].action);
                float cy = (i - (items.Length - 1) / 2f) * (h + gap) + 80f;
                holder.AnchorLeft = 0.5f; holder.AnchorRight = 0.5f;
                holder.AnchorTop = 0.5f; holder.AnchorBottom = 0.5f;
                holder.OffsetLeft = -190f; holder.OffsetRight = 190f;
                holder.OffsetTop = cy - h / 2f; holder.OffsetBottom = cy + h / 2f;
                AddChild(holder);

                // Появление по очереди снизу вверх (нижняя кнопка первой).
                holder.Modulate = new Color(1, 1, 1, 0);
                float delay = (items.Length - 1 - i) * 0.1f;
                var t = holder.CreateTween();
                t.TweenInterval(delay);
                t.TweenProperty(holder, "modulate:a", 1f, 0.25f);
            }
        }

        private void BuildVersion()
        {
            var version = new Label
            {
                Text = GameVersion,
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            version.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            version.AddThemeFontSizeOverride("font_size", 14);
            version.AnchorLeft = 0f; version.AnchorRight = 1f;
            version.AnchorTop = 1f; version.AnchorBottom = 1f;
            version.OffsetTop = -36f; version.OffsetBottom = -10f;
            AddChild(version);
        }

        // Кнопка меню: текстура menu_button.png + золотой текст, scale 1.05 при наведении.
        private Control MakeMenuButton(string text, Action onPressed)
        {
            var holder = new Control { PivotOffset = new Vector2(190f, 32f) };

            var tb = new TextureButton
            {
                TextureNormal = ButtonTex,
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.Scale,
            };
            tb.SetAnchorsPreset(LayoutPreset.FullRect);
            holder.AddChild(tb);

            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.SetAnchorsPreset(LayoutPreset.FullRect);
            label.AddThemeColorOverride("font_color", Gold);
            label.AddThemeFontSizeOverride("font_size", 20);
            holder.AddChild(label);

            tb.Pressed += () => onPressed();
            tb.MouseEntered += () => ScaleHolder(holder, 1.05f);
            tb.MouseExited += () => ScaleHolder(holder, 1.0f);
            return holder;
        }

        private static void ScaleHolder(Control holder, float scale)
        {
            var t = holder.CreateTween();
            t.TweenProperty(holder, "scale", new Vector2(scale, scale), 0.1f);
        }

        private void GoTo(string scenePath) => GetTree().ChangeSceneToFile(scenePath);
    }
}
