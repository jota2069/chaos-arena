using Godot;
using System.Collections.Generic;
using ChaosArena.autoload;

namespace ChaosArena.systems
{
    /// <summary>Категория предмета магазина.</summary>
    public enum ShopItemType
    {
        Weapon,      // оружие (вкладка ОРУЖИЕ), занимает слот 0 (PvE) или 1 (PvP)
        Consumable,  // расходник (вкладка РАСХОДНИКИ)
        Upgrade,     // улучшение (вкладка РАСХОДНИКИ), часть стакается
        Sabotage     // саботаж (вкладка САБОТАЖ)
    }

    /// <summary>Данные одного товара магазина (read-only описание из SHOP.md).</summary>
    public sealed class ShopItem
    {
        public string Id;
        public string Name;
        public int Price;
        public ShopItemType Type;
        public int WeaponSlot;     // 0=PvE, 1=PvP; -1 для не-оружия
        public string Description;
        public string IconPath;
        public int MaxStack;       // сколько штук можно держать (оружие=1, броня=3, и т.д.)
    }

    /// <summary>
    /// Логика магазина (ШЕСТОЙ автолоад, после SceneLoader). Хранит каталог всех
    /// 21 товара из SHOP.md, инвентарь каждого игрока и сгенерированный набор
    /// товаров на раунд. Покупка/продажа идут через EconomyManager. Состояние —
    /// автолоад, чтобы переживать смену сцен (магазин -> бой -> снова магазин).
    /// </summary>
    public partial class ShopSystem : Node
    {
        // Каталог: id -> данные товара.
        private readonly Dictionary<string, ShopItem> _catalog = new();

        // Инвентарь на игрока (0/1). Для оружия — id в слоте, для остального — счётчики.
        private readonly PlayerInventory[] _inventory =
        {
            new PlayerInventory(), new PlayerInventory()
        };

        // Кэш сгенерированного набора товаров: "player:round" -> список.
        private readonly Dictionary<string, List<ShopItem>> _offerCache = new();

        private readonly System.Random _rng = new();

        public override void _Ready()
        {
            BuildCatalog();
        }

        // --- Публичный доступ ---

        /// <summary>Все товары категории (для справки/будущего полного каталога).</summary>
        public IReadOnlyDictionary<string, ShopItem> Catalog => _catalog;

        public ShopItem GetItem(string id) => _catalog.GetValueOrDefault(id);

        /// <summary>
        /// Набор товаров для игрока на данный раунд (3 — раунд 1, 4 — раунд 2,
        /// 5 — раунд 3+). Генерируется один раз и кэшируется, чтобы повторное
        /// открытие магазина показывало те же товары.
        /// </summary>
        public List<ShopItem> GetOffer(int playerId, int round)
        {
            string key = $"{playerId}:{round}";
            if (!_offerCache.TryGetValue(key, out var offer))
            {
                offer = GenerateOffer(playerId, round);
                _offerCache[key] = offer;
            }
            return offer;
        }

        /// <summary>Игрок уже владеет этим предметом (для кнопки ПРОДАТЬ).</summary>
        public bool Owns(int playerId, string itemId)
        {
            var item = GetItem(itemId);
            if (item == null || !IsValidPlayer(playerId)) return false;

            if (item.Type == ShopItemType.Weapon)
                return _inventory[playerId].WeaponSlots[item.WeaponSlot] == itemId;

            return _inventory[playerId].Counts.GetValueOrDefault(itemId) > 0;
        }

        /// <summary>Сколько единиц предмета у игрока (для оружия: 0 или 1).</summary>
        public int OwnedCount(int playerId, string itemId)
        {
            if (!IsValidPlayer(playerId)) return 0;
            var item = GetItem(itemId);
            if (item == null) return 0;
            if (item.Type == ShopItemType.Weapon)
                return _inventory[playerId].WeaponSlots[item.WeaponSlot] == itemId ? 1 : 0;
            return _inventory[playerId].Counts.GetValueOrDefault(itemId);
        }

        /// <summary>Цена продажи — 50% от покупки (округление вниз).</summary>
        public int GetSellPrice(string itemId)
        {
            var item = GetItem(itemId);
            return item == null ? 0 : item.Price / 2;
        }

        /// <summary>
        /// Можно ли купить ещё одну единицу (без учёта золота — это проверяет
        /// EconomyManager при покупке). Ограничено стаком/слотом.
        /// </summary>
        public bool CanBuyMore(int playerId, string itemId)
        {
            var item = GetItem(itemId);
            if (item == null || !IsValidPlayer(playerId)) return false;

            if (item.Type == ShopItemType.Weapon)
                // нельзя «купить» оружие, которое уже стоит в своём слоте
                return _inventory[playerId].WeaponSlots[item.WeaponSlot] != itemId;

            return _inventory[playerId].Counts.GetValueOrDefault(itemId) < item.MaxStack;
        }

