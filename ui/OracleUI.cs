using Godot;
using System.Collections.Generic;
using ChaosArena.autoload;
using ChaosArena.systems;

namespace ChaosArena.ui
{
    /// <summary>
    /// Экран Оракула Хаоса (сцена ChaosOracle.tscn). Крутит слот, тормозит,
    /// увеличивает финальную иконку, переворачивает карту (flip) и показывает
    /// раскрытую карту с кнопками по типу. Логику/эффекты держит OracleSystem.
    /// Группа "oracle_ui" — чтобы дебаг-панель могла форсить карту (F8).
    /// Шрифт Press Start 2P в проект не добавлен — используем размер/цвет, как в HUD.
    /// </summary>
    public partial class OracleUI : Control
    {
        private enum State { Spinning, Revealing, Decision }

        private static readonly Color Gold = new(1f, 0.843f, 0f);

        // Тайминги слота (сек): 0–2 быстро, 2–3 замедление, 3 — стоп.
        private const float FastUntil = 2f;
        private const float StopAt = 3f;
        private const float FastInterval = 0.05f;
        private const float SlowInterval = 0.3f;

        private TextureRect _background, _iconAbove, _iconCenter, _iconBelow;
        private TextureRect _card, _cardIcon;
        private Control _slot;
        private Label _cardName, _cardDesc, _typeLabel, _goldLabel;
        private ColorRect _typeBar;
        private HBoxContainer _buttons;
        private Button _acceptBtn, _rerollBtn, _sendBtn;

        private OracleSystem _oracle;
        private EconomyManager _economy;
        private EventBus _eventBus;
        private GameManager _gameManager;
        private NetworkManager _network;

        private readonly List<Texture2D> _iconTextures = new();
        private Texture2D _spinBgTex, _revealedBgTex, _templateTex;

        private int _localId;
        private State _state;
        private float _spinElapsed, _tickAccum, _spinInterval;
        private OracleCard _finalCard;
        private bool _decided;
        private readonly RandomNumberGenerator _rng = new();

        public override void _Ready()
        {
            _rng.Randomize();

            _background = GetNode<TextureRect>("Background");
            _slot = GetNode<Control>("Slot");
            _iconAbove = GetNode<TextureRect>("Slot/IconAbove");
            _iconCenter = GetNode<TextureRect>("Slot/IconCenter");
            _iconBelow = GetNode<TextureRect>("Slot/IconBelow");
            _card = GetNode<TextureRect>("Card");
            _cardIcon = GetNode<TextureRect>("Card/CardIcon");
            _cardName = GetNode<Label>("CardName");
            _cardDesc = GetNode<Label>("CardDesc");
            _typeBar = GetNode<ColorRect>("TypeBar");
            _typeLabel = GetNode<Label>("TypeBar/TypeLabel");
            _buttons = GetNode<HBoxContainer>("Buttons");
            _acceptBtn = GetNode<Button>("Buttons/AcceptBtn");
            _rerollBtn = GetNode<Button>("Buttons/RerollBtn");
            _sendBtn = GetNode<Button>("Buttons/SendBtn");
            _goldLabel = GetNode<Label>("GoldLabel");

            _oracle = GetNode<OracleSystem>("/root/OracleSystem");
            _economy = GetNode<EconomyManager>("/root/EconomyManager");
            _eventBus = GetNode<EventBus>("/root/EventBus");
            _gameManager = GetNode<GameManager>("/root/GameManager");
            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            _localId = _network?.LocalPlayerId ?? 0;

            // Текстуры: иконки для слота + фоны + рубашка карты.
            foreach (var c in _oracle.Cards)
                _iconTextures.Add(GD.Load<Texture2D>(c.IconPath));
            _spinBgTex = GD.Load<Texture2D>("res://assets/ui/oracle/ui/oracle_slot_bg.png");
            _revealedBgTex = GD.Load<Texture2D>("res://assets/ui/oracle/ui/card_revealed_bg.png");
            _templateTex = GD.Load<Texture2D>("res://assets/ui/oracle/ui/card_template.png");

            _acceptBtn.Pressed += OnAccept;
            _rerollBtn.Pressed += OnReroll;
            _sendBtn.Pressed += OnSend;

            _eventBus.CurrencyChanged += OnCurrencyChanged;
            AddToGroup("oracle_ui"); // для дебаг-панели (F8)

            RefreshGold();
            StartSpin();
        }

        public override void _ExitTree()
        {
            if (_eventBus != null && GodotObject.IsInstanceValid(_eventBus))
                _eventBus.CurrencyChanged -= OnCurrencyChanged;
        }

        // --- Анимация слота ---

        private void StartSpin()
        {
            _state = State.Spinning;
            _decided = false;
            _spinElapsed = 0f;
            _tickAccum = 0f;
            _spinInterval = FastInterval;
            _finalCard = null;

            _background.Texture = _spinBgTex;
            _slot.Visible = true;
            _iconCenter.Scale = Vector2.One;
            _card.Visible = false;
            _card.Scale = Vector2.One;
            _cardIcon.Visible = false;
            _cardName.Visible = false;
            _cardDesc.Visible = false;
            _typeBar.Visible = false;
            _buttons.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (_state != State.Spinning) return;

            _spinElapsed += (float)delta;
            if (_spinElapsed >= StopAt)
            {
                StopSpin();
                return;
            }

            _spinInterval = _spinElapsed < FastUntil
                ? FastInterval
                : Mathf.Lerp(FastInterval, SlowInterval, (_spinElapsed - FastUntil) / (StopAt - FastUntil));

            _tickAccum += (float)delta;
            if (_tickAccum < _spinInterval) return;
            _tickAccum = 0f;

            _iconAbove.Texture = RandomIcon();
            _iconCenter.Texture = RandomIcon();
            _iconBelow.Texture = RandomIcon();
        }

