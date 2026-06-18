using Godot;
using ChaosArena.autoload;

namespace ChaosArena.scenes
{
    /// <summary>
    /// Временная заглушка экрана конца раунда: показывает текущий счёт и кнопку
    /// «Далее» для досрочного старта следующего раунда. Автопереход через
    /// RoundEndDuration обеспечивает сам GameManager — здесь второго таймера нет.
    /// </summary>
    public partial class RoundEnd : Node2D
    {
        private GameManager _gameManager;

        public override void _Ready()
        {
            _gameManager = GetNode<GameManager>("/root/GameManager");

            int[] wins = _gameManager.WinCount;
            GetNode<Label>("ScoreLabel").Text = $"СЧЁТ   {wins[0]} : {wins[1]}";

            GetNode<Button>("NextButton").Pressed += OnNextPressed;
        }

        // Досрочный переход к следующему раунду. Клиент фазы не ведёт —
        // переход инициирует только хост/оффлайн (см. GameManager.IsNetworkClient).
        private void OnNextPressed()
        {
            if (_gameManager.IsNetworkClient) return;
            _gameManager.StartNextRound();
        }
    }
}