        /// <summary>
        /// Покупка: списывает золото через EconomyManager и кладёт предмет в
        /// инвентарь. Возвращает false если стак переполнен или не хватает золота.
        /// </summary>
        public bool Buy(int playerId, string itemId)
        {
            if (!CanBuyMore(playerId, itemId)) return false;

            var item = GetItem(itemId);
            var economy = GetNodeOrNull<EconomyManager>("/root/EconomyManager");
            if (economy == null || !economy.SpendCurrency(playerId, item.Price))
                return false;

            if (item.Type == ShopItemType.Weapon)
                // оружие заменяет текущее в своём слоте
                _inventory[playerId].WeaponSlots[item.WeaponSlot] = itemId;
            else
                _inventory[playerId].Counts[itemId] =
                    _inventory[playerId].Counts.GetValueOrDefault(itemId) + 1;

            return true;
        }

        /// <summary>
        /// Продажа: возвращает предмет из инвентаря и начисляет 50% цены.
        /// </summary>
        public bool Sell(int playerId, string itemId)
        {
            if (!Owns(playerId, itemId)) return false;

            var item = GetItem(itemId);
            if (item.Type == ShopItemType.Weapon)
            {
                _inventory[playerId].WeaponSlots[item.WeaponSlot] = null;
            }
            else
            {
                int n = _inventory[playerId].Counts.GetValueOrDefault(itemId);
                if (n <= 1) _inventory[playerId].Counts.Remove(itemId);
                else _inventory[playerId].Counts[itemId] = n - 1;
            }

            GetNodeOrNull<EconomyManager>("/root/EconomyManager")?
                .AddCurrency(playerId, GetSellPrice(itemId));
            return true;
        }

        /// <summary>Сбрасывает инвентари и кэш товаров (на новый матч).</summary>
        public void ResetForNewMatch()
        {
            _inventory[0] = new PlayerInventory();
            _inventory[1] = new PlayerInventory();
            _offerCache.Clear();
        }

        // --- Генерация набора товаров ---

        // 1 оружие + 1 расходник + остальное случайно из не-оружия,
        // исключая уже купленные (переполненные по стаку) улучшения.
        private List<ShopItem> GenerateOffer(int playerId, int round)
        {
            int total = round <= 1 ? 3 : round == 2 ? 4 : 5;
            var offer = new List<ShopItem>();

            var weapons = ItemsOfType(ShopItemType.Weapon);
            var consumables = ItemsOfType(ShopItemType.Consumable);

            // Пул для случайного добора: всё не-оружие, кроме переполненных по стаку.
            var fillPool = new List<ShopItem>();
            foreach (var item in _catalog.Values)
                if (item.Type != ShopItemType.Weapon && CanBuyMore(playerId, item.Id))
                    fillPool.Add(item);

            // 1) гарантированное оружие
            AddRandom(offer, weapons);
            // 2) гарантированный расходник
            AddRandom(offer, consumables);
            // 3) добор случайными не-оружейными товарами без повторов
            fillPool.RemoveAll(i => offer.Contains(i));
            while (offer.Count < total && fillPool.Count > 0)
            {
                int idx = _rng.Next(fillPool.Count);
                offer.Add(fillPool[idx]);
                fillPool.RemoveAt(idx);
            }

            return offer;
        }

        // Берёт случайный предмет из списка и добавляет в набор (без повтора).
        private void AddRandom(List<ShopItem> offer, List<ShopItem> pool)
        {
            var available = pool.FindAll(i => !offer.Contains(i));
            if (available.Count == 0) return;
            offer.Add(available[_rng.Next(available.Count)]);
        }

        private List<ShopItem> ItemsOfType(ShopItemType type)
        {
            var list = new List<ShopItem>();
            foreach (var item in _catalog.Values)
                if (item.Type == type) list.Add(item);
            return list;
        }

        private bool IsValidPlayer(int playerId) => playerId >= 0 && playerId < _inventory.Length;

        // --- Каталог (все 21 товара из SHOP.md) ---

