using Godot;
using ChaosArena.autoload;
using ChaosArena.entities.weapons;
using ChaosArena.systems;

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

        // --- Эффекты Оракула Хаоса (множители/флаги; потребляются боевыми системами) ---
        public float DamageMultiplier { get; set; } = 1f;
        public float DamageReceivedMultiplier { get; set; } = 1f;
        public float SpeedMultiplier { get; set; } = 1f;
        public float VampirismPercent { get; set; } = 0f;
        public float GoldMultiplier { get; set; } = 1f;
        public bool FireBullets { get; set; } = false;
        public bool InvertControls { get; set; } = false;

        // Базовое макс. HP (без бонусов Оракула) — чтобы корректно сбрасывать эффекты.
        private float _baseMaxHealth;

        // Два слота оружия: 0 = фарм (PvE), 1 = дуэль (PvP)
        protected WeaponBase[] Weapons = new WeaponBase[2];
        protected int ActiveWeaponSlot = 0;

        private EventBus _eventBus;

        public override void _Ready()
        {
            _baseMaxHealth = MaxHealth;
            CurrentHealth = MaxHealth;
            _eventBus = GetNode<EventBus>("/root/EventBus");

            OnReady();

            // Игрок мог переспавниться в новой фазе — подтягиваем активные эффекты Оракула.
            GetNodeOrNull<OracleSystem>("/root/OracleSystem")?.ReapplyTo(this);
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
        /// Урон, который не может убить (HP остаётся минимум 1). Для карты «Жнец».
        /// </summary>
        public void TakeNonLethalDamage(float amount)
        {
            if (IsDead) return;
            float safe = Mathf.Min(amount, CurrentHealth - 1f);
            if (safe > 0f) TakeDamage(safe);
        }

        /// <summary>
        /// Сбрасывает все эффекты Оракула к значениям по умолчанию (начало раунда).
        /// </summary>
        public void ResetOracleEffects()
        {
            DamageMultiplier = 1f;
            DamageReceivedMultiplier = 1f;
            SpeedMultiplier = 1f;
            VampirismPercent = 0f;
            GoldMultiplier = 1f;
            FireBullets = false;
            InvertControls = false;
            MaxHealth = _baseMaxHealth;
            Modulate = Colors.White;
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
