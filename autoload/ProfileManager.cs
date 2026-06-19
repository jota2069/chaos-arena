using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChaosArena.autoload
{
    /// <summary>Накопительная статистика игрока (сохраняется в profile.json).</summary>
    public sealed class PlayerStats
    {
        [JsonPropertyName("matches_played")] public int MatchesPlayed { get; set; }
        [JsonPropertyName("matches_won")] public int MatchesWon { get; set; }
        [JsonPropertyName("matches_lost")] public int MatchesLost { get; set; }
        [JsonPropertyName("rounds_won")] public int RoundsWon { get; set; }
        [JsonPropertyName("enemies_killed")] public int EnemiesKilled { get; set; }
        [JsonPropertyName("gold_earned")] public int GoldEarned { get; set; }
        [JsonPropertyName("sabotages_used")] public int SabotagesUsed { get; set; }
        [JsonPropertyName("favorite_weapon")] public string FavoriteWeapon { get; set; } = "fire_staff";
        [JsonPropertyName("damage_dealt")] public int DamageDealt { get; set; }
        [JsonPropertyName("damage_taken")] public int DamageTaken { get; set; }
    }

    /// <summary>Профиль игрока: личность + класс (= аватар) + статистика + настройки.</summary>
    public sealed class PlayerProfile
    {
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("nickname")] public string Nickname { get; set; } = "Player";
        [JsonPropertyName("avatar")] public int AvatarIndex { get; set; }
        [JsonPropertyName("class")] public string ClassName { get; set; } = "warrior";
        [JsonPropertyName("stats")] public PlayerStats Stats { get; set; } = new();

        // Настройки (экран Настройки сохраняет сюда же — отдельного файла нет).
        [JsonPropertyName("music_volume")] public int MusicVolume { get; set; } = 70;
        [JsonPropertyName("sfx_volume")] public int SfxVolume { get; set; } = 80;
        [JsonPropertyName("fullscreen")] public bool Fullscreen { get; set; }
    }

    /// <summary>
    /// ОДИННАДЦАТЫЙ автолоад (после ComebackSystem). Хранит локальный профиль игрока
    /// в user://profile.json: личность, класс (= индекс аватара), карьерную статистику
    /// и настройки звука/экрана. Статистику копит во время матча через EventBus и
    /// фиксирует в файл на MatchEnded. Аватар профиля напрямую задаёт класс в игре.
    /// </summary>
    public partial class ProfileManager : Node
    {
        public const string SavePath = "user://profile.json";

        // Индекс аватара -> имя класса. Аватар в профиле = класс в бою (см. CLAUDE.md).
        private static readonly string[] ClassByAvatar = { "warrior", "mage", "rogue", "knight" };
        private static readonly string[] AvatarFiles =
            { "avatar_warrior", "avatar_mage", "avatar_rogue", "avatar_knight" };

        public PlayerProfile Profile { get; private set; } = new();

        private EventBus _eventBus;

        // Счётчики текущего матча — копятся через EventBus, сливаются в Stats на MatchEnded.
        private int _matchEnemies, _matchGold, _matchRoundsWon, _matchDamageDealt, _matchDamageTaken;
        private float _lastHealth = -1f; // для оценки полученного урона по PlayerHealthChanged

        public override void _Ready()
        {
            _eventBus = GetNode<EventBus>("/root/EventBus");

            LoadProfile();
            ApplySettings();

            // Статистика обновляется через сигналы матча.
            _eventBus.RoundStarted += OnRoundStarted;
            _eventBus.RoundEnded += OnRoundEnded;
            _eventBus.MatchEnded += OnMatchEnded;
            _eventBus.EnemyDied += OnEnemyDied;
            _eventBus.PlayerHealthChanged += OnPlayerHealthChanged;

            // Сообщаем UI, что профиль готов (никнейм/аватар можно показывать).
            _eventBus.EmitSignal(EventBus.SignalName.ProfileLoaded, Profile.Nickname, Profile.AvatarIndex);
        }

        public override void _ExitTree()
        {
            if (_eventBus == null || !GodotObject.IsInstanceValid(_eventBus)) return;
            _eventBus.RoundStarted -= OnRoundStarted;
            _eventBus.RoundEnded -= OnRoundEnded;
            _eventBus.MatchEnded -= OnMatchEnded;
            _eventBus.EnemyDied -= OnEnemyDied;
            _eventBus.PlayerHealthChanged -= OnPlayerHealthChanged;
        }

        // --- Загрузка / сохранение ---

        /// <summary>Загружает профиль из user://profile.json. Если файла нет — создаёт дефолтный.</summary>
        public void LoadProfile()
        {
            if (!FileAccess.FileExists(SavePath))
            {
                Profile = CreateDefault();
                SaveProfile();
                return;
            }

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"[ProfileManager] Не удалось открыть {SavePath}: {FileAccess.GetOpenError()}");
                Profile = CreateDefault();
                return;
            }

            string json = file.GetAsText();
            try
            {
                Profile = JsonSerializer.Deserialize<PlayerProfile>(json) ?? CreateDefault();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[ProfileManager] Битый profile.json ({e.Message}) — создаём новый.");
                Profile = CreateDefault();
            }

            // Подстраховка целостности: id, статистика и согласованность класса с аватаром.
            if (string.IsNullOrEmpty(Profile.Id)) Profile.Id = Guid.NewGuid().ToString();
            Profile.Stats ??= new PlayerStats();
            Profile.AvatarIndex = Mathf.Clamp(Profile.AvatarIndex, 0, ClassByAvatar.Length - 1);
            Profile.ClassName = ClassByAvatar[Profile.AvatarIndex];
        }

        /// <summary>Сохраняет профиль в user://profile.json (форматированный JSON).</summary>
        public void SaveProfile()
        {
            // Класс всегда выводится из аватара — держим поля согласованными перед записью.
            Profile.AvatarIndex = Mathf.Clamp(Profile.AvatarIndex, 0, ClassByAvatar.Length - 1);
            Profile.ClassName = ClassByAvatar[Profile.AvatarIndex];

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"[ProfileManager] Не удалось записать {SavePath}: {FileAccess.GetOpenError()}");
                return;
            }

            string json = JsonSerializer.Serialize(Profile, new JsonSerializerOptions { WriteIndented = true });
            file.StoreString(json);
        }

        private PlayerProfile CreateDefault() => new()
        {
            Id = Guid.NewGuid().ToString(),
            Nickname = "Player",
            AvatarIndex = 0,
            ClassName = ClassByAvatar[0],
            Stats = new PlayerStats(),
        };

        // --- Класс / аватар / никнейм ---

        /// <summary>
        /// Имя класса игрока по индексу аватара (warrior/mage/rogue/knight).
        /// new — намеренно перекрывает GodotObject.GetClass() (движок использует свой
        /// нативный метод, C#-вызовы здесь всегда означают игровой класс).
        /// </summary>
        public new string GetClass()
        {
            int idx = Mathf.Clamp(Profile.AvatarIndex, 0, ClassByAvatar.Length - 1);
            return ClassByAvatar[idx];
        }

        public string GetNickname() => string.IsNullOrWhiteSpace(Profile.Nickname) ? "Player" : Profile.Nickname;
        public int GetAvatarIndex() => Profile.AvatarIndex;

        /// <summary>Путь к текстуре аватара текущего класса (для иконки класса на HUD).</summary>
        public string GetAvatarTexturePath() => AvatarTexturePath(Profile.AvatarIndex);

        public static string AvatarTexturePath(int avatarIndex)
        {
            int idx = Mathf.Clamp(avatarIndex, 0, AvatarFiles.Length - 1);
            return $"res://assets/ui/profiles/{AvatarFiles[idx]}.png";
        }

        /// <summary>Меняет аватар (а значит и класс). Не сохраняет на диск сам.</summary>
        public void SetAvatar(int avatarIndex)
        {
            Profile.AvatarIndex = Mathf.Clamp(avatarIndex, 0, ClassByAvatar.Length - 1);
            Profile.ClassName = ClassByAvatar[Profile.AvatarIndex];
        }

        /// <summary>Меняет никнейм (макс 12 символов). Не сохраняет на диск сам.</summary>
        public void SetNickname(string nickname)
        {
            nickname = (nickname ?? "").Trim();
            if (nickname.Length > 12) nickname = nickname.Substring(0, 12);
            Profile.Nickname = string.IsNullOrEmpty(nickname) ? "Player" : nickname;
        }

        // --- Настройки ---

        /// <summary>Применяет настройки звука и режима экрана из профиля к движку.</summary>
        public void ApplySettings()
        {
            SetBusVolume("Music", Profile.MusicVolume);
            SetBusVolume("Master", Profile.SfxVolume); // SFX-шины пока нет — общий регулятор на Master
            SetBusVolume("SFX", Profile.SfxVolume);

            DisplayServer.WindowSetMode(Profile.Fullscreen
                ? DisplayServer.WindowMode.ExclusiveFullscreen
                : DisplayServer.WindowMode.Windowed);
        }

        private static void SetBusVolume(string busName, int percent)
        {
            int idx = AudioServer.GetBusIndex(busName);
            if (idx < 0) return; // такой шины нет — пропускаем
            float v = Mathf.Clamp(percent, 0, 100) / 100f;
            AudioServer.SetBusVolumeDb(idx, v <= 0f ? -80f : Mathf.LinearToDb(v));
        }

        // --- Статистика ---

        /// <summary>
        /// Фиксирует итоги матча в карьерную статистику и сохраняет профиль.
        /// </summary>
        public void UpdateStatsAfterMatch(bool won, int enemies, int gold, int damage)
        {
            var s = Profile.Stats;
            s.MatchesPlayed++;
            if (won) s.MatchesWon++; else s.MatchesLost++;
            s.EnemiesKilled += Mathf.Max(0, enemies);
            s.GoldEarned += Mathf.Max(0, gold);
            s.DamageDealt += Mathf.Max(0, damage);

            SaveProfile();
            _eventBus.EmitSignal(EventBus.SignalName.StatsUpdated);
        }

        private void OnRoundStarted(int round)
        {
            // Новый матч — обнуляем счётчики матча.
            if (round == 1) ResetMatchCounters();
        }

        private void OnRoundEnded(int winnerPlayerId)
        {
            if (winnerPlayerId == LocalPlayerId()) _matchRoundsWon++;
        }

        private void OnMatchEnded(int winnerPlayerId)
        {
            bool won = winnerPlayerId == LocalPlayerId();
            Profile.Stats.RoundsWon += _matchRoundsWon;
            Profile.Stats.DamageTaken += _matchDamageTaken;
            UpdateStatsAfterMatch(won, _matchEnemies, _matchGold, _matchDamageDealt);
            ResetMatchCounters();
        }

        private void OnEnemyDied(Vector2 position, int reward, int ownerPlayerId)
        {
            if (ownerPlayerId != LocalPlayerId()) return;
            _matchEnemies++;
            _matchGold += Mathf.Max(0, reward);
        }

        // Оценка полученного урона по падению HP локального игрока (рост = лечение/респавн — игнорируем).
        private void OnPlayerHealthChanged(int playerId, float newHealth)
        {
            if (playerId != LocalPlayerId()) return;
            if (_lastHealth >= 0f && newHealth < _lastHealth)
                _matchDamageTaken += Mathf.RoundToInt(_lastHealth - newHealth);
            _lastHealth = newHealth;
        }

        private void ResetMatchCounters()
        {
            _matchEnemies = _matchGold = _matchRoundsWon = _matchDamageDealt = _matchDamageTaken = 0;
            _lastHealth = -1f;
        }

        // Id локального игрока (0 в оффлайне). Профиль принадлежит локальной машине.
        private int LocalPlayerId()
        {
            var net = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            return net?.LocalPlayerId ?? 0;
        }
    }
}
