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

        // Защита от двойного попадания: тело врага (слой 1) и его хитбокс (слой 4)
        // могут сработать в одном кадре до отложенного QueueFree.
        private bool _hasHit;

        public void Init(Vector2 direction)
        {
            _direction = direction.Normalized();
        }

        public override void _Ready()
        {
            CollisionLayer = 2;
            CollisionMask = 1 | 4;

            
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
            if (_hasHit) return;
            if (body is EnemyBase enemy)
            {
                _hasHit = true;
                enemy.TakeDamage(Damage);
                QueueFree();
            }
        }

        private void OnAreaEntered(Area2D area)
        {
            if (_hasHit) return;
            // Хитбокс врага — его родитель EnemyBase
            if (area.IsInGroup("enemy_hitboxes") && area.GetParent() is EnemyBase enemy)
            {
                _hasHit = true;
                enemy.TakeDamage(Damage);
                QueueFree();
            }
        }
    }
}