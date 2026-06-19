using Godot;
using ChaosArena.autoload;

namespace ChaosArena.ui
{
    /// <summary>
    /// Экран профиля. Выбор аватара (= класс), ввод никнейма (макс 12 символов) и блок
    /// карьерной статистики. При выборе аватара подсвечивается рамка и показываются
    /// характеристики класса. СОХРАНИТЬ -> ProfileManager.SaveProfile() (аватар = класс).
    /// Шрифт Press Start 2P в проект не добавлен — стиль как в остальном UI.
    /// </summary>
    public partial class ProfileScreen : Control
    {
        private const string MainMenuScene = "res://scenes/MainMenu.tscn";

        private static readonly Color Gold = new(1f, 0.843f, 0f);
        private static readonly Color Bg = new(0.101961f, 0.039216f, 0.180392f);

        // Порядок строго совпадает с ProfileManager: 0=Воин,1=Маг,2=Ассасин,3=Рыцарь.
        private static readonly string[] ClassNames = { "Воин", "Маг", "Ассасин", "Рыцарь" };
        private static readonly string[] ClassInfo =
        {
            "Воин:  HP 130 | Скорость 90 | Пассивка: ярость",
            "Маг:  HP 80 | Скорость 100 | Пассивка: удача оракула",
            "Ассасин:  HP 90 | Скорость 140 | Пассивка: крит",
            "Рыцарь:  HP 120 | Скорость 70 | Пассивка: броня",
        };

        private ProfileManager _profile;
        private readonly Panel[] _frames = new Panel[4];
        private Label _classInfoLabel;
        private LineEdit _nickField;
        private int _selectedAvatar;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            _profile = GetNode<ProfileManager>("/root/ProfileManager");
            _selectedAvatar = _profile.GetAvatarIndex();

            BuildBackground();
            BuildTitle();
            BuildAvatars();
            BuildClassInfo();
            BuildNickname();
            BuildStats();
            BuildButtons();

            SelectAvatar(_selectedAvatar);
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
            panelBg.OffsetLeft = 120f; panelBg.OffsetRight = -120f;
            panelBg.OffsetTop = 24f; panelBg.OffsetBottom = -24f;
            AddChild(panelBg);
        }

        private void BuildTitle()
        {
            var title = MakeLabel("МОЙ ПРОФИЛЬ", 32, Gold, HorizontalAlignment.Center);
            title.AnchorLeft = 0f; title.AnchorRight = 1f;
            title.OffsetTop = 40f; title.OffsetBottom = 84f;
            AddChild(title);
        }

        // Четыре аватара в ряд с рамкой выделения.
        private void BuildAvatars()
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 28);
            row.AnchorLeft = 0f; row.AnchorRight = 1f;
            row.AnchorTop = 0f; row.AnchorBottom = 0f;
            row.OffsetTop = 110f; row.OffsetBottom = 240f;
            AddChild(row);

            for (int i = 0; i < 4; i++)
            {
                var holder = new Control { CustomMinimumSize = new Vector2(120, 120) };

                var frame = new Panel { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
                frame.SetAnchorsPreset(LayoutPreset.FullRect);
                var sb = new StyleBoxFlat
                {
                    BgColor = new Color(0, 0, 0, 0),
                    BorderColor = Gold,
                    BorderWidthLeft = 4, BorderWidthTop = 4, BorderWidthRight = 4, BorderWidthBottom = 4,
                    CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
                    CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                };
                frame.AddThemeStyleboxOverride("panel", sb);
                holder.AddChild(frame);
                _frames[i] = frame;

                var btn = new TextureButton
                {
                    TextureNormal = GD.Load<Texture2D>(ProfileManager.AvatarTexturePath(i)),
                    IgnoreTextureSize = true,
                    StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                };
                btn.SetAnchorsPreset(LayoutPreset.FullRect);
                btn.OffsetLeft = 8; btn.OffsetTop = 8; btn.OffsetRight = -8; btn.OffsetBottom = -8;
                int idx = i;
                btn.Pressed += () => SelectAvatar(idx);
                holder.AddChild(btn);

                row.AddChild(holder);
            }
        }

        private void BuildClassInfo()
        {
            _classInfoLabel = MakeLabel("", 16, Colors.White, HorizontalAlignment.Center);
            _classInfoLabel.AnchorLeft = 0f; _classInfoLabel.AnchorRight = 1f;
            _classInfoLabel.OffsetTop = 250f; _classInfoLabel.OffsetBottom = 282f;
            AddChild(_classInfoLabel);
        }

        private void BuildNickname()
        {
            var caption = MakeLabel("Никнейм:", 16, Gold, HorizontalAlignment.Center);
            caption.AnchorLeft = 0f; caption.AnchorRight = 1f;
            caption.OffsetTop = 298f; caption.OffsetBottom = 326f;
            AddChild(caption);

            _nickField = new LineEdit
            {
                Text = _profile.GetNickname(),
                MaxLength = 12,
                Alignment = HorizontalAlignment.Center,
            };
            _nickField.AddThemeFontSizeOverride("font_size", 18);
            _nickField.AnchorLeft = 0.5f; _nickField.AnchorRight = 0.5f;
            _nickField.OffsetLeft = -150f; _nickField.OffsetRight = 150f;
            _nickField.OffsetTop = 330f; _nickField.OffsetBottom = 374f;
            AddChild(_nickField);
        }

        private void BuildStats()
        {
            var s = _profile.Profile.Stats;
            string text =
                $"СТАТИСТИКА\n" +
                $"Матчей: {s.MatchesPlayed}    Побед: {s.MatchesWon}    Поражений: {s.MatchesLost}\n" +
                $"Мобов убито: {s.EnemiesKilled}    Золота заработано: {s.GoldEarned}g\n" +
                $"Урона нанесено: {s.DamageDealt}    получено: {s.DamageTaken}";

            var stats = MakeLabel(text, 15, new Color(0.85f, 0.85f, 0.85f), HorizontalAlignment.Center);
            stats.AnchorLeft = 0f; stats.AnchorRight = 1f;
            stats.OffsetTop = 388f; stats.OffsetBottom = 500f;
            AddChild(stats);
        }

        private void BuildButtons()
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 30);
            row.AnchorLeft = 0f; row.AnchorRight = 1f;
            row.AnchorTop = 1f; row.AnchorBottom = 1f;
            row.OffsetTop = -90f; row.OffsetBottom = -34f;
            AddChild(row);

            row.AddChild(MakeButton("💾  СОХРАНИТЬ", OnSavePressed));
            row.AddChild(MakeButton("←  НАЗАД", () => GetTree().ChangeSceneToFile(MainMenuScene)));
        }

        private void SelectAvatar(int index)
        {
            _selectedAvatar = Mathf.Clamp(index, 0, 3);
            for (int i = 0; i < _frames.Length; i++)
                _frames[i].Visible = i == _selectedAvatar;
            _classInfoLabel.Text = ClassInfo[_selectedAvatar];
        }

        private void OnSavePressed()
        {
            _profile.SetNickname(_nickField.Text);
            _profile.SetAvatar(_selectedAvatar);
            _profile.SaveProfile();
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
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeFontSizeOverride("font_size", size);
            return label;
        }
    }
}