        private void BuildCatalog()
        {
            // Оружие слота 0 (PvE)
            Add("fire_staff", "Огненный Посох", 80, ShopItemType.Weapon, 0,
                "Поджигает врага: +5 урона/сек на 3 сек", "weapons/fire_staff.png");
            Add("ice_crossbow", "Ледяной Арбалет", 95, ShopItemType.Weapon, 0,
                "30% шанс заморозить врага на 1.5 сек", "weapons/ice_crossbow.png");
            Add("lightning_wand", "Молниевый Жезл", 110, ShopItemType.Weapon, 0,
                "Молния прыгает на 2 врагов (50% урона)", "weapons/lightning_wand.png");
            Add("necro_staff", "Посох Некромансера", 140, ShopItemType.Weapon, 0,
                "25% шанс поднять зомби на 8 сек", "weapons/necro_staff.png");

            // Оружие слота 1 (PvP)
            Add("shadow_dagger", "Теневой Кинжал", 90, ShopItemType.Weapon, 1,
                "Каждый 5й удар — крит x2.5", "weapons/shadow_dagger.png");
            Add("sniper_musket", "Снайперский Мушкет", 120, ShopItemType.Weapon, 1,
                "Пробивает стены 1 раз, нет разброса", "weapons/sniper_musket.png");
            Add("chaos_launcher", "Гранатомёт Хаоса", 130, ShopItemType.Weapon, 1,
                "AoE взрыв радиус 80px, knockback", "weapons/chaos_launcher.png");
            Add("portal_gun", "Портальный Пистолет", 160, ShopItemType.Weapon, 1,
                "Пуля телепортируется за соперника", "weapons/portal_gun.png");
            Add("mirror_shield", "Зеркальный Щит", 100, ShopItemType.Weapon, 1,
                "Раз в 5 сек отражает снаряд", "weapons/mirror_shield.png");

            // Расходники
            Add("health_potion", "Зелье Здоровья", 40, ShopItemType.Consumable, -1,
                "+40 HP мгновенно (Q)", "consumables/health_potion.png", maxStack: 5);
            Add("speed_potion", "Зелье Скорости", 50, ShopItemType.Consumable, -1,
                "+80% скорость на 8 сек (Q)", "consumables/speed_potion.png", maxStack: 5);
            Add("trap_bomb", "Бомба-Ловушка", 60, ShopItemType.Consumable, -1,
                "Взрывается при наступании (50 урона)", "consumables/trap_bomb.png", maxStack: 5);
            Add("smoke_grenade", "Дымовая Граната", 75, ShopItemType.Consumable, -1,
                "Невидимость на 4 сек (только PvP)", "consumables/smoke_grenade.png", maxStack: 5);
            Add("gold_magnet", "Магнит Золота", 35, ShopItemType.Consumable, -1,
                "+25% золота от мобов следующий PvE", "consumables/gold_magnet.png", maxStack: 3);

            // Саботаж (в SHOP.md помечен как Тип: Саботаж)
            Add("arena_poison", "Яд Арены", 90, ShopItemType.Sabotage, -1,
                "Соперник получает 3 урона/сек весь PvE", "consumables/arena_poison.png", maxStack: 1);

            // Улучшения
            Add("steel_armor", "Стальная Броня", 70, ShopItemType.Upgrade, -1,
                "+25 максимальное HP (до 3 раз)", "upgrades/steel_armor.png", maxStack: 3);
            Add("wind_boots", "Сапоги Ветра", 65, ShopItemType.Upgrade, -1,
                "+30% скорость навсегда", "upgrades/wind_boots.png");
            Add("vampirism", "Вампиризм", 100, ShopItemType.Upgrade, -1,
                "15% от урона восстанавливает HP", "upgrades/vampirism.png");
            Add("luck_amulet", "Амулет Удачи", 85, ShopItemType.Upgrade, -1,
                "+40% шанс бафф от Оракула", "upgrades/luck_amulet.png");
            Add("berserker_ring", "Кольцо Берсерка", 110, ShopItemType.Upgrade, -1,
                "При HP < 30% урон +50%", "upgrades/berserker_ring.png");
            Add("ricochet_gloves", "Перчатки Рикошета", 95, ShopItemType.Upgrade, -1,
                "Пули рикошетят от стен 1 раз", "upgrades/ricochet_gloves.png");
        }

        private void Add(string id, string name, int price, ShopItemType type, int slot,
                         string desc, string iconFile, int maxStack = 1)
        {
            _catalog[id] = new ShopItem
            {
                Id = id,
                Name = name,
                Price = price,
                Type = type,
                WeaponSlot = slot,
                Description = desc,
                IconPath = $"res://assets/ui/shop/{iconFile}",
                MaxStack = maxStack,
            };
        }

        // Инвентарь одного игрока.
        private sealed class PlayerInventory
        {
            // id оружия в слоте 0 (PvE) и 1 (PvP), либо null.
            public readonly string[] WeaponSlots = new string[2];
            // расходники/улучшения/саботаж: id -> количество.
            public readonly Dictionary<string, int> Counts = new();
        }
    }
}
