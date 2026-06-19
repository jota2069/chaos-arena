using Godot;
using System.Collections.Generic;
using ChaosArena.autoload;
using ChaosArena.entities.enemies;
using ChaosArena.entities.player;

namespace ChaosArena.systems
{
    /// <summary>Данные одного саботажа (из SABOTAGE.md).</summary>
    public sealed class SabotageData
    {
        public string Id;
        public string Name;
        public int Price;
        public string Description;
        public string IconPath;
    }

    /// <summary>
    /// Система Саботажа (ДЕВЯТЫЙ автолоад). Хранит 12 саботажей, состояние покупки
    /// и активации (1 саботаж за раунд), и применяет эффект на арене СОПЕРНИКА.
    /// Покупка идёт через магазин (вкладка САБОТАЖ), активация — кнопкой на HUD в PvE.
    ///
    /// В оффлайне арена соперника отсутствует, поэтому эффект применяется к
    /// доступному игроку (для теста). Сетевой проброс через RPC — позже (ЭТАП 2).
    /// </summary>
    public partial class SabotageSystem : Node
    {
        private static readonly PackedScene EnemyScene =
            GD.Load<PackedScene>("res://entities/enemies/BasicEnemy.tscn");

        private readonly List<SabotageData> _all = new();
        private readonly Dictionary<string, SabotageData> _byId = new();

        // Состояние на раунд: что куплено (null = ничего) и активировано ли.
        private readonly string[] _purchasedId = new string[2];
        private readonly bool[] _activated = new bool[2];

        private readonly RandomNumberGenerator _rng = new();

        private EventBus _eventBus;
        private EconomyManager _economy;

        public override void _Ready()
        {
            _rng.Randomize();
            _eventBus = GetNode<EventBus>("/root/EventBus");
            _economy = GetNode<EconomyManager>("/root/EconomyManager");

            BuildCatalog();
            _eventBus.RoundStarted += OnRoundStarted;
        }

        public override void _ExitTree()
        {
            if (_eventBus != null && GodotObject.IsInstanceValid(_eventBus))
                _eventBus.RoundStarted -= OnRoundStarted;
        }

        // --- Доступ для магазина / HUD ---

        public IReadOnlyList<SabotageData> All => _all;
        public SabotageData Get(string id) => _byId.GetValueOrDefault(id);
        public bool CanBuy(int playerId) => Valid(playerId) && _purchasedId[playerId] == null;
        public string PurchasedId(int playerId) => Valid(playerId) ? _purchasedId[playerId] : null;
        public bool HasUnused(int playerId) =>
            Valid(playerId) && _purchasedId[playerId] != null && !_activated[playerId];

        /// <summary>Покупка саботажа (1 за раунд). Списывает золото через EconomyManager.</summary>
        public bool Buy(int playerId, string id)
        {
            if (!CanBuy(playerId)) return false;
            var data = Get(id);
            if (data == null || !_economy.SpendCurrency(playerId, data.Price)) return false;

            _purchasedId[playerId] = id;
            _activated[playerId] = false;
            _eventBus.EmitSignal(EventBus.SignalName.SabotagePurchased, playerId, 1 - playerId, id);
            return true;
        }

        /// <summary>Активация: применяет купленный саботаж к сопернику. Один раз за раунд.</summary>
        public void Activate(int playerId)
        {
            if (!HasUnused(playerId)) return;
            string id = _purchasedId[playerId];
            _activated[playerId] = true;
            ApplyEffect(id, 1 - playerId);
        }

        private void OnRoundStarted(int round)
        {
            _purchasedId[0] = null;
            _purchasedId[1] = null;
            _activated[0] = false;
            _activated[1] = false;
        }

        // --- Применение эффектов ---

