using Godot;

/// <summary>
/// Простой враг — идёт к игроку и наносит урон при касании.
/// </summary>
public partial class BasicEnemy : EnemyBase
{
    [Export] public float ContactDamage = 5f;

    // Ссылка на игрока — найдём через группу
    private PlayerBase _target;

    protected override void OnReady()
    {
        // Ищем игрока по группе "players"
        var players = GetTree().GetNodesInGroup("players");
        if (players.Count > 0)
            _target = players[0] as PlayerBase;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target == null) return;

        // Двигаемся к игроку
        Vector2 direction = (_target.GlobalPosition - GlobalPosition).Normalized();
        Velocity = direction * MoveSpeed;
        MoveAndSlide();

        // Наносим урон при близком контакте
        float distance = GlobalPosition.DistanceTo(_target.GlobalPosition);
        if (distance < 40f)
            _target.TakeDamage(ContactDamage * (float)delta);
    }
}