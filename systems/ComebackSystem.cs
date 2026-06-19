using Godot;
using System.Collections.Generic;
using ChaosArena.autoload;
using ChaosArena.entities.player;
using ChaosArena.scenes;

namespace ChaosArena.systems
{
    /// <summary>Минимальный счёт, с которого предмет становится доступен.</summary>
    public enum ComebackLevel { Small, Medium, Large }

    /// <summary>Данные одного предмета Камбэка (из COMEBACK.md).</summary>
    public sealed class ComebackItem
    {
        public int Id;
        public string Name;
        public string Description;
        public string IconPath;
        public ComebackLevel MinLevel;
    }

    /// <summary>
    /// Система Камбэка (ДЕСЯТЫЙ автолоад). После каждого раунда выдаёт проигравшему
    /// предмет по разнице счёта (Малый — авто, Средний — выбор 1 из 3 + 30g). Эффекты
    /// хранятся как флаги на PlayerBase и накатываются при каждом спавне через ReapplyTo.
    /// Также: лечение за убийства и множитель золота (EnemyDied), а при счёте 3:0 —
    /// экран «Либо пан, либо пропал» с Даром Отчаяния и Бременем Чести.
    /// </summary>
    public partial class ComebackSystem : Node
    {
        private static readonly PackedScene ComebackScreenScene =
            GD.Load<PackedScene>("res://scenes/ComebackScreen.tscn");
        private static readonly PackedScene PanScene =
            GD.Load<PackedScene>("res://scenes/PanOrPropalo.tscn");

        private const float ReviveHealthFraction = 0.35f;

        private readonly List<ComebackItem> _catalog = new();
        private readonly Dictionary<int, ComebackItem> _byId = new();

        // Активные на текущий раунд эффекты на игрока.
        private readonly List<int>[] _items = { new(), new() };
        private readonly bool[] _desperation = new bool[2];  // Дар Отчаяния
        private readonly bool[] _honorBurden = new bool[2];  // Бремя Чести
        private readonly bool[] _reviveUsed = new bool[2];

        private readonly RandomNumberGenerator _rng = new();

        private EventBus _eventBus;
        private GameManager _gameManager;
        private EconomyManager _economy;
        private OracleSystem _oracle;

        public override void _Ready()
        {
            _rng.Randomize();
            _eventBus = GetNode<EventBus>("/root/EventBus");
            _gameManager = GetNode<GameManager>("/root/GameManager");
            _economy = GetNode<EconomyManager>("/root/EconomyManager");
            _oracle = GetNodeOrNull<OracleSystem>("/root/OracleSystem");

            BuildCatalog();

            _eventBus.RoundEnded += OnRoundEnded;
            _eventBus.MatchEnded += OnMatchEnded;
            _eventBus.EnemyDied += OnEnemyDied;
            _eventBus.RoundStarted += OnRoundStarted;
        }

        public override void _ExitTree()
        {
            if (_eventBus == null || !GodotObject.IsInstanceValid(_eventBus)) return;
            _eventBus.RoundEnded -= OnRoundEnded;
            _eventBus.MatchEnded -= OnMatchEnded;
            _eventBus.EnemyDied -= OnEnemyDied;
            _eventBus.RoundStarted -= OnRoundStarted;
        }

        // --- Применение к игроку (вызывает PlayerBase при спавне) ---

        /// <summary>Накатывает текущие эффекты Камбэка/Дара Отчаяния на узел игрока.</summary>
        public void ReapplyTo(PlayerBase player)
        {
            if (player == null) return;
            int id = player.PlayerId;
            if (!Valid(id)) return;

            // Сброс контролируемых Камбэком полей к умолчанию.
            player.BaseDamageMultiplier = 1f;
            player.BaseSpeedMultiplier = 1f;
            player.BaseGoldMultiplier = 1f;
            player.BonusMaxHealth = 0f;
            player.OnDeathExplosion = false;
            player.HealOnKill = 0;
            player.AutoAimPercent = 0f;
            player.EchoShot = false;
            player.ShieldCharges = 0;

            bool wantRevive = false;

            // Дар Отчаяния (проигравший в 4м раунде).
            if (_desperation[id])
            {
                player.BaseDamageMultiplier *= 1.25f;
                player.BonusMaxHealth += 50f;
                wantRevive = true;
            }
            // Бремя Чести (победитель в 4м раунде) — старт с 80 HP.
            if (_honorBurden[id])
                player.BonusMaxHealth -= 20f;

            foreach (int itemId in _items[id])
                wantRevive |= ApplyItemFlags(player, itemId);

            player.ReviveOnce = wantRevive && !_reviveUsed[id];
        }

