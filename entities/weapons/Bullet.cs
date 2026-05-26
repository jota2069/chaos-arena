using Godot;
using ChaosArena.entities.enemies;

namespace ChaosArena.entities.weapons
{
    public partial class Bullet : Area2D
    {
        [Export] public float Speed = 300f;
        [Export] public float Damage = 10f;
        [Export] public float Lifetime = 2f;

        private Vector2 _direction;

        public void Init(Vector2 direction)
        {
            _direction = direction.Normalized();
        }

        public override void _Ready()
        {
            BodyEntered += OnBodyEntered;
            AreaEntered += OnAreaEntered;
            
            // Автоудаление по таймеру
            var timer = GetTree().CreateTimer(Lifetime);
            timer.Timeout += QueueFree;
        }

        public override void _PhysicsProcess(double delta)
        {
            Position += _direction * Speed * (float)delta;
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body is EnemyBase enemy)
            {
                enemy.TakeDamage(Damage);
                QueueFree();
            }
        }

        private void OnAreaEntered(Area2D area)
        {
            if (area.GetParent() is EnemyBase enemy)
            {
                enemy.TakeDamage(Damage);
                QueueFree();
            }
        }
    }
}