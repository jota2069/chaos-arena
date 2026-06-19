using System.Collections.Generic;
using Godot;
using ChaosArena.autoload;
using ChaosArena.entities;
using ChaosArena.entities.weapons;

namespace ChaosArena.entities.player
{
    /// <summary>
    /// Локальный игрок: ввод WASD, стрельба ЛКМ по направлению мыши. Визуал собирается
    /// из кода: AnimatedSprite2D тела (скин по PlayerId — синий/красный), WeaponHolder с
    /// оружием (вращается к мыши, отдача), тень, пыль при беге, шейдер мигания при уроне.
    ///
    /// Примечание по арту: сгенерированные спрайтшиты — один фронтальный цикл ходьбы
    /// (без отдельных видов вверх/вбок), поэтому направления различаются только зеркалом
    /// (FlipH к мыши); анимации сведены к idle/run/hurt/death.
    /// Клавиши 1–9 переключают оружие (тест WeaponHolder и визуала снарядов).
    /// </summary>
    public partial class LocalPlayer : PlayerBase
    {
        private const float BodyHeight = 30f;
        private const float WeaponHoldDist = 14f;   // локальный сдвиг оружия от центра
        private const float MuzzleDist = 20f;       // вынос точки выстрела вперёд
        private const string WeaponsSheet = "res://assets/weapons/weapons_sheet.png";

        private readonly PackedScene _bulletScene =
            GD.Load<PackedScene>("res://entities/weapons/Bullet.tscn");

        private float _shootCooldown;
        private const float ShootDelay = 0.3f;

        private AnimatedSprite2D _body;
        private Sprite2D _shadow;
        private CpuParticles2D _dust;
        private Node2D _weaponHolder;
        private Sprite2D _weaponSprite;
        private ShaderMaterial _flashMat;

        private int _currentWeapon;           // 0 = fire_staff
        private float _hurtTime;              // пока > 0 — держим анимацию hurt
        private Color _teamColor = new(0.9f, 0.9f, 1f);

        // Нужен, чтобы помечать пули владельцем только в фазе PvP.
        private GameManager _gameManager;

        protected override void OnReady()
        {
            AddToGroup("players");
            _gameManager = GetNodeOrNull<GameManager>("/root/GameManager");

            BuildShadow();
            BuildBody();
            BuildDust();
            BuildWeapon();
        }

        // --- Сборка визуала ---

        private void BuildBody()
        {
            string sheet = PlayerId == 1
                ? "res://assets/characters/player_red.png"
                : "res://assets/characters/player_blue.png";

            _body = SpriteSheetSlicer.BuildBody(sheet, BodyHeight, PlayerPlan, "idle");
            _body.SelfModulate = _teamColor;

            var shader = GD.Load<Shader>("res://entities/player/flash.gdshader");
            if (shader != null)
            {
                _flashMat = new ShaderMaterial { Shader = shader };
                _body.Material = _flashMat;
            }
            AddChild(_body);
        }

        // План анимаций тела по числу найденных кадров N (последние 3 — смерть).
        private static IEnumerable<AnimSpec> PlayerPlan(int n)
        {
            int death = Mathf.Clamp(n - 1, 1, 3);
            int walkEnd = Mathf.Max(1, n - death);     // кадры ходьбы [0, walkEnd)
            yield return new AnimSpec("idle", 6f, true, SpriteSheetSlicer.Range(0, Mathf.Min(2, walkEnd)));
            yield return new AnimSpec("run", 10f, true, SpriteSheetSlicer.Range(0, walkEnd));
            yield return new AnimSpec("hurt", 8f, false, SpriteSheetSlicer.Range(0, Mathf.Min(2, walkEnd)));
            yield return new AnimSpec("death", 6f, false, SpriteSheetSlicer.Range(walkEnd, death));
        }

        private void BuildShadow()
        {
            _shadow = new Sprite2D
            {
                Texture = CreateShadowTexture(),
                Position = new Vector2(0, 12),
                Scale = new Vector2(1.6f, 0.7f),
                Modulate = new Color(0, 0, 0, 0.35f),
                ZIndex = -1,
            };
            AddChild(_shadow);
        }

        private void BuildDust()
        {
            _dust = new CpuParticles2D
            {
                Texture = Fx.DotTexture(),
                Position = new Vector2(0, 12),
                Amount = 12,
                Lifetime = 0.35f,
                OneShot = false,
                Emitting = false,
                Direction = new Vector2(0, -1),
                Spread = 40f,
                Gravity = new Vector2(0, 30),
                InitialVelocityMin = 8f,
                InitialVelocityMax = 24f,
                ScaleAmountMin = 0.6f,
                ScaleAmountMax = 1.4f,
                Color = new Color(0.8f, 0.74f, 0.66f, 0.55f),
            };
            AddChild(_dust);
        }

        private void BuildWeapon()
        {
            _weaponHolder = new Node2D();
            AddChild(_weaponHolder);

            _weaponSprite = new Sprite2D { Position = new Vector2(WeaponHoldDist, 0) };
            _weaponHolder.AddChild(_weaponSprite);

            SetWeaponSprite(_currentWeapon);
        }

