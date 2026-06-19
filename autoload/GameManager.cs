using Godot;
using ChaosArena.entities.player;

namespace ChaosArena.autoload
{
    /// <summary>
    /// Главный синглтон игры. Единственный авторитет смены фаз в оффлайн-режиме.
    /// Управляет полным игровым циклом: Lobby -> PvE -> Shop -> Chaos -> PvP -> RoundEnd -> ...
    /// Все изменения транслируются через EventBus.
    /// </summary>
    public partial class GameManager : Node
    {
        // Фазы игрового цикла
        public enum GamePhase
        {
            Lobby,      // Ожидание игроков / стартовый экран
            PvE,        // Зачистка мобов на раздельных аренах
            Shop,       // Магазин между раундами
            Chaos,      // Рулетка хаоса
            PvP,        // Дуэль на общей арене
            RoundEnd,   // Конец раунда, короткая пауза
            MatchEnd    // Матч окончен, есть победитель
        }

        // Текущая фаза (только чтение снаружи)
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;

        // В сетевой игре клиент НЕ ведёт фазы сам — их транслирует авторитетный хост.
        public bool IsNetworkClient { get; private set; } = false;

        // Номер текущего раунда (начинается с 1 после StartMatch)
        public int CurrentRound { get; private set; } = 0;

        // Счёт побед: индекс = id игрока, значение = количество побед
        public int[] WinCount { get; private set; } = new int[2];

        // Побед нужно для победы в матче
        public const int WinsToWin = 3;

        // --- Длительности фаз (секунды). Экспортированы для тонкой настройки. ---
        [Export] public float PvEDuration = 60f;
        [Export] public float ShopDuration = 20f;
        [Export] public float ChaosDuration = 5f;
        [Export] public float RoundEndDuration = 3f;

        // Автостарт матча при запуске. По умолчанию выключен: загрузочная сцена
        // показывает Главное меню, а матч стартует из лобби (управляет Boot.cs).
        [Export] public bool AutoStartMatch = false;

        // Таймер текущей фазы
        private float _phaseTimer = 0f;
        private bool _phaseTimerActive = false;

        private EventBus _eventBus;

        public override void _Ready()
        {
            _eventBus = GetNode<EventBus>("/root/EventBus");

            if (AutoStartMatch)
                CallDeferred(nameof(StartMatch));
        }

        public override void _Process(double delta)
        {
            if (!_phaseTimerActive) return;

            _phaseTimer = Mathf.Max(0f, _phaseTimer - (float)delta);
            _eventBus.EmitSignal(EventBus.SignalName.PhaseTimerChanged, _phaseTimer);

            if (_phaseTimer <= 0f)
            {
                _phaseTimerActive = false;
                // В сети фазы ведёт хост; клиент ждёт его команды и сам не переключает.
                if (!IsNetworkClient)
                    OnPhaseTimerFinished();
            }
        }

        // --- Управление матчем ---

        /// <summary>
        /// Начинает новый матч: сбрасывает счёт и запускает первый раунд.
        /// </summary>
        public void StartMatch()
        {
            WinCount = new int[2];
            CurrentRound = 0;
            ApplyLocalClass();   // класс из профиля = аватар (CLAUDE.md)
            StartNextRound();
        }

        // Применяет класс из профиля локальному игроку, если он уже в сцене. Основное
        // применение — в PlayerBase._Ready при спавне игрока; здесь — для уже
        // существующего узла (например при рестарте матча без пересоздания сцены).
        private void ApplyLocalClass()
        {
            var profile = GetNodeOrNull<ProfileManager>("/root/ProfileManager");
            if (profile == null) return;

            int localId = GetNodeOrNull<NetworkManager>("/root/NetworkManager")?.LocalPlayerId ?? 0;
            var cls = PlayerBase.ClassFromString(profile.GetClass());
            foreach (var node in GetTree().GetNodesInGroup("players"))
                if (node is PlayerBase p && p.PlayerId == localId)
                {
                    p.ApplyClassStats(cls);
                    break;
                }
        }

        /// <summary>
        /// Запускает следующий раунд с фазы PvE.
        /// </summary>
        public void StartNextRound()
        {
            CurrentRound++;
            _eventBus.EmitSignal(EventBus.SignalName.RoundStarted, CurrentRound);
            ChangePhase(GamePhase.PvE);
        }

