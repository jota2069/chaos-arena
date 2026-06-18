using Godot;
using System.Collections.Generic;
using ChaosArena.autoload;
using ChaosArena.entities.player;

namespace ChaosArena.systems
{
    /// <summary>Тип карты Оракула.</summary>
    public enum OracleCardType { Buff, Debuff, Chaos }

    /// <summary>Данные одной карты Оракула (из CHAOS_ORACLE.md).</summary>
    public sealed class OracleCard
    {
        public int Id;              // 1..20
        public string Name;
        public OracleCardType Type;
        public string EffectId;     // короткий id для сигнала/проверок
        public string Description;
        public string IconPath;
        public string CardPath;
    }

    /// <summary>
    /// Логика Оракула Хаоса (СЕДЬМОЙ автолоад). Хранит 20 карт, выдаёт случайную,
    /// применяет эффект к игроку (валюта/урон — сразу; множители/флаги — на узел
    /// игрока, в т.ч. при переспавне в PvP). Перекрут (50g, макс 2/раунд) и
    /// перенаправление дебаффа сопернику (100g) — тоже здесь. Эффекты живут один
    /// раунд и сбрасываются на RoundStarted.
    /// </summary>
    public partial class OracleSystem : Node
    {
        private const int MaxRerolls = 2;
        private const int RerollCost = 50;
        private const int SendCost = 100;

        private readonly List<OracleCard> _cards = new();
        private readonly Dictionary<int, OracleCard> _byId = new();

        // Принятые в этом раунде карты на игрока (для повторного применения при переспавне).
        private readonly Dictionary<int, List<int>> _activeCards = new()
        {
            { 0, new List<int>() }, { 1, new List<int>() }
        };

        private readonly int[] _rerollsUsed = new int[2];
        private readonly RandomNumberGenerator _rng = new();

        private EventBus _eventBus;
        private GameManager _gameManager;
        private EconomyManager _economy;

        public override void _Ready()
        {
            _rng.Randomize();
            _eventBus = GetNode<EventBus>("/root/EventBus");
            _gameManager = GetNode<GameManager>("/root/GameManager");
            _economy = GetNode<EconomyManager>("/root/EconomyManager");

            BuildCards();

            _eventBus.PhaseChanged += OnPhaseChanged;
            _eventBus.RoundStarted += OnRoundStarted;

            // Оракул управляется игроком (Принять -> PvP), а не 5-сек таймером заглушки.
            // Отключаем авто-таймер фазы Chaos через ПУБЛИЧНОЕ экспорт-поле GameManager
            // (его код не трогаем). 0 -> фаза без таймера, переход только по действию.
            _gameManager.ChaosDuration = 0f;
        }

        public override void _ExitTree()
        {
            if (_eventBus == null || !GodotObject.IsInstanceValid(_eventBus)) return;
            _eventBus.PhaseChanged -= OnPhaseChanged;
            _eventBus.RoundStarted -= OnRoundStarted;
        }

        // --- Доступ к данным ---

        public IReadOnlyList<OracleCard> Cards => _cards;
        public OracleCard GetCard(int id) => _byId.GetValueOrDefault(id);
        public int RerollsLeft(int playerId) => IsValid(playerId) ? MaxRerolls - _rerollsUsed[playerId] : 0;

        /// <summary>Случайная карта; эмитит OracleCardDrawn.</summary>
        public OracleCard DrawRandom(int playerId)
        {
            var card = _cards[_rng.RandiRange(0, _cards.Count - 1)];
            _eventBus.EmitSignal(EventBus.SignalName.OracleCardDrawn, playerId, card.Id);
            return card;
        }

        // --- Действия игрока ---

        /// <summary>Перекрут за 50g (макс 2 раза за раунд). true если получилось.</summary>
        public bool TryReroll(int playerId)
        {
            if (!IsValid(playerId) || _rerollsUsed[playerId] >= MaxRerolls) return false;
            if (!_economy.SpendCurrency(playerId, RerollCost)) return false;
            _rerollsUsed[playerId]++;
            return true;
        }

        /// <summary>Отправить дебафф сопернику за 100g (только для дебаффов).</summary>
        public bool TrySendToOpponent(int playerId, int cardId)
        {
            var card = GetCard(cardId);
            if (card == null || card.Type != OracleCardType.Debuff) return false;
            if (!_economy.SpendCurrency(playerId, SendCost)) return false;
            ApplyEffect(1 - playerId, cardId); // дебафф уходит сопернику вместо тебя
            return true;
        }