        /// <summary>Применяет саботаж <paramref name="id"/> к игроку <paramref name="targetId"/>.</summary>
        public void ApplyEffect(string id, int targetId)
        {
            var arena = GetTree().CurrentScene;
            if (arena == null) return;

            var target = FindTarget(targetId);
            int tid = target?.PlayerId ?? targetId;
            Vector2 around = target?.GlobalPosition ?? Vector2.Zero;

            switch (id)
            {
                case "eclipse":
                    Darkness(arena, target, 20f);
                    break;
                case "invasion":
                    SpawnEnemies(arena, around, tid, count: 3, hpMult: 2f, dmgMult: 1.5f, lifetime: 0f);
                    break;
                case "ice_floor":
                    target?.MakeSlippery(15f);
                    break;
                case "spider_web":
                    for (int i = 0; i < 5; i++)
                        arena.AddChild(SabotageZone.Create(SabotageZone.ZoneKind.Web, ScatterAround(around, 80f, 260f), tid));
                    break;
                case "minefield":
                    for (int i = 0; i < 5; i++)
                        arena.AddChild(SabotageZone.Create(SabotageZone.ZoneKind.Mine, ScatterAround(around, 60f, 240f), tid));
                    break;
                case "tornado":
                    arena.AddChild(SabotageChaser.Create(SabotageChaser.ChaserKind.Tornado, ScatterAround(around, 60f, 160f), tid));
                    break;
                case "rats":
                    for (int i = 0; i < 10; i++)
                        arena.AddChild(SabotageChaser.Create(SabotageChaser.ChaserKind.Rat, ScatterAround(around, 100f, 300f), tid));
                    break;
                case "gravity_flip":
                    InvertControls(target, 10f);
                    break;
                case "electroshock":
                    target?.TakeDamage(15f);
                    target?.Stun(2f);
                    break;
                case "hallucinations":
                    SpawnEnemies(arena, around, tid, count: 5, hpMult: 1f, dmgMult: 0f, lifetime: 15f);
                    break;
                case "gold_magnet":
                    StealGold(targetId, 0.25f);
                    break;
                case "giant_curse":
                    EmpowerAllEnemies(2f, 2f);
                    break;
            }
        }

        private void SpawnEnemies(Node arena, Vector2 around, int ownerId, int count,
                                  float hpMult, float dmgMult, float lifetime)
        {
            if (EnemyScene == null) return;

            for (int i = 0; i < count; i++)
            {
                var enemy = EnemyScene.Instantiate<BasicEnemy>();
                enemy.MaxHealth *= hpMult;           // до AddChild -> _Ready задаст CurrentHealth
                enemy.ContactDamage *= dmgMult;      // dmgMult=0 => галлюцинации не бьют
                enemy.OwnerPlayerId = ownerId;
                if (dmgMult <= 0f)
                    enemy.Modulate = new Color(0.7f, 0.7f, 1f, 0.85f); // призрачный вид
                enemy.GlobalPosition = ScatterAround(around, 100f, 200f);
                arena.AddChild(enemy);

                if (lifetime > 0f)
                {
                    var ghost = enemy;
                    var timer = GetTree().CreateTimer(lifetime);
                    timer.Timeout += () => { if (GodotObject.IsInstanceValid(ghost)) ghost.QueueFree(); };
                }
            }
        }

