using Godot;

namespace ChaosArena.autoload
{
    /// <summary>
    /// Глобальная шина событий. Все системы общаются через сигналы здесь,
    /// не держа прямых ссылок друг на друга.
    /// </summary>
    public partial class EventBus : Node
    {
        // --- Экономика ---
        [Signal] public delegate void CurrencyChangedEventHandler(int playerId, int newAmount);
        [Signal] public delegate void SabotagePurchasedEventHandler(int buyerId, int targetId, string sabotageType);

        // --- Враги ---
        [Signal] public delegate void EnemyDiedEventHandler(Vector2 position, int reward, int ownerPlayerId);

        // --- Фазы игры ---
        [Signal] public delegate void PhaseChangedEventHandler(int newPhase);
        [Signal] public delegate void PhaseTimerChangedEventHandler(float timeLeft);

        // --- Раунды и матч ---
        [Signal] public delegate void RoundStartedEventHandler(int roundNumber);
        [Signal] public delegate void RoundEndedEventHandler(int winnerPlayerId);
        [Signal] public delegate void MatchEndedEventHandler(int winnerPlayerId);

        // --- Игрок ---
        [Signal] public delegate void PlayerDiedEventHandler(int playerId);
        [Signal] public delegate void PlayerHealthChangedEventHandler(int playerId, float newHealth);

        // --- Хаос ---
        [Signal] public delegate void ChaosEffectAppliedEventHandler(string effectId, int targetPlayerId);

        // --- Оракул Хаоса ---
        [Signal] public delegate void OracleCardDrawnEventHandler(int playerId, int cardId);
        [Signal] public delegate void OracleEffectAppliedEventHandler(int playerId, string effectId);

        // --- Профиль игрока ---
        [Signal] public delegate void ProfileLoadedEventHandler(string nickname, int avatarIndex);
        [Signal] public delegate void StatsUpdatedEventHandler();
    }
}
