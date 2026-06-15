# Аудит ChaosArena

> Дата: 2026-06-16. Базовая сборка: `dotnet build` — 0 ошибок, 0 предупреждений.
> Найдено 17 пунктов: критические баги, нарушения архитектуры CLAUDE.md, мусор и пробелы.

## 🔴 Критические баги

| # | Файл | Проблема |
|---|------|----------|
| 1 | `ui/HUD.tscn` + `ui/HUD.cs` | HP-бар сделан на `TextureProgressBar` — это прямо запрещено в CLAUDE.md (подводный камень №4: «не обновляется визуально»). По доке бар должен быть `ColorRect` + `OffsetRight`. Плюс не реализована смена цвета по % HP (>50% красный, >25% оранжевый, <25% ярко-красный). |
| 2 | `ui/HUD.cs` | Подписка через `_eventBus.Connect(..., Callable.From<...>(...))`, а в `_ExitTree` — `Disconnect` со **свежесозданным** `Callable.From`. Это другой объект Callable → отписка ненадёжна, риск утечки/дублирования подписки. Нарушает соглашение CLAUDE.md «подписка C#-стилем, не через Connect». |
| 3 | `entities/weapons/Bullet.cs` | Пуля бьёт дважды: маска `1\|4` ловит и тело врага (слой 1, `BodyEntered`), и его хитбокс (слой 4, `AreaEntered`). Оба колбэка успевают вызвать `TakeDamage` до отложенного `QueueFree`. Нет флага «уже попал». |

## 🟠 Нарушения архитектуры CLAUDE.md

| # | Файл | Проблема |
|---|------|----------|
| 4 | `systems/EnemySpawner.cs` | Подписка через `Connect`/`Disconnect`, а не C#-стилем `+=`/`-=` (соглашение CLAUDE.md). Callable хранится корректно, но стиль не соответствует доке. |
| 5 | `autoload/EconomyManager.cs` | Подписан на `EnemyDied` в `_Ready`, но нет отписки в `_ExitTree`. Нарушает правило «Всегда отписывайся от EventBus в `_ExitTree()`». |
| 6 | autoloads + `PlayerBase` + `WeaponBase` | 5 файлов в **глобальном** namespace, остальные — в `ChaosArena.*`. Несогласованно. Должно быть `ChaosArena.autoload`, `ChaosArena.entities.player`, `ChaosArena.entities.weapons`. |
| 7 | Почти все файлы | 25 отладочных `GD.Print` (см. ниже). По заданию оставляем только `GD.PrintErr`. |
| 8 | Большинство классов | Нет XML-комментариев у классов: `Main`, `LocalPlayer`, `Bullet`, `EnemyBase`, `BasicEnemy`, `MapGenerator`, `HUD`. |

## 🟡 Мусор и доковый дрейф

| # | Файл | Проблема |
|---|------|----------|
| 9 | `entities/player/PlayerBase.cs` | Отладочный вывод `GetInstanceId()` EventBus и лог в `TakeDamage` — диагностический мусор. |
| 10 | `Main.cs` | Комментарии-история правок («ИСПРАВЛЕНИЕ: Убраны двойные скобки <<», «Исправлен namespace…») — не документация. |
| 11 | `ui/HUD.cs` | Неиспользуемый `using ChaosArena.systems;`. |
| 12 | `CLAUDE.md` (таблица сигналов) | Дрейф: реально `EnemyDied(position, reward, ownerPlayerId)` — 3 параметра, а в таблице 2; `SabotagePurchased(buyerId, targetId, sabotageType)` — 3, в таблице 2. |
| 13 | `CLAUDE.md` (EnemySpawner) | Дрейф: дока говорит «первая волна сразу в `SetSpawnPoints()`», но код теперь спавнит по фазе PvE (через `PhaseChanged`). Код лучше доки — обновить доку. |

## ⚪ Незакрытые TODO (по статусу CLAUDE.md)

| # | Что | Статус |
|---|-----|--------|
| 14 | `NetworkManager` (ENet P2P) | ❌ нет файла — **реализуется в этом проходе** |
| 15 | `RemotePlayer` | 🔄 заглушки нет; нужен для полной 2-player игры (вне объёма задачи) |
| 16 | `ShopUI`, `ChaosWheel`, `SabotageSystem`, `ComebackSystem` | ❌ вне объёма задачи |
| 17 | PvP-дуэль, Лобби | ❌ вне объёма задачи |

## Проверка путей GetNode (все совпадают со сценами)

- `Main.cs` → `Map`, `LocalPlayer` — есть в `Main.tscn` ✅
- `MapGenerator` → `/root/Main/EnemySpawner` — корень `Main`, узел `EnemySpawner` ✅
- `HUD.cs` → `HPBar`, `CurrencyLabel` — есть в `HUD.tscn` ✅ (узел `HPBar` будет заменён в фиксе HP-бара)
- `LocalPlayer` → `Sprite2D`, `Sprite2D/WandHolder` — есть в `LocalPlayer.tscn` ✅
- `BasicEnemy`/`EnemyBase` → `Sprite2D`, `Hitbox` — есть в `BasicEnemy.tscn` ✅

## План исправлений

1. **Фиксы**: HP-бар на `ColorRect`+`OffsetRight` со сменой цвета; подписки HUD/EnemySpawner/EconomyManager → `+=`/`-=` с `_ExitTree`; флаг попадания в `Bullet`.
2. **Рефакторинг**: namespace `ChaosArena.*` везде; XML-комментарии всем классам; убрать 25 `GD.Print` (оставить 10 `GD.PrintErr`).
3. **Сеть**: `NetworkManager` (ENet, порт 7000, host/client, RPC позиций 20 Гц, RPC фаз) + хуки в `GameManager`.
4. **Тесты**: `dotnet build` после каждого шага.
