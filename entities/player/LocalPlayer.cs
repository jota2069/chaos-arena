using Godot;

/// <summary>
/// Локальный игрок — управляется с клавиатуры.
/// </summary>
public partial class LocalPlayer : PlayerBase
{
    protected override void OnReady()
    {
        GD.Print($"LocalPlayer {PlayerId}: готов");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;
        Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = direction * MoveSpeed;
        MoveAndSlide();
    }

    private Vector2 GetInputDirection()
    {
        return Input.GetVector("move_left", "move_right", "move_up", "move_down");
    }
    
}