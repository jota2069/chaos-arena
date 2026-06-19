using Godot;
using System.Text;
using ChaosArena.autoload;

namespace ChaosArena.ui
{
    /// <summary>
    /// Экран «Играть» / лобби. Два пути: СОЗДАТЬ ЛОББИ (NetworkManager.HostGame, показ
    /// локальных IP, ожидание игрока, кнопка НАЧАТЬ у хоста) и ПОДКЛЮЧИТЬСЯ
    /// (ввод IP -> NetworkManager.JoinGame, статус подключения). Когда хост жмёт НАЧАТЬ,
    /// GameManager.StartMatch меняет фазу на PvE -> SceneLoader грузит арену у обоих.
    /// Шрифт Press Start 2P в проект не добавлен — стиль как в остальном UI.
    /// </summary>
    public partial class LobbyScreen : Control
    {
        private const string MainMenuScene = "res://scenes/MainMenu.tscn";

        private static readonly Color Gold = new(1f, 0.843f, 0f);
        private static readonly Color Bg = new(0.101961f, 0.039216f, 0.180392f);

        private NetworkManager _network;
        private GameManager _gameManager;

        private VBoxContainer _content;
        private Label _title;
        private Label _status;
        private Button _startButton;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            _network = GetNode<NetworkManager>("/root/NetworkManager");
            _gameManager = GetNode<GameManager>("/root/GameManager");

            BuildFrame();
            ShowChoice();

            // Следим за соединением, чтобы обновлять статус и показывать НАЧАТЬ.
            Multiplayer.PeerConnected += OnPeerConnected;
            Multiplayer.PeerDisconnected += OnPeerDisconnected;
            Multiplayer.ConnectedToServer += OnConnectedToServer;
            Multiplayer.ConnectionFailed += OnConnectionFailed;
        }

        public override void _ExitTree()
        {
            Multiplayer.PeerConnected -= OnPeerConnected;
            Multiplayer.PeerDisconnected -= OnPeerDisconnected;
            Multiplayer.ConnectedToServer -= OnConnectedToServer;
            Multiplayer.ConnectionFailed -= OnConnectionFailed;
        }

        // --- Каркас экрана ---

        private void BuildFrame()
        {
            var fill = new ColorRect { Color = Bg, MouseFilter = MouseFilterEnum.Ignore };
            fill.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(fill);

            var panelBg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://assets/ui/menu/menu_panel_bg.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            panelBg.AnchorLeft = 0.5f; panelBg.AnchorRight = 0.5f;
            panelBg.AnchorTop = 0.5f; panelBg.AnchorBottom = 0.5f;
            panelBg.OffsetLeft = -320f; panelBg.OffsetRight = 320f;
            panelBg.OffsetTop = -260f; panelBg.OffsetBottom = 260f;
            AddChild(panelBg);

            _title = MakeLabel("ВЫБОР РЕЖИМА", 32, Gold, HorizontalAlignment.Center);
            _title.AnchorLeft = 0f; _title.AnchorRight = 1f;
            _title.OffsetTop = 60f; _title.OffsetBottom = 110f;
            AddChild(_title);

            _content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _content.AddThemeConstantOverride("separation", 18);
            _content.AnchorLeft = 0.5f; _content.AnchorRight = 0.5f;
            _content.AnchorTop = 0.5f; _content.AnchorBottom = 0.5f;
            _content.OffsetLeft = -220f; _content.OffsetRight = 220f;
            _content.OffsetTop = -120f; _content.OffsetBottom = 160f;
            AddChild(_content);
        }

        // --- Состояния ---

        private void ShowChoice()
        {
            _title.Text = "ВЫБОР РЕЖИМА";
            ClearContent();
            _content.AddChild(MakeButton("🏠  СОЗДАТЬ ЛОББИ", OnCreatePressed));
            _content.AddChild(MakeButton("🔗  ПОДКЛЮЧИТЬСЯ", ShowJoin));
            _content.AddChild(MakeButton("←  НАЗАД", () => GetTree().ChangeSceneToFile(MainMenuScene)));
        }

        private void OnCreatePressed()
        {
            Error err = _network.HostGame();
            if (err != Error.Ok)
            {
                _title.Text = "ОШИБКА";
                ClearContent();
                _content.AddChild(MakeLabel($"Не удалось создать лобби: {err}", 16, Colors.White, HorizontalAlignment.Center));
                _content.AddChild(MakeButton("←  НАЗАД", ShowChoice));
                return;
            }
            ShowHost();
        }

