# CLAUDE.md — ChaosArena Project Context

> Этот файл читается автоматически. Обновляй при изменении архитектуры или соглашений.

---

## 🎮 Проект в трёх предложениях

ChaosArena — сессионный PvPvE рогалик для двух игроков на Godot 4.6 (C# / .NET 8).
Игровой цикл: PvE-зачистка → Магазин → Рулетка Хаоса → PvP-дуэль.
Сеть: P2P Host-Client через ENet + Godot RPC.

---

## 🛠️ Команды сборки и запуска

```bash
# Открыть проект в Godot
godot4 ~/chaos-arena/project.godot

# Собрать C# проект (из Godot: Project → Tools → C# → Build)
# Или через Rider: Ctrl+F9

# Запустить игру
# F5 в Godot или кнопка Play

# Git
git add .
git commit -m "описание на русском"
git push
```

---

## 📁 Структура проекта

```
res://
├── autoload/           # Синглтоны (namespace ChaosArena.autoload) — грузятся первыми
│   ├── EventBus.cs     # ПЕРВЫЙ в автозагрузке. Глобальные сигналы.
│   ├── GameManager.cs  # ВТОРОЙ. State Machine фаз игры.
│   ├── EconomyManager.cs # ТРЕТИЙ. Валюта и транзакции.
│   └── NetworkManager.cs # ЧЕТВЁРТЫЙ. ENet P2P, RPC фаз и позиций.
├── entities/
│   ├── player/
│   │   ├── PlayerBase.cs       # abstract CharacterBody2D. HP, оружие, смерть.
│   │   ├── LocalPlayer.cs      # Ввод WASD, стрельба ЛКМ, анимация.
│   │   └── RemotePlayer.cs     # Интерполяция по RPC (TODO: сеть).
│   ├── weapons/
│   │   ├── WeaponBase.cs       # abstract Node2D. GetDamage(phase).
│   │   ├── Bullet.cs           # Area2D. CollisionLayer=2, Mask=1|4.
│   │   └── RangedWeapon.cs     # Реализация дальнобойного оружия.
│   └── enemies/
│       ├── EnemyBase.cs        # abstract CharacterBody2D. HP, смерть, награда.
│       └── BasicEnemy.cs       # AI: преследование, knockback, кулдаун атаки.
├── scenes/
│   └── MapGenerator.cs         # TileMapLayer. BSP-генерация подземелья.
├── systems/
│   ├── EnemySpawner.cs         # Волновой спавнер. SetSpawnPoints() из MapGenerator.
│   ├── SabotageSystem.cs       # TODO
│   ├── ChaosWheel.cs           # TODO
│   └── ComebackSystem.cs       # TODO
├── ui/
│   ├── HUD.cs / HUD.tscn       # CanvasLayer. HP бар (ColorRect) + Gold (Label).
│   └── ShopUI.cs               # TODO
└── assets/
    ├── ui/                     # hp_bar_frame.png, coin.png (hp_bar_fill.png — legacy, не используется)
    ├── Tilemap/tilemap_packed.png  # Тайлсет 16x16, источник 0
    └── Tiles/                  # Отдельные тайлы
```

---

## 🏛️ Архитектурные решения

### Порядок автозагрузки (ВАЖНО)
```
1. EventBus       → /root/EventBus
2. GameManager    → /root/GameManager
3. EconomyManager → /root/EconomyManager
4. NetworkManager → /root/NetworkManager
```
Никогда не меняй порядок первых трёх — `EconomyManager` подписывается на `EventBus`
в `_Ready()`. `NetworkManager` грузится последним: ему нужны готовые EventBus и
GameManager. Новые синглтоны добавляй только в конец списка.

### EventBus — шина событий
Все системы общаются ТОЛЬКО через EventBus. Никаких прямых ссылок между системами.

```csharp
// Подписка (C# стиль, не через Connect):
_eventBus.PlayerHealthChanged += OnPlayerHealthChanged;

// Эмит:
_eventBus.EmitSignal(EventBus.SignalName.PlayerHealthChanged, playerId, health);

// Отписка в _ExitTree() — ОБЯЗАТЕЛЬНО:
_eventBus.PlayerHealthChanged -= OnPlayerHealthChanged;
```

### Сигналы EventBus
| Сигнал | Параметры | Когда |
|--------|-----------|-------|
| `CurrencyChanged` | `(int playerId, int newAmount)` | При изменении баланса |
| `EnemyDied` | `(Vector2 position, int reward, int ownerPlayerId)` | При смерти врага |
| `PhaseChanged` | `(int newPhase)` | При смене фазы игры |
| `PhaseTimerChanged` | `(float timeLeft)` | Каждый кадр активной фазы |
| `PlayerDied` | `(int playerId)` | При смерти игрока |
| `PlayerHealthChanged` | `(int playerId, float newHealth)` | При изменении HP |
| `RoundStarted` | `(int roundNumber)` | В начале раунда |
| `RoundEnded` | `(int winnerPlayerId)` | По итогу дуэли |
| `MatchEnded` | `(int winnerPlayerId)` | При победе в матче |
| `SabotagePurchased` | `(int buyerId, int targetId, string sabotageType)` | При покупке саботажа |
| `ChaosEffectApplied` | `(string effectId, int targetPlayerId)` | При применении эффекта хаоса |

### Фазы игры (GameManager.GamePhase)
```csharp
Lobby → PvE → Shop → Chaos → PvP → RoundEnd → [повтор]
```

### Коллизионные слои
| Слой | Кто | Маска |
|------|-----|-------|
| 1 | Игроки, враги (тела), стены карты | 1 |
| 2 | Пули (Bullet) | 1 \| 4 |
| 4 | Хитбоксы врагов (Area2D Hitbox) | 0 |

### Фазовый урон оружия
```csharp
// WeaponBase.GetDamage(phase):
PvE → BaseDamage * PvEMultiplier (1.5f по умолчанию)
PvP → BaseDamage * PvPMultiplier (0.8f по умолчанию)
```

---

## 🗺️ MapGenerator

- Тип: `TileMapLayer` с прикреплённым скриптом
- Тайлсет: `tilemap_packed.png`, тайл 16x16, источник ID = 0
- Пол: `Vector2I(1, 0)` — первый коричневый тайл
- Стены: `Vector2I(4,3)`, `(9,4)`, `(10,4)`, `(11,4)` — случайный выбор
- После генерации вызывает `NotifySpawner()` → путь `/root/Main/EnemySpawner`
- Позиция карты: `-(GridSize / 2f) * 16f` по обеим осям (центровка)
- `PlayerSpawnCell` = центр первой комнаты `_rooms[0].Center`

---

## 👾 EnemySpawner

- Находится в сцене Main по пути `/root/Main/EnemySpawner`
- `SetSpawnPoints(List<Vector2>)` вызывается из `MapGenerator.NotifySpawner()`
- Спавн привязан к фазе: слушает `PhaseChanged`, спавнит только в `PvE`,
  на выходе из PvE останавливается и очищает арену (`ClearEnemies`)
- Первая волна — сразу при входе в PvE (или при `SetSpawnPoints`, если PvE уже идёт)
- Следующие волны каждые `WaveInterval` секунд (по умолчанию 12)
- Максимум `MaxEnemies` врагов одновременно (по умолчанию 8)

---

## 👤 Игрок

### LocalPlayer
- Группа: `"players"` (через `AddToGroup` в `OnReady`)
- Управление: WASD (через Input Map: `move_left/right/up/down`)
- Стрельба: ЛКМ (`shoot` в Input Map)
- Переключение оружия: `1` / `2` (Input Map: `weapon_slot_1/2`)
- Камера: `Camera2D` дочерний узел, Zoom = 2

### PlayerBase
- `IsDead` — проверять перед любым действием
- `TakeDamage(float)` — эмитирует `PlayerHealthChanged`
- `Die()` — скрывает игрока, отключает процессинг и коллизии
- `PlayerId = 0` для хоста, `PlayerId = 1` для клиента

---

## 🎯 BasicEnemy

- Ищет ближайшего живого игрока через группу `"players"`
- Обновляет цель раз в секунду (Timer в `_Ready`)
- `AttackInterval = 0.8f` — кулдаун между ударами (НЕ DOT!)
- Knockback при получении урона: `180f` в направлении от цели
- Хитбокс: дочерний `Area2D` с именем `"Hitbox"`, группа `"enemy_hitboxes"`, Layer=4

---

## 🖥️ HUD

- Тип: `CanvasLayer` (всегда поверх игры)
- `HPBackground` — `ColorRect`, тёмная подложка бара
- `HPFill` — `ColorRect`, ширина меняется через `OffsetRight` (НЕ TextureProgressBar!)
- `HPFrame` — `TextureRect` с рамкой черепов (поверх HPFill)
- `CurrencyLabel` — `Label` с текстом `"Gold: {amount}"`
- `CoinIcon` — `TextureRect` с иконкой монеты
- Полная ширина бара читается из сцены (`HPFill.OffsetRight - OffsetLeft` при 100% HP),
  magic-чисел в коде нет
- Цвет меняется: >50% красный, >25% оранжевый, <25% ярко-красный
- У всех узлов HUD `mouse_filter = 2` (Ignore) — оверлей не перехватывает клики стрельбы
- Стартовые значения тянутся в `InitFromState` (deferred): HP из игрока, баланс из `EconomyManager`

---

## 🚫 Жёсткие ограничения

- **Никогда** не добавляй прямые ссылки между системами — только через EventBus.
- **Никогда** не вызывай `QueueFree()` на мёртвом узле без проверки `IsInstanceValid()`.
- **Никогда** не меняй порядок автозагрузки без обновления этого файла.
- **Никогда** не спавни узлы на клиенте без `SetMultiplayerAuthority()` (сеть).
- **Всегда** отписывайся от EventBus в `_ExitTree()`.
- **Всегда** проверяй `IsDead` перед `TakeDamage`.
- **Всегда** используй `CallDeferred()` если нужно найти узлы сразу после `_Ready`.

---

## ⚠️ Известные подводные камни

1. **HUD класс должен называться `HUD`** (заглавные буквы), не `Hud` — иначе Godot не найдёт скрипт.
2. **MapGenerator ищет EnemySpawner по пути `/root/Main/EnemySpawner`** — если переименуешь корневой узел сцены, путь сломается.
3. **EventBus подписка** — только C#-стилем `+=` в `_Ready()` и парный `-=` в `_ExitTree()`.
   НЕ через `Connect`/`Callable.From` (свежий `Callable` не совпадёт при `Disconnect`).
4. **TextureProgressBar не обновляется визуально** при изменении `Value` если `texture_progress` не настроен как отдельная текстура заливки. Используем `ColorRect` + `OffsetRight`.
5. **Motion Mode у CharacterBody2D** должен быть `Floating` для top-down игры, не `Grounded`.
6. **Первая волна врагов** спавнится при входе в фазу `PvE` (не сразу в `SetSpawnPoints`,
   если PvE ещё не началась) — спавнер слушает `PhaseChanged`.
7. **Пуля** использует `BodyEntered` + `AreaEntered` — нужны оба для попадания по телу и
   хитбоксу. Флаг `_hasHit` защищает от двойного урона, если сработали оба в одном кадре.

---

## 📐 Соглашения по коду

```
Namespace:      ChaosArena.autoload / ChaosArena.entities.player / ChaosArena.ui /
                ChaosArena.entities.weapons / ChaosArena.systems / ChaosArena.scenes
                (namespace = путь папки; корневой Main — просто ChaosArena)
Приватные поля: _camelCase
Публичные:      PascalCase
Экспорты:       [Export] public Type Name
Коммиты:        на русском языке, краткие
Комментарии:    на русском языке
Один файл:      один класс
```

---

## 🔌 Сетевая архитектура (NetworkManager — базовый слой готов)

```
Хост (PlayerId=0) ←── ENet P2P Port 7000 ──→ Клиент (PlayerId=1)

Authority:
- Хост: фазы, экономика, спавн врагов, валидация саботажа
- Каждый игрок: своя позиция, своя стрельба

RPC частота позиций: 20 Гц (каждые 0.05 сек)
```

### NetworkManager (`/root/NetworkManager`, autoload)
- `HostGame(port=7000)` — поднимает ENet-сервер, локальный игрок = 0.
- `JoinGame(address="127.0.0.1", port=7000)` — клиент, локальный игрок = 1,
  переводит `GameManager` в режим клиента (`SetNetworkClient(true)`).
- `Disconnect()` — закрывает peer, возврат в оффлайн.
- **Синхронизация фаз** (надёжно): хост слушает `EventBus.PhaseChanged` и шлёт
  `ReceivePhase` всем; новому клиенту при коннекте шлёт текущую фазу адресно.
  Клиент применяет через `GameManager.ApplyNetworkPhase` и сам фазы не переключает.
- **Синхронизация позиций** (20 Гц, ненадёжно): локальная позиция игрока рассылается
  RPC `ReceivePosition(playerId, pos, vel)`; входящие пишутся в `_remotePositions`
  и применяются к узлу удалённого игрока (если он есть в группе `"players"`).
- Триггеры пока временные (нет лобби): в `Main._UnhandledInput` — `F1` хост, `F2` join.

### TODO сети (вне текущего объёма)
- `RemotePlayer`: спавн узла удалённого игрока + интерполяция по `TryGetRemotePosition`.
- Репликация врагов (сейчас спавнер работает локально на каждом пире).
- Синхронизация стрельбы/урона, экономики, лобби.

---

## ✅ Статус (обновляй при изменениях)

```
✅ EventBus, GameManager, EconomyManager
✅ MapGenerator (BSP, комнаты + коридоры)
✅ LocalPlayer (WASD, анимация, тень, частицы)
✅ BasicEnemy (AI, knockback, кулдаун)
✅ EnemySpawner (волны)
✅ Bullet (стрельба в направлении мыши)
✅ HUD (HP бар ColorRect+OffsetRight, цвет по %, Gold)
✅ Коллизии стен
✅ Спрайты (player.png, enemy.png, bullet.png, wand.png)
✅ NetworkManager (ENet P2P, host/client, RPC фаз + позиций 20 Гц)
🔄 RemotePlayer (узел есть только в планах; данные позиций уже приходят)
❌ ShopUI
❌ ChaosWheel
❌ SabotageSystem
❌ ComebackSystem
❌ PvP дуэль
❌ Лобби
```