        /// <summary>Помечает возрождение использованным (вызывает PvP-арена).</summary>
        public void ConsumeRevive(int playerId)
        {
            if (Valid(playerId)) _reviveUsed[playerId] = true;
        }

        // Возвращает true, если предмет даёт возрождение.
        private bool ApplyItemFlags(PlayerBase p, int itemId)
        {
            switch (itemId)
            {
                case 1: p.BaseDamageMultiplier *= 1.35f; break;                 // Эликсир Ярости
                case 2: p.ShieldCharges = Mathf.Max(p.ShieldCharges, 2); break; // Щит Возмездия
                case 3: return true;                                            // Воля к Жизни
                case 4: p.OnDeathExplosion = true; break;                       // Проклятый Амулет
                case 5: p.HealOnKill = 12; break;                               // Кровавая Жажда
                case 6: p.BaseSpeedMultiplier *= 1.5f; break;                   // Молниеносный Рефлекс
                case 7: p.AutoAimPercent = 15f; break;                          // Глаз Охотника
                case 8: p.EchoShot = true; break;                              // Эхо Выстрела
                case 9: break;                                                  // Кристалл Удачи (разовый при выдаче)
                case 10: p.BaseGoldMultiplier *= 1.2f; break;                   // Золотая Лихорадка
            }
            return false;
        }

        // --- Сигналы ---

        private void OnRoundEnded(int winnerId)
        {
            if (!Valid(winnerId)) return;
            int loser = 1 - winnerId;
            int ww = _gameManager.WinCount[winnerId];
            int lw = _gameManager.WinCount[loser];

            // Эффекты Камбэка живут один раунд — чистим перед новой выдачей.
            ClearGrants();

            // Матч завершается: камбэка нет (нет следующего раунда). 3:0 обработает OnMatchEnded.
            if (ww >= GameManager.WinsToWin) return;

            int gap = ww - lw;
            if (gap <= 0) return; // проигравший не позади — без камбэка

            if (gap == 1)
            {
                int id = RandomFromPool(ComebackLevel.Small);
                GrantItem(loser, id);
                ShowInfo(id);
            }
            else
            {
                var level = gap == 2 ? ComebackLevel.Medium : ComebackLevel.Large;
                int gold = level == ComebackLevel.Medium ? 30 : 50;
                ShowChoice(loser, PickChoices(level, 3), gold);
            }
        }

        private void OnMatchEnded(int winnerId)
        {
            if (!Valid(winnerId)) return;
            int loser = 1 - winnerId;
            // «Либо пан, либо пропал» — только при чистом 3:0.
            if (_gameManager.WinCount[winnerId] >= GameManager.WinsToWin && _gameManager.WinCount[loser] == 0)
                ShowPan(winnerId);
        }

        private void OnEnemyDied(Vector2 position, int reward, int ownerPlayerId)
        {
            var p = FindPlayer(ownerPlayerId);
            if (p == null) return;

            if (p.HealOnKill > 0)
                p.Heal(p.HealOnKill);

            // Множитель золота (Оракул «Фортуна» + камбэк «Золотая Лихорадка»):
            // EconomyManager уже начислил базовую награду, добавляем разницу.
            if (p.GoldMultiplier > 1f)
            {
                int bonus = Mathf.FloorToInt(reward * (p.GoldMultiplier - 1f));
                if (bonus > 0) _economy.AddCurrency(ownerPlayerId, bonus);
            }
        }

        private void OnRoundStarted(int round)
        {
            if (round == 1) ClearGrants(); // старт нового матча
        }

        // --- Экраны ---

        private void ShowInfo(int itemId)
        {
            var screen = ComebackScreenScene.Instantiate<ComebackScreen>();
            AddChild(screen);
            screen.ShowInfo(_byId[itemId], 0);
        }

        private void ShowChoice(int loser, List<ComebackItem> choices, int gold)
        {
            var screen = ComebackScreenScene.Instantiate<ComebackScreen>();
            AddChild(screen);
            screen.ShowChoice(choices, gold, chosenId =>
            {
                GrantItem(loser, chosenId);
                _economy.AddCurrency(loser, gold);
            });
        }

