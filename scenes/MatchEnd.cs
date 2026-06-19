using Godot;
using ChaosArena.autoload;

namespace ChaosArena.scenes
{
    /// <summary>
    /// Временная заглушка экрана конца матча: показывает победителя и кнопку
    /// «В меню» (сброс матча в Lobby через GameManager.ResetMatch).
    /// </summary>
    public partial class MatchEnd : Node2D
    {
        private GameManager _gameManager;

        public override void _Ready()
        {
            _gameManager = GetNode<GameManager>("/root/GameManager");

            GetNode<Label>("WinnerLabel").Text = $"ИГРОК {GetWinnerId() + 1} ПОБЕДИЛ";

            GetNode<Button>("MenuButton").Pressed += OnMenuPressed;
        }

        // Победитель — игрок, набравший больше побед в матче.
        private int GetWinnerId()
        {
            int[] wins = _gameManager.WinCount;
            return wins[1] > wins[0] ? 1 : 0;
        }

        private void OnMenuPressed()
        {
            // Завершаем сетевую сессию (если была), сбрасываем матч и возвращаемся
            // в Главное меню.
            var net = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (net != null && net.IsNetworked) net.Disconnect();

            _gameManager.ResetMatch();
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
        }
    }
}