        /// <summary>Применяет эффект карты к игроку playerId.</summary>
        public void ApplyEffect(int playerId, int cardId)
        {
            var card = GetCard(cardId);
            if (card == null || !IsValid(playerId)) return;

            if (!_activeCards[playerId].Contains(cardId))
                _activeCards[playerId].Add(cardId);

            ApplyImmediate(playerId, card);   // валюта/урон/обмен — сразу
            ReapplyToCurrentPlayer(playerId); // множители/флаги — на текущий узел игрока, если есть

            _eventBus.EmitSignal(EventBus.SignalName.OracleEffectApplied, playerId, card.EffectId);
        }

        // --- Применение эффектов ---

        // Немедленные эффекты — не зависят от узла игрока в бою.
        private void ApplyImmediate(int playerId, OracleCard card)
        {
            switch (card.Id)
            {
                case 1: // Король Золота — +50 обоим
                    _economy.AddCurrency(0, 50);
                    _economy.AddCurrency(1, 50);
                    break;
                case 8: // Жнец — -25 обоим (нельзя умереть)
                    for (int id = 0; id < 2; id++)
                        FindPlayer(id)?.TakeNonLethalDamage(25f);
                    break;
                case 13: // Налог — -30% золота
                    int bal = _economy.GetBalance(playerId);
                    _economy.SpendCurrency(playerId, Mathf.FloorToInt(bal * 0.3f));
                    break;
                case 11: // Проклятие Оружия — случайный игрок теряет оружие слота 1
                    GetNodeOrNull<ShopSystem>("/root/ShopSystem")?.ClearWeaponSlot(_rng.RandiRange(0, 1), 1);
                    break;
                case 18: // Рулетка Судьбы — рандом бафф одному, рандом дебафф другому
                    int lucky = _rng.RandiRange(0, 1);
                    ApplyEffect(lucky, RandomCardOfType(OracleCardType.Buff).Id);
                    ApplyEffect(1 - lucky, RandomCardOfType(OracleCardType.Debuff).Id);
                    break;
                case 19: // Обмен — игроки меняются оружием обоих слотов
                    GetNodeOrNull<ShopSystem>("/root/ShopSystem")?.SwapWeapons(0, 1);
                    break;
                // 10 Затмение, 14 Зеркало, 16 Портальный Хаос, 20 Конец Времён — эффекты
                // уровня PvP-арены/позиций; читаются ареной из активных эффектов (TODO PvP).
            }
        }

        /// <summary>Переустанавливает множители/флаги игрока из его активных карт.</summary>
        public void ReapplyTo(PlayerBase player)
        {
            if (player == null) return;
            player.ResetOracleEffects();

            if (!_activeCards.TryGetValue(player.PlayerId, out var ids)) return;
            foreach (int id in ids)
                ApplyPlayerFlags(player, GetCard(id));
        }

        private void ReapplyToCurrentPlayer(int playerId)
        {
            var p = FindPlayer(playerId);
            if (p != null) ReapplyTo(p);
        }

        // Множители/флаги на узел игрока (накопительно поверх сброшенных значений).
        private static void ApplyPlayerFlags(PlayerBase p, OracleCard card)
        {
            switch (card.Id)
            {
                case 2: p.FireBullets = true; break;                                          // Инферно
                case 3: p.SpeedMultiplier *= 1.6f; break;                                     // Молния Богов
                case 4: p.GoldMultiplier *= 2f; break;                                        // Фортуна
                case 5: p.VampirismPercent = 20f; break;                                      // Вампир
                case 6: p.MaxHealth += 40f; break;                                            // Железная Кожа
                case 7: p.DamageMultiplier *= 1.4f; break;                                    // Меткий Глаз
                case 9: p.InvertControls = true; break;                                       // Шут
                case 12: p.SpeedMultiplier *= 0.5f; break;                                    // Болото
                case 15: p.DamageMultiplier *= 2f; p.DamageReceivedMultiplier *= 2f; break;   // Берсерк
                case 17: p.Modulate = new Color(1f, 1f, 1f, 0.3f); break;                     // Призраки
            }
        }

        // --- Для дебаг-оверлея ---

        public IReadOnlyList<string> ActiveEffectNames(int playerId)
        {
            var names = new List<string>();
            if (_activeCards.TryGetValue(playerId, out var ids))
                foreach (int id in ids) names.Add(GetCard(id).Name);
            return names;
        }

        // --- Сигналы ---

        private void OnPhaseChanged(int newPhase)
        {
            // Новый визит Оракула — обнуляем перекруты для обоих.
            if ((GameManager.GamePhase)newPhase == GameManager.GamePhase.Chaos)
            {
                _rerollsUsed[0] = 0;
                _rerollsUsed[1] = 0;
            }
        }

