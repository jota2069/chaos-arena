using Godot;
using ChaosArena.systems;
using ChaosArena.entities.player; 

namespace ChaosArena.ui
{
    public partial class HUD : CanvasLayer
    {
        [Export] public int PlayerId { get; set; }
        [Export] public string CurrencyPrefix { get; set; } = "Gold: ";

        private ProgressBar _hpBar;
        private Label _currencyLabel;
        private EventBus _eventBus;

        public override void _Ready()
        {
            _hpBar = GetNode<ProgressBar>("HPBar");
            _currencyLabel = GetNode<Label>("CurrencyLabel");
            _eventBus = GetNodeOrNull<EventBus>("/root/EventBus");

            if (_eventBus == null)
            {
                GD.PrintErr("[HUD] EventBus не найден!");
                return;
            }

            // Прямая C# подписка на события
            _eventBus.PlayerHealthChanged += OnPlayerHealthChanged;
            _eventBus.CurrencyChanged += OnCurrencyChanged;

            // Дефолтные значения до инициализации игрока
            _hpBar.Value = 100;
            _currencyLabel.Text = $"{CurrencyPrefix}100";

            // Отложенная инициализация, чтобы убедиться, что игрок уже в группе
            CallDeferred(nameof(InitFromPlayer));
        }

        private void InitFromPlayer()
        {
            var players = GetTree().GetNodesInGroup("players");
            
            foreach (var node in players)
            {
                if (node is PlayerBase p && p.PlayerId == PlayerId)
                {
                    _hpBar.MaxValue = p.MaxHealth;
                    _hpBar.Value = p.CurrentHealth;
                    break;
                }
            }
        }

        public override void _ExitTree()
        {
            if (_eventBus is null) return;

            // Отписка от событий для предотвращения утечек памяти
            _eventBus.PlayerHealthChanged -= OnPlayerHealthChanged;
            _eventBus.CurrencyChanged -= OnCurrencyChanged;
        }

        private void OnPlayerHealthChanged(int playerId, float newHealth)
        {
            if (playerId != PlayerId) return;
            _hpBar.Value = newHealth;
        }

        private void OnCurrencyChanged(int playerId, int newAmount)
        {
            if (playerId != PlayerId) return;
            _currencyLabel.Text = $"{CurrencyPrefix}{newAmount}";
        }

        public void SetPlayerId(int id)
        {
            PlayerId = id;
        }
    }
}