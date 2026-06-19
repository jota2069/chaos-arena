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

        // --- Боевые состояния PvP ---
        /// <summary>Неуязвимость (3 сек после спавна/возрождения в PvP). Урон игнорируется.</summary>
        public bool IsInvulnerable { get; set; } = false;
        /// <summary>Заряды щита-бонуса: каждый поглощает один источник урона целиком.</summary>
        public int ShieldCharges { get; set; } = 0;

        // --- Постоянные модификаторы (Камбэк / Дар Отчаяния) ---
        // Переживают ReapplyTo Оракула: множители Оракула стакаются ПОВЕРХ этих баз.
        public float BaseDamageMultiplier { get; set; } = 1f;
        public float BaseSpeedMultiplier { get; set; } = 1f;
        public float BaseGoldMultiplier { get; set; } = 1f;
        public float BonusMaxHealth { get; set; } = 0f;

        // --- Флаги Камбэка ---
        public bool ReviveOnce { get; set; } = false;        // возрождение 1 раз в PvP
        public bool OnDeathExplosion { get; set; } = false;  // взрыв при смерти (45 урона r=120)
        public int HealOnKill { get; set; } = 0;             // +HP за убийство моба в PvE
        public float AutoAimPercent { get; set; } = 0f;      // доводка прицела к цели
        public bool EchoShot { get; set; } = false;          // дубль выстрела под 15°

        // --- Транзиентные флаги Саботажа (самоснимаются по таймеру) ---
        public bool IsStunned { get; set; } = false;         // оглушение — нельзя двигаться
        public bool IceFloor { get; set; } = false;          // скользкий пол — инерция движения

        // Базовое макс. HP (без бонусов Оракула/Камбэка) — чтобы корректно сбрасывать эффекты.
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

            // Подтягиваем персистентные эффекты. Сначала Камбэк (ставит базовые
            // множители/BonusMaxHealth), затем Оракул (стакается поверх баз).
            GetNodeOrNull<ComebackSystem>("/root/ComebackSystem")?.ReapplyTo(this);
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
            if (IsDead || IsInvulnerable) return;

            // Щит-бонус из PvP поглощает один источник урона целиком.
            if (ShieldCharges > 0 && amount > 0f)
            {
                ShieldCharges--;
                return;
            }

            // Берсерк и прочие эффекты Оракула на получаемый урон.
            amount *= DamageReceivedMultiplier;

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
            // Прямой слив HP мимо множителей/щита/неуязвимости — чтобы «Жнец»
            // гарантированно не убивал даже при активном Берсерке.
            float safe = Mathf.Min(amount, CurrentHealth - 1f);
            if (safe <= 0f) return;
            CurrentHealth -= safe;
            _eventBus.EmitSignal(EventBus.SignalName.PlayerHealthChanged, PlayerId, CurrentHealth);
        }

        /// <summary>
        /// Сбрасывает все эффекты Оракула к значениям по умолчанию (начало раунда).
        /// </summary>
        public void ResetOracleEffects()
        {
            // Сброс к базам Камбэка (а не к 1) — иначе Оракул затирал бы бонусы Камбэка.
            DamageMultiplier = BaseDamageMultiplier;
            DamageReceivedMultiplier = 1f;
            SpeedMultiplier = BaseSpeedMultiplier;
            VampirismPercent = 0f;
            GoldMultiplier = BaseGoldMultiplier;
            FireBullets = false;
            InvertControls = false;
            MaxHealth = _baseMaxHealth + BonusMaxHealth;
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

        /// <summary>
        /// Возрождает игрока в указанной точке: снимает смерть, включает обработку,
        /// подтягивает эффекты Оракула (могут менять MaxHealth) и лечит до полного HP.
        /// Неуязвимость после спавна выставляет вызывающая сторона (PvP-арена).
        /// </summary>
        public void Respawn(Vector2 globalPosition, float healthFraction = 1f)
        {
            IsDead = false;
            GlobalPosition = globalPosition;
            Visible = true;
            SetProcess(true);
            SetPhysicsProcess(true);

            GetNodeOrNull<ComebackSystem>("/root/ComebackSystem")?.ReapplyTo(this);
            GetNodeOrNull<OracleSystem>("/root/OracleSystem")?.ReapplyTo(this);
            CurrentHealth = Mathf.Clamp(MaxHealth * healthFraction, 1f, MaxHealth);
            _eventBus.EmitSignal(EventBus.SignalName.PlayerHealthChanged, PlayerId, CurrentHealth);
        }

        /// <summary>
        /// Поджог: наносит <paramref name="damagePerSecond"/> урона раз в секунду
        /// в течение <paramref name="seconds"/> секунд (карта «Инферно» в PvP).
        /// </summary>
        public void Ignite(float damagePerSecond, float seconds)
        {
            if (IsDead || damagePerSecond <= 0f || seconds <= 0f) return;

            int ticksLeft = Mathf.Max(1, Mathf.RoundToInt(seconds));
            var timer = new Timer { WaitTime = 1.0, OneShot = false };
            AddChild(timer);
            timer.Timeout += () =>
            {
                if (IsDead || ticksLeft <= 0)
                {
                    timer.QueueFree();
                    return;
                }
                TakeDamage(damagePerSecond);
                ticksLeft--;
                if (ticksLeft <= 0) timer.QueueFree();
            };
            timer.Start();
        }

        /// <summary>Оглушение (саботаж): на время блокирует движение игрока.</summary>
        public void Stun(float seconds)
        {
            if (IsDead || seconds <= 0f) return;
            IsStunned = true;
            var timer = GetTree().CreateTimer(seconds);
            timer.Timeout += () => { if (GodotObject.IsInstanceValid(this)) IsStunned = false; };
        }

        /// <summary>Скользкий пол (саботаж «Ледяной Пол»): инерция движения на время.</summary>
        public void MakeSlippery(float seconds)
        {
            if (seconds <= 0f) return;
            IceFloor = true;
            var timer = GetTree().CreateTimer(seconds);
            timer.Timeout += () => { if (GodotObject.IsInstanceValid(this)) IceFloor = false; };
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
