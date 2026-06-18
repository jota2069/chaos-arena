using Godot;

namespace ChaosArena.ui
{
    /// <summary>
    /// Дебаг-панель разработчика (ВОСЬМОЙ автолоад). Только в DEBUG-сборке:
    /// горячие клавиши управления фазами/золотом/раундами и оверлей состояния
    /// в углу экрана. F1/F2 (хост/клиент) живут в Main.cs — здесь не дублируются.
    /// </summary>
    public partial class DebugPanel : Node
    {
#if DEBUG
        private static readonly Color Gold = new(1f, 0.843f, 0f);

        private CanvasLayer _layer;
        private Label _overlay;
        private bool _overlayVisible = true;
        private int _debugCardNum;

        private autoload.GameManager _gameManager;
        private autoload.EconomyManager _economy;
        private systems.OracleSystem _oracle;
        private autoload.NetworkManager _network;

        private int LocalId => _network?.LocalPlayerId ?? 0;

        public override void _Ready()
        {
            _gameManager = GetNode<autoload.GameManager>("/root/GameManager");
            _economy = GetNode<autoload.EconomyManager>("/root/EconomyManager");
            _oracle = GetNodeOrNull<systems.OracleSystem>("/root/OracleSystem");
            _network = GetNodeOrNull<autoload.NetworkManager>("/root/NetworkManager");

            _layer = new CanvasLayer { Layer = 200 };
            AddChild(_layer);

            var bg = new ColorRect
            {
                Position = new Vector2(8, 8),
                Size = new Vector2(400, 168),
                Color = new Color(0, 0, 0, 0.6f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _layer.AddChild(bg);

            _overlay = new Label
            {
                Position = new Vector2(16, 12),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _overlay.AddThemeColorOverride("font_color", Gold);
            _overlay.AddThemeFontSizeOverride("font_size", 13);
            _layer.AddChild(_overlay);
        }

        public override void _Process(double delta)
        {
            if (_overlayVisible) _overlay.Text = BuildOverlayText();
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            switch (key.Keycode)
            {
                case Key.F3: SkipPhase(); break;
                case Key.F4: _economy.AddCurrency(LocalId, 500); break;
                case Key.F5: ToggleOverlay(); break;
                case Key.F6: WinRound(0); break;
                case Key.F7: WinRound(1); break;
                case Key.F8: NextOracleCard(); break;
            }
        }

        // F3 — перейти к следующей фазе по обычному циклу.
        private void SkipPhase()
        {
            switch (_gameManager.CurrentPhase)
            {
                case autoload.GameManager.GamePhase.Lobby: _gameManager.StartNextRound(); break;
                case autoload.GameManager.GamePhase.PvE: _gameManager.ChangePhase(autoload.GameManager.GamePhase.Shop); break;
                case autoload.GameManager.GamePhase.Shop: _gameManager.ChangePhase(autoload.GameManager.GamePhase.Chaos); break;
                case autoload.GameManager.GamePhase.Chaos: _gameManager.ChangePhase(autoload.GameManager.GamePhase.PvP); break;
                case autoload.GameManager.GamePhase.PvP: _gameManager.ChangePhase(autoload.GameManager.GamePhase.RoundEnd); break;
                case autoload.GameManager.GamePhase.RoundEnd: _gameManager.StartNextRound(); break;
                case autoload.GameManager.GamePhase.MatchEnd: _gameManager.ResetMatch(); break;
            }
        }

        // F6/F7 — победа/поражение текущего раунда (только в PvP).
        private void WinRound(int winnerId)
        {
            if (_gameManager.CurrentPhase == autoload.GameManager.GamePhase.PvP)
                _gameManager.EndDuel(winnerId);
            else
                GD.Print($"[Debug] EndDuel доступен только в PvP (сейчас {_gameManager.CurrentPhase})");
        }

        private void ToggleOverlay()
        {
            _overlayVisible = !_overlayVisible;
            _layer.Visible = _overlayVisible;
        }

        // F8 — показать следующую карту Оракула (1..20) на активном экране.
        private void NextOracleCard()
        {
            if (_oracle == null) return;
            _debugCardNum = _debugCardNum % 20 + 1;

            var node = GetTree().GetFirstNodeInGroup("oracle_ui");
            if (node is OracleUI ui) ui.DebugShowCard(_debugCardNum);
            else GD.Print($"[Debug] Оракул не активен — карта {_debugCardNum} не показана");
        }

        private string BuildOverlayText()
        {
            int[] wins = _gameManager.WinCount;
            string eff0 = _oracle != null ? string.Join(", ", _oracle.ActiveEffectNames(0)) : "";
            string eff1 = _oracle != null ? string.Join(", ", _oracle.ActiveEffectNames(1)) : "";

            return "DEBUG  F3:фаза F4:+500g F5:скрыть F6:win F7:lose F8:карта\n" +
                   $"Фаза: {_gameManager.CurrentPhase}\n" +
                   $"Раунд: {_gameManager.CurrentRound}\n" +
                   $"Счёт: {wins[0]} : {wins[1]}\n" +
                   $"Золото  P0: {_economy.GetBalance(0)}   P1: {_economy.GetBalance(1)}\n" +
                   $"Эффекты P0: {eff0}\n" +
                   $"Эффекты P1: {eff1}";
        }
#endif
    }
}
