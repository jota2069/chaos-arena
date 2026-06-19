using Godot;
using ChaosArena.entities.player;

namespace ChaosArena.entities.enemies
{
    /// <summary>
    /// Обычный враг ближнего боя: преследует ближайшего живого игрока, бьёт по
    /// кулдауну и отлетает (knockback) при получении урона. База для конкретных
    /// мобов ближнего боя (Скелет, Зомби, Паук — переопределяют статы/поведение).
    /// </summary>
    public partial class BasicEnemy : EnemyBase
    {
        [Export] public float ContactDamage = 10f;
        [Export] public float AttackRange = 24f;

        protected PlayerBase Target;
        protected float AttackCooldown;
        protected const float AttackInterval = 0.8f;

        private Vector2 _knockback = Vector2.Zero;

        public override void _Ready()
        {
            base._Ready();

            // Обновляем цель раз в секунду (безопасно для памяти и производительности).
            var timer = new Timer { WaitTime = 1f, Autostart = true };
            timer.Timeout += UpdateTarget;
            AddChild(timer);

            UpdateTarget();
        }

        protected void UpdateTarget()
        {
            PlayerBase closest = null;
            float minDist = float.MaxValue;

            foreach (var node in GetTree().GetNodesInGroup("players"))
            {
                if (node is PlayerBase p && !p.IsDead)
                {
                    float d = GlobalPosition.DistanceSquaredTo(p.GlobalPosition);
                    if (d < minDist) { minDist = d; closest = p; }
                }
            }
            Target = closest;
        }

        protected bool HasTarget => Target != null && IsInstanceValid(Target) && !Target.IsDead;

        protected Vector2 DirToTarget() =>
            HasTarget ? (Target.GlobalPosition - GlobalPosition).Normalized() : Vector2.Zero;

        // Урон игроку в ближнем бою по кулдауну (кулдаун тикает всегда).
        protected void ContactStep(float dt)
        {
            AttackCooldown = Mathf.Max(0f, AttackCooldown - dt);
            if (!HasTarget) return;

            if (GlobalPosition.DistanceTo(Target.GlobalPosition) < AttackRange && AttackCooldown <= 0f)
            {
                Target.TakeDamage(ContactDamage);
                AttackCooldown = AttackInterval;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;
            ContactStep(dt);

            Vector2 moveVelocity = Vector2.Zero;
            if (HasTarget)
            {
                Vector2 dir = DirToTarget();
                moveVelocity = dir * MoveSpeed;
                FlipBody(dir.X);
            }

            _knockback = _knockback.Lerp(Vector2.Zero, 10f * dt);
            Velocity = moveVelocity + _knockback;
            MoveAndSlide();

            PlayLoco(moveVelocity != Vector2.Zero);
        }

        public override void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0) return;

            base.TakeDamage(amount);
            if (IsDead) return; // базовый Die() мог уже сработать

            if (HasTarget)
                _knockback = (GlobalPosition - Target.GlobalPosition).Normalized() * 180f;
        }
    }
}
