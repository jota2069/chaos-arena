using Godot;
using ChaosArena.autoload;
using ChaosArena.entities.weapons;
using ChaosArena.systems;

namespace ChaosArena.entities.player
{
    /// <summary>Игровой класс (= аватар профиля). Задаёт HP, скорость и пассивку.</summary>
    public enum PlayerClass { Warrior, Mage, Rogue, Knight }

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

        // --- Класс игрока и его пассивки ---
        /// <summary>Текущий игровой класс (по аватару профиля).</summary>
        public PlayerClass Class { get; private set; } = PlayerClass.Warrior;
        /// <summary>Воин: при HP &lt; 30% исходящий урон +30%.</summary>
        public bool ClassFuryBelow30 { get; private set; }
        /// <summary>Маг: +20% к шансу баффа Оракула (читается системой удачи Оракула — TODO).</summary>
        public float ClassOracleLuckBonus { get; private set; }
        /// <summary>Ассасин: каждый 3й выстрел — крит x2 (через <see cref="ConsumeClassCrit"/>).</summary>
        public bool ClassCritEveryThird { get; private set; }
        /// <summary>Рыцарь: сколько ближайших ударов за раунд ещё будут поглощены.</summary>
        public int ClassHitAbsorb { get; set; }
        private int _classShotCounter; // счётчик выстрелов для крита Ассасина

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

            // Класс (= аватар профиля) задаёт базовые HP/скорость. Применяем ДО
            // эффектов Камбэка/Оракула, чтобы их бонусы стакались поверх базы класса.
            ApplyClassFromProfile();

            // Подтягиваем персистентные эффекты. Сначала Камбэк (ставит базовые
            // множители/BonusMaxHealth), затем Оракул (стакается поверх баз).
            GetNodeOrNull<ComebackSystem>("/root/ComebackSystem")?.ReapplyTo(this);
            GetNodeOrNull<OracleSystem>("/root/OracleSystem")?.ReapplyTo(this);
        }

        // Берёт класс из локального профиля и применяет — только своему (локальному)
        // игроку. Класс соперника по сети назначит NetworkManager (TODO ЭТАП 2).
        private void ApplyClassFromProfile()
        {
            int localId = GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.LocalPlayerId ?? 0;
            if (PlayerId != localId) return;

            var profile = GetNodeOrNull<ProfileManager>("/root/ProfileManager");
            if (profile == null) return;
            ApplyClassStats(ClassFromString(profile.GetClass()));
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

            // Рыцарь: первые удары за раунд поглощаются классовой бронёй.
            if (ClassHitAbsorb > 0 && amount > 0f)
            {
                ClassHitAbsorb--;
                return;
            }

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
        /// Применяет характеристики класса: базовые HP/скорость и пассивку. Аватар
        /// профиля = класс (см. CLAUDE.md). Базовое HP кладётся в _baseMaxHealth, чтобы
        /// бонусы Камбэка/Оракула стакались поверх. Лечит до полного HP класса.
        /// </summary>
        public void ApplyClassStats(PlayerClass cls)
        {
            Class = cls;

            // Сброс классовых пассивок (на случай переназначения класса).
            ClassFuryBelow30 = false;
            ClassOracleLuckBonus = 0f;
            ClassCritEveryThird = false;
            ClassHitAbsorb = 0;
            _classShotCounter = 0;

            switch (cls)
            {
                case PlayerClass.Warrior:                  // ярость
                    _baseMaxHealth = 130f; MoveSpeed = 90f;
                    ClassFuryBelow30 = true;
                    break;
                case PlayerClass.Mage:                     // удача Оракула
                    _baseMaxHealth = 80f; MoveSpeed = 100f;
                    ClassOracleLuckBonus = 0.2f;
                    break;
                case PlayerClass.Rogue:                    // крит
                    _baseMaxHealth = 90f; MoveSpeed = 140f;
                    ClassCritEveryThird = true;
                    break;
                case PlayerClass.Knight:                   // броня
                    _baseMaxHealth = 120f; MoveSpeed = 70f;
                    ClassHitAbsorb = 2;
                    break;
            }

            MaxHealth = _baseMaxHealth + BonusMaxHealth;
            CurrentHealth = MaxHealth;
            _eventBus?.EmitSignal(EventBus.SignalName.PlayerHealthChanged, PlayerId, CurrentHealth);
        }

        /// <summary>Преобразует строковое имя класса (warrior/mage/rogue/knight) в enum.</summary>
        public static PlayerClass ClassFromString(string name) => name switch
        {
            "mage" => PlayerClass.Mage,
            "rogue" => PlayerClass.Rogue,
            "knight" => PlayerClass.Knight,
            _ => PlayerClass.Warrior,
        };

        /// <summary>
        /// Множитель крита для следующего выстрела (Ассасин: каждый 3й = x2, иначе x1).
        /// Вызывается стреляющим кодом на каждый произведённый выстрел.
        /// </summary>
        public float ConsumeClassCrit()
        {
            if (!ClassCritEveryThird) return 1f;
            _classShotCounter++;
            return _classShotCounter % 3 == 0 ? 2f : 1f;
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