        // Слот остановился: фиксируем финальную карту и запускаем увеличение иконки.
        private void StopSpin()
        {
            _state = State.Revealing;
            _finalCard ??= _oracle.DrawRandom(_localId);

            _iconCenter.Texture = GD.Load<Texture2D>(_finalCard.IconPath);
            _iconAbove.Texture = RandomIcon();
            _iconBelow.Texture = RandomIcon();

            _iconCenter.PivotOffset = _iconCenter.Size / 2f;
            var tween = CreateTween();
            tween.TweenProperty(_iconCenter, "scale", new Vector2(1.5f, 1.5f), 0.3f);
            tween.TweenProperty(_iconCenter, "scale", Vector2.One, 0.3f);
            tween.TweenCallback(Callable.From(StartFlip));
        }

        // Переворот карты: рубашка с иконкой -> (scale.x 1->0) -> полная карта -> (0->1).
        private void StartFlip()
        {
            _slot.Visible = false;
            _card.Texture = _templateTex;
            _cardIcon.Texture = GD.Load<Texture2D>(_finalCard.IconPath);
            _cardIcon.Visible = true;
            _card.Visible = true;
            _card.PivotOffset = _card.Size / 2f;

            var tween = CreateTween();
            tween.TweenProperty(_card, "scale:x", 0f, 0.3f);
            tween.TweenCallback(Callable.From(() =>
            {
                _card.Texture = GD.Load<Texture2D>(_finalCard.CardPath);
                _cardIcon.Visible = false;
            }));
            tween.TweenProperty(_card, "scale:x", 1f, 0.3f);
            tween.TweenCallback(Callable.From(OnRevealComplete));
        }

        private void OnRevealComplete()
        {
            _state = State.Decision;
            _background.Texture = _revealedBgTex;

            _cardName.Text = _finalCard.Name;
            _cardName.Visible = true;
            _cardDesc.Text = _finalCard.Description;
            _cardDesc.Visible = true;

            SetTypeBar(_finalCard.Type);
            ConfigureButtons(_finalCard.Type);
        }

        // --- Кнопки ---

        private void SetTypeBar(OracleCardType type)
        {
            (_typeLabel.Text, _typeBar.Color) = type switch
            {
                OracleCardType.Buff => ("🟢 БАФФ", new Color(0.18f, 0.7f, 0.3f)),
                OracleCardType.Debuff => ("🔴 ДЕБАФФ", new Color(0.8f, 0.2f, 0.2f)),
                _ => ("🟣 ХАОС", new Color(0.55f, 0.2f, 0.75f)),
            };
            _typeBar.Visible = true;
        }

        private void ConfigureButtons(OracleCardType type)
        {
            _acceptBtn.Visible = true;
            _rerollBtn.Visible = type != OracleCardType.Buff;          // бафф не перекручивают
            _sendBtn.Visible = type == OracleCardType.Debuff;          // отправить можно только дебафф
            UpdateButtonStates();
            _buttons.Visible = true;
        }

        private void UpdateButtonStates()
        {
            int gold = _economy.GetBalance(_localId);
            int left = _oracle.RerollsLeft(_localId);
            _rerollBtn.Disabled = left <= 0 || gold < 50;
            _rerollBtn.Text = $"ПЕРЕКРУТИТЬ 50g ({left})";
            _sendBtn.Disabled = gold < 100;
        }

        private void OnAccept()
        {
            if (_decided) return;
            _decided = true;
            _oracle.ApplyEffect(_localId, _finalCard.Id);
            Finish();
        }

        private void OnReroll()
        {
            if (_decided || _state != State.Decision) return;
            if (_oracle.TryReroll(_localId))
                StartSpin(); // новая прокрутка за 50g
        }

        private void OnSend()
        {
            if (_decided) return;
            if (!_oracle.TrySendToOpponent(_localId, _finalCard.Id)) return;
            _decided = true;
            Finish(); // дебафф ушёл сопернику — Оракул завершён
        }

        // Завершение Оракула: оффлайн (или хост) → PvP. Клиент ждёт фазу от хоста.
        private void Finish()
        {
            _buttons.Visible = false;
            bool networked = _network != null && _network.IsNetworked;
            if (!networked || _network.IsHost)
                _gameManager.ChangePhase(GameManager.GamePhase.PvP);
        }

        // --- Дебаг (F8): показать конкретную карту без прокрутки ---

        public void DebugShowCard(int cardId)
        {
            var card = _oracle.GetCard(cardId);
            if (card == null) return;
            _finalCard = card;
            _state = State.Revealing;
            _iconCenter.Texture = GD.Load<Texture2D>(card.IconPath);
            StartFlip();
        }

        // --- Прочее ---

        private Texture2D RandomIcon() => _iconTextures[_rng.RandiRange(0, _iconTextures.Count - 1)];

        private void OnCurrencyChanged(int playerId, int newAmount)
        {
            if (playerId != _localId) return;
            _goldLabel.Text = $"Золото: {newAmount}";
            if (_state == State.Decision) UpdateButtonStates();
        }

        private void RefreshGold() => _goldLabel.Text = $"Золото: {_economy.GetBalance(_localId)}";
    }
}
