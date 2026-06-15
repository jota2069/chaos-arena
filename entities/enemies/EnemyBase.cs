using Godot;
using ChaosArena.autoload;

namespace ChaosArena.entities.enemies
{
    /// <summary>
    /// Базовый класс врага: HP, мигание при уроне, смерть и награда владельцу арены.
    /// BasicEnemy и другие типы наследуются от него.
    /// </summary>
    public abstract partial class EnemyBase : CharacterBody2D
    {
        [Export] public float MaxHealth = 30f;
        [Export] public float MoveSpeed = 80f;
        [Export] public int MinReward = 5;
        [Export] public int MaxReward = 15;

        // Id игрока-владельца арены: награда за убийство идёт ему.
        // Спавнер выставляет это значение при создании врага.
        [Export] public int OwnerPlayerId { get; set; } = 0;

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
            _eventBus.EmitSignal(EventBus.SignalName.EnemyDied, GlobalPosition, reward, OwnerPlayerId);

            QueueFree();
        }
    }
}