        /// <summary>Меняет спрайт оружия в руке на оружие №index (0..8) из weapons_sheet.</summary>
        public void SetWeaponSprite(int index)
        {
            if (_weaponSprite == null) return;
            var frames = SpriteSheetSlicer.GetFrames(WeaponsSheet);
            if (frames.Count == 0) return;

            int n = frames.Count;
            int fi = Mathf.Clamp(Mathf.RoundToInt(index * (n - 1) / 8f), 0, n - 1);
            var tex = frames[fi];
            _weaponSprite.Texture = tex;
            float s = tex.GetHeight() > 0 ? 16f / tex.GetHeight() : 0.12f;
            _weaponSprite.Scale = new Vector2(s, s);
        }

        public override void SetTeamColor(Color color)
        {
            _teamColor = color;
            if (_body != null) _body.SelfModulate = color;
        }

        // --- Цикл ---

        public override void _PhysicsProcess(double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;
            if (_hurtTime > 0f) _hurtTime -= dt;

            // Движение. Эффекты: инверсия управления, множитель скорости,
            // оглушение (саботаж) — стоп, ледяной пол (саботаж) — инерция.
            Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            if (IsStunned) direction = Vector2.Zero;
            if (InvertControls) direction = -direction;

            Vector2 targetVelocity = direction * MoveSpeed * SpeedMultiplier;
            if (IceFloor)
                Velocity = Velocity.Lerp(targetVelocity, 3f * dt); // скольжение
            else
                Velocity = targetVelocity;
            MoveAndSlide();

            bool moving = direction != Vector2.Zero && !IsStunned;
            UpdateBodyAnim(moving);
            UpdateWeapon();
            UpdateDust(direction, moving);

            // Стрельба
            _shootCooldown -= dt;
            if (Input.IsActionPressed("shoot") && _shootCooldown <= 0f)
            {
                Shoot();
                _shootCooldown = ShootDelay;
            }
        }

        private void UpdateBodyAnim(bool moving)
        {
            if (_body == null) return;

            // Лицом к мыши (зеркалим по X).
            _body.FlipH = GetGlobalMousePosition().X < GlobalPosition.X;

            if (_hurtTime > 0f) return; // не перебиваем анимацию hurt

            string want = moving ? "run" : "idle";
            if (_body.Animation != want && _body.SpriteFrames != null && _body.SpriteFrames.HasAnimation(want))
                _body.Play(want);
        }

        private void UpdateWeapon()
        {
            if (_weaponHolder == null) return;

            float ang = (GetGlobalMousePosition() - GlobalPosition).Angle();
            _weaponHolder.Rotation = ang;

            // Чтобы оружие не было «вверх ногами» при прицеле влево.
            if (_weaponSprite != null)
                _weaponSprite.FlipV = Mathf.Abs(ang) > Mathf.Pi / 2f;
        }

        private void UpdateDust(Vector2 direction, bool moving)
        {
            if (_dust == null) return;
            _dust.Emitting = moving;
            if (moving) _dust.Direction = (-direction).Normalized();
        }

        // --- Урон / смерть (визуал) ---

        protected override void OnDamaged(float amount)
        {
            FlashRed();
            if (_body?.SpriteFrames != null && _body.SpriteFrames.HasAnimation("hurt"))
            {
                _body.Play("hurt");
                _hurtTime = 0.2f;
            }
        }

        private void FlashRed()
        {
            if (_flashMat == null) return;
            var t = CreateTween();
            t.TweenProperty(_flashMat, "shader_parameter/flash_intensity", 1f, 0.1f);
            t.TweenProperty(_flashMat, "shader_parameter/flash_intensity", 0f, 0.2f);
        }

        protected override void OnDeath()
        {
            SpawnDeathEcho();
            Fx.DeathBurst(GetTree(), GlobalPosition, _teamColor, 24, 170f);
        }

        // Отдельный «труп», проигрывающий death и растворяющийся — тело игрока к этому
        // моменту скрывается (логика возрождения), поэтому смерть показываем копией.
        private void SpawnDeathEcho()
        {
            if (_body?.SpriteFrames == null) return;

            var echo = new AnimatedSprite2D
            {
                SpriteFrames = _body.SpriteFrames,
                Scale = _body.Scale,
                FlipH = _body.FlipH,
                SelfModulate = _teamColor,
                Position = GlobalPosition,
                ZIndex = 5,
            };
            echo.Play(echo.SpriteFrames.HasAnimation("death") ? "death" : "idle");
            echo.Ready += () =>
            {
                var t = echo.CreateTween();
                t.TweenInterval(0.4);
                t.TweenProperty(echo, "modulate:a", 0f, 0.4f);
                t.TweenCallback(Callable.From(echo.QueueFree));
            };
            (GetTree()?.CurrentScene ?? GetTree()?.Root)?.CallDeferred(Node.MethodName.AddChild, echo);
        }

        // --- Тест: переключение оружия клавишами 1–9 ---

        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;
            int idx = (int)k.Keycode - (int)Key.Key1;
            if (idx >= 0 && idx < 9)
            {
                _currentWeapon = idx;
                SetWeaponSprite(idx);
            }
        }

