using Godot;

/// <summary>
/// Главный синглтон игры. Управляет фазами игрового цикла.
/// Только хост меняет фазы — клиент получает изменения через RPC.
/// </summary>
public partial class GameManager : Node
{
    // Фазы игрового цикла
    public enum GamePhase
    {
        Lobby,      // Ожидание игроков
        PvE,        // Зачистка мобов на раздельных аренах
        Shop,       // Магазин между раундами
        Chaos,      // Рулетка хаоса
        PvP,        // Дуэль на общей арене
        RoundEnd    // Конец раунда, подсчёт очков
    }

    // Текущая фаза (только чтение снаружи)
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;

    // Счёт побед: ключ = id игрока, значение = количество побед
    public int[] WinCount { get; private set; } = new int[2];

    // Побед нужно для победы в матче
    public const int WinsToWin = 3;

    // Таймер текущей фазы
    private float _phaseTimer = 0f;

    // Длительность PvE фазы в секундах
    public const float PvEDuration = 150f;

    // Ссылка на EventBus (получаем через автозагрузку)
    private EventBus _eventBus;

    public override void _Ready()
    {
        // Получаем EventBus из автозагрузки Godot
        _eventBus = GetNode<EventBus>("/root/EventBus");
        GD.Print("GameManager: готов");
    }

    public override void _Process(double delta)
    {
        // Таймер работает только во время PvE
        if (CurrentPhase == GamePhase.PvE)
        {
            _phaseTimer -= (float)delta;
            if (_phaseTimer <= 0f)
            {
                // Время вышло — переходим в магазин
                ChangePhase(GamePhase.Shop);
            }
        }
    }

    /// <summary>
    /// Меняет фазу игры. Вызывать только на хосте.
    /// </summary>
    public void ChangePhase(GamePhase newPhase)
    {
        CurrentPhase = newPhase;
        _phaseTimer = newPhase == GamePhase.PvE ? PvEDuration : 0f;

        GD.Print($"GameManager: фаза изменена на {newPhase}");

        // Оповещаем все системы через EventBus
        _eventBus.EmitSignal(EventBus.SignalName.PhaseChanged, (int)newPhase);
    }

    /// <summary>
    /// Засчитывает победу игроку. Возвращает true если матч окончен.
    /// </summary>
    public bool AddWin(int playerId)
    {
        WinCount[playerId]++;
        GD.Print($"GameManager: игрок {playerId} выиграл раунд. Счёт: {WinCount[0]}:{WinCount[1]}");

        if (WinCount[playerId] >= WinsToWin)
        {
            GD.Print($"GameManager: игрок {playerId} победил в матче!");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Сбрасывает счёт и возвращает в лобби.
    /// </summary>
    public void ResetMatch()
    {
        WinCount = new int[2];
        ChangePhase(GamePhase.Lobby);
    }
}