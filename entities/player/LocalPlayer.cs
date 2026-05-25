using Godot;

public partial class LocalPlayer : PlayerBase
{
    // Путь к сцене пули
    private readonly PackedScene _bulletScene = 
        GD.Load<PackedScene>("res://entities/weapons/Bullet.tscn");

    // Задержка между выстрелами
    private float _shootCooldown = 0f;
    private const float ShootDelay = 0.3f;

    protected override void OnReady()
    {
        AddToGroup("players");
        GD.Print($"LocalPlayer {PlayerId}: готов");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;

        // Движение
        Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = direction * MoveSpeed;
        MoveAndSlide();
        
        var wand = GetNodeOrNull<Sprite2D>("Wand");
        if (wand != null)
        {
            Vector2 mousePos = GetGlobalMousePosition();
            wand.LookAt(mousePos);
        }

        // Стрельба
        _shootCooldown -= (float)delta;
        if (Input.IsActionPressed("shoot") && _shootCooldown <= 0f)
        {
            Shoot();
            _shootCooldown = ShootDelay;
        }
    }

    private void Shoot()
    {
        if (_bulletScene == null) return;

        // Направление к мыши
        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 direction = (mousePos - GlobalPosition).Normalized();

        // Создаём пулю
        var bullet = _bulletScene.Instantiate<Bullet>();
        bullet.GlobalPosition = GlobalPosition;
        bullet.Init(direction);

        // Добавляем пулю на сцену
        GetTree().Root.AddChild(bullet);
    }
}