        private void ShowHost()
        {
            _title.Text = "ЛОББИ СОЗДАНО";
            ClearContent();

            _content.AddChild(MakeLabel("Адрес для подключения:", 16, Gold, HorizontalAlignment.Center));
            _content.AddChild(MakeLabel(LocalAddresses(), 18, Colors.White, HorizontalAlignment.Center));
            _content.AddChild(MakeLabel($"Порт: {NetworkManager.DefaultPort}", 14, new Color(0.7f, 0.7f, 0.7f), HorizontalAlignment.Center));

            _status = MakeLabel("Ожидание игрока...", 16, Colors.White, HorizontalAlignment.Center);
            _content.AddChild(_status);

            _startButton = MakeButton("▶  НАЧАТЬ", OnStartPressed);
            _startButton.Visible = false; // появится, когда подключится второй игрок
            _content.AddChild(_startButton);

            _content.AddChild(MakeButton("✖  ОТМЕНА", CancelNetworking));
        }

        private void ShowJoin()
        {
            _title.Text = "ПОДКЛЮЧЕНИЕ";
            ClearContent();

            _content.AddChild(MakeLabel("IP адрес хоста:", 16, Gold, HorizontalAlignment.Center));

            var ipField = new LineEdit
            {
                PlaceholderText = "127.0.0.1",
                Alignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 44),
            };
            ipField.AddThemeFontSizeOverride("font_size", 18);
            _content.AddChild(ipField);

            _status = MakeLabel("", 16, Colors.White, HorizontalAlignment.Center);
            _content.AddChild(_status);

            _content.AddChild(MakeButton("🔗  ПОДКЛЮЧИТЬСЯ", () => OnJoinPressed(ipField.Text)));
            _content.AddChild(MakeButton("←  НАЗАД", () => { CancelNetworking(); }));
        }

        private void OnJoinPressed(string ip)
        {
            ip = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
            Error err = _network.JoinGame(ip);
            if (_status != null)
                _status.Text = err == Error.Ok ? "Подключение..." : $"Ошибка: {err}";
        }

        private void OnStartPressed()
        {
            // Хост — авторитет: запускаем матч. Клиенту фазу разошлёт NetworkManager,
            // а SceneLoader у обоих сменит сцену на арену PvE.
            _gameManager.StartMatch();
        }

        private void CancelNetworking()
        {
            if (_network.IsNetworked) _network.Disconnect();
            ShowChoice();
        }

        // --- Сигналы соединения ---

        private void OnPeerConnected(long id)
        {
            if (_status != null) _status.Text = "Игрок подключился!";
            if (_startButton != null && _network.IsHost) _startButton.Visible = true;
        }

        private void OnPeerDisconnected(long id)
        {
            if (_status != null) _status.Text = "Игрок отключился. Ожидание...";
            if (_startButton != null) _startButton.Visible = false;
        }

        private void OnConnectedToServer()
        {
            if (_status != null) _status.Text = "Подключено! Ожидание старта хоста...";
        }

        private void OnConnectionFailed()
        {
            if (_status != null) _status.Text = "Не удалось подключиться.";
        }

        // --- Вспомогательное ---

        // Список локальных IPv4-адресов (по одному в строке).
        private static string LocalAddresses()
        {
            var sb = new StringBuilder();
            foreach (string ip in IP.GetLocalAddresses())
            {
                if (ip.Contains(':')) continue;      // пропускаем IPv6
                if (ip.StartsWith("169.254")) continue; // link-local
                sb.AppendLine(ip);
            }
            string result = sb.ToString().Trim();
            return result.Length == 0 ? "127.0.0.1" : result;
        }

        private void ClearContent()
        {
            foreach (Node child in _content.GetChildren())
                child.QueueFree();
            _status = null;
            _startButton = null;
        }

        private Button MakeButton(string text, System.Action onPressed)
        {
            var btn = new Button { Text = text, CustomMinimumSize = new Vector2(360, 52) };
            btn.AddThemeColorOverride("font_color", Gold);
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            btn.AddThemeFontSizeOverride("font_size", 18);
            btn.Pressed += () => onPressed();
            return btn;
        }

        private static Label MakeLabel(string text, int size, Color color, HorizontalAlignment align)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeFontSizeOverride("font_size", size);
            return label;
        }
    }
}
