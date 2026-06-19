using Godot;
using System;
using System.Collections.Generic;
using ChaosArena.systems;

namespace ChaosArena.scenes
{
    /// <summary>
    /// Экран Камбэка. Два режима:
    ///  • ShowInfo — показывает один автоматически выданный предмет (Малый камбэк),
    ///    закрывается сам через несколько секунд;
    ///  • ShowChoice — три карточки на выбор (Средний/Большой), ставит игру на паузу
    ///    до выбора, затем применяет и снимает паузу.
    /// Инстанцируется ComebackSystem и живёт на его CanvasLayer.
    /// </summary>
    public partial class ComebackScreen : CanvasLayer
    {
        private static readonly Color Bg = new(0.101961f, 0.039216f, 0.180392f);
        private static readonly Color Gold = new(1f, 0.843f, 0f);
        private static readonly Color Red = new(0.95f, 0.2f, 0.2f);

        private Control _root;
        private Action<int> _onPick;

        public override void _Ready() => Layer = 100;

        /// <summary>Малый камбэк: показать выданный предмет и закрыться через 2.5 сек.</summary>
        public void ShowInfo(ComebackItem item, int gold)
        {
            BuildFrame(gold, blockInput: false); // информационный режим — не мешает игре
            var row = MakeRow();
            row.AddChild(MakeCard(item, clickable: false));

            var timer = GetTree().CreateTimer(2.5f);
            timer.Timeout += Close;
        }

        /// <summary>Средний/Большой камбэк: выбор 1 из 3. Пауза до выбора.</summary>
        public void ShowChoice(List<ComebackItem> items, int gold, Action<int> onPick)
        {
            _onPick = onPick;
            BuildFrame(gold, blockInput: true);

            var row = MakeRow();
            foreach (var item in items)
                row.AddChild(MakeCard(item, clickable: true));

            // Пауза, чтобы раунд не ушёл вперёд, пока игрок выбирает.
            ProcessMode = ProcessModeEnum.Always;
            GetTree().Paused = true;
        }

        // --- Построение ---

        private void BuildFrame(int gold, bool blockInput)
        {
            _root = new Control
            {
                MouseFilter = blockInput ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore,
            };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            var bg = new ColorRect
            {
                Color = Bg,
                MouseFilter = blockInput ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore,
            };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(bg);

            var title = MakeLabel("КАМБЭК!", 48, Red, HorizontalAlignment.Center);
            title.AnchorLeft = 0; title.AnchorRight = 1;
            title.OffsetTop = 60; title.OffsetBottom = 130;
            _root.AddChild(title);

            // Пульсация заголовка.
            var pulse = title.CreateTween().SetLoops();
            pulse.TweenProperty(title, "scale", new Vector2(1.1f, 1.1f), 0.5);
            pulse.TweenProperty(title, "scale", new Vector2(1f, 1f), 0.5);

            if (gold > 0)
            {
                var goldLabel = MakeLabel($"+{gold} золота", 22, Gold, HorizontalAlignment.Center);
                goldLabel.AnchorLeft = 0; goldLabel.AnchorRight = 1;
                goldLabel.OffsetTop = 140; goldLabel.OffsetBottom = 172;
                _root.AddChild(goldLabel);
            }
        }

        private HBoxContainer MakeRow()
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 24);
            row.Alignment = BoxContainer.AlignmentMode.Center;
            row.SetAnchorsPreset(Control.LayoutPreset.Center);
            row.AnchorLeft = 0; row.AnchorRight = 1;
            row.OffsetLeft = 0; row.OffsetRight = 0;
            row.OffsetTop = 190; row.OffsetBottom = 520;
            _root.AddChild(row);
            return row;
        }

        private Control MakeCard(ComebackItem item, bool clickable)
        {
            var card = new Panel { CustomMinimumSize = new Vector2(240, 300) };

            var icon = new TextureRect
            {
                Texture = GD.Load<Texture2D>(item.IconPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new Vector2(80, 16),
                Size = new Vector2(80, 80),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            card.AddChild(icon);

            var name = MakeLabel(item.Name, 16, Gold, HorizontalAlignment.Center);
            name.Position = new Vector2(8, 104);
            name.Size = new Vector2(224, 28);
            card.AddChild(name);

            var desc = MakeLabel(item.Description, 13, Colors.White, HorizontalAlignment.Center);
            desc.Position = new Vector2(12, 140);
            desc.Size = new Vector2(216, 96);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            card.AddChild(desc);

            if (clickable)
            {
                var take = new Button { Text = "Взять", Position = new Vector2(60, 250), Size = new Vector2(120, 36) };
                int id = item.Id;
                take.Pressed += () => Pick(id);
                card.AddChild(take);
            }

            return card;
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

        private void Pick(int id)
        {
            _onPick?.Invoke(id);
            _onPick = null;
            Close();
        }

        private void Close()
        {
            if (GetTree().Paused) GetTree().Paused = false;
            QueueFree();
        }
    }
}