        /// <summary>
        /// Меняет фазу игры. В оффлайн-режиме это единственная точка смены фазы.
        /// </summary>
        public void ChangePhase(GamePhase newPhase)
        {
            CurrentPhase = newPhase;
            _phaseTimer = GetPhaseDuration(newPhase);
            _phaseTimerActive = _phaseTimer > 0f;

            // Оповещаем все системы через EventBus
            _eventBus.EmitSignal(EventBus.SignalName.PhaseChanged, (int)newPhase);
            _eventBus.EmitSignal(EventBus.SignalName.PhaseTimerChanged, _phaseTimer);
        }

        /// <summary>
        /// Завершает дуэль: засчитывает победу и решает, продолжать матч или закончить.
        /// Вызывается из DuelSystem (или дебаг-клавишей) только во время PvP.
        /// </summary>
        public void EndDuel(int winnerPlayerId)
        {
            if (CurrentPhase != GamePhase.PvP)
            {
                GD.PrintErr($"GameManager: EndDuel вызван вне фазы PvP (текущая: {CurrentPhase})");
                return;
            }

            if (!IsValidPlayerId(winnerPlayerId))
            {
                GD.PrintErr($"GameManager: EndDuel с некорректным id игрока {winnerPlayerId}");
                return;
            }

            WinCount[winnerPlayerId]++;
            _eventBus.EmitSignal(EventBus.SignalName.RoundEnded, winnerPlayerId);

            if (WinCount[winnerPlayerId] >= WinsToWin)
                EndMatch(winnerPlayerId);
            else
                ChangePhase(GamePhase.RoundEnd);
        }

        /// <summary>
        /// Сбрасывает матч и возвращает в лобби.
        /// </summary>
        public void ResetMatch()
        {
            WinCount = new int[2];
            CurrentRound = 0;
            ChangePhase(GamePhase.Lobby);
        }

        /// <summary>
        /// Сколько секунд осталось до конца текущей фазы (0 если без таймера).
        /// </summary>
        public float GetPhaseTimeLeft() => _phaseTimer;

        // --- Сетевой режим ---

        /// <summary>
        /// Переводит менеджер в режим сетевого клиента: фазы приходят от хоста,
        /// локальный автопереход выключается. Вызывается из NetworkManager.
        /// </summary>
        public void SetNetworkClient(bool value)
        {
            IsNetworkClient = value;
        }

        /// <summary>
        /// Применяет фазу, присланную хостом по сети. Клиент не запускает свой
        /// автопереход — только отображает фазу и таймер через EventBus.
        /// </summary>
        public void ApplyNetworkPhase(int phaseInt, float timeLeft)
        {
            CurrentPhase = (GamePhase)phaseInt;
            _phaseTimer = timeLeft;
            // Таймер тикает для UI, но OnPhaseTimerFinished у клиента не сработает.
            _phaseTimerActive = timeLeft > 0f;

            _eventBus.EmitSignal(EventBus.SignalName.PhaseChanged, phaseInt);
            _eventBus.EmitSignal(EventBus.SignalName.PhaseTimerChanged, timeLeft);
        }

        // --- Внутреннее ---

        // Вызывается, когда таймер фазы дошёл до нуля — автоматический переход дальше.
        private void OnPhaseTimerFinished()
        {
            switch (CurrentPhase)
            {
                case GamePhase.PvE:
                    ChangePhase(GamePhase.Shop);
                    break;
                case GamePhase.Shop:
                    ChangePhase(GamePhase.Chaos);
                    break;
                case GamePhase.Chaos:
                    ChangePhase(GamePhase.PvP);
                    break;
                case GamePhase.RoundEnd:
                    StartNextRound();
                    break;
                // PvP завершается через EndDuel, у Lobby/MatchEnd нет таймера.
            }
        }

        private void EndMatch(int winnerPlayerId)
        {
            ChangePhase(GamePhase.MatchEnd);
            _eventBus.EmitSignal(EventBus.SignalName.MatchEnded, winnerPlayerId);
        }

        // Длительность фазы. PvP длится до победы, Lobby/MatchEnd — без таймера.
        private float GetPhaseDuration(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.PvE => PvEDuration,
                GamePhase.Shop => ShopDuration,
                GamePhase.Chaos => ChaosDuration,
                GamePhase.RoundEnd => RoundEndDuration,
                _ => 0f
            };
        }

        private bool IsValidPlayerId(int playerId)
        {
            return playerId >= 0 && playerId < WinCount.Length;
        }
    }
}
