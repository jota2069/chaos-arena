using Godot;
using ChaosArena.entities;

namespace ChaosArena.entities.enemies
{
    /// <summary>🕷️ Гигантский Паук: преследует, каждые 3 сек делает рывок и оставляет паутину.</summary>
    public partial class GiantSpider : BasicEnemy
    {
        protected override string SheetPath => "res://assets/enemies/giant_spider.png";
        protected override int DeathFrames => 3;
        protected override float BodyHeight => 30f;
        protected override Vector2 CollisionSize => new(18, 14);
        protected override Vector2 HitboxSize => new(22, 18);

        private float _dashTimer = 3f;
        private float _dashTime;

        protected override void OnReady()
        {
            MaxHealth = 40f;
            MoveSpeed = 100f;
            MinReward = 12;
            MaxReward = 18;
            ContactDamage = 12f;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;
            ContactStep(dt);

            Vector2 dir = DirToTarget();

            _dashTimer -= dt;
            if (_dashTimer <= 0f && HasTarget)
            {
                _dashTimer = 3f;
                _dashTime = 0.2f; // рывок ~200px при скорости x10
                PlayAnim("attack");
                DropWeb();
            }

            float speed = _dashTime > 0f ? MoveSpeed * 10f : MoveSpeed;
            if (_dashTime > 0f) _dashTime -= dt;

            if (HasTarget) FlipBody(dir.X);
            Velocity = dir * speed;
            MoveAndSlide();

            PlayLoco(dir != Vector2.Zero);
        }

        private void DropWeb()
        {
            var web = new EnemyWeb { Position = GlobalPosition };
            (GetTree().CurrentScene ?? (Node)GetTree().Root).CallDeferred(Node.MethodName.AddChild, web);
        }

        protected override void OnDeath(Vector2 position)
        {
            Fx.DeathBurst(GetTree(), position, new Color(0.25f, 0.18f, 0.15f), 20, 160f);
        }
    }
}
