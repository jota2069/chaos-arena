# 🔊 AUDIO.md — Звуки и Музыка

> Список всех звуков и музыки которые нужны в игре.
> Пока звуков нет — используем заглушки. Добавляем в последнюю очередь.

---

## 🎵 МУЗЫКА

| Трек | Когда играет | Настроение |
|------|-------------|-----------|
| `music_menu.ogg` | Главное меню | Эпичный, тёмный, данжеон |
| `music_pve.ogg` | PvE фаза | Напряжённый, быстрый, экшн |
| `music_shop.ogg` | Арена торговца | Расслабленный, таинственный |
| `music_oracle.ogg` | Оракул Хаоса | Мистический, тревожный |
| `music_pvp.ogg` | PvP дуэль | Очень напряжённый, финальный |
| `music_victory.ogg` | Экран победы | Триумфальный |

**Настройки музыки:**
- Громкость регулируется в настройках
- Плавное затухание при смене треков (2 сек)
- Зацикливание

---

## 🔉 ЗВУКОВЫЕ ЭФФЕКТЫ

### Оружие и бой
| Звук | Файл | Когда |
|------|------|-------|
| Выстрел посохом | `sfx_shoot_staff.ogg` | Выстрел огненного посоха |
| Выстрел арбалетом | `sfx_shoot_crossbow.ogg` | Выстрел ледяного арбалета |
| Выстрел молнией | `sfx_shoot_lightning.ogg` | Молния |
| Выстрел мушкетом | `sfx_shoot_musket.ogg` | Снайперский выстрел |
| Взрыв гранаты | `sfx_explosion.ogg` | Граната взрывается |
| Попадание в игрока | `sfx_hit_player.ogg` | Снаряд попал в игрока |
| Попадание в моба | `sfx_hit_enemy.ogg` | Снаряд попал в моба |
| Блок щитом | `sfx_shield_block.ogg` | Зеркальный щит отразил |
| Критический удар | `sfx_crit.ogg` | Критический удар |

### Игрок
| Звук | Файл | Когда |
|------|------|-------|
| Получение урона | `sfx_player_hurt.ogg` | Игрок получил урон |
| Смерть игрока | `sfx_player_death.ogg` | Игрок умер |
| Шаги | `sfx_footsteps.ogg` | Игрок движется |
| Подбор бонуса | `sfx_pickup.ogg` | Подобрал бонус на PvP |
| Использование зелья | `sfx_potion.ogg` | Нажал Q |

### Мобы
| Звук | Файл | Когда |
|------|------|-------|
| Смерть моба | `sfx_enemy_death.ogg` | Моб умер |
| Удар моба | `sfx_enemy_attack.ogg` | Моб атакует |
| Рывок паука | `sfx_spider_dash.ogg` | Паук делает рывок |
| Выстрел мага | `sfx_ghost_shoot.ogg` | Призрак стреляет |

### Игровой цикл
| Звук | Файл | Когда |
|------|------|-------|
| Гонг | `sfx_gong.ogg` | Конец PvE / начало PvP |
| Телепорт | `sfx_teleport.ogg` | Игрок телепортируется |
| Победа раунда | `sfx_round_win.ogg` | Победил раунд |
| Поражение раунда | `sfx_round_lose.ogg` | Проиграл раунд |
| Счётчик золота | `sfx_coin.ogg` | Получил золото |
| Покупка | `sfx_buy.ogg` | Купил предмет в магазине |
| Продажа | `sfx_sell.ogg` | Продал предмет |

### Оракул Хаоса
| Звук | Файл | Когда |
|------|------|-------|
| Кручение слота | `sfx_oracle_spin.ogg` | Иконки крутятся |
| Замедление | `sfx_oracle_slow.ogg` | Слот замедляется |
| Остановка | `sfx_oracle_stop.ogg` | Иконка остановилась |
| Флип карты | `sfx_card_flip.ogg` | Карта переворачивается |
| Бафф карта | `sfx_card_buff.ogg` | Выпал бафф |
| Дебафф карта | `sfx_card_debuff.ogg` | Выпал дебафф |
| Хаос карта | `sfx_card_chaos.ogg` | Выпал хаос |

### Саботаж
| Звук | Файл | Когда |
|------|------|-------|
| Активация | `sfx_sabotage_activate.ogg` | Использовал саботаж |
| Затмение | `sfx_eclipse.ogg` | Свет выключился |
| Электрошок | `sfx_electroshock.ogg` | Электрошок применён |
| Торнадо | `sfx_tornado.ogg` | Торнадо летает |

### UI
| Звук | Файл | Когда |
|------|------|-------|
| Клик кнопки | `sfx_button_click.ogg` | Нажата кнопка |
| Наведение | `sfx_button_hover.ogg` | Наведение на кнопку |
| Открытие меню | `sfx_menu_open.ogg` | Открылся экран |
| Закрытие меню | `sfx_menu_close.ogg` | Закрылся экран |
| Уведомление | `sfx_notification.ogg` | "<ник> готов сразиться" |

---

## 📁 Структура папок

```
assets/audio/
├── music/
│   ├── music_menu.ogg
│   ├── music_pve.ogg
│   ├── music_shop.ogg
│   ├── music_oracle.ogg
│   ├── music_pvp.ogg
│   └── music_victory.ogg
└── sfx/
    ├── weapons/
    │   ├── sfx_shoot_staff.ogg
    │   ├── sfx_shoot_crossbow.ogg
    │   └── ...
    ├── player/
    │   ├── sfx_player_hurt.ogg
    │   └── ...
    ├── enemies/
    │   ├── sfx_enemy_death.ogg
    │   └── ...
    ├── gameplay/
    │   ├── sfx_gong.ogg
    │   └── ...
    ├── oracle/
    │   ├── sfx_oracle_spin.ogg
    │   └── ...
    ├── sabotage/
    │   └── ...
    └── ui/
        ├── sfx_button_click.ogg
        └── ...
```

---

## 🛠️ Техническая реализация

```csharp
// AudioManager как автозагрузчик
public partial class AudioManager : Node
{
    // Музыка
    public void PlayMusic(string trackName, float fadeTime = 2f)
    public void StopMusic(float fadeTime = 2f)
    
    // Звуки
    public void PlaySfx(string sfxName, float volume = 1f)
    public void PlaySfxAt(string sfxName, Vector2 position)
    
    // Настройки
    public void SetMusicVolume(float volume)  // 0-1
    public void SetSfxVolume(float volume)    // 0-1
}
```

**AudioBus настройки в Godot:**
- Master bus
- Music bus (с Reverb эффектом)
- SFX bus

---

## ✅ Статус

```
TODO (всё):
⬜ AudioManager.cs автозагрузчик
⬜ Музыка (6 треков) — найти/создать
⬜ Звуки оружий
⬜ Звуки игрока
⬜ Звуки мобов
⬜ Звуки игрового цикла (гонг!)
⬜ Звуки Оракула
⬜ Звуки UI
⬜ Настройки громкости
```

**Приоритет звуков (что добавить первым):**
1. Гонг (`sfx_gong.ogg`) — ключевой момент игры
2. Выстрел (`sfx_shoot_staff.ogg`) — слышишь каждую секунду
3. Попадание (`sfx_hit_player.ogg`) — важен для фидбека
4. Смерть моба (`sfx_enemy_death.ogg`) — удовлетворение
5. Монета (`sfx_coin.ogg`) — позитивный фидбек
