using Godot;
using ChaosArena.autoload;
using ChaosArena.entities.weapons;

namespace ChaosArena.entities.player
{
    /// <summary>
    /// Базовый класс игрока. Содержит общую логику HP и оружия.
    /// LocalPlayer и RemotePlayer наследуются от него.
    /// </summary>
    public abstract partial class PlayerBase : CharacterBody2D
    {
        [Export] public float MaxHealth = 100f;
        [Export] public float MoveSpeed = 200f;
        [Export] public int PlayerId = 0;

        public float CurrentHealth { get; private set; }

        // Два слота оружия: 0 = фарм (PvE), 1 = дуэль (PvP)
        protected WeaponBase[] Weapons = new WeaponBase[2];
        protected int ActiveWeaponSlot = 0;

        private EventBus _eventBus;

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            _eventBus = GetNode<EventBus>("/root/EventBus");

            OnReady();
        }

        public bool IsDead { get; private set; } = false;

        // Дочерние классы переопределяют для своей инициализации
        protected virtual void OnReady() { }

        /// <summary>
        /// Наносит урон игроку.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            _eventBus.EmitSignal(EventBus.SignalName.PlayerHealthChanged, PlayerId, CurrentHealth);

            if (CurrentHealth <= 0f)
                Die();
        }

        /// <summary>
        /// Лечит игрока.
        /// </summary>
        public void Heal(float amount)
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            _eventBus.EmitSignal(EventBus.SignalName.PlayerHealthChanged, PlayerId, CurrentHealth);
        }

        /// <summary>
        /// Переключает активный слот оружия (0 или 1).
        /// </summary>
        public void SwitchWeapon(int slot)
        {
            if (slot < 0 || slot > 1) return;
            ActiveWeaponSlot = slot;
        }

        private void Die()
        {
            if (IsDead) return; // защита от двойного вызова
            IsDead = true;
            _eventBus.EmitSignal(EventBus.SignalName.PlayerDied, PlayerId);

            // Скрываем игрока пока нет системы возрождения
            Visible = false;
            SetProcess(false);
            SetPhysicsProcess(false);
        }
    }
}