        private void OnRoundStarted(int round)
        {
            // Эффекты Оракула живут один раунд.
            _activeCards[0].Clear();
            _activeCards[1].Clear();
            _rerollsUsed[0] = 0;
            _rerollsUsed[1] = 0;
        }

        // --- Вспомогательное ---

        private OracleCard RandomCardOfType(OracleCardType type)
        {
            var pool = _cards.FindAll(c => c.Type == type);
            return pool[_rng.RandiRange(0, pool.Count - 1)];
        }

        private PlayerBase FindPlayer(int playerId)
        {
            foreach (var node in GetTree().GetNodesInGroup("players"))
                if (node is PlayerBase p && p.PlayerId == playerId) return p;
            return null;
        }

        private static bool IsValid(int playerId) => playerId is 0 or 1;

        // --- Каталог 20 карт ---

        private void BuildCards()
        {
            // Баффы
            Add(1, "Король Золота", OracleCardType.Buff, "king_gold", "01_king_gold",
                "Оба игрока получают +50 золота");
            Add(2, "Инферно", OracleCardType.Buff, "inferno", "02_inferno",
                "Твои пули поджигают врагов: +5 урона/сек, 4 сек");
            Add(3, "Молния Богов", OracleCardType.Buff, "lightning", "03_lightning",
                "+60% скорость передвижения на раунд");
            Add(4, "Фортуна", OracleCardType.Buff, "fortune", "04_fortune",
                "x2 золото от мобов в следующем PvE");
            Add(5, "Вампир", OracleCardType.Buff, "vampire", "05_vampire",
                "20% от нанесённого урона восстанавливает HP");
            Add(6, "Железная Кожа", OracleCardType.Buff, "iron_skin", "06_iron_skin",
                "+40 максимального HP на 2 раунда");
            Add(7, "Меткий Глаз", OracleCardType.Buff, "sharp_eye", "07_sharp_eye",
                "+40% урон от всего оружия");

            // Дебаффы
            Add(8, "Жнец", OracleCardType.Debuff, "reaper", "08_reaper",
                "Оба игрока теряют -25 HP (нельзя умереть)");
            Add(9, "Шут", OracleCardType.Debuff, "jester", "09_jester",
                "Управление инвертируется на 20 сек");
            Add(10, "Затмение", OracleCardType.Debuff, "eclipse", "10_eclipse",
                "Темнота на PvP арене 30 сек");
            Add(11, "Проклятие Оружия", OracleCardType.Debuff, "cursed_weapon", "11_cursed_weapon",
                "Случайный игрок теряет оружие слота 1");
            Add(12, "Болото", OracleCardType.Debuff, "swamp", "12_swamp",
                "-50% скорость на раунд");
            Add(13, "Налог", OracleCardType.Debuff, "tax", "13_tax",
                "Теряешь 30% текущего золота");
            Add(14, "Зеркало", OracleCardType.Debuff, "mirror", "14_mirror",
                "Позиции игроков меняются местами");

            // Хаос
            Add(15, "Берсерк", OracleCardType.Chaos, "berserk", "15_berserk",
                "x2 твой урон И x2 получаемый урон");
            Add(16, "Портальный Хаос", OracleCardType.Chaos, "portal_chaos", "16_portal_chaos",
                "Каждые 10 сек случайный телепорт");
            Add(17, "Призраки", OracleCardType.Chaos, "ghosts", "17_ghosts",
                "Полупрозрачность на весь раунд");
            Add(18, "Рулетка Судьбы", OracleCardType.Chaos, "fate_roulette", "18_fate_roulette",
                "Случайный бафф одному, дебафф другому");
            Add(19, "Обмен", OracleCardType.Chaos, "swap", "19_swap",
                "Игроки меняются всем оружием");
            Add(20, "Конец Времён", OracleCardType.Chaos, "end_of_times", "20_end_of_times",
                "PvP раунд длится максимум 30 сек");
        }

        private void Add(int id, string name, OracleCardType type, string effectId,
                         string fileSuffix, string desc)
        {
            var card = new OracleCard
            {
                Id = id,
                Name = name,
                Type = type,
                EffectId = effectId,
                Description = desc,
                IconPath = $"res://assets/ui/oracle/icons/icon_{fileSuffix}.png",
                CardPath = $"res://assets/ui/oracle/cards/card_{fileSuffix}.png",
            };
            _cards.Add(card);
            _byId[id] = card;
        }
    }
}
