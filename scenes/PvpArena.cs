using Godot;
using System.Collections.Generic;
using ChaosArena.autoload;
using ChaosArena.entities.player;
using ChaosArena.systems;
using ChaosArena.ui;

namespace ChaosArena.scenes
{
    /// <summary>
    /// PvP-арена — финальная фаза раунда. Строится целиком из кода: фон, обзорная
    /// камера, 4 колонны, 2 портала, центральный бонус (каждые 25 сек), границы,
    /// два игрока, система жизней с возрождением и неуязвимостью, сужение арены
    /// после 60 сек и эффекты Оракула уровня сцены (затмение, конец времён).
    /// Дуэль завершает GameManager.EndDuel, когда у кого-то кончаются жизни.
    ///
    /// ВНИМАНИЕ: игрок 1 — копия LocalPlayer и пока читает тот же ввод, что игрок 0
    /// (полноценное разделение управления — в шаге сети, промпт №8).
    /// </summary>
    public partial class PvpArena : Node2D
    {
        // --- Геометрия арены (мир == локаль, корень в (0,0)) ---
        private const float ArenaSize = 1000f;
        private const float WallThickness = 60f;
        private static readonly Vector2 ArenaCenter = new(ArenaSize / 2f, ArenaSize / 2f);

        // --- Тайминги/баланс ---
        private const int Lives = 3;
        private const float BonusInterval = 25f;
        private const float ShrinkStart = 60f;
        private const float ShrinkInterval = 10f;
        private const float ShrinkStep = 32f;
        private const float MaxInset = 400f;
        private const float ZoneDps = 5f;
        private const float InvulnSeconds = 3f;
        private const float RespawnDelay = 3f;
        private const float PortalCooldown = 2f;
        private const float EndOfTimesCap = 30f;
        private const float PortalChaosInterval = 10f;

        // --- Ресурсы ---
        private static readonly PackedScene PlayerScene =
            GD.Load<PackedScene>("res://entities/player/LocalPlayer.tscn");
        private static readonly Texture2D BgTex = GD.Load<Texture2D>("res://assets/pvp/pvp_bg.png");
        private static readonly Texture2D ColumnTex = GD.Load<Texture2D>("res://assets/pvp/pvp_column.png");
        private static readonly Texture2D PortalTex = GD.Load<Texture2D>("res://assets/pvp/pvp_portal.png");
        private static readonly Texture2D BonusTex = GD.Load<Texture2D>("res://assets/pvp/pvp_bonus_drop.png");

        private static readonly Color BlueTint = new(0.6f, 0.78f, 1f);
        private static readonly Color RedTint = new(1f, 0.62f, 0.62f);

        // --- Состояние ---
        private readonly PlayerBase[] _players = new PlayerBase[2];
        private readonly int[] _lives = { Lives, Lives };
        private readonly Area2D[] _portals = new Area2D[2];
        private readonly double[] _portalCdEnds = new double[2];
        private readonly ColorRect[] _redZones = new ColorRect[4];
        private readonly RandomNumberGenerator _rng = new();

        private GameManager _gameManager;
        private EventBus _eventBus;
        private OracleSystem _oracle;

        private PvpHud _hud;
        private TextureRect _bgRect;
        private Camera2D _camera;
        private Area2D _bonus;

        private double _elapsed;
        private float _inset;
        private bool _duelOver;
        private bool _endOfTimes;
        private double _portalChaosAccum;

        public override void _Ready()
        {
            _rng.Randomize();
            _gameManager = GetNode<GameManager>("/root/GameManager");
            _eventBus = GetNode<EventBus>("/root/EventBus");
            _oracle = GetNodeOrNull<OracleSystem>("/root/OracleSystem");

            _eventBus.PlayerDied += OnPlayerDied;
            _eventBus.PhaseChanged += OnPhaseChanged;

            BuildBackground();
            BuildCamera();
            BuildBoundaries();
            BuildColumns();
            BuildPortals();
            BuildRedZones();
            SpawnPlayers();
            BuildHud();
            ApplyArenaOracleEffects();
            StartBonusSpawner();

            CallDeferred(nameof(ActivateCamera));
        }