        private void ShowPan(int winnerId)
        {
            var screen = PanScene.Instantiate<PanOrPropalo>();
            AddChild(screen);
            screen.Show(onYes: () => OnPanYes(winnerId), onNo: () => { });
        }

        private void OnPanYes(int winnerId)
        {
            int loser = 1 - winnerId;
            ClearGrants();
            _desperation[loser] = true;     // +50 HP, +25% урон, возрождение
            _honorBurden[winnerId] = true;  // старт с 80 HP
            GrantItem(loser, RandomFromPool(ComebackLevel.Large)); // случайный предмет из пула

            // Сразу в PvP — без PvE и магазина. Счёт остаётся 3:0.
            _gameManager.ChangePhase(GameManager.GamePhase.PvP);
        }

        // --- Выдача ---

        private void GrantItem(int playerId, int itemId)
        {
            if (!Valid(playerId)) return;
            if (!_items[playerId].Contains(itemId)) _items[playerId].Add(itemId);

            // Разовые эффекты при выдаче (не через ReapplyTo).
            if (itemId == 10) _economy.AddCurrency(playerId, 40);  // Золотая Лихорадка: +40g
            if (itemId == 9) _oracle?.SetGuaranteedBuff(playerId); // Кристалл Удачи
        }

        private void ClearGrants()
        {
            for (int id = 0; id < 2; id++)
            {
                _items[id].Clear();
                _desperation[id] = false;
                _honorBurden[id] = false;
                _reviveUsed[id] = false;
            }
        }

        // --- Вспомогательное ---

        private List<ComebackItem> Pool(ComebackLevel level)
        {
            var pool = new List<ComebackItem>();
            foreach (var item in _catalog)
                if (item.MinLevel <= level) pool.Add(item);
            return pool;
        }

        private int RandomFromPool(ComebackLevel level)
        {
            var pool = Pool(level);
            return pool[_rng.RandiRange(0, pool.Count - 1)].Id;
        }

        private List<ComebackItem> PickChoices(ComebackLevel level, int count)
        {
            var pool = Pool(level);
            var chosen = new List<ComebackItem>();
            while (chosen.Count < count && pool.Count > 0)
            {
                int idx = _rng.RandiRange(0, pool.Count - 1);
                chosen.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return chosen;
        }

        private PlayerBase FindPlayer(int playerId)
        {
            foreach (var node in GetTree().GetNodesInGroup("players"))
                if (node is PlayerBase p && p.PlayerId == playerId) return p;
            return null;
        }

        private static bool Valid(int playerId) => playerId is 0 or 1;

        // --- Каталог 10 предметов (COMEBACK.md) ---

        private void BuildCatalog()
        {
            Add(1, "Эликсир Ярости", "+35% урон на следующий раунд", "01_rage_elixir", ComebackLevel.Small);
            Add(2, "Щит Возмездия", "Поглощает первые 2 удара", "02_revenge_shield", ComebackLevel.Medium);
            Add(3, "Воля к Жизни", "Возрождение 1 раз с 35% HP (PvP)", "03_will_to_live", ComebackLevel.Large);
            Add(4, "Проклятый Амулет", "При смерти взрыв: 45 урона в r=120", "04_cursed_amulet", ComebackLevel.Medium);
            Add(5, "Кровавая Жажда", "Убийство моба в PvE = +12 HP", "05_blood_thirst", ComebackLevel.Small);
            Add(6, "Молниеносный Рефлекс", "+50% скорость на следующий раунд", "06_lightning_reflex", ComebackLevel.Small);
            Add(7, "Глаз Охотника", "+15% автоприцел снарядов", "07_hunter_eye", ComebackLevel.Medium);
            Add(8, "Эхо Выстрела", "Каждый выстрел дублируется под 15°", "08_echo_shot", ComebackLevel.Large);
            Add(9, "Кристалл Удачи", "Следующий Оракул гарантированно бафф", "09_luck_crystal", ComebackLevel.Medium);
            Add(10, "Золотая Лихорадка", "+40g и +20% золота от мобов в PvE", "10_gold_fever", ComebackLevel.Small);
        }

        private void Add(int id, string name, string desc, string iconFile, ComebackLevel minLevel)
        {
            var item = new ComebackItem
            {
                Id = id,
                Name = name,
                Description = desc,
                IconPath = $"res://assets/ui/comeback/comeback_{iconFile}.png",
                MinLevel = minLevel,
            };
            _catalog.Add(item);
            _byId[id] = item;
        }
    }
}
