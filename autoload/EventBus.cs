using Godot;

/// <summary>
/// Глобальная шина событий. Все системы общаются через сигналы здесь,
/// не держа прямых ссылок друг на друга.
/// </summary>
public partial class EventBus : Node
{
    // --- Экономика ---
    [Signal] public delegate void CurrencyChangedEventHandler(int playerId, int newAmount);
    [Signal] public delegate void SabotagePurchasedEventHandler(int buyerId, string sabotageType);

    // --- Враги ---
    [Signal] public delegate void EnemyDiedEventHandler(Vector2 position, int reward);

    // --- Фазы игры ---
    [Signal] public delegate void PhaseChangedEventHandler(int newPhase);

    // --- Игрок ---
    [Signal] public delegate void PlayerDiedEventHandler(int playerId);
    [Signal] public delegate void PlayerHealthChangedEventHandler(int playerId, float newHealth);
}