        // --- Стрельба ---

        private void Shoot()
        {
            if (_bulletScene == null) return;

            Vector2 forward = Vector2.Right.Rotated(_weaponHolder?.Rotation ?? 0f);
            Vector2 spawnPos = _weaponHolder != null
                ? _weaponHolder.GlobalPosition + forward * MuzzleDist
                : GlobalPosition;

            Vector2 direction = (GetGlobalMousePosition() - spawnPos).Normalized();
            direction = ApplyAutoAim(direction, spawnPos); // «Глаз Охотника» (камбэк)

            FireBullet(direction, spawnPos);

            // «Эхо Выстрела» (камбэк): дубль под углом 15°.
            if (EchoShot)
                FireBullet(direction.Rotated(Mathf.DegToRad(15f)), spawnPos);

            PlayRecoil();
        }

        private void PlayRecoil()
        {
            if (_weaponSprite == null) return;
            var t = CreateTween();
            t.TweenProperty(_weaponSprite, "position:x", WeaponHoldDist - 8f, 0.05f);
            t.TweenProperty(_weaponSprite, "position:x", WeaponHoldDist, 0.1f);
        }

        // Создаёт и настраивает одну пулю (включая визуал и боевые эффекты Оракула в PvP).
        private void FireBullet(Vector2 direction, Vector2 spawnPos)
        {
            var bullet = _bulletScene.Instantiate<Bullet>();
            bullet.GlobalPosition = spawnPos;
            bullet.Init(direction);
            bullet.SetVisual(WeaponVisual(_currentWeapon));

            if (_gameManager != null && _gameManager.CurrentPhase == GameManager.GamePhase.PvP)
            {
                bullet.OwnerPlayerId = PlayerId;
                bullet.Damage *= DamageMultiplier;
                bullet.Damage *= ConsumeClassCrit();                       // Ассасин: каждый 3й выстрел крит x2
                if (ClassFuryBelow30 && CurrentHealth < MaxHealth * 0.3f)  // Воин: ярость при HP < 30%
                    bullet.Damage *= 1.3f;
                bullet.Incendiary = FireBullets;
                bullet.Vampirism = VampirismPercent;
                bullet.SetOwner(this);
            }

            GetTree().Root.AddChild(bullet);
        }

        // Сопоставление оружия (0..8) визуалу снаряда.
        private static Bullet.Visual WeaponVisual(int idx) => idx switch
        {
            0 => Bullet.Visual.Fire,       // fire_staff
            1 => Bullet.Visual.Ice,        // ice_crossbow
            2 => Bullet.Visual.Lightning,  // lightning_wand
            3 => Bullet.Visual.Dark,       // necro_staff
            4 => Bullet.Visual.Bullet,     // shadow_dagger
            5 => Bullet.Visual.Bullet,     // sniper_musket
            6 => Bullet.Visual.Grenade,    // chaos_launcher
            7 => Bullet.Visual.Portal,     // portal_gun
            _ => Bullet.Visual.Default,    // mirror_shield и пр.
        };

        // Доводит направление к ближайшей цели на AutoAimPercent% (0 => без изменений).
        private Vector2 ApplyAutoAim(Vector2 direction, Vector2 from)
        {
            if (AutoAimPercent <= 0f) return direction;

            Vector2? target = NearestTargetPosition(from);
            if (target == null) return direction;

            Vector2 toTarget = (target.Value - from).Normalized();
            float t = Mathf.Clamp(AutoAimPercent / 100f, 0f, 1f);
            return direction.Slerp(toTarget, t).Normalized();
        }

        // Ближайшая цель: в PvP — соперник, иначе — ближайший враг (по хитбоксам).
        private Vector2? NearestTargetPosition(Vector2 from)
        {
            bool pvp = _gameManager != null && _gameManager.CurrentPhase == GameManager.GamePhase.PvP;
            Vector2? best = null;
            float bestDist = float.MaxValue;

            if (pvp)
            {
                foreach (var node in GetTree().GetNodesInGroup("players"))
                    if (node is PlayerBase p && p.PlayerId != PlayerId && !p.IsDead)
                        Consider(p.GlobalPosition, from, ref best, ref bestDist);
            }
            else
            {
                foreach (var node in GetTree().GetNodesInGroup("enemy_hitboxes"))
                    if (node is Node2D hb && IsInstanceValid(hb))
                        Consider(hb.GlobalPosition, from, ref best, ref bestDist);
            }
            return best;
        }

        private static void Consider(Vector2 pos, Vector2 from, ref Vector2? best, ref float bestDist)
        {
            float d = from.DistanceSquaredTo(pos);
            if (d < bestDist) { bestDist = d; best = pos; }
        }

        private Texture2D CreateShadowTexture()
        {
            var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    float dx = (x - 8) / 8f;
                    float dy = (y - 8) / 4f;
                    if (dx * dx + dy * dy <= 1f)
                        image.SetPixel(x, y, new Color(0, 0, 0, 0.5f));
                }
            }
            return ImageTexture.CreateFromImage(image);
        }
    }
}
