using Godot;
using ChaosArena.entities;

namespace ChaosArena.entities.enemies
{
    /// <summary>💀 Скелет-Воин: базовый ближний бой, блокирует щитом 20% урона.</summary>
    public partial class SkeletonWarrior : BasicEnemy
    {
        protected override string SheetPath => "res://assets/enemies/skeleton_warrior.png";
        protected override int DeathFrames => 3;
        protected override float BodyHeight => 30f;
        protected override Vector2 CollisionSize => new(14, 14);
        protected override Vector2 HitboxSize => new(18, 18);

        protected override void OnReady()
        {
            MaxHealth = 30f;
            MoveSpeed = 80f;
            MinReward = 10;
            MaxReward = 15;
            ContactDamage = 10f;
        }

        public override void TakeDamage(float amount)
        {
            base.TakeDamage(amount * 0.8f); // щит поглощает 20%
        }

        protected override void OnDeath(Vector2 position)
        {
            // Белые косточки разлетаются.
            Fx.DeathBurst(GetTree(), position, new Color(0.95f, 0.95f, 0.9f), 18, 150f);
        }
    }
}
