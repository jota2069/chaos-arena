using Godot;

/// <summary>
/// Базовый класс врага. Содержит HP и базовый AI.
/// </summary>
public abstract partial class EnemyBase : CharacterBody2D
{
    [Export] public float MaxHealth = 30f;
    [Export] public float MoveSpeed = 80f;
    [Export] public int RewardOnDeath = 10;

    public float CurrentHealth { get; private set; }

    private EventBus _eventBus;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        _eventBus = GetNode<EventBus>("/root/EventBus");
        OnReady();
    }

    protected virtual void OnReady() { }

    public void TakeDamage(float amount)
    {
        CurrentHealth -= amount;
        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        _eventBus.EmitSignal(EventBus.SignalName.EnemyDied, GlobalPosition, RewardOnDeath);
        QueueFree(); // удаляем узел из сцены
    }
}