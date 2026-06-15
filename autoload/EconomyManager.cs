using Godot;

namespace ChaosArena.autoload
{
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
        }

        public override void _ExitTree()
        {
            if (_eventBus == null || !GodotObject.IsInstanceValid(_eventBus)) return;

            _eventBus.EnemyDied -= OnEnemyDied;
        }

        /// <summary>
        /// Возвращает текущий баланс игрока (0 при некорректном id).
        /// </summary>
        public int GetBalance(int playerId)
        {
            if (!IsValidPlayerId(playerId))
            {
                GD.PrintErr($"EconomyManager: GetBalance с некорректным id {playerId}");
                return 0;
            }
            return _balance[playerId];
        }

        /// <summary>
        /// Начисляет валюту игроку.
        /// </summary>
        public void AddCurrency(int playerId, int amount)
        {
            if (!IsValidPlayerId(playerId))
            {
                GD.PrintErr($"EconomyManager: AddCurrency с некорректным id {playerId}");
                return;
            }

            _balance[playerId] += amount;
            _eventBus.EmitSignal(EventBus.SignalName.CurrencyChanged, playerId, _balance[playerId]);
        }

        /// <summary>
        /// Списывает валюту. Возвращает false если id некорректен или не хватает денег.
        /// </summary>
        public bool SpendCurrency(int playerId, int amount)
        {
            if (!IsValidPlayerId(playerId))
            {
                GD.PrintErr($"EconomyManager: SpendCurrency с некорректным id {playerId}");
                return false;
            }

            if (_balance[playerId] < amount)
                return false;

            _balance[playerId] -= amount;
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

        // Вызывается когда умирает враг — награду получает владелец арены.
        private void OnEnemyDied(Vector2 position, int reward, int ownerPlayerId)
        {
            AddCurrency(ownerPlayerId, reward);
        }

        // Проверка, что id игрока попадает в границы массива балансов.
        private bool IsValidPlayerId(int playerId)
        {
            return playerId >= 0 && playerId < _balance.Length;
        }
    }
}
