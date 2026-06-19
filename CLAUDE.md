# CLAUDE.md — ChaosArena Project Context

> Этот файл читается автоматически Claude Code перед любой работой.
> Обновляй при изменении архитектуры или соглашений.
> Последнее обновление: июнь 2026

---

## 🎮 Проект в трёх предложениях

ChaosArena — сессионный PvPvE рогалик для двух игроков на Godot 4.6 (C# / .NET 8).
Игровой цикл: PvE-зачистка (60 сек) → Арена торговца (магазин + груша) → Оракул Хаоса (личный для каждого) → PvP-дуэль (3 жизни).
Сеть: P2P Host-Client через ENet + Godot RPC. Профили и лобби через Supabase (бесплатный тариф).

---

## 🛠️ Команды сборки и запуска

```bash
# Открыть проект
godot4 ~/chaos-arena/project.godot

# Собрать C# (из Godot: Project → Tools → C# → Build)
# Запустить: F5 в Godot
# Два окна для теста: запустить второй экземпляр через Debug → Run Second Instance

# Git
git add .
git commit -m "описание на русском"
git push
```

---

## 🎮 Игровой цикл

```
Главное меню → Лобби (хост F1 / клиент F2) →
  [синхронизация профилей и классов] →
PvE арена (60 сек, мобы, золото, секундомер) →
  [гонг + "ВРЕМЯ ЗАКУПОК" + телепорт] →
Арена торговца (магазин + груша, ждём "Готов" обоих, 15 сек таймаут) →
  [гонг + телепорт] →
Оракул Хаоса (КАЖДЫЙ крутит СВОЙ независимо) →
  [оба подтвердили → телепорт] →
PvP дуэль (3 жизни, колонны, порталы, бонусы, сужение после 60 сек) →
  [победа раунда → экран счёта] →
[повтор до 3 побед] →
  [если 3:0 → "Либо пан либо пропал"?] →
Экран победы/поражения → Главное меню
```

---

## 👤 СИСТЕМА КЛАССОВ

### Важно: Аватар профиля = Класс в игре
Игрок меняет аватар в профиле → меняется класс. Отдельного экрана выбора класса нет.

### 4 класса

| Класс | Аватар | HP | Скорость | Особенность |
|-------|--------|-----|----------|-------------|
| ⚔️ Воин | avatar_warrior.png | 130 | 90 | При HP < 30% урон +30% |
| 🛡️ Рыцарь | avatar_knight.png | 120 | 70 | Первые 2 удара за раунд поглощаются |
| 🔮 Маг | avatar_mage.png | 80 | 100 | Шанс бафф от Оракула +20% |
| 🗡️ Ассасин | avatar_rogue.png | 90 | 140 | Каждый 3й удар крит x2 |

### Оружие по классам (СТРОГО — другое нельзя носить)
Детали по оружиям обсуждаются отдельно. Архитектурно:
- Каждый предмет оружия в магазине имеет список `AllowedClasses`
- Если класс игрока не в списке → кнопка купить задизейблена + tooltip "Только для [класс]"
- Стартовое оружие фиксированное для каждого класса (определяется позже)

---

## 🃏 ОРАКУЛ ХАОСА — личный для каждого

### Ключевое правило
Каждый игрок крутит СВОЙ оракул независимо. Эффекты применяются только к тому кто крутил.

### Механика
```
1. Иконки мелькают в слоте быстро (icon_XX.png из assets/ui/oracle/icons/)
2. Замедляются → останавливаются
3. Выбранная иконка плавно увеличивается (Tween scale)
4. Иконка вставляется в центр card_template.png
5. Карта переворачивается (флип: scale X 1→0→1)
6. Спереди появляется полная карта (card_XX.png)
7. Фон экрана = card_revealed_bg.png
8. Название (Press Start 2P, золотой #FFD700)
9. Описание (белый текст)
10. Полоска типа: 🟢БАФФ / 🔴ДЕБАФФ / 🟣ХАОС
```

### Действия после результата
| Тип карты | Действие | Цена |
|-----------|----------|------|
| Бафф | Принять (бесплатно) или Перекрутить (50g, макс 2 раза) | — |
| Дебафф | Принять / Перекрутить 50g / Отправить сопернику 100g | — |
| Хаос | Принять или Перекрутить 50g. Отправить нельзя | — |

### Карты "на обоих" — переработать
Карты типа "Обмен инвентарём", "Телепорт обоих", "Болото -50% обоим" — при личном оракуле применяются только к владельцу карты. Карту "Обмен" переработать: получаешь копию оружия соперника на 1 раунд.

---

## 🏪 Арена торговца

- Отдельная сцена `ShopArena.tscn`
- NPC торговец — подойти + нажать E → ShopUI
- Груша в углу — бесконечное HP, показывает цифры урона (тест оружия)
- Ждём пока оба нажмут "Готов"
- Если один нажал → плашка "<ник> готов сразиться"
- 15 сек без ответа → автотелепорт
- Можно отменить "Готов"
- Магазин показывает только оружия доступные классу игрока

---

## ⚔️ PvP арена

- Симметричная арена
- Игрок 1 (синий) → левый верхний угол
- Игрок 2 (красный) → правый нижний угол
- 4 колонны (pvp_column.png) по углам — блокируют пули и движение
- 2 портала (pvp_portal.png) по диагонали — мгновенный телепорт
- Бонус (pvp_bonus_drop.png) падает каждые 25 сек по центру
- После 60 сек — стены сужаются каждые 10 сек на 1 тайл
- 3 жизни (heart_full/heart_empty.png в HUD)
- При смерти — возрождение + 3 сек неуязвимость (мигание)
- Секундомер — длительность раунда

### Бонусы по центру (рандом)
- ❤️ Аптечка +30 HP
- ⚡ Ускорение x1.5 на 8 сек
- 💥 Двойной урон на 6 сек
- 🛡️ Щит поглощает 1 выстрел
- 🌀 Телепорт в случайную точку

---

## 💀 Система Саботажа

- Покупается в магазине (отдельная вкладка)
- 1 саботаж за раунд
- Активируется кнопкой sabotage_button.png на HUD во время PvE
- Соперник НЕ знает — видит только эффект
- Все 12 саботажей в SABOTAGE.md

---

## 🔄 Система Камбэка

- Выдаётся автоматически проигравшему после каждого раунда
- Счёт 0:1 → 1 случайный предмет
- Счёт 0:2 → выбор из 3 + 30g
- Счёт 0:3 → выбор из 3 + 50g + скидка 50% в магазине
- Все 10 предметов в COMEBACK.md

### Либо пан либо пропал
- При счёте 3:0 победитель видит экран выбора (pan_or_propalo_bg.png)
- Таймер 15 сек (по умолчанию НЕТ)
- ДА → проигравший: +50HP, +25% урон, возрождение 1 раз
- ДА → победитель: -20 HP старт (Бремя Чести)
- Сразу PvP без PvE и магазина

---

## 👤 ПРОФИЛИ И SUPABASE

### Локальное сохранение
```
user://profile.json — всегда на компьютере игрока
При обновлении игры НЕ удаляется
```

### Структура профиля
```json
{
  "id": "uuid-уникальный",
  "nickname": "Player1",
  "avatar": 0,
  "class": "warrior",
  "stats": {
    "matches_played": 0,
    "matches_won": 0,
    "matches_lost": 0,
    "rounds_won": 0,
    "enemies_killed": 0,
    "gold_earned": 0,
    "sabotages_used": 0,
    "favorite_weapon": "fire_staff",
    "damage_dealt": 0,
    "damage_taken": 0
  }
}
```

### Supabase (бэкенд)
- Бесплатный тариф — хватит для старта
- Все профили дублируются в Supabase при создании и обновлении
- Активные лобби хранятся в Supabase (для Room Code системы)
- Разраб видит всё через Supabase Dashboard

### Room Code система (лобби)
```
Хост создаёт лобби → генерируется код CHAOS-XXXX →
Supabase хранит: код + IP хоста + статус →
Друг вводит код → Supabase возвращает IP →
P2P подключение через ENet
```

### Auto-updater
```
Запуск → HTTPRequest к GitHub Releases API →
Сравнить версию → есть новая? → диалог →
Скачать zip → распаковать → перезапустить
```
```
GET https://api.github.com/repos/jota2069/chaos-arena/releases/latest
```

---

## 🏛️ Архитектура

### Порядок автозагрузки (НЕ МЕНЯТЬ НИКОГДА)
```
1. EventBus
2. GameManager
3. EconomyManager
4. NetworkManager
```

### EventBus сигналы
| Сигнал | Параметры |
|--------|-----------|
| CurrencyChanged | (int playerId, int newAmount) |
| EnemyDied | (Vector2 position, int reward) |
| PhaseChanged | (int newPhase) |
| PlayerDied | (int playerId) |
| PlayerHealthChanged | (int playerId, float newHealth) |
| SabotagePurchased | (int buyerId, string type) |
| RoundWon | (int playerId) |
| MatchWon | (int playerId) |
| ClassChanged | (int playerId, string className) |
| OracleCardDrawn | (int playerId, int cardId) |
| SabotageActivated | (int targetPlayerId, string sabotageType) |

### Фазы игры (GamePhase enum)
```csharp
Lobby → PvE → ShopArena → ChaosOracle → PvP → RoundEnd → [повтор]
```

### Коллизионные слои
| Слой | Кто |
|------|-----|
| 1 | Игроки, враги, стены |
| 2 | Пули (Mask=1 и 4) |
| 4 | Хитбоксы врагов Area2D |

---

## 📁 Структура проекта

```
res://
├── autoload/
│   ├── EventBus.cs           # ПЕРВЫЙ. Глобальные сигналы.
│   ├── GameManager.cs        # ВТОРОЙ. State Machine фаз.
│   ├── EconomyManager.cs     # ТРЕТИЙ. Валюта.
│   └── NetworkManager.cs     # ЧЕТВЁРТЫЙ. ENet P2P порт 7000.
├── entities/
│   ├── player/
│   │   ├── PlayerBase.cs     # Базовый класс. HP, урон, класс.
│   │   ├── LocalPlayer.cs    # Локальный игрок. WASD, мышь.
│   │   └── RemotePlayer.cs   # Сетевой игрок. Интерполяция.
│   ├── weapons/
│   │   ├── WeaponBase.cs
│   │   ├── WeaponData.cs     # ScriptableObject: урон, класс, AllowedClasses[]
│   │   ├── Bullet.cs
│   │   └── RangedWeapon.cs
│   └── enemies/
│       ├── EnemyBase.cs
│       ├── SkeletonWarrior.cs
│       ├── ZombieBrute.cs
│       ├── Bat.cs
│       ├── GhostMage.cs
│       └── GiantSpider.cs
├── scenes/
│   ├── MapGenerator.cs
│   ├── ShopArena.tscn
│   └── PvpArena.tscn
├── systems/
│   ├── EnemySpawner.cs
│   ├── ClassSystem.cs        # TODO: логика классов
│   ├── ShopSystem.cs         # TODO
│   ├── ChaosOracle.cs        # TODO
│   ├── SabotageSystem.cs     # TODO
│   ├── ComebackSystem.cs     # TODO
│   └── SupabaseManager.cs    # TODO: профили + лобби
├── ui/
│   ├── HUD.cs / HUD.tscn
│   ├── ShopUI.cs             # TODO
│   ├── OracleUI.cs           # TODO
│   ├── MainMenu.cs           # TODO
│   └── ProfileScreen.cs      # TODO
└── assets/
    [см. ниже]
```

---

## 🖼️ Все пути к ассетам

```
assets/
├── characters/
│   ├── player_blue.png        # Синий воин спрайтшит (3 направления, 2 ряда!)
│   ├── player_red.png         # Красный воин спрайтшит (3 направления, 2 ряда!)
│   ├── merchant_npc.png
│   └── training_dummy.png
├── enemies/
│   ├── skeleton_warrior.png   # 32x32, idle(3)/run(5)/hurt(2)/death(3)
│   ├── zombie_brute.png       # 48x48, idle(3)/run(4)/hurt(2)/death(3)
│   ├── bat.png                # 24x24, idle(2)/fly(4)/hurt(2)/death(2)
│   ├── ghost_mage.png         # 32x32, idle(3)/move(4)/attack(3)/death(3)
│   └── giant_spider.png       # 40x40, idle(3)/run(5)/attack(2)/death(3)
├── weapons/
│   └── weapons_sheet.png      # 9 оружий в ряд (наклонные для руки)
├── projectiles/
│   ├── fireball.png
│   ├── ice_arrow.png
│   ├── lightning.png
│   ├── dark_orb.png
│   ├── bullet.png
│   ├── grenade.png
│   ├── portal_orb.png
│   └── energy_beam.png
├── pvp/
│   ├── pvp_bg.png
│   ├── pvp_column.png
│   ├── pvp_portal.png
│   └── pvp_bonus_drop.png
└── ui/
    ├── hud/
    │   ├── heart_full.png
    │   ├── heart_empty.png
    │   ├── timer_bg.png
    │   ├── sabotage_button.png
    │   ├── hp_bar_frame.png
    │   ├── hp_bar_fill.png
    │   └── coin.png
    ├── menu/
    │   ├── main_menu_bg.png
    │   ├── menu_logo.png
    │   ├── menu_button.png
    │   └── menu_panel_bg.png
    ├── profiles/
    │   ├── avatar_warrior.png  # Класс: Воин
    │   ├── avatar_mage.png     # Класс: Маг
    │   ├── avatar_rogue.png    # Класс: Ассасин
    │   └── avatar_knight.png   # Класс: Рыцарь
    ├── screens/
    │   ├── victory_screen_bg.png
    │   ├── defeat_screen_bg.png
    │   └── pan_or_propalo_bg.png
    ├── shop/
    │   ├── shop_background.png
    │   ├── shop_counter.png
    │   ├── ui/
    │   │   ├── shop_panel_bg.png
    │   │   ├── shop_item_slot.png
    │   │   ├── shop_button_buy.png
    │   │   ├── shop_button_sell.png
    │   │   └── shop_button_ready.png
    │   ├── weapons/
    │   │   ├── fire_staff.png
    │   │   ├── ice_crossbow.png
    │   │   ├── lightning_wand.png
    │   │   ├── necro_staff.png
    │   │   ├── shadow_dagger.png
    │   │   ├── sniper_musket.png
    │   │   ├── chaos_launcher.png
    │   │   ├── portal_gun.png
    │   │   └── mirror_shield.png
    │   ├── consumables/
    │   │   ├── health_potion.png
    │   │   ├── speed_potion.png
    │   │   ├── trap_bomb.png
    │   │   ├── smoke_grenade.png
    │   │   ├── gold_magnet.png
    │   │   └── arena_poison.png
    │   └── upgrades/
    │       ├── steel_armor.png
    │       ├── wind_boots.png
    │       ├── vampirism.png
    │       ├── luck_amulet.png
    │       ├── berserker_ring.png
    │       └── ricochet_gloves.png
    ├── sabotage/
    │   ├── sabotage_01_eclipse.png
    │   ├── sabotage_02_invasion.png
    │   ├── sabotage_03_ice_floor.png
    │   ├── sabotage_04_spider_web.png
    │   ├── sabotage_05_minefield.png
    │   ├── sabotage_06_tornado.png
    │   ├── sabotage_07_rats.png
    │   ├── sabotage_08_gravity_flip.png
    │   ├── sabotage_09_electroshock.png
    │   ├── sabotage_10_hallucinations.png
    │   ├── sabotage_11_gold_magnet.png
    │   └── sabotage_12_giant_curse.png
    ├── comeback/
    │   ├── comeback_01_rage_elixir.png
    │   ├── comeback_02_revenge_shield.png
    │   ├── comeback_03_will_to_live.png
    │   ├── comeback_04_cursed_amulet.png
    │   ├── comeback_05_blood_thirst.png
    │   ├── comeback_06_lightning_reflex.png
    │   ├── comeback_07_hunter_eye.png
    │   ├── comeback_08_echo_shot.png
    │   ├── comeback_09_luck_crystal.png
    │   └── comeback_10_gold_fever.png
    └── oracle/
        ├── ui/
        │   ├── oracle_slot_frame.png
        │   ├── oracle_screen_bg.png
        │   ├── card_template.png
        │   └── card_revealed_bg.png
        ├── icons/
        │   ├── icon_01_king_gold.png
        │   └── ... (до icon_20)
        └── cards/
            ├── card_01_king_gold.png
            └── ... (до card_20)
```

---

## 🖥️ HUD

- HPFill — ColorRect, ширина через OffsetRight, BarWidth=200f
- HPFrame — TextureRect hp_bar_frame.png поверх
- CurrencyLabel — Label + CoinIcon (coin.png)
- ClassIcon — иконка текущего класса
- Hearts — 3x heart_full/heart_empty (только в PvP)
- Timer — Label на фоне timer_bg.png
- SabotageButton — sabotage_button.png (только в PvE если куплен)
- Шрифт: Press Start 2P
- Цвета: золотой #FFD700, фиолетовый #1A0A2E

---

## 🚫 Жёсткие ограничения

- НИКОГДА не менять порядок автозагрузки
- НИКОГДА не добавлять прямые ссылки между системами — только EventBus
- ВСЕГДА отписываться от EventBus в _ExitTree()
- ВСЕГДА проверять IsDead перед TakeDamage
- ВСЕГДА CallDeferred для поиска узлов после _Ready
- Класс HUD = HUD (не Hud, не hud)
- EnemySpawner путь = /root/Main/EnemySpawner
- Motion Mode CharacterBody2D = Floating
- Спрайтшиты персонажей — 2 ряда вместо 1, учитывать при нарезке!

---

## ⚠️ Известные подводные камни

1. HUD класс ОБЯЗАТЕЛЬНО `HUD` заглавными
2. MapGenerator → EnemySpawner путь `/root/Main/EnemySpawner`
3. EventBus двойная подписка → флаг `_subscribed`
4. TextureProgressBar не работает для HP → ColorRect + OffsetRight
5. Motion Mode = Floating для top-down
6. Первая волна спавнится сразу в SetSpawnPoints()
7. Спрайтшиты — 2 ряда вместо 1, учитывать Hframes/Vframes
8. Классы оружий проверять до показа в магазине

---

## 📐 Соглашения

```
Namespace:   ChaosArena.entities.player / ChaosArena.ui / ChaosArena.systems
Приватные:   _camelCase
Публичные:   PascalCase
Коммиты:     на русском языке
Комментарии: на русском языке
Шрифт UI:    Press Start 2P (Google Fonts)
Цвета:       #FFD700 золотой, #1A0A2E фиолетовый
Версия:      public const string GameVersion = "1.0.0";
```

---

## ✅ Статус

```
ГОТОВО:
✅ EventBus, GameManager, EconomyManager, NetworkManager
✅ MapGenerator (BSP, комнаты + коридоры)
✅ LocalPlayer (WASD, статичный спрайт пока)
✅ BasicEnemy (AI, knockback, кулдаун 0.8 сек)
✅ EnemySpawner (волны каждые 12 сек, макс 8)
✅ Bullet (стрельба к мыши, флаг _hasHit)
✅ HUD (HP бар ColorRect + Gold Label)
✅ Коллизии стен
✅ Все ассеты сгенерированы и разложены по папкам

TODO (по приоритету — сначала рабочий цикл, потом красота):

ЭТАП 1 — Игровой цикл end-to-end:
⬜ Игровой цикл: PvE таймер (60 сек) → гонг → телепорт в ShopArena
⬜ ShopArena.tscn: NPC + E + ShopUI базовый + груша
⬜ Магазин: все 21 предмет из SHOP.md + фильтр по классу
⬜ Оракул Хаоса: личный для каждого, слот + флип + карта + 20 карт
⬜ PvP арена: колонны, порталы, бонусы, сужение, 3 жизни
⬜ Счёт раундов + экраны результатов
⬜ Система Саботажа: 12 эффектов из SABOTAGE.md
⬜ Система Камбэка: 10 предметов + "Либо пан либо пропал"

ЭТАП 2 — Сеть:
⬜ Лобби (хост/клиент, синхронизация профилей и классов)
⬜ Синхронизация PvE (каждый на своей арене, саботаж через RPC)
⬜ Синхронизация магазина (готов/не готов)
⬜ Синхронизация оракула (личный, но результат виден обоим)
⬜ Синхронизация PvP (позиции, HP, жизни, победа)

ЭТАП 3 — Профили и Supabase:
⬜ Главное меню + профили + аватар = класс
⬜ Локальное сохранение user://profile.json
⬜ Supabase: регистрация профиля, Room Code лобби
⬜ Auto-updater через GitHub Releases API
⬜ Статистика (обновляется после каждого матча)

ЭТАП 4 — Визуал и звук:
⬜ AnimatedSprite2D персонажей + мобов
⬜ WeaponHolder + вращение к мыши + отдача
⬜ Снаряды с GPUParticles2D + PointLight2D
⬜ FloatingDamage цифры урона
⬜ Шейдер мигания при уроне
⬜ Звуки и музыка (6 треков + все SFX из AUDIO.md)

ЭТАП 5 — Полировка:
⬜ Таблица лидеров (Supabase)
⬜ Сезонная система
⬜ Зрительский режим
⬜ Балансировка классов и оружий
```
