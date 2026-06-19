using Godot;

namespace ChaosArena.entities
{
    /// <summary>
    /// Лёгкие визуальные эффекты, собираемые из кода: радиальный свет, частицы-взрывы,
    /// вспышка попадания, эффект убийства в PvP (замедление + белая вспышка), тряска
    /// камеры. Эффекты подвешиваются к текущей сцене и сами себя удаляют.
    ///
    /// Частицы — CpuParticles2D (как пыль игрока в проекте): не требуют GPU-материала
    /// и стабильнее в сборке. Визуально эквивалентны GPUParticles2D из ТЗ.
    /// </summary>
    public static class Fx
    {
        private static Texture2D _light;
        private static Texture2D _dot;

        /// <summary>Белая радиальная текстура для PointLight2D/частиц (тинтуется цветом узла).</summary>
        public static Texture2D LightTexture()
        {
            if (_light != null) return _light;
            var g = new Gradient();
            g.SetColor(0, new Color(1, 1, 1, 1));
            g.SetColor(1, new Color(1, 1, 1, 0));
            _light = new GradientTexture2D
            {
                Gradient = g,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
                Width = 128,
                Height = 128,
            };
            return _light;
        }

        /// <summary>Мягкая «точка» для частиц (иначе CpuParticles2D рисует 1px).</summary>
        public static Texture2D DotTexture()
        {
            if (_dot != null) return _dot;
            const int s = 16;
            var img = Image.CreateEmpty(s, s, false, Image.Format.Rgba8);
            float c = (s - 1) / 2f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float a = Mathf.Clamp(1f - Mathf.Sqrt(dx * dx + dy * dy), 0f, 1f);
                    img.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            _dot = ImageTexture.CreateFromImage(img);
            return _dot;
        }

        // Куда безопасно подвесить эффект (текущая сцена, иначе корень дерева).
        private static Node Host(SceneTree tree) => tree?.CurrentScene ?? tree?.Root;

        /// <summary>Настроенный CpuParticles2D-взрыв (one-shot, круговой разлёт).</summary>
        public static CpuParticles2D MakeBurst(Color color, int amount, float velMin, float velMax,
            float lifetime = 0.5f, float scaleMin = 1f, float scaleMax = 2.5f)
        {
            return new CpuParticles2D
            {
                Texture = DotTexture(),
                Emitting = true,
                OneShot = true,
                Amount = Mathf.Max(1, amount),
                Lifetime = lifetime,
                Explosiveness = 1f,
                Direction = Vector2.Right,
                Spread = 180f,
                InitialVelocityMin = velMin,
                InitialVelocityMax = velMax,
                Gravity = Vector2.Zero,
                DampingMin = velMax * 0.5f,
                DampingMax = velMax,
                ScaleAmountMin = scaleMin,
                ScaleAmountMax = scaleMax,
                Color = color,
            };
        }

        /// <summary>Вспышка попадания: световой «пых» + разлёт частиц в точке globalPos.</summary>
        public static void HitSpark(SceneTree tree, Vector2 globalPos, Color color)
        {
            var host = Host(tree);
            if (host == null) return;

            var root = new Node2D { Position = globalPos, ZIndex = 50 };
            var light = new PointLight2D
            {
                Texture = LightTexture(),
                Color = color,
                Energy = 1.4f,
                TextureScale = 0.6f,
            };
            root.AddChild(light);
            root.AddChild(MakeBurst(color, 10, 60f, 160f, 0.35f, 1f, 2f));

            root.Ready += () =>
            {
                var t = root.CreateTween();
                t.SetParallel(true);
                t.TweenProperty(light, "texture_scale", 1.8f, 0.1);
                t.TweenProperty(light, "energy", 0f, 0.1);
                t.SetParallel(false);
                t.TweenInterval(0.35);
                t.TweenCallback(Callable.From(root.QueueFree));
            };
            host.CallDeferred(Node.MethodName.AddChild, root);
        }

        /// <summary>Взрыв частиц при смерти (мобы): цвет и размер задаются вызывающим.</summary>
        public static void DeathBurst(SceneTree tree, Vector2 globalPos, Color color, int amount, float vel = 140f)
        {
            var host = Host(tree);
            if (host == null) return;

            var root = new Node2D { Position = globalPos, ZIndex = 40 };
            root.AddChild(MakeBurst(color, amount, vel * 0.35f, vel, 0.6f, 1.5f, 3.2f));
            root.Ready += () =>
            {
                var t = root.CreateTween();
                t.TweenInterval(0.8);
                t.TweenCallback(Callable.From(root.QueueFree));
            };
            host.CallDeferred(Node.MethodName.AddChild, root);
        }

        /// <summary>Эффект убийства в PvP: замедление времени на 0.5 сек + белая вспышка экрана.</summary>
        public static void PvpKill(SceneTree tree)
        {
            if (tree == null) return;

            var layer = new CanvasLayer { Layer = 100 };
            var rect = new ColorRect
            {
                Color = new Color(1, 1, 1, 0.8f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(rect);
            layer.Ready += () =>
            {
                var t = rect.CreateTween();
                t.TweenProperty(rect, "color:a", 0f, 0.5).SetTrans(Tween.TransitionType.Sine);
                t.TweenCallback(Callable.From(layer.QueueFree));
            };
            (Host(tree) ?? tree.Root)?.CallDeferred(Node.MethodName.AddChild, layer);

            // Замедление: восстанавливаем по реальному времени (ignoreTimeScale).
            Engine.TimeScale = 0.35;
            var timer = tree.CreateTimer(0.5, processAlways: true, processInPhysics: false, ignoreTimeScale: true);
            timer.Timeout += () => Engine.TimeScale = 1.0;
        }

        /// <summary>Лёгкая тряска активной камеры (например, при смерти Зомби-Громилы).</summary>
        public static void ScreenShake(SceneTree tree, float amount = 6f, float time = 0.25f)
        {
            var cam = tree?.Root?.GetViewport()?.GetCamera2D();
            if (cam == null) return;

            var rng = new RandomNumberGenerator();
            rng.Randomize();

            var t = cam.CreateTween();
            const int steps = 5;
            for (int i = 0; i < steps; i++)
            {
                var off = new Vector2(rng.RandfRange(-amount, amount), rng.RandfRange(-amount, amount));
                t.TweenProperty(cam, "offset", off, time / (steps + 1));
            }
            t.TweenProperty(cam, "offset", Vector2.Zero, time / (steps + 1));
        }
    }
}
