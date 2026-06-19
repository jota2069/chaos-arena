using Godot;
using ChaosArena.entities;

namespace ChaosArena.entities.enemies
{
    /// <summary>🧙 Маг-Призрак: держит дистанцию 150–200px, стреляет тёмными шарами каждые 2 сек.</summary>
    public partial class GhostMage : BasicEnemy
    {
        protected override string SheetPath => "res://assets/enemies/ghost_mage.png";
        protected override int DeathFrames => 3;
        protected override float BodyHeight => 32f;
        protected override Vector2 CollisionSize => new(14, 14);
        protected override Vector2 HitboxSize => new(18, 18);

        private float _shootTimer = 2f;

        protected override void OnReady()
        {
            MaxHealth = 25f;
            MoveSpeed = 60f;
            MinReward = 15;
            MaxReward = 20;
            ContactDamage = 0f; // только дальний бой
            AttackRange = 0f;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;
            _shootTimer -= dt;

            Vector2 vel = Vector2.Zero;
            if (HasTarget)
            {
                float dist = GlobalPosition.DistanceTo(Target.GlobalPosition);
                Vector2 dir = DirToTarget();
                FlipBody(dir.X);

                if (dist < 150f) vel = -dir * MoveSpeed;       // слишком близко — отступаем
                else if (dist > 200f) vel = dir * MoveSpeed;   // далеко — приближаемся
                // 150..200 — держим позицию

                if (_shootTimer <= 0f)
                {
                    _shootTimer = 2f;
                    ShootBolt(dir);
                }
            }

            Velocity = vel;
            MoveAndSlide();

            PlayLoco(vel != Vector2.Zero);
        }

        private void ShootBolt(Vector2 dir)
        {
            PlayAnim("attack");
            HurtTime = 0.2f; // держим кадр каста (PlayLoco не перебивает, пока HurtTime > 0)

            var bolt = new EnemyBolt();
            bolt.Init(dir, 15f);
            bolt.Position = GlobalPosition;
            (GetTree().CurrentScene ?? (Node)GetTree().Root).CallDeferred(Node.MethodName.AddChild, bolt);
        }

        protected override void OnDeath(Vector2 position)
        {
            Fx.DeathBurst(GetTree(), position, new Color(0.6f, 0.3f, 0.9f), 18, 150f);
        }
    }
}
