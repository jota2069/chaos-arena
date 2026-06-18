using Godot;
using ChaosArena.autoload;
using ChaosArena.entities.player;
using ChaosArena.ui;

namespace ChaosArena.scenes
{
    /// <summary>
    /// Арена торговца. Связывает игрока, NPC-торговца (вход в зону -> «Нажми E» ->
    /// открыть/закрыть ShopUI) и грушу для теста урона. По кнопке ГОТОВ показывает
    /// плашку и переходит к Оракулу Хаоса (оффлайн — сразу; сеть — хост по таймауту).
    /// </summary>
    public partial class ShopArena : Node2D
    {
        // Сколько ждать второго игрока в сети, прежде чем хост переключит фазу.
        private const float ReadyTimeout = 15f;

        private ShopUI _shopUI;
        private Label _prompt;
        private Label _banner;
        private PlayerBase _player;
        private GameManager _gameManager;
        private NetworkManager _network;

        private int _localPlayerId;
        private bool _playerInZone;

        public override void _Ready()
        {
            _gameManager = GetNode<GameManager>("/root/GameManager");
            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            _localPlayerId = _network?.LocalPlayerId ?? 0;

            _player = GetNodeOrNull<PlayerBase>("LocalPlayer");
            _prompt = GetNode<Label>("Merchant/Prompt");
            _banner = GetNode<Label>("BannerLayer/Banner");

            _shopUI = GetNode<ShopUI>("ShopUI");
            _shopUI.ReadyPressed += OnReadyPressed;

            var zone = GetNode<Area2D>("Merchant/InteractZone");
            zone.BodyEntered += OnZoneBodyEntered;
            zone.BodyExited += OnZoneBodyExited;

            _prompt.Visible = false;
            _banner.Visible = false;
        }

        public override void _ExitTree()
        {
            if (_shopUI != null && GodotObject.IsInstanceValid(_shopUI))
                _shopUI.ReadyPressed -= OnReadyPressed;
        }

        // E у торговца: открыть магазин, либо закрыть если уже открыт.
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.E })
                return;

            if (_shopUI.IsOpen) CloseShop();
            else if (_playerInZone) OpenShop();
        }

        private void OnZoneBodyEntered(Node2D body)
        {
            if (body is not PlayerBase) return;
            _playerInZone = true;
            if (!_shopUI.IsOpen) _prompt.Visible = true;
        }

        private void OnZoneBodyExited(Node2D body)
        {
            if (body is not PlayerBase) return;
            _playerInZone = false;
            _prompt.Visible = false;
        }

        private void OpenShop()
        {
            _shopUI.Open(_localPlayerId);
            _prompt.Visible = false;
            // Пока магазин открыт — игрок не двигается и не стреляет (клик = покупка).
            _player?.SetPhysicsProcess(false);
        }

        private void CloseShop()
        {
            _shopUI.Close();
            _player?.SetPhysicsProcess(true);
            if (_playerInZone) _prompt.Visible = true;
        }

        // Нажата ГОТОВ.
        private void OnReadyPressed()
        {
            CloseShop();
            _banner.Text = $"Игрок {_localPlayerId + 1} готов сразиться";
            _banner.Visible = true;

            bool networked = _network != null && _network.IsNetworked;
            if (!networked)
            {
                // Оффлайн: второго игрока нет — сразу к Оракулу Хаоса.
                _gameManager.ChangePhase(GameManager.GamePhase.Chaos);
                return;
            }

            // Сеть: фазу ведёт хост. Ждём второго до 15 сек, иначе автопереход.
            // Полная синхронизация готовности обоих — TODO ЭТАП 2.
            if (_network.IsHost)
            {
                var timer = GetTree().CreateTimer(ReadyTimeout);
                timer.Timeout += () =>
                {
                    if (_gameManager.CurrentPhase == GameManager.GamePhase.Shop)
                        _gameManager.ChangePhase(GameManager.GamePhase.Chaos);
                };
            }
        }
    }
}
