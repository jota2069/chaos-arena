using Godot;
using ChaosArena.entities;

namespace ChaosArena.entities.enemies
{
    /// <summary>🧟 Зомби-Громила: много HP, большой урон, медленный. Тяжёлая смерть.</summary>
    public partial class ZombieBrute : BasicEnemy
    {
        protected override string SheetPath => "res://assets/enemies/zombie_brute.png";
        protected override int DeathFrames => 3;
        protected override float BodyHeight => 46f;
        protected override Vector2 CollisionSize => new(22, 22);
        protected override Vector2 HitboxSize => new(26, 26);

        protected override void OnReady()
        {
            MaxHealth = 80f;
            MoveSpeed = 40f;
            MinReward = 20;
            MaxReward = 30;
            ContactDamage = 25f;
            AttackRange = 30f;
        }

        protected override void OnDeath(Vector2 position)
        {
            // Большой взрыв зелёных частиц + лёгкая тряска экрана.
            Fx.DeathBurst(GetTree(), position, new Color(0.4f, 0.85f, 0.3f), 28, 180f);
            Fx.ScreenShake(GetTree(), 5f, 0.25f);
        }
    }
}
