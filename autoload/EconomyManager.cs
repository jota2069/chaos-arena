using Godot;

/// <summary>
/// Управляет валютой игроков. Все транзакции только через этот класс.
/// </summary>
public partial class EconomyManager : Node
{
    // Начальный баланс каждого игрока
    public const int StartingCurrency = 100;

    // Награда за убийство обычного моба
    public const int BasicEnemyReward = 10;

    // Баланс каждого игрока (индекс = id игрока)
    private int[] _balance = new int[2];

    private EventBus _eventBus;

    public override void _Ready()
    {
        _eventBus = GetNode<EventBus>("/root/EventBus");

        // Подписываемся на смерть врага — автоматически начисляем награду
        _eventBus.EnemyDied += OnEnemyDied;

        // Выдаём стартовый баланс
        _balance[0] = StartingCurrency;
        _balance[1] = StartingCurrency;

        GD.Print("EconomyManager: готов");
    }

    /// <summary>
    /// Возвращает текущий баланс игрока.
    /// </summary>
    public int GetBalance(int playerId) => _balance[playerId];

    /// <summary>
    /// Начисляет валюту игроку.
    /// </summary>
    public void AddCurrency(int playerId, int amount)
    {
        _balance[playerId] += amount;
        GD.Print($"EconomyManager: игрок {playerId} получил {amount}. Баланс: {_balance[playerId]}");
        _eventBus.EmitSignal(EventBus.SignalName.CurrencyChanged, playerId, _balance[playerId]);
    }

    /// <summary>
    /// Списывает валюту. Возвращает false если не хватает денег.
    /// </summary>
    public bool SpendCurrency(int playerId, int amount)
    {
        if (_balance[playerId] < amount)
        {
            GD.Print($"EconomyManager: игрок {playerId} — недостаточно средств");
            return false;
        }

        _balance[playerId] -= amount;
        GD.Print($"EconomyManager: игрок {playerId} потратил {amount}. Баланс: {_balance[playerId]}");
        _eventBus.EmitSignal(EventBus.SignalName.CurrencyChanged, playerId, _balance[playerId]);
        return true;
    }

    /// <summary>
    /// Сбрасывает баланс в начале нового матча.
    /// </summary>
    public void ResetBalances()
    {
        _balance[0] = StartingCurrency;
        _balance[1] = StartingCurrency;
        _eventBus.EmitSignal(EventBus.SignalName.CurrencyChanged, 0, _balance[0]);
        _eventBus.EmitSignal(EventBus.SignalName.CurrencyChanged, 1, _balance[1]);
    }

    // Вызывается когда умирает враг — начисляем награду игроку 0 пока нет разделения арен
    private void OnEnemyDied(Vector2 position, int reward)
    {
        // TODO: определять какому игроку принадлежит арена
        AddCurrency(0, reward);
    }
}