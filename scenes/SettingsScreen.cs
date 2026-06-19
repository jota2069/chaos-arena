using Godot;
using ChaosArena.autoload;

namespace ChaosArena.ui
{
    /// <summary>
    /// Экран настроек: громкость музыки/звуков (слайдеры 0-100), полноэкранный режим
    /// (чекбокс) и справка по управлению (только показ). СОХРАНИТЬ пишет значения в
    /// профиль (profile.json) и применяет их к движку. Шрифт Press Start 2P в проект
    /// не добавлен — стиль как в остальном UI.
    /// </summary>
    public partial class SettingsScreen : Control
    {
        private const string MainMenuScene = "res://scenes/MainMenu.tscn";

        private static readonly Color Gold = new(1f, 0.843f, 0f);
        private static readonly Color Bg = new(0.101961f, 0.039216f, 0.180392f);

        private ProfileManager _profile;
        private HSlider _music;
        private HSlider _sfx;
        private Label _musicValue;
        private Label _sfxValue;
        private CheckBox _fullscreen;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            _profile = GetNode<ProfileManager>("/root/ProfileManager");

            BuildBackground();
            BuildTitle();
            BuildBody();
        }

        private void BuildBackground()
        {
            var fill = new ColorRect { Color = Bg, MouseFilter = MouseFilterEnum.Ignore };
            fill.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(fill);

            var panelBg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://assets/ui/menu/menu_panel_bg.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            panelBg.SetAnchorsPreset(LayoutPreset.FullRect);
            panelBg.OffsetLeft = 140f; panelBg.OffsetRight = -140f;
            panelBg.OffsetTop = 24f; panelBg.OffsetBottom = -24f;
            AddChild(panelBg);
        }

        private void BuildTitle()
        {
            var title = MakeLabel("НАСТРОЙКИ", 32, Gold, HorizontalAlignment.Center);
            title.AnchorLeft = 0f; title.AnchorRight = 1f;
            title.OffsetTop = 44f; title.OffsetBottom = 88f;
            AddChild(title);
        }

        private void BuildBody()
        {
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 16);
            box.AnchorLeft = 0.5f; box.AnchorRight = 0.5f;
            box.AnchorTop = 0f; box.AnchorBottom = 0f;
            box.OffsetLeft = -260f; box.OffsetRight = 260f;
            box.OffsetTop = 120f; box.OffsetBottom = 520f;
            AddChild(box);

            // Музыка.
            _music = MakeSlider(_profile.Profile.MusicVolume);
            _musicValue = MakeLabel($"{(int)_music.Value}%", 16, Colors.White, HorizontalAlignment.Right);
            _music.ValueChanged += v => _musicValue.Text = $"{(int)v}%";
            box.AddChild(MakeSliderRow("🔊 Музыка", _music, _musicValue));

            // Звуки.
            _sfx = MakeSlider(_profile.Profile.SfxVolume);
            _sfxValue = MakeLabel($"{(int)_sfx.Value}%", 16, Colors.White, HorizontalAlignment.Right);
            _sfx.ValueChanged += v => _sfxValue.Text = $"{(int)v}%";
            box.AddChild(MakeSliderRow("🔉 Звуки", _sfx, _sfxValue));

            // Полный экран.
            _fullscreen = new CheckBox { Text = "Полный экран", ButtonPressed = _profile.Profile.Fullscreen };
            _fullscreen.AddThemeColorOverride("font_color", Colors.White);
            _fullscreen.AddThemeFontSizeOverride("font_size", 18);
            box.AddChild(_fullscreen);

            // Управление (только показ).
            box.AddChild(MakeLabel("УПРАВЛЕНИЕ", 18, Gold, HorizontalAlignment.Center));
            box.AddChild(MakeLabel(
                "Движение: WASD     Стрельба: ЛКМ\n" +
                "Расходник: Q     Саботаж: G\n" +
                "Слот 1: 1     Слот 2: 2",
                15, new Color(0.8f, 0.8f, 0.8f), HorizontalAlignment.Center));

            // Кнопки.
            var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            buttons.AddThemeConstantOverride("separation", 30);
            buttons.AddChild(MakeButton("💾  СОХРАНИТЬ", OnSavePressed));
            buttons.AddChild(MakeButton("←  НАЗАД", () => GetTree().ChangeSceneToFile(MainMenuScene)));
            box.AddChild(buttons);
        }

        private HBoxContainer MakeSliderRow(string caption, HSlider slider, Label valueLabel)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 16);

            var label = MakeLabel(caption, 16, Colors.White, HorizontalAlignment.Left);
            label.CustomMinimumSize = new Vector2(150, 0);
            row.AddChild(label);

            slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(slider);

            valueLabel.CustomMinimumSize = new Vector2(56, 0);
            row.AddChild(valueLabel);
            return row;
        }

        private static HSlider MakeSlider(int value)
        {
            return new HSlider
            {
                MinValue = 0,
                MaxValue = 100,
                Step = 1,
                Value = Mathf.Clamp(value, 0, 100),
                CustomMinimumSize = new Vector2(220, 24),
            };
        }

        private void OnSavePressed()
        {
            _profile.Profile.MusicVolume = (int)_music.Value;
            _profile.Profile.SfxVolume = (int)_sfx.Value;
            _profile.Profile.Fullscreen = _fullscreen.ButtonPressed;
            _profile.SaveProfile();
            _profile.ApplySettings();
            GetTree().ChangeSceneToFile(MainMenuScene);
        }

        private Button MakeButton(string text, System.Action onPressed)
        {
            var btn = new Button { Text = text, CustomMinimumSize = new Vector2(220, 52) };
            btn.AddThemeColorOverride("font_color", Gold);
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            btn.AddThemeFontSizeOverride("font_size", 18);
            btn.Pressed += () => onPressed();
            return btn;
        }

        private static Label MakeLabel(string text, int size, Color color, HorizontalAlignment align)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeFontSizeOverride("font_size", size);
            return label;
        }
    }
}
