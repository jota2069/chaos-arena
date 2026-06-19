using Godot;
using ChaosArena.entities;

namespace ChaosArena.entities.enemies
{
    /// <summary>🦇 Летучая Мышь: очень быстрая, хаотичное движение, мало HP.</summary>
    public partial class Bat : BasicEnemy
    {
        protected override string SheetPath => "res://assets/enemies/bat.png";
        protected override int DeathFrames => 2;
        protected override float BodyHeight => 18f;
        protected override Vector2 CollisionSize => new(10, 10);
        protected override Vector2 HitboxSize => new(14, 14);

        private readonly RandomNumberGenerator _rng = new();
        private Vector2 _wander;
        private float _changeTimer;

        protected override void OnReady()
        {
            MaxHealth = 15f;
            MoveSpeed = 160f;
            MinReward = 5;
            MaxReward = 10;
            ContactDamage = 5f;
            AttackRange = 20f;

            _rng.Randomize();
            _wander = RandomDir();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;
            ContactStep(dt);

            // Меняем случайное направление каждые 0.5–1 сек.
            _changeTimer -= dt;
            if (_changeTimer <= 0f)
            {
                _changeTimer = _rng.RandfRange(0.5f, 1.0f);
                _wander = RandomDir();
            }

            // Хаос: к игроку + случайный занос.
            Vector2 dir = (DirToTarget() + _wander * 0.9f).Normalized();
            if (dir == Vector2.Zero) dir = _wander;

            Velocity = dir * MoveSpeed;
            MoveAndSlide();

            FlipBody(dir.X);
            PlayLoco(true);
        }

        private Vector2 RandomDir() =>
            new Vector2(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f)).Normalized();

        protected override void OnDeath(Vector2 position)
        {
            Fx.DeathBurst(GetTree(), position, new Color(0.2f, 0.12f, 0.25f), 12, 120f);
        }
    }
}