        public override void _ExitTree()
        {
            if (_eventBus == null || !GodotObject.IsInstanceValid(_eventBus)) return;
            _eventBus.PlayerDied -= OnPlayerDied;
            _eventBus.PhaseChanged -= OnPhaseChanged;
        }

        // --- Покадровая логика ---

        public override void _Process(double delta)
        {
            if (_duelOver) return;

            _elapsed += delta;
            _hud?.SetTime(_elapsed);

            UpdateShrink();
            HandleEndOfTimes();
            HandlePortalChaos(delta);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_duelOver || _inset <= 0f) return;

            // Урон от красной зоны: вне безопасного прямоугольника — 5 HP/сек.
            float dmg = ZoneDps * (float)delta;
            foreach (var p in _players)
            {
                if (p == null || !GodotObject.IsInstanceValid(p) || p.IsDead) continue;
                if (!IsInsideSafe(p.GlobalPosition)) p.TakeDamage(dmg);
            }
        }

        // --- Сужение арены ---

        private void UpdateShrink()
        {
            float target;
            if (_elapsed < ShrinkStart)
                target = 0f;
            else
            {
                int steps = 1 + (int)((_elapsed - ShrinkStart) / ShrinkInterval);
                target = Mathf.Min(steps * ShrinkStep, MaxInset);
            }

            if (!Mathf.IsEqualApprox(target, _inset))
            {
                _inset = target;
                UpdateRedZones();
            }
        }

        private void UpdateRedZones()
        {
            bool show = _inset > 0f;
            float far = ArenaSize - _inset;

            // top, bottom, left, right
            SetZone(_redZones[0], new Vector2(0, 0), new Vector2(ArenaSize, _inset), show);
            SetZone(_redZones[1], new Vector2(0, far), new Vector2(ArenaSize, _inset), show);
            SetZone(_redZones[2], new Vector2(0, 0), new Vector2(_inset, ArenaSize), show);
            SetZone(_redZones[3], new Vector2(far, 0), new Vector2(_inset, ArenaSize), show);
        }

        private static void SetZone(ColorRect zone, Vector2 pos, Vector2 size, bool show)
        {
            zone.Position = pos;
            zone.Size = size;
            zone.Visible = show;
        }

        private bool IsInsideSafe(Vector2 pos) =>
            pos.X >= _inset && pos.X <= ArenaSize - _inset &&
            pos.Y >= _inset && pos.Y <= ArenaSize - _inset;

        // --- Эффекты Оракула уровня сцены ---

        private void HandleEndOfTimes()
        {
            if (_endOfTimes && _elapsed >= EndOfTimesCap)
                WinDuel(MoreAlivePlayer());
        }

        private void HandlePortalChaos(double delta)
        {
            if (_oracle == null) return;
            bool any = _oracle.HasEffect(0, "portal_chaos") || _oracle.HasEffect(1, "portal_chaos");
            if (!any) return;

            _portalChaosAccum += delta;
            if (_portalChaosAccum < PortalChaosInterval) return;
            _portalChaosAccum -= PortalChaosInterval;

            for (int id = 0; id < 2; id++)
            {
                var p = _players[id];
                if (p == null || !GodotObject.IsInstanceValid(p) || p.IsDead) continue;
                if (_oracle.HasEffect(id, "portal_chaos")) p.GlobalPosition = RandomSafePoint();
            }
        }

        private void ApplyArenaOracleEffects()
        {
            if (_oracle == null) return;

            _endOfTimes = _oracle.HasEffect(0, "end_of_times") || _oracle.HasEffect(1, "end_of_times");

            bool eclipse = _oracle.HasEffect(0, "eclipse") || _oracle.HasEffect(1, "eclipse");
            if (eclipse) EnableDarkness();
        }

        // Затмение: затемняем мир + фон, у каждого игрока — световой круг ~150px.
        private void EnableDarkness()
        {
            if (_bgRect != null) _bgRect.Modulate = new Color(0.28f, 0.26f, 0.36f);

            AddChild(new CanvasModulate { Color = new Color(0.26f, 0.24f, 0.34f) });

            var lightTex = MakeLightTexture();
            foreach (var p in _players)
            {
                if (p == null) continue;
                p.AddChild(new PointLight2D
                {
                    Texture = lightTex,
                    TextureScale = 1.2f,
                    Energy = 1.3f,
                    Color = new Color(1f, 0.95f, 0.85f),
                });
            }
        }

