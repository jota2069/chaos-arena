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
        Vector2 direction = GetInputDirection();
        Velocity = direction * MoveSpeed;
        MoveAndSlide();
    }

    private Vector2 GetInputDirection()
    {
        Vector2 dir = Vector2.Zero;

        if (Input.IsActionPressed("ui_right")) dir.X += 1f;
        if (Input.IsActionPressed("ui_left"))  dir.X -= 1f;
        if (Input.IsActionPressed("ui_down"))  dir.Y += 1f;
        if (Input.IsActionPressed("ui_up"))    dir.Y -= 1f;

        return dir.Normalized();
    }
}