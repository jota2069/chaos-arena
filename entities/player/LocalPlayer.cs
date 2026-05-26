using Godot;
using ChaosArena.entities.weapons;

namespace ChaosArena.entities.player
{
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

        protected override void OnReady()
        {
            AddToGroup("players");
            GD.Print($"LocalPlayer {PlayerId}: готов");
            
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

            // Движение
            Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            Velocity = direction * MoveSpeed;
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
            Vector2 spawnPos = GlobalPosition;
            Vector2 mousePos = GetGlobalMousePosition();

            if (wandHolder != null && _sprite != null)
            {
                // Снаряд спавнится четко из глобальной позиции холдера
                spawnPos = wandHolder.GlobalPosition;
            }

            Vector2 direction = (mousePos - spawnPos).Normalized();

            var bullet = _bulletScene.Instantiate<Bullet>();
            bullet.GlobalPosition = spawnPos;
            bullet.Init(direction);

            GetTree().Root.AddChild(bullet);
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