        // Затмение: затемнение мира + световой круг вокруг цели на N секунд.
        private void Darkness(Node arena, PlayerBase target, float seconds)
        {
            var canvasMod = new CanvasModulate { Color = new Color(0.26f, 0.24f, 0.34f) };
            arena.AddChild(canvasMod);

            PointLight2D light = null;
            if (target != null)
            {
                light = new PointLight2D
                {
                    Texture = SabotageFx.MakeLightTexture(),
                    TextureScale = 1.2f,
                    Energy = 1.3f,
                    Color = new Color(1f, 0.95f, 0.85f),
                };
                target.AddChild(light);
            }

            var timer = GetTree().CreateTimer(seconds);
            timer.Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(canvasMod)) canvasMod.QueueFree();
                if (light != null && GodotObject.IsInstanceValid(light)) light.QueueFree();
            };
        }

        private void InvertControls(PlayerBase target, float seconds)
        {
            if (target == null) return;
            target.InvertControls = true;
            var timer = GetTree().CreateTimer(seconds);
            timer.Timeout += () => { if (GodotObject.IsInstanceValid(target)) target.InvertControls = false; };
        }

        private void StealGold(int targetId, float fraction)
        {
            int balance = _economy.GetBalance(targetId);
            int amount = Mathf.FloorToInt(balance * fraction);
            if (amount > 0) _economy.SpendCurrency(targetId, amount);
        }

        private void EmpowerAllEnemies(float hpMult, float scaleMult)
        {
            var seen = new HashSet<ulong>();
            foreach (var node in GetTree().GetNodesInGroup("enemy_hitboxes"))
            {
                if (node is Node hb && hb.GetParent() is EnemyBase enemy && seen.Add(enemy.GetInstanceId()))
                    enemy.Empower(hpMult, scaleMult);
            }
        }

        // --- Вспомогательное ---

        // Цель: игрок с нужным id, иначе любой доступный (оффлайн-тест).
        private PlayerBase FindTarget(int targetId)
        {
            PlayerBase any = null;
            foreach (var node in GetTree().GetNodesInGroup("players"))
                if (node is PlayerBase p)
                {
                    if (p.PlayerId == targetId) return p;
                    any = p;
                }
            return any;
        }

        private Vector2 ScatterAround(Vector2 center, float minR, float maxR)
        {
            float angle = _rng.RandfRange(0f, Mathf.Tau);
            float dist = _rng.RandfRange(minR, maxR);
            return center + Vector2.Right.Rotated(angle) * dist;
        }

        private static bool Valid(int playerId) => playerId is 0 or 1;

        // --- Каталог 12 саботажей (SABOTAGE.md) ---

        private void BuildCatalog()
        {
            Add("eclipse", "Затмение", 60, "Свет на арене соперника гаснет на 20 сек", "sabotage_01_eclipse");
            Add("invasion", "Нашествие", 75, "3 жирных моба (x2 HP, x1.5 урон) на арену соперника", "sabotage_02_invasion");
            Add("ice_floor", "Ледяной Пол", 55, "Пол соперника скользкий 15 сек (инерция)", "sabotage_03_ice_floor");
            Add("spider_web", "Паутина", 50, "5 зон паутины: -70% скорость на 3 сек", "sabotage_04_spider_web");
            Add("minefield", "Минное Поле", 90, "5 невидимых мин: 20 урона + оглушение", "sabotage_05_minefield");
            Add("tornado", "Торнадо", 80, "Торнадо 12 сек: 10 урона + отбрасывание", "sabotage_06_tornado");
            Add("rats", "Крысиное Нашествие", 45, "10 быстрых крыс: 2 урона при касании", "sabotage_07_rats");
            Add("gravity_flip", "Гравитационный Переворот", 100, "Управление соперника инвертируется 10 сек", "sabotage_08_gravity_flip");
            Add("electroshock", "Электрошок", 70, "15 урона + оглушение соперника 2 сек", "sabotage_09_electroshock");
            Add("hallucinations", "Галлюцинации", 85, "5 фантомных мобов (не наносят урон) 15 сек", "sabotage_10_hallucinations");
            Add("gold_magnet", "Воровской Магнит", 65, "Соперник теряет 25% золота", "sabotage_11_gold_magnet");
            Add("giant_curse", "Проклятие Великана", 110, "Все мобы соперника x2 размер и x2 HP", "sabotage_12_giant_curse");
        }

        private void Add(string id, string name, int price, string desc, string iconFile)
        {
            var data = new SabotageData
            {
                Id = id,
                Name = name,
                Price = price,
                Description = desc,
                IconPath = $"res://assets/ui/sabotage/{iconFile}.png",
            };
            _all.Add(data);
            _byId[id] = data;
        }
    }
}
