using Godot;

/// <summary>
/// Пуля — летит в направлении, наносит урон врагам при касании.
/// </summary>
public partial class Bullet : Area2D
{
    [Export] public float Speed = 300f;
    [Export] public float Damage = 10f;
    [Export] public float Lifetime = 2f;

    private Vector2 _direction;
    private float _timer;

    public void Init(Vector2 direction)
    {
        _direction = direction.Normalized();
    }

    public override void _PhysicsProcess(double delta)
    {
        _timer += (float)delta;
        if (_timer >= Lifetime)
        {
            QueueFree();
            return;
        }

        Position += _direction * Speed * (float)delta;
    }

    public override void _Ready()
    {
        // Подписываемся на сигнал столкновения
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is EnemyBase enemy)
        {
            enemy.TakeDamage(Damage);
            QueueFree();
        }
    }
}