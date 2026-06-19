using Godot;
using ChaosArena.autoload;
using ChaosArena.entities.player;
using ChaosArena.systems;

namespace ChaosArena.ui
{
    /// <summary>
    /// Игровой HUD: полоса HP и счётчик валюты. Слушает EventBus и обновляется
    /// в реальном времени. HP-бар — ColorRect, ширина которого меняется через
    /// OffsetRight (TextureProgressBar визуально не обновлялся — см. CLAUDE.md).
    /// </summary>
    public partial class HUD : CanvasLayer
    {
        [Export] public int PlayerId { get; set; }
        [Export] public string CurrencyPrefix { get; set; } = "Gold: ";

        private ColorRect _hpFill;
        private Label _currencyLabel;
        private EventBus _eventBus;

        // Кнопка саботажа: показывается в PvE, если саботаж куплен и не активирован.
        private TextureButton _sabotageButton;
        private SabotageSystem _sabotage;
        private GameManager _gameManager;

        // Геометрия бара читается из сцены при 100% HP — без magic-чисел в коде.
        private float _barLeft;
        private float _barFullWidth;
        private float _maxHealth = 100f;

        public override void _Ready()
        {
            _hpFill = GetNode<ColorRect>("HPFill");
            _currencyLabel = GetNode<Label>("CurrencyLabel");
            _eventBus = GetNodeOrNull<EventBus>("/root/EventBus");

            if (_eventBus == null)
            {
                GD.PrintErr("[HUD] EventBus не найден!");
                return;
            }

            // Запоминаем полную ширину бара (правый край при полном HP).
            _barLeft = _hpFill.OffsetLeft;
            _barFullWidth = _hpFill.OffsetRight - _hpFill.OffsetLeft;

            // C#-стиль подписки (соглашение CLAUDE.md), отписка в _ExitTree.
            _eventBus.PlayerHealthChanged += OnPlayerHealthChanged;
            _eventBus.CurrencyChanged += OnCurrencyChanged;

            _sabotage = GetNodeOrNull<SabotageSystem>("/root/SabotageSystem");
            _gameManager = GetNodeOrNull<GameManager>("/root/GameManager");
            BuildSabotageButton();

            CallDeferred(nameof(InitFromState));
        }

        // Кнопка саботажа в левом нижнем углу (sabotage_button.png).
        private void BuildSabotageButton()
        {
            _sabotageButton = new TextureButton
            {
                TextureNormal = GD.Load<Texture2D>("res://assets/ui/hud/sabotage_button.png"),
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                Visible = false,
                TooltipText = "Активировать саботаж (G)",
            };
            _sabotageButton.AnchorLeft = 0f; _sabotageButton.AnchorRight = 0f;
            _sabotageButton.AnchorTop = 1f; _sabotageButton.AnchorBottom = 1f;
            _sabotageButton.OffsetLeft = 20f; _sabotageButton.OffsetRight = 92f;
            _sabotageButton.OffsetTop = -92f; _sabotageButton.OffsetBottom = -20f;
            _sabotageButton.Pressed += ActivateSabotage;
            AddChild(_sabotageButton);
        }

        // G — активировать купленный саботаж во время PvE.
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.G })
                ActivateSabotage();
        }

        private void ActivateSabotage()
        {
            if (_sabotage == null || !_sabotage.HasUnused(PlayerId)) return;
            _sabotage.Activate(PlayerId);
            UpdateSabotageButton();
        }

        // Показывает кнопку только в PvE, если есть неактивированный саботаж.
        private void UpdateSabotageButton()
        {
            if (_sabotageButton == null) return;
            bool pve = _gameManager != null && _gameManager.CurrentPhase == GameManager.GamePhase.PvE;
            _sabotageButton.Visible = pve && _sabotage != null && _sabotage.HasUnused(PlayerId);
        }

        public override void _ExitTree()
        {
            if (_eventBus == null || !GodotObject.IsInstanceValid(_eventBus)) return;

            _eventBus.PlayerHealthChanged -= OnPlayerHealthChanged;
            _eventBus.CurrencyChanged -= OnCurrencyChanged;
        }

        // Подтягиваем стартовые значения: HP — из игрока, баланс — из EconomyManager.
        private void InitFromState()
        {
            foreach (var node in GetTree().GetNodesInGroup("players"))
            {
                if (node is PlayerBase p && p.PlayerId == PlayerId)
                {
                    _maxHealth = p.MaxHealth;
                    UpdateHealthBar(p.CurrentHealth);
                    break;
                }
            }

            var economy = GetNodeOrNull<EconomyManager>("/root/EconomyManager");
            int balance = economy != null ? economy.GetBalance(PlayerId) : EconomyManager.StartingCurrency;
            _currencyLabel.Text = $"{CurrencyPrefix}{balance}";

            UpdateSabotageButton();
        }

        private void OnPlayerHealthChanged(int playerId, float newHealth)
        {
            if (playerId != PlayerId) return;
            UpdateHealthBar(newHealth);
        }

        private void OnCurrencyChanged(int playerId, int newAmount)
        {
            if (playerId != PlayerId) return;
            _currencyLabel.Text = $"{CurrencyPrefix}{newAmount}";
        }

        // Меняет ширину заливки и её цвет в зависимости от доли HP.
        private void UpdateHealthBar(float health)
        {
            float ratio = _maxHealth > 0f ? Mathf.Clamp(health / _maxHealth, 0f, 1f) : 0f;
            _hpFill.OffsetRight = _barLeft + _barFullWidth * ratio;

            if (ratio > 0.5f)
                _hpFill.Color = new Color(0.85f, 0.2f, 0.2f);   // >50% — красный
            else if (ratio > 0.25f)
                _hpFill.Color = new Color(0.95f, 0.55f, 0.1f);  // >25% — оранжевый
            else
                _hpFill.Color = new Color(1f, 0.1f, 0.1f);      // <25% — ярко-красный
        }

        public void SetPlayerId(int id) => PlayerId = id;
    }
}
