using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChaosArena.entities
{
    /// <summary>Описание одной анимации внутри собранного SpriteFrames.</summary>
    public readonly struct AnimSpec
    {
        public readonly string Name;
        public readonly float Fps;
        public readonly bool Loop;
        public readonly int[] Frames; // индексы кадров (в порядке детекции слева направо)

        public AnimSpec(string name, float fps, bool loop, int[] frames)
        {
            Name = name;
            Fps = fps;
            Loop = loop;
            Frames = frames;
        }
    }

    /// <summary>
    /// Нарезает «грязные» спрайтшиты (сгенерированные ИИ: один горизонтальный ряд
    /// с неравными отступами, кадры иногда слипаются) в кадры через детекцию по альфе.
    /// Это надёжнее равномерной сетки Hframes/Vframes, которая на таких листах
    /// разъезжается, и переживает перегенерацию арта. Результат кэшируется по пути
    /// ресурса — каждый лист режется один раз за запуск.
    /// </summary>
    public static class SpriteSheetSlicer
    {
        // Кэш: путь листа -> (кадры как AtlasTexture, высота кадра в пикселях).
        private static readonly Dictionary<string, (List<Texture2D> frames, int frameH)> _cache = new();

        /// <summary>Индексы кадров [start, start+count).</summary>
        public static int[] Range(int start, int count)
        {
            var r = new int[Mathf.Max(0, count)];
            for (int i = 0; i < r.Length; i++) r[i] = start + i;
            return r;
        }

        /// <summary>
        /// Собирает AnimatedSprite2D из листа: режет кадры, строит SpriteFrames по
        /// плану (план зависит от числа найденных кадров N) и масштабирует узел так,
        /// чтобы высота кадра на экране была ~targetHeight пикселей.
        /// </summary>
        public static AnimatedSprite2D BuildBody(
            string sheetPath, float targetHeight,
            Func<int, IEnumerable<AnimSpec>> planFactory, string defaultAnim)
        {
            var (frames, frameH) = DetectAtlas(sheetPath);

            var sf = new SpriteFrames();
            sf.RemoveAnimation("default");

            int n = frames.Count;
            foreach (var spec in planFactory(n))
            {
                if (spec.Frames.Length == 0) continue;
                if (!sf.HasAnimation(spec.Name)) sf.AddAnimation(spec.Name);
                sf.SetAnimationSpeed(spec.Name, spec.Fps);
                sf.SetAnimationLoop(spec.Name, spec.Loop);
                foreach (int idx in spec.Frames)
                    if (idx >= 0 && idx < n)
                        sf.AddFrame(spec.Name, frames[idx]);
            }

            var names = sf.GetAnimationNames();
            string startAnim = sf.HasAnimation(defaultAnim) ? defaultAnim
                : (names.Length > 0 ? names[0] : "default");

            float scale = frameH > 0 ? targetHeight / frameH : 1f;
            var node = new AnimatedSprite2D
            {
                SpriteFrames = sf,
                Animation = startAnim,
                Scale = new Vector2(scale, scale),
            };
            node.Play(startAnim);
            return node;
        }

        /// <summary>Сырой список кадров листа (для выбора кадра вручную, напр. оружие).</summary>
        public static IReadOnlyList<Texture2D> GetFrames(string sheetPath) => DetectAtlas(sheetPath).frames;

        // Кэш одиночных иконок (снаряды): путь -> AtlasTexture по общему bbox содержимого.
        private static readonly Dictionary<string, Texture2D> _iconCache = new();

        /// <summary>
        /// Возвращает иконку, обрезанную по общему bounding box непрозрачных пикселей
        /// (для снарядов: одна картинка в большом прозрачном холсте). null если файла нет.
        /// </summary>
        public static Texture2D CroppedIcon(string sheetPath)
        {
            if (_iconCache.TryGetValue(sheetPath, out var cached)) return cached;

            Texture2D icon = null;
            var tex = GD.Load<Texture2D>(sheetPath);
            if (tex != null)
            {
                var img = tex.GetImage();
                if (img != null)
                {
                    if (img.IsCompressed()) img.Decompress();
                    if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);

                    int w = img.GetWidth(), h = img.GetHeight();
                    byte[] d = img.GetData();
                    int x0 = w, y0 = h, x1 = -1, y1 = -1;
                    for (int y = 0; y < h; y++)
                    {
                        int row = y * w;
                        for (int x = 0; x < w; x++)
                            if (d[(row + x) * 4 + 3] > 32)
                            {
                                if (x < x0) x0 = x;
                                if (x > x1) x1 = x;
                                if (y < y0) y0 = y;
                                if (y > y1) y1 = y;
                            }
                    }
                    if (x1 >= 0)
                        icon = new AtlasTexture
                        {
                            Atlas = tex,
                            Region = new Rect2(x0, y0, x1 - x0 + 1, y1 - y0 + 1),
                            FilterClip = true,
                        };
                }
            }
            _iconCache[sheetPath] = icon;
            return icon;
        }

        // Детекция кадров с кэшем.
        private static (List<Texture2D> frames, int frameH) DetectAtlas(string sheetPath)
        {
            if (_cache.TryGetValue(sheetPath, out var cached)) return cached;

            var tex = GD.Load<Texture2D>(sheetPath);
            var result = SliceTexture(tex);
            _cache[sheetPath] = result;
            return result;
        }

        // Возвращает кадры (AtlasTexture одинакового размера, центрированные по
        // силуэтам) и высоту кадра. Лист считается однорядным.
        private static (List<Texture2D> frames, int frameH) SliceTexture(Texture2D tex)
        {
            var frames = new List<Texture2D>();
            if (tex == null) return (frames, 1);

            var img = tex.GetImage();
            if (img == null) return (frames, 1);
            if (img.IsCompressed()) img.Decompress();
            if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);

            int w = img.GetWidth(), h = img.GetHeight();
            byte[] d = img.GetData();

            const int AlphaThresh = 32;
            var colInk = new int[w];
            int y0 = h, y1 = -1, x0 = w, x1 = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    byte a = d[(row + x) * 4 + 3];
                    if (a > AlphaThresh)
                    {
                        colInk[x]++;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                    }
                }
            }
            if (y1 < 0) return (frames, h); // пустой лист

            int frameH = y1 - y0 + 1;

            // Сегментация: группы непустых столбцов, разделённые пустыми промежутками
            // шириной >= MinGap.
            const int MinGap = 4, MinFrameW = 10;
            var segs = new List<(int s, int e)>();
            bool inSeg = false;
            int start = 0, gap = 0;
            for (int x = 0; x < w; x++)
            {
                if (colInk[x] > 0)
                {
                    if (!inSeg) { inSeg = true; start = x; }
                    gap = 0;
                }
                else if (inSeg)
                {
                    gap++;
                    if (gap >= MinGap) { segs.Add((start, x - gap)); inSeg = false; }
                }
            }
            if (inSeg) segs.Add((start, w - 1));
            segs = segs.Where(s => s.e - s.s + 1 >= MinFrameW).ToList();
            if (segs.Count == 0) segs.Add((x0, x1));

            // Медиана ширины — порог для разбивки слипшихся кадров (широких сегментов).
            var widths = segs.Select(s => s.e - s.s + 1).OrderBy(v => v).ToList();
            int median = widths[widths.Count / 2];

            var final = new List<(int s, int e)>();
            foreach (var (s, e) in segs)
            {
                int wseg = e - s + 1;
                if (median > 0 && wseg > median * 1.7f)
                {
                    int k = Mathf.Max(1, Mathf.RoundToInt((float)wseg / median));
                    for (int i = 0; i < k; i++)
                    {
                        int ss = s + Mathf.RoundToInt(i * wseg / (float)k);
                        int ee = s + Mathf.RoundToInt((i + 1) * wseg / (float)k) - 1;
                        final.Add((ss, ee));
                    }
                }
                else final.Add((s, e));
            }

            // Единый размер кадра (макс. ширина сегмента), центрируем по силуэтам —
            // спрайт не «дышит» по горизонтали при проигрывании.
            int frameW = Mathf.Min(final.Max(f => f.e - f.s + 1), w);

            foreach (var (s, e) in final)
            {
                int cx = (s + e) / 2;
                int rx = Mathf.Clamp(cx - frameW / 2, 0, w - frameW);
                frames.Add(new AtlasTexture
                {
                    Atlas = tex,
                    Region = new Rect2(rx, y0, frameW, frameH),
                    FilterClip = true,
                });
            }
            return (frames, frameH);
        }
    }
}