        private static Texture2D MakeLightTexture()
        {
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1f, 1f, 1f, 1f));
            gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
            return new GradientTexture2D
            {
                Gradient = gradient,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
                Width = 256,
                Height = 256,
            };
        }

        // --- Жизни / смерть / возрождение ---

        private void OnPlayerDied(int playerId)
        {
            if (_duelOver || playerId is < 0 or > 1) return;

            _lives[playerId] = Mathf.Max(0, _lives[playerId] - 1);
            _hud?.SetLives(playerId, _lives[playerId]);

            if (_lives[playerId] <= 0)
            {
                WinDuel(1 - playerId);
                return;
            }

            // Пауза 3 сек, затем возрождение в случайном краю.
            var timer = GetTree().CreateTimer(RespawnDelay);
            timer.Timeout += () => { if (!_duelOver) RespawnPlayer(playerId); };
        }

        private void RespawnPlayer(int id)
        {
            var p = _players[id];
            if (p == null || !GodotObject.IsInstanceValid(p)) return;

            p.Respawn(RandomEdgeSpawn());
            GrantInvulnerability(p);
        }

        // Неуязвимость на 3 сек + мигание; по окончании восстанавливаем корректную
        // прозрачность через ReapplyTo (учитывает «Призраков»).
        private void GrantInvulnerability(PlayerBase p)
        {
            p.IsInvulnerable = true;

            var blink = p.CreateTween().SetLoops(Mathf.RoundToInt(InvulnSeconds / 0.3f));
            blink.TweenProperty(p, "modulate:a", 0.25f, 0.15f);
            blink.TweenProperty(p, "modulate:a", 1f, 0.15f);

            var timer = GetTree().CreateTimer(InvulnSeconds);
            timer.Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(p)) return;
                p.IsInvulnerable = false;
                _oracle?.ReapplyTo(p);
            };
        }

        private void WinDuel(int winnerId)
        {
            if (_duelOver) return;
            _duelOver = true;
            SetProcess(false);
            SetPhysicsProcess(false);

            // В сети дуэль завершает авторитетный хост; клиент только отображает.
            if (_gameManager.IsNetworkClient) return;
            _gameManager.EndDuel(winnerId);
        }

        // Победитель «по живучести» (конец времён): больше жизней, при равенстве — больше HP.
        private int MoreAlivePlayer()
        {
            if (_lives[0] != _lives[1]) return _lives[0] > _lives[1] ? 0 : 1;
            return PlayerHealth(0) >= PlayerHealth(1) ? 0 : 1;
        }

        private float PlayerHealth(int id)
        {
            var p = _players[id];
            return p != null && GodotObject.IsInstanceValid(p) && !p.IsDead ? p.CurrentHealth : 0f;
        }

        // --- Спавн игроков ---

        private void SpawnPlayers()
        {
            SpawnPlayer(0, new Vector2(200, 200), BlueTint);
            SpawnPlayer(1, new Vector2(ArenaSize - 200, ArenaSize - 200), RedTint);
        }

        private void SpawnPlayer(int id, Vector2 pos, Color tint)
        {
            var p = PlayerScene.Instantiate<PlayerBase>();
            p.PlayerId = id;
            p.Name = $"Player{id}";
            AddChild(p); // _Ready -> ReapplyTo подтянет эффекты Оракула для этого id

            p.GlobalPosition = pos;
            p.Heal(p.MaxHealth); // полный HP с учётом «Железной Кожи»

            // Цвет команды — на дочерний спрайт (SelfModulate), чтобы не конфликтовать
            // с Modulate узла (мигание неуязвимости / «Призраки»).
            var sprite = p.GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null) sprite.SelfModulate = tint;

            // Гасим персональную камеру игрока — используем общую обзорную.
            var cam = p.GetNodeOrNull<Camera2D>("Camera2D");
            if (cam != null) cam.Enabled = false;

            _players[id] = p;
            GrantInvulnerability(p);
        }

        // Случайный угол арены для возрождения (внутри безопасной зоны, иначе центр).
        private Vector2 RandomEdgeSpawn()
        {
            Vector2[] corners =
            {
                new(200, 200), new(ArenaSize - 200, 200),
                new(200, ArenaSize - 200), new(ArenaSize - 200, ArenaSize - 200),
            };

            var safe = new List<Vector2>();
            foreach (var c in corners)
                if (IsInsideSafe(c)) safe.Add(c);

            if (safe.Count == 0) return ArenaCenter;
            return safe[_rng.RandiRange(0, safe.Count - 1)];
        }

        private Vector2 RandomSafePoint()
        {
            float lo = _inset + 60f;
            float hi = ArenaSize - _inset - 60f;
            if (hi <= lo) return ArenaCenter;
            return new Vector2(_rng.RandfRange(lo, hi), _rng.RandfRange(lo, hi));
        }

        // --- Бонусы ---

        private void StartBonusSpawner()
        {
            var timer = new Timer { WaitTime = BonusInterval, OneShot = false, Autostart = true };
            AddChild(timer);
            timer.Timeout += SpawnBonus;
        }

        private void SpawnBonus()
        {
            if (_duelOver) return;
            if (_bonus != null && GodotObject.IsInstanceValid(_bonus)) return; // прошлый ещё не собран

            var area = new Area2D
            {
                Position = ArenaCenter,
                CollisionLayer = 0,
                CollisionMask = 1,
                Monitoring = true,
                ZIndex = 5,
            };
            var sprite = new Sprite2D { Texture = BonusTex, Scale = new Vector2(0.05f, 0.05f) };
            area.AddChild(sprite);
            area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 30f } });
            AddChild(area);

            area.BodyEntered += body => OnBonusEntered(area, body);

            var pulse = sprite.CreateTween().SetLoops();
            pulse.TweenProperty(sprite, "scale", new Vector2(0.058f, 0.058f), 0.7);
            pulse.TweenProperty(sprite, "scale", new Vector2(0.05f, 0.05f), 0.7);

            _bonus = area;
        }

        private void OnBonusEntered(Area2D bonus, Node2D body)
        {
            if (_duelOver || body is not PlayerBase p || p.IsDead) return;

            ApplyRandomBonus(p);

            if (GodotObject.IsInstanceValid(bonus)) bonus.QueueFree();
            if (_bonus == bonus) _bonus = null;
        }

        private void ApplyRandomBonus(PlayerBase p)
        {
            switch (_rng.RandiRange(0, 4))
            {
                case 0: p.Heal(30f); break;                       // аптечка
                case 1: TempMultiplier(p, isSpeed: true, 1.5f, 8f); break;  // ускорение
                case 2: TempMultiplier(p, isSpeed: false, 2f, 6f); break;   // двойной урон
                case 3: p.ShieldCharges += 1; break;             // щит (1 выстрел)
                case 4: p.GlobalPosition = RandomSafePoint(); break;        // телепорт
            }
        }

        // Временный множитель скорости/урона с откатом по таймеру.
        private void TempMultiplier(PlayerBase p, bool isSpeed, float factor, float seconds)
        {
            if (isSpeed) p.SpeedMultiplier *= factor;
            else p.DamageMultiplier *= factor;

            var timer = GetTree().CreateTimer(seconds);
            timer.Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(p) || p.IsDead) return; // после смерти эффект и так сброшен
                if (isSpeed) p.SpeedMultiplier /= factor;
                else p.DamageMultiplier /= factor;
            };
        }

        // --- Порталы ---

        private void BuildPortals()
        {
            _portals[0] = MakePortal(new Vector2(250, ArenaSize - 250));
            _portals[1] = MakePortal(new Vector2(ArenaSize - 250, 250));
        }

        private Area2D MakePortal(Vector2 pos)
        {
            var area = new Area2D
            {
                Position = pos,
                CollisionLayer = 0,
                CollisionMask = 1,
                Monitoring = true,
            };
            var sprite = new Sprite2D { Texture = PortalTex, Scale = new Vector2(0.06f, 0.06f) };
            area.AddChild(sprite);
            area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 34f } });
            AddChild(area);

            area.BodyEntered += body => OnPortalEntered(area, body);

            // Бесконечное плавное вращение.
            var spin = sprite.CreateTween().SetLoops();
            spin.TweenProperty(sprite, "rotation", Mathf.Tau, 4.0).AsRelative();

            return area;
        }

        private void OnPortalEntered(Area2D portal, Node2D body)
        {
            if (_duelOver || body is not PlayerBase p) return;

            int id = p.PlayerId;
            double now = Time.GetTicksMsec() / 1000.0;
            if (now < _portalCdEnds[id]) return;

            int other = portal == _portals[0] ? 1 : 0;
            p.GlobalPosition = _portals[other].GlobalPosition;
            _portalCdEnds[id] = now + PortalCooldown;
        }

        // --- Статичная геометрия ---

        private void BuildColumns()
        {
            Vector2[] spots = { new(350, 350), new(650, 350), new(350, 650), new(650, 650) };
            foreach (var spot in spots)
            {
                var body = new StaticBody2D { Position = spot, CollisionLayer = 1, CollisionMask = 0 };
                body.AddChild(new Sprite2D { Texture = ColumnTex, Scale = new Vector2(0.08f, 0.08f) });
                body.AddChild(new CollisionShape2D
                {
                    Shape = new RectangleShape2D { Size = new Vector2(56f, 120f) },
                });
                AddChild(body);
            }
        }

        private void BuildBoundaries()
        {
            float h = ArenaSize;
            float t = WallThickness;
            // (центр, размер)
            MakeWall(new Vector2(-t / 2f, h / 2f), new Vector2(t, h + 2 * t));            // лево
            MakeWall(new Vector2(h + t / 2f, h / 2f), new Vector2(t, h + 2 * t));         // право
            MakeWall(new Vector2(h / 2f, -t / 2f), new Vector2(h + 2 * t, t));            // верх
            MakeWall(new Vector2(h / 2f, h + t / 2f), new Vector2(h + 2 * t, t));         // низ
        }

        private void MakeWall(Vector2 center, Vector2 size)
        {
            var wall = new StaticBody2D { Position = center, CollisionLayer = 1, CollisionMask = 0 };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
            AddChild(wall);
        }

        private void BuildRedZones()
        {
            for (int i = 0; i < _redZones.Length; i++)
            {
                var zone = new ColorRect
                {
                    Color = new Color(1f, 0f, 0f, 0.32f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Visible = false,
                    ZIndex = 1,
                };
                _redZones[i] = zone;
                AddChild(zone);
            }
        }

        // --- Фон, камера, HUD ---

        private void BuildBackground()
        {
            var layer = new CanvasLayer { Layer = -10 };
            AddChild(layer);

            var fill = new ColorRect
            {
                Color = new Color(0.101961f, 0.039216f, 0.180392f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(fill);

            _bgRect = new TextureRect
            {
                Texture = BgTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _bgRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(_bgRect);
        }

        private void BuildCamera()
        {
            _camera = new Camera2D
            {
                Position = ArenaCenter,
                Zoom = new Vector2(0.6f, 0.6f),
            };
            AddChild(_camera);
        }

        private void ActivateCamera()
        {
            if (_camera != null && GodotObject.IsInstanceValid(_camera))
                _camera.MakeCurrent();
        }

        private void BuildHud()
        {
            _hud = new PvpHud();
            AddChild(_hud);

            _hud.SetLives(0, _lives[0]);
            _hud.SetLives(1, _lives[1]);
            _hud.SetScore(_gameManager.WinCount[0], _gameManager.WinCount[1]);
            _hud.SetTime(0);
        }

        // --- Прочее ---

        private void OnPhaseChanged(int newPhase)
        {
            // Если по какой-то причине вышли из PvP до EndDuel — глушим логику
            // (сцену всё равно сменит SceneLoader).
            if ((GameManager.GamePhase)newPhase != GameManager.GamePhase.PvP)
                _duelOver = true;
        }
    }
}
