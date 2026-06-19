using Godot;
using ChaosArena.autoload;
using ChaosArena.entities.weapons;

namespace ChaosArena.entities.player
{
    /// <summary>
    /// Локальный игрок: ввод WASD, стрельба ЛКМ по направлению мыши,
    /// процедурная анимация (покачивание, тень, частицы пыли).
    /// </summary>
    public partial class LocalPlayer : PlayerBase
    {
        // Путь к сцене пули
        private readonly PackedScene _bulletScene = 
            GD.Load<PackedScene>("res://entities/weapons/Bullet.tscn");

        // Задержка между выстрелами
        private float _shootCooldown;
        private const float ShootDelay = 0.3f;

        // Анимация
        private Sprite2D _sprite;
        private Sprite2D _shadow;
        private CpuParticles2D _dust;
        private float _animTime;
        private Vector2 _lastDirection = Vector2.Right;

        // Нужен, чтобы помечать пули владельцем только в фазе PvP.
        private GameManager _gameManager;

        protected override void OnReady()
        {
            AddToGroup("players");

            _gameManager = GetNodeOrNull<GameManager>("/root/GameManager");
            _sprite = GetNode<Sprite2D>("Sprite2D");
            
            // Создаем тень программно
            _shadow = new Sprite2D();
            _shadow.Texture = CreateShadowTexture();
            _shadow.Position = new Vector2(0, 8);
            _shadow.Scale = new Vector2(0.8f, 0.3f);
            _shadow.Modulate = new Color(0, 0, 0, 0.3f);
            _shadow.ZIndex = -1;
            AddChild(_shadow);
            
            // Создаем частицы пыли программно
            _dust = new CpuParticles2D();
            _dust.Position = new Vector2(0, 6);
            _dust.Amount = 8;
            _dust.Lifetime = 0.3f;
            _dust.OneShot = true;
            _dust.Explosiveness = 0.5f;
            _dust.Direction = new Vector2(0, -1);
            _dust.Spread = 30f;
            _dust.Gravity = new Vector2(0, 50);
            _dust.InitialVelocityMin = 20f;
            _dust.InitialVelocityMax = 40f;
            _dust.ScaleAmountMin = 0.5f;
            _dust.ScaleAmountMax = 1f;
            _dust.Modulate = new Color(0.8f, 0.7f, 0.6f, 0.6f);
            _dust.Emitting = false;
            AddChild(_dust);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;

            // Движение. Эффекты: «Шут»/гравитация (инверсия), множитель скорости,
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
            
            UpdateAnimation(direction, dt);
            
            // Фиксация посоха в руке
            var wandHolder = GetNodeOrNull<Marker2D>("Sprite2D/WandHolder");
            if (wandHolder != null && _sprite != null)
            {
                Vector2 mousePos = GetGlobalMousePosition();
                bool mouseRight = mousePos.X > GlobalPosition.X;
                
                // 1. Зеркалим персонажа в сторону мыши
                float baseScale = Mathf.Abs(_sprite.Scale.Y);
                _sprite.Scale = new Vector2(mouseRight ? baseScale : -baseScale, _sprite.Scale.Y);
                
                // 2. Позиция WandHolder в координатах спрайта
                wandHolder.Position = new Vector2(231f, 20f);
                
                // 3. Сбрасываем вращение холдера, чтобы он наследовал только покачивание тела
                wandHolder.Rotation = 0f;
                wandHolder.Scale = new Vector2(1f, 1f);
            }

            // Стрельба
            _shootCooldown -= dt;
            if (Input.IsActionPressed("shoot") && _shootCooldown <= 0f)
            {
                Shoot();
                _shootCooldown = ShootDelay;
            }
        }

        private void UpdateAnimation(Vector2 direction, float delta)
        {
            if (_sprite == null) return;

            if (direction != Vector2.Zero)
            {
                _lastDirection = direction;
                _animTime += delta * 15f;
                
                float targetRotation = direction.X * 0.2f;
                _sprite.Rotation = Mathf.Lerp(_sprite.Rotation, targetRotation, 10f * delta);
                
                float bounce = Mathf.Abs(Mathf.Sin(_animTime)) * 3f;
                _sprite.Position = new Vector2(0, -bounce);
                
                float pulse = 1f + Mathf.Abs(Mathf.Sin(_animTime * 2f)) * 0.05f;
                float signX = Mathf.Sign(_sprite.Scale.X);
                _sprite.Scale = new Vector2(signX * pulse * 0.1f, pulse * 0.1f);
                
                if (_dust != null && Mathf.Sin(_animTime) > 0.95f)
                    _dust.Emitting = true;
            }
            else
            {
                _animTime = 0f;
                _sprite.Rotation = Mathf.Lerp(_sprite.Rotation, 0f, 10f * delta);
                _sprite.Position = Vector2.Zero;
                
                float breathe = 1f + Mathf.Sin((float)Time.GetTicksMsec() / 200f) * 0.02f;
                float signX = Mathf.Sign(_sprite.Scale.X);
                _sprite.Scale = new Vector2(signX * breathe * 0.1f, breathe * 0.1f);
            }
        }

        private void Shoot()
        {
            if (_bulletScene == null) return;

            var wandHolder = GetNodeOrNull<Marker2D>("Sprite2D/WandHolder");
            Vector2 spawnPos = (wandHolder != null && _sprite != null)
                ? wandHolder.GlobalPosition
                : GlobalPosition;

            Vector2 direction = (GetGlobalMousePosition() - spawnPos).Normalized();
            direction = ApplyAutoAim(direction, spawnPos); // «Глаз Охотника» (камбэк)

            FireBullet(direction, spawnPos);

            // «Эхо Выстрела» (камбэк): дубль под углом 15°.
            if (EchoShot)
                FireBullet(direction.Rotated(Mathf.DegToRad(15f)), spawnPos);
        }

        // Создаёт и настраивает одну пулю (включая боевые эффекты Оракула в PvP).
        private void FireBullet(Vector2 direction, Vector2 spawnPos)
        {
            var bullet = _bulletScene.Instantiate<Bullet>();
            bullet.GlobalPosition = spawnPos;
            bullet.Init(direction);

            if (_gameManager != null && _gameManager.CurrentPhase == GameManager.GamePhase.PvP)
            {
                bullet.OwnerPlayerId = PlayerId;
                bullet.Damage *= DamageMultiplier;
                bullet.Incendiary = FireBullets;
                bullet.Vampirism = VampirismPercent;
                bullet.SetOwner(this);
            }

            GetTree().Root.AddChild(bullet);
        }

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