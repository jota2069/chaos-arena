using Godot;

namespace ChaosArena.entities.enemies
{
    public abstract partial class EnemyBase : CharacterBody2D
    {
        [Export] public float MaxHealth = 30f;
        [Export] public float MoveSpeed = 80f;
        [Export] public int MinReward = 5;
        [Export] public int MaxReward = 15;

        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        // Элементы для дочерних классов
        protected Sprite2D Sprite;
        private EventBus _eventBus;

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            IsDead = false;
            
            // Находим спрайт внутри сцены врага
            Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            _eventBus = GetNode<EventBus>("/root/EventBus");
            
            OnReady();
        }

        protected virtual void OnReady() { }

        // Добавлено virtual, чтобы BasicEnemy мог его переопределить через override
        public virtual void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0) return;
            
            CurrentHealth -= amount;
            GD.Print($"[Enemy] HP: {CurrentHealth}/{MaxHealth}");
            
            // Эффект мигания при получении урона
            if (Sprite != null)
            {
                var tween = CreateTween();
                tween.TweenProperty(Sprite, "modulate", new Color(1, 0, 0), 0.1f);
                tween.TweenProperty(Sprite, "modulate", new Color(1, 1, 1), 0.1f);
            }
            
            if (CurrentHealth <= 0f)
                Die();
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            
            int reward = GD.RandRange(MinReward, MaxReward);
            _eventBus.EmitSignal(EventBus.SignalName.EnemyDied, GlobalPosition, reward);
            GD.Print($"[Enemy] Died, reward: {reward}");
            
            QueueFree();
        }
